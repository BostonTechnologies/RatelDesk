#!/usr/bin/env python3
"""No-push regression tests for validate-stable-promotion.py.

The tests run the helper from a temporary non-git directory and only use
fixture JSON. They never contact GitHub or a registry.
"""

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
HELPER = ROOT / "tools/release/validate-stable-promotion.py"
VERSION = "1.2.3"
TAG = f"v{VERSION}"
REVISION = "a" * 40
IMAGES = [f"ghcr.io/public-owner/{name}:{VERSION}" for name in ("rateldesk-api", "rateldesk-web", "rateldesk-mcp-http")]


def archives() -> set[str]:
    return {
        "rateldesk-cli-1.2.3-linux-x64.tar.gz", "rateldesk-cli-1.2.3-linux-arm64.tar.gz", "rateldesk-cli-1.2.3-win-x64.zip",
        "rateldesk-mcp-stdio-1.2.3-linux-x64.tar.gz", "rateldesk-mcp-stdio-1.2.3-linux-arm64.tar.gz", "rateldesk-mcp-stdio-1.2.3-win-x64.zip",
        "rateldesk-deployment-1.2.3.tar.gz",
    }


class StablePromotionValidatorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.directory = Path(self.temp.name)
        self.manifest = {
            "tag": TAG,
            "version": VERSION,
            "sourceRevision": REVISION,
            "assets": {name: "b" * 64 for name in archives()},
            "containers": [{"image": image, "digest": f"sha256:{character * 64}"} for image, character in zip(IMAGES, "cde", strict=True)],
        }
        self.release = {
            "tagName": TAG,
            "isDraft": False,
            "isPrerelease": False,
            "assets": [
                {"name": name, "digest": f"sha256:{self.manifest['assets'][name]}" if name in self.manifest["assets"] else "sha256:" + "f" * 64}
                for name in archives() | {"SHA256SUMS", "release-manifest.json", "release-manifest.json.sha256"}
            ],
        }
        self.pages = [[{"tag_name": TAG, "draft": False, "prerelease": False}]]
        self.inspections = {image: {"manifests": [{"os": "linux", "architecture": "amd64"}, {"os": "linux", "architecture": "arm64"}]} for image in IMAGES}
        self.checksum_override: str | None = None
        self.sums_override: str | None = None

    def tearDown(self) -> None:
        self.temp.cleanup()

    def run_helper(self) -> subprocess.CompletedProcess[str]:
        paths = {"release": self.directory / "release.json", "manifest": self.directory / "release-manifest.json", "pages": self.directory / "pages.json", "inspections": self.directory / "inspections.json", "sums": self.directory / "SHA256SUMS"}
        for key, value in (("release", self.release), ("manifest", self.manifest), ("pages", self.pages), ("inspections", self.inspections)):
            paths[key].write_text(json.dumps(value), encoding="utf-8")
        checksum = self.directory / "release-manifest.json.sha256"
        checksum.write_text(f"{self.checksum_override or hashlib.sha256(paths['manifest'].read_bytes()).hexdigest()}  release-manifest.json\n", encoding="utf-8")
        paths["sums"].write_text(
            self.sums_override or "".join(f"{digest}  {name}\n" for name, digest in sorted(self.manifest["assets"].items())),
            encoding="utf-8",
        )
        return subprocess.run(
            [sys.executable, str(HELPER), "--tag", TAG, "--registry-owner", "public-owner", "--source-revision", REVISION, "--release", str(paths["release"]), "--manifest", str(paths["manifest"]), "--manifest-checksum", str(checksum), "--sha256sums", str(paths["sums"]), "--release-pages", str(paths["pages"]), "--inspections", str(paths["inspections"])],
            cwd=self.directory,
            text=True,
            capture_output=True,
            check=False,
        )

    def assert_rejected(self, text: str) -> None:
        result = self.run_helper()
        self.assertNotEqual(0, result.returncode)
        self.assertIn(text, result.stderr)

    def test_valid_plan_runs_from_empty_non_git_directory_is_idempotent_and_has_exact_latest_targets(self) -> None:
        first = self.run_helper()
        second = self.run_helper()
        self.assertEqual(0, first.returncode, first.stderr)
        self.assertEqual(first.stdout, second.stdout)
        plan = json.loads(first.stdout)
        self.assertEqual(
            {f"ghcr.io/public-owner/{name}:latest" for name in ("rateldesk-api", "rateldesk-web", "rateldesk-mcp-http")},
            {item["latest"] for item in plan["images"]},
        )
        self.assertTrue(all(f":{VERSION}:latest" not in item["latest"] for item in plan["images"]))

    def test_rejects_mismatched_source_and_draft_or_prerelease_tags(self) -> None:
        self.manifest["sourceRevision"] = "f" * 40
        self.assert_rejected("source revision")
        self.manifest["sourceRevision"] = REVISION
        self.release["isPrerelease"] = True
        self.assert_rejected("published stable")

    def test_rejects_missing_asset_altered_checksum_asset_digest_and_older_release(self) -> None:
        self.manifest["assets"].pop(next(iter(self.manifest["assets"])))
        self.assert_rejected("exact required archive set")
        self.manifest["assets"] = {name: "b" * 64 for name in archives()}
        self.checksum_override = "0" * 64
        self.assert_rejected("checksum")
        self.checksum_override = None
        self.manifest["assets"] = {name: "b" * 64 for name in archives()}
        first_archive = next(iter(self.manifest["assets"]))
        self.sums_override = f"{'0' * 64}  {first_archive}\n"
        self.assert_rejected("SHA256SUMS does not exactly match")
        self.sums_override = None
        self.release["assets"][0]["digest"] = "sha256:" + "0" * 64
        self.assert_rejected("asset digest mismatch")
        self.release["assets"][0]["digest"] = f"sha256:{self.manifest['assets'][self.release['assets'][0]['name']]}" if self.release["assets"][0]["name"] in self.manifest["assets"] else "sha256:" + "f" * 64
        self.pages = [[{"tag_name": "v9.0.0", "draft": False, "prerelease": False}], *self.pages]
        self.assert_rejected("move latest backwards")

    def test_rejects_malformed_duplicate_and_wrong_repository_images(self) -> None:
        self.manifest["containers"][0]["digest"] = "not-a-digest"
        self.assert_rejected("invalid image digest")
        self.manifest["containers"][0]["digest"] = "sha256:" + "c" * 64
        self.manifest["containers"][1]["image"] = self.manifest["containers"][0]["image"]
        self.assert_rejected("unknown or duplicate")
        self.manifest["containers"][1]["image"] = "ghcr.io/wrong-owner/rateldesk-web:1.2.3"
        self.assert_rejected("unknown or duplicate")

    def test_rejects_missing_architecture_in_each_later_image_before_a_plan_is_emitted(self) -> None:
        for image in IMAGES[1:]:
            self.inspections[image]["manifests"].pop()
            result = self.run_helper()
            self.assertNotEqual(0, result.returncode)
            self.assertEqual("", result.stdout)
            self.assertIn("missing a required Linux platform", result.stderr)
            self.inspections[image]["manifests"].append({"os": "linux", "architecture": "arm64"})


if __name__ == "__main__":
    unittest.main()

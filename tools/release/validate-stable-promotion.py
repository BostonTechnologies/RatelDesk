#!/usr/bin/env python3
"""Validate the immutable inputs to a stable-channel promotion.

This helper is deliberately side-effect free so it can be exercised from an
empty, non-git directory. The workflow obtains release and registry data,
then calls this program before it creates any mutable registry reference.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any


STABLE_TAG = re.compile(r"^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$")
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")
IMAGE_NAMES = ("rateldesk-api", "rateldesk-web", "rateldesk-mcp-http")
REQUIRED_PLATFORMS = {("linux", "amd64"), ("linux", "arm64")}


def fail(message: str) -> None:
    raise SystemExit(message)


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def version(tag: str) -> tuple[int, int, int]:
    match = STABLE_TAG.fullmatch(tag)
    if not match:
        fail("Only a stable SemVer tag may promote latest.")
    return tuple(int(part) for part in match.groups())


def expected_assets(release_version: str) -> set[str]:
    archives = {
        *(f"rateldesk-cli-{release_version}-{rid}.{'zip' if rid == 'win-x64' else 'tar.gz'}" for rid in ("linux-x64", "linux-arm64", "win-x64")),
        *(f"rateldesk-mcp-stdio-{release_version}-{rid}.{'zip' if rid == 'win-x64' else 'tar.gz'}" for rid in ("linux-x64", "linux-arm64", "win-x64")),
        f"rateldesk-deployment-{release_version}.tar.gz",
    }
    return archives


def release_pages(value: Any) -> list[dict[str, Any]]:
    if isinstance(value, list) and all(isinstance(page, list) for page in value):
        return [release for page in value for release in page]
    if isinstance(value, list):
        return value
    fail("Release pages must be an array or an array of pages.")


def file_sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate(args: argparse.Namespace) -> dict[str, Any]:
    candidate_version = version(args.tag)
    release = load_json(args.release)
    if release.get("tagName") != args.tag or release.get("isDraft") or release.get("isPrerelease"):
        fail("Promotion requires an already-published stable release.")

    manifest = load_json(args.manifest)
    checksum = args.manifest_checksum.read_text(encoding="utf-8").strip().split()
    if len(checksum) != 2 or checksum[1] != "release-manifest.json" or checksum[0] != file_sha256(args.manifest):
        fail("The release manifest checksum is missing or mismatched.")
    release_version = args.tag[1:]
    if manifest.get("tag") != args.tag or manifest.get("version") != release_version:
        fail("The release manifest tag and version must match the requested stable tag.")
    if manifest.get("sourceRevision") != args.source_revision:
        fail("The release manifest source revision does not match the peeled tag commit.")

    pages = release_pages(load_json(args.release_pages))
    published_stable = [version(item["tag_name"]) for item in pages if not item.get("draft") and not item.get("prerelease") and STABLE_TAG.fullmatch(item.get("tag_name", ""))]
    if published_stable and candidate_version < max(published_stable):
        fail("Refusing to move latest backwards from a newer published stable release.")

    assets = manifest.get("assets")
    if not isinstance(assets, dict) or set(assets) != expected_assets(release_version):
        fail("The release manifest does not contain the exact required archive set.")
    if any(not isinstance(digest, str) or not re.fullmatch(r"[0-9a-f]{64}", digest) for digest in assets.values()):
        fail("The release manifest contains an invalid archive checksum.")
    published_asset_names = {asset.get("name") for asset in release.get("assets", [])}
    required_release_assets = set(assets) | {"SHA256SUMS", "release-manifest.json", "release-manifest.json.sha256"}
    if published_asset_names != required_release_assets:
        fail("The published release does not contain the exact expected asset set.")

    expected_images = {f"ghcr.io/{args.registry_owner.lower()}/{name}:{release_version}" for name in IMAGE_NAMES}
    containers = manifest.get("containers")
    if not isinstance(containers, list) or len(containers) != len(expected_images):
        fail("The release manifest must contain exactly three container entries.")
    observed_images = [item.get("image") for item in containers if isinstance(item, dict)]
    if set(observed_images) != expected_images or len(set(observed_images)) != len(expected_images):
        fail("The release manifest contains an unknown or duplicate container destination.")
    image_digests = {item["image"]: item.get("digest") for item in containers}
    if any(not isinstance(digest, str) or not DIGEST.fullmatch(digest) for digest in image_digests.values()):
        fail("The release manifest contains an invalid image digest.")

    inspections = load_json(args.inspections)
    if set(inspections) != expected_images:
        fail("Registry inspection data must cover exactly the three expected images.")
    for image, digest in image_digests.items():
        platforms = {
            ((item.get("platform") or item).get("os"), (item.get("platform") or item).get("architecture"))
            for item in inspections[image].get("manifests", [])
        }
        if REQUIRED_PLATFORMS - platforms:
            fail(f"{image}@{digest} is missing a required Linux platform.")

    return {"tag": args.tag, "sourceRevision": args.source_revision, "images": [{"image": image, "digest": image_digests[image]} for image in sorted(expected_images)]}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tag", required=True)
    parser.add_argument("--registry-owner", required=True)
    parser.add_argument("--source-revision", required=True)
    parser.add_argument("--release", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--manifest-checksum", type=Path, required=True)
    parser.add_argument("--release-pages", type=Path, required=True)
    parser.add_argument("--inspections", type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(validate(args), sort_keys=True))


if __name__ == "__main__":
    main()

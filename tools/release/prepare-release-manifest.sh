#!/usr/bin/env bash
set -euo pipefail

# Finalizes the manifest after image publication. SHA256SUMS deliberately covers
# only the immutable archives; the detached manifest checksum avoids a
# checksum/manifest self-reference cycle.
if [[ $# -ne 7 ]]; then
  echo "usage: $0 <version> <source-revision> <asset-directory> <web-digest> <api-digest> <mcp-http-digest> <tag>" >&2
  exit 64
fi

version="$1"
source_revision="$2"
asset_directory="$3"
web_digest="$4"
api_digest="$5"
mcp_http_digest="$6"
tag="$7"

if [[ ! -d "$asset_directory" || ! -f "$asset_directory/SHA256SUMS" ]]; then
  echo "Release assets and SHA256SUMS are required." >&2
  exit 1
fi

(cd "$asset_directory" && sha256sum --check SHA256SUMS)

python3 - "$asset_directory" "$version" "$source_revision" "$web_digest" "$api_digest" "$mcp_http_digest" "$tag" <<'PY'
import hashlib
import json
import pathlib
import sys

directory = pathlib.Path(sys.argv[1])
version, revision, web, api, mcp, tag = sys.argv[2:]
assets = {}
for line in (directory / "SHA256SUMS").read_text(encoding="utf-8").splitlines():
    digest, name = line.split(maxsplit=1)
    name = name.strip()
    if not (directory / name).is_file():
        raise SystemExit(f"Checksummed asset is missing: {name}")
    assets[name] = digest

expected_archives = {
    *(f"rateldesk-cli-{version}-{rid}.{'zip' if rid == 'win-x64' else 'tar.gz'}" for rid in ("linux-x64", "linux-arm64", "win-x64")),
    *(f"rateldesk-mcp-stdio-{version}-{rid}.{'zip' if rid == 'win-x64' else 'tar.gz'}" for rid in ("linux-x64", "linux-arm64", "win-x64")),
    f"rateldesk-deployment-{version}.tar.gz",
}
if set(assets) != expected_archives:
    missing = sorted(expected_archives - set(assets))
    unexpected = sorted(set(assets) - expected_archives)
    raise SystemExit(f"Unexpected checksum set; missing={missing}, unexpected={unexpected}")

manifest = {
    "version": version,
    "tag": tag,
    "sourceRevision": revision,
    "supportedRids": ["linux-x64", "linux-arm64", "win-x64"],
    "assets": dict(sorted(assets.items())),
    "containers": [
        {"image": f"ghcr.io/bostontechnologies/rateldesk-web:{version}", "digest": web},
        {"image": f"ghcr.io/bostontechnologies/rateldesk-api:{version}", "digest": api},
        {"image": f"ghcr.io/bostontechnologies/rateldesk-mcp-http:{version}", "digest": mcp},
    ],
}
(directory / "release-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
PY

(cd "$asset_directory" && sha256sum release-manifest.json > release-manifest.json.sha256)

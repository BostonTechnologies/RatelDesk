#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
temporary_directory="$(mktemp -d)"
trap 'rm -rf "$temporary_directory"' EXIT
assets="$temporary_directory/assets"
fake_gh_root="$temporary_directory/fake-gh"
mkdir -p "$assets" "$fake_gh_root/assets"

version="0.0.0-test"
tag="v$version"
declare -a rids=(linux-x64 linux-arm64 win-x64)
declare -a archives=()
for rid in "${rids[@]}"; do
  suffix=tar.gz
  [[ "$rid" == win-x64 ]] && suffix=zip
  archives+=("rateldesk-cli-${version}-${rid}.${suffix}" "rateldesk-mcp-stdio-${version}-${rid}.${suffix}")
done
archives+=("rateldesk-deployment-${version}.tar.gz")
expected_asset_count=$((${#archives[@]} + 3)) # SHA256SUMS and the manifest plus its detached checksum
for asset in "${archives[@]}"; do printf '%s\n' "$asset" > "$assets/$asset"; done
(cd "$assets" && printf '%s\n' "${archives[@]}" | sort | xargs sha256sum > SHA256SUMS)
"$repository_root/tools/release/prepare-release-manifest.sh" "$version" deadbeef "$assets" sha256:web sha256:api sha256:mcp "$tag"

cat > "$temporary_directory/gh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
root="${FAKE_GH_ROOT:?}"
if [[ "$1 $2" == "release view" ]]; then
  [[ -f "$root/release" ]] || exit 1
  python3 - "$root" <<'PY'
import json, pathlib, sys
root = pathlib.Path(sys.argv[1])
print(json.dumps({"isDraft": True, "assets": [{"name": item.name} for item in sorted((root / "assets").iterdir())]}))
PY
elif [[ "$1 $2" == "release create" ]]; then
  touch "$root/release"
elif [[ "$1 $2" == "release upload" ]]; then
  [[ "${FAKE_GH_FAIL_UPLOAD:-false}" != true ]] || exit 1
  cp "$4" "$root/assets/$(basename "$4")"
elif [[ "$1 $2" == "release download" ]]; then
  pattern=""
  directory=""
  for ((index=1; index <= $#; index++)); do
    argument="${!index}"
    if [[ "$argument" == --pattern ]]; then next=$((index + 1)); pattern="${!next}"; fi
    if [[ "$argument" == --dir ]]; then next=$((index + 1)); directory="${!next}"; fi
  done
  mkdir -p "$directory"
  cp "$root/assets/$pattern" "$directory/$pattern"
else
  echo "Unexpected fake gh command: $*" >&2
  exit 64
fi
EOF
chmod 700 "$temporary_directory/gh"

RELEASE_ASSET_DRY_RUN=true "$repository_root/tools/release/publish-release-assets.sh" "$tag" "$version" "$assets" > "$temporary_directory/dry-run"
test "$(wc -l < "$temporary_directory/dry-run")" = "$expected_asset_count"

# Missing assets are rejected before any release command is run.
mv "$assets/${archives[0]}" "$temporary_directory/missing-asset"
if RELEASE_ASSET_DRY_RUN=true "$repository_root/tools/release/publish-release-assets.sh" "$tag" "$version" "$assets"; then
  echo "Missing release asset unexpectedly succeeded." >&2
  exit 1
fi
mv "$temporary_directory/missing-asset" "$assets/${archives[0]}"

# The first run creates a draft and uploads the complete payload.
FAKE_GH_ROOT="$fake_gh_root" GH_BIN="$temporary_directory/gh" "$repository_root/tools/release/publish-release-assets.sh" "$tag" "$version" "$assets"
test "$(find "$fake_gh_root/assets" -maxdepth 1 -type f | wc -l)" = "$expected_asset_count"

# A complete rerun reuses verified assets, while differing content is rejected.
FAKE_GH_ROOT="$fake_gh_root" GH_BIN="$temporary_directory/gh" "$repository_root/tools/release/publish-release-assets.sh" "$tag" "$version" "$assets"

# A failed upload leaves a resumable draft; a later run verifies its matching
# asset and uploads the remainder.
partial_root="$temporary_directory/partial-gh"
mkdir -p "$partial_root/assets"
touch "$partial_root/release"
cp "$assets/${archives[0]}" "$partial_root/assets/${archives[0]}"
if FAKE_GH_ROOT="$partial_root" FAKE_GH_FAIL_UPLOAD=true GH_BIN="$temporary_directory/gh" "$repository_root/tools/release/publish-release-assets.sh" "$tag" "$version" "$assets"; then
  echo "Upload failure unexpectedly succeeded." >&2
  exit 1
fi
FAKE_GH_ROOT="$partial_root" GH_BIN="$temporary_directory/gh" "$repository_root/tools/release/publish-release-assets.sh" "$tag" "$version" "$assets"
test "$(find "$partial_root/assets" -maxdepth 1 -type f | wc -l)" = "$expected_asset_count"

printf '%s\n' changed > "$assets/${archives[0]}"
(cd "$assets" && printf '%s\n' "${archives[@]}" | sort | xargs sha256sum > SHA256SUMS)
"$repository_root/tools/release/prepare-release-manifest.sh" "$version" deadbeef "$assets" sha256:web sha256:api sha256:mcp "$tag"
if FAKE_GH_ROOT="$fake_gh_root" GH_BIN="$temporary_directory/gh" "$repository_root/tools/release/publish-release-assets.sh" "$tag" "$version" "$assets"; then
  echo "Conflicting release asset unexpectedly succeeded." >&2
  exit 1
fi

#!/usr/bin/env bash
set -euo pipefail

# Creates or resumes a *draft* GitHub release and appends only verified assets.
# Existing assets are downloaded and checked before reuse; this script never
# overwrites a release asset with different content.
if [[ $# -ne 3 ]]; then
  echo "usage: $0 <tag> <version> <asset-directory>" >&2
  exit 64
fi

tag="$1"
version="$2"
asset_directory="$3"
gh_bin="${GH_BIN:-gh}"

if [[ ! -d "$asset_directory" ]]; then
  echo "Release asset directory does not exist: $asset_directory" >&2
  exit 1
fi

declare -a rids=(linux-x64 linux-arm64 win-x64 osx-x64 osx-arm64)
declare -a assets=()
for rid in "${rids[@]}"; do
  suffix=tar.gz
  [[ "$rid" == win-x64 ]] && suffix=zip
  assets+=("rateldesk-cli-${version}-${rid}.${suffix}")
  assets+=("rateldesk-mcp-stdio-${version}-${rid}.${suffix}")
done
assets+=("rateldesk-deployment-${version}.tar.gz" "SHA256SUMS" "release-manifest.json" "release-manifest.json.sha256")

for asset in "${assets[@]}"; do
  [[ -s "$asset_directory/$asset" ]] || { echo "Expected release asset is missing or empty: $asset" >&2; exit 1; }
done
(cd "$asset_directory" && sha256sum --check --status SHA256SUMS)
(cd "$asset_directory" && sha256sum --check --status release-manifest.json.sha256)

if [[ "${RELEASE_ASSET_DRY_RUN:-false}" == true ]]; then
  printf '%s\n' "${assets[@]}"
  exit 0
fi

metadata=""
if metadata="$($gh_bin release view "$tag" --json isDraft,assets 2>/dev/null)"; then
  is_draft="$(python3 -c 'import json,sys; print(str(json.load(sys.stdin).get("isDraft", False)).lower())' <<<"$metadata")"
  [[ "$is_draft" == true ]] || { echo "Existing release '$tag' is not a draft; refusing to modify it." >&2; exit 1; }
else
  "$gh_bin" release create "$tag" --draft --title "RatelDesk $version" --notes "Release assets are being verified before publication."
  metadata='{"assets":[]}'
fi

temporary_directory="$(mktemp -d)"
trap 'rm -rf "$temporary_directory"' EXIT

for asset in "${assets[@]}"; do
  existing="$(python3 -c 'import json,sys; print("present" if any(item.get("name") == sys.argv[1] for item in json.load(sys.stdin).get("assets", [])) else "")' "$asset" <<<"$metadata")"
  if [[ "$existing" == present ]]; then
    "$gh_bin" release download "$tag" --pattern "$asset" --dir "$temporary_directory"
    if ! cmp --silent "$asset_directory/$asset" "$temporary_directory/$asset"; then
      echo "Existing release asset '$asset' conflicts with the validated payload." >&2
      exit 1
    fi
    continue
  fi
  "$gh_bin" release upload "$tag" "$asset_directory/$asset"
done

final_metadata="$($gh_bin release view "$tag" --json assets)"
python3 -c 'import json,sys; expected=set(sys.argv[1:]); actual={item.get("name") for item in json.load(sys.stdin).get("assets", [])}; missing=sorted(expected-actual); unexpected=sorted(actual-expected); (not (missing or unexpected)) or (_ for _ in ()).throw(SystemExit(f"Draft release assets are incomplete; missing={missing}, unexpected={unexpected}"))' "${assets[@]}" <<<"$final_metadata"

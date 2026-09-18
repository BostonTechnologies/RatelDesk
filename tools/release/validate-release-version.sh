#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "usage: $0 <vMAJOR.MINOR.PATCH[-prerelease]>" >&2
  exit 64
fi

python3 - "$1" <<'PY'
import re
import sys

tag = sys.argv[1]
identifier = r"(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)"
pattern = re.compile(
    rf"^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-({identifier}(?:\.{identifier})*))?$")
match = pattern.fullmatch(tag)
if match is None:
    raise SystemExit(f"Tag '{tag}' is not a supported SemVer release tag.")

version = tag[1:]
print(f"version={version}")
print(f"prerelease={'true' if match.group(4) else 'false'}")
PY

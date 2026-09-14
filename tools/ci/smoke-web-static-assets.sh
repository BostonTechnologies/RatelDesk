#!/usr/bin/env bash

set -euo pipefail

image_name=${1:?usage: smoke-web-static-assets.sh <image>}
container_name="helpdesk-web-static-assets-${RANDOM}${RANDOM}"
asset_directory="$(mktemp -d)"

cleanup() {
  docker rm --force "$container_name" >/dev/null 2>&1 || true
  rm -rf "$asset_directory"
}
trap cleanup EXIT

docker run --detach --name "$container_name" --publish 127.0.0.1:18111:8111 \
  --env ApiBaseUrl=http://127.0.0.1:8222 \
  --env Authentication__Authentik__ClientId=helpdesk-ci \
  --env Authentication__Authentik__ApiScope=helpdesk-api \
  --env AUTHENTIK_CLIENT_SECRET=ci-placeholder \
  "$image_name" >/dev/null

for _ in {1..30}; do
  # This image-only check has no API; browser entry correctly reports unavailable.
  # Probe the published asset directly. Compose and UX checks cover ready sign-in.
  if curl --fail --silent --max-time 5 --dump-header "$asset_directory/headers" \
    --output "$asset_directory/blazor.web.js" http://127.0.0.1:18111/_framework/blazor.web.js; then
    # A successful HTML fallback is not a usable Blazor bootstrap script.
    grep -Eiq '^content-type: (text|application)/(x-)?javascript([;[:space:]]|$)' "$asset_directory/headers"
    [[ -s "$asset_directory/blazor.web.js" ]]
    exit 0
  fi

  sleep 1
done

docker logs "$container_name" >&2 || true
printf 'Published web image did not serve the Blazor bootstrap asset.\n' >&2
exit 1

#!/usr/bin/env bash

set -euo pipefail

image_name=${1:?usage: smoke-web-static-assets.sh <image>}
container_name="helpdesk-web-static-assets-${RANDOM}${RANDOM}"

cleanup() {
  docker rm --force "$container_name" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker run --detach --name "$container_name" --publish 127.0.0.1:18111:8111 \
  --env ApiBaseUrl=http://127.0.0.1:8222 \
  --env Authentication__Authentik__ClientId=helpdesk-ci \
  --env Authentication__Authentik__ApiScope=helpdesk-api \
  --env AUTHENTIK_CLIENT_SECRET=ci-placeholder \
  "$image_name" >/dev/null

for _ in {1..30}; do
  if curl --fail --silent --output /dev/null http://127.0.0.1:18111/login; then
    curl --fail --silent --show-error --output /dev/null http://127.0.0.1:18111/_framework/blazor.web.js
    exit 0
  fi

  sleep 1
done

docker logs "$container_name" >&2 || true
printf 'Published web image did not serve its login page.\n' >&2
exit 1

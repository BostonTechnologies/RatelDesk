#!/usr/bin/env bash
set -Eeuo pipefail

image=${1:?Usage: test-mcp-compose-config.sh <mcp-http-image>}
work_directory=$(mktemp -d)
archive_directory="$work_directory/archive"
project_name="rateldesk-mcp-config-${RANDOM}${RANDOM}"
image_tag="compose-validation-${RANDOM}${RANDOM}"
mcp_image="ghcr.io/bostontechnologies/rateldesk-mcp-http:$image_tag"
config_file="$work_directory/config.json"
standalone_compose=(
  -p "$project_name"
  -f docker/examples/mcp-http/docker-compose.yml
)
compose_environment=()

cleanup() {
  local status=$?
  if [[ "$status" -ne 0 ]]; then
    env "${compose_environment[@]}" docker compose "${standalone_compose[@]}" logs --no-color >&2 || true
  fi
  env "${compose_environment[@]}" docker compose "${standalone_compose[@]}" down --volumes --remove-orphans >/dev/null 2>&1 || true
  docker image rm "$mcp_image" >/dev/null 2>&1 || true
  rm -rf "$work_directory"
  exit "$status"
}
trap cleanup EXIT

printf '%s\n' '{"apiBaseUrl":"https://api.example.test/","credentialMode":"gateway"}' > "$config_file"
chmod 0600 "$config_file"

compose_environment=(
  "RATELDESK_VERSION=$image_tag"
  "RATELDESK_MCP_CONFIG_FILE=$config_file"
  'RATELDESK_API_BASE_URL=https://api.example.test/'
  'RATELDESK_MCP_PUBLIC_RESOURCE_URI=https://mcp.example.test/mcp'
  'RATELDESK_MCP_ALLOWED_ORIGIN=https://client.example.test'
  'RATELDESK_MCP_PORT=18223'
)
authentik_environment=(
  "${compose_environment[@]}"
  'RATELDESK_MCP_AUTHENTIK_AUTHORITY=https://auth.example.test/'
)

docker tag "$image" "$mcp_image"

env "${compose_environment[@]}" docker compose "${standalone_compose[@]}" config --quiet
env "${compose_environment[@]}" docker compose -f docker/docker-compose.yml -f docker/docker-compose.mcp.gateway.yml config --quiet
env "${compose_environment[@]}" docker compose -f docker/docker-compose.release.yml -f docker/docker-compose.mcp.gateway.release.yml config --quiet
env "${authentik_environment[@]}" docker compose -f docker/docker-compose.yml -f docker/docker-compose.mcp.yml config --quiet
env "${authentik_environment[@]}" docker compose -f docker/docker-compose.release.yml -f docker/docker-compose.mcp.release.yml config --quiet

# Render the same image-only recipes after extracting the deployment payload.
# This guards the archive layout and ensures examples never require a checkout.
mkdir -p "$archive_directory/source" "$archive_directory/extracted"
cp -a docker/. "$archive_directory/source/"
tar -C "$archive_directory/source" -czf "$archive_directory/deployment.tar.gz" .
tar -C "$archive_directory/extracted" -xzf "$archive_directory/deployment.tar.gz"
grep -Fqx 'RATELDESK_VERSION=' "$archive_directory/extracted/examples/mcp-http/.env.example"
if grep -Eq '^RATELDESK_VERSION=[0-9]+\.[0-9]+' "$archive_directory/extracted/examples/mcp-http/.env.example"; then
  echo "The extracted standalone MCP example must require an explicit release version." >&2
  exit 1
fi
env "${compose_environment[@]}" docker compose \
  -f "$archive_directory/extracted/examples/mcp-http/docker-compose.yml" config --quiet
env "${compose_environment[@]}" docker compose \
  -f "$archive_directory/extracted/docker-compose.release.yml" \
  -f "$archive_directory/extracted/docker-compose.mcp.gateway.release.yml" config --quiet
env "${authentik_environment[@]}" docker compose \
  -f "$archive_directory/extracted/docker-compose.release.yml" \
  -f "$archive_directory/extracted/docker-compose.mcp.release.yml" config --quiet

env "${compose_environment[@]}" docker compose "${standalone_compose[@]}" up --detach --wait
curl --retry 6 --retry-all-errors --retry-delay 1 --fail --show-error --silent \
  http://127.0.0.1:18223/health/live > /dev/null
env "${compose_environment[@]}" docker compose "${standalone_compose[@]}" exec -T mcp-http /bin/sh -ec '
    test "$(id -u)" = 10001
    test -r /run/rateldesk-mcp/config.json
    test "$(stat -c "%u:%g:%a" /run/rateldesk-mcp/config.json)" = "10001:10001:400"
    grep -Fqx "{\"apiBaseUrl\":\"https://api.example.test/\",\"credentialMode\":\"gateway\"}" /run/rateldesk-mcp/config.json
  '

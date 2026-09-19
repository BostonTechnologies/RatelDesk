#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
work_directory="$(mktemp -d)"
trap 'rm -rf "$work_directory"' EXIT

plan="$work_directory/plan.json"
log="$work_directory/docker.log"
mock_docker="$work_directory/docker"
digest="sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"

printf '%s\n' '{"images":[' > "$plan"
for image in rateldesk-api rateldesk-web rateldesk-mcp-http; do
  printf '{"image":"ghcr.io/public-owner/%s:1.2.3","latest":"ghcr.io/public-owner/%s:latest","digest":"%s"},\n' "$image" "$image" "$digest" >> "$plan"
done
sed -i '$ s/,$/]/' "$plan"
printf '%s\n' '}' >> "$plan"

cat > "$mock_docker" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$MOCK_DOCKER_LOG"
if [[ "$*" == *"imagetools create"* && "${FAKE_FAIL_TARGET:-}" != "" && "$*" == *"$FAKE_FAIL_TARGET"* ]]; then
  exit 42
fi
if [[ "$*" == *"imagetools inspect"* ]]; then
  printf '%s\n' "$MOCK_DOCKER_DIGEST"
fi
EOF
chmod 0700 "$mock_docker"

MOCK_DOCKER_LOG="$log" MOCK_DOCKER_DIGEST="$digest" DOCKER_BIN="$mock_docker" \
  "$repository_root/tools/release/promote-stable-images.sh" "$plan"

grep -Fqx "buildx imagetools create --tag ghcr.io/public-owner/rateldesk-api:latest ghcr.io/public-owner/rateldesk-api:1.2.3@$digest" "$log"
grep -Fqx "buildx imagetools create --tag ghcr.io/public-owner/rateldesk-web:latest ghcr.io/public-owner/rateldesk-web:1.2.3@$digest" "$log"
grep -Fqx "buildx imagetools create --tag ghcr.io/public-owner/rateldesk-mcp-http:latest ghcr.io/public-owner/rateldesk-mcp-http:1.2.3@$digest" "$log"
if grep -Fq ':1.2.3:latest' "$log"; then
  echo "The promotion command constructed an invalid mutable image reference." >&2
  exit 1
fi

if MOCK_DOCKER_LOG="$log" MOCK_DOCKER_DIGEST="$digest" FAKE_FAIL_TARGET='rateldesk-web:latest' DOCKER_BIN="$mock_docker" \
  "$repository_root/tools/release/promote-stable-images.sh" "$plan" >"$work_directory/partial.stdout" 2>"$work_directory/partial.stderr"; then
  echo "The simulated partial registry failure unexpectedly succeeded." >&2
  exit 1
fi
grep -Fq 'already updated: ghcr.io/public-owner/rateldesk-api:latest' "$work_directory/partial.stderr"

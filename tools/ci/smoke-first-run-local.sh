#!/usr/bin/env bash
set -euo pipefail

compose_file="${1:-docker/docker-compose.yml}"
api_base_url="${RATELDESK_API_URL:-http://127.0.0.1:8222}"
web_base_url="${RATELDESK_WEB_URL:-http://127.0.0.1:8111}"
work_directory="$(mktemp -d)"
cookie_jar="$work_directory/cookies.txt"

cleanup() {
  rm -rf "$work_directory"
}
trap cleanup EXIT

setup_code="$(docker compose -f "$compose_file" exec -T api sh -c 'cat /var/lib/rateldesk/bootstrap/setup-code')"
password="Rc4-$(openssl rand -hex 24)"

session_payload="$(jq -nc --arg setupCode "$setup_code" '{setupCode: $setupCode}')"
session="$(curl --fail --silent --show-error \
  --header 'Content-Type: application/json' \
  --data "$session_payload" \
  "$api_base_url/api/v1/setup/session" | jq -er '.session')"

storage_payload='{"provider":"Sqlite"}'
curl --fail --silent --show-error \
  --header 'Content-Type: application/json' \
  --header "X-RatelDesk-Setup-Session: $session" \
  --data "$storage_payload" \
  "$api_base_url/api/v1/setup/storage" | jq -e '.state == "Configuring"' > /dev/null

initialize_payload="$(jq -nc --arg password "$password" '{email: "admin@example.test", displayName: "RC4 Test Administrator", password: $password, organizationName: "RC4 Test Organization", applicationName: "RatelDesk RC4", applicationUrl: "http://127.0.0.1:8111"}')"
curl --fail --silent --show-error \
  --header 'Content-Type: application/json' \
  --header "X-RatelDesk-Setup-Session: $session" \
  --data "$initialize_payload" \
  "$api_base_url/api/v1/setup/initialize" | jq -e '.state == "Ready"' > /dev/null

login_payload="$(jq -nc --arg password "$password" '{email: "admin@example.test", password: $password, rememberMe: false}')"
for attempt in $(seq 1 30); do
  login_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
    --cookie-jar "$cookie_jar" \
    --header 'Content-Type: application/json' \
    --data "$login_payload" \
    "$api_base_url/api/v1/local-auth/login" || true)"
  if [[ "$login_status" == "204" ]]; then
    break
  fi

  if [[ "$attempt" == "30" ]]; then
    echo "API did not restart into the normal application host after setup." >&2
    exit 1
  fi

  sleep 2
done

web_login_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --cookie-jar "$cookie_jar" \
  --cookie "$cookie_jar" \
  --data-urlencode 'email=admin@example.test' \
  --data-urlencode "password=$password" \
  "$web_base_url/local-login")"
[[ "$web_login_status" == "302" ]]

curl --fail --silent --show-error \
  --cookie "$cookie_jar" \
  "$web_base_url/api/v1/auth/me" | jq -e '.isAuthenticated == true and .isHelpdeskAdmin == true' > /dev/null

curl --fail --silent --show-error "$api_base_url/api/v1/setup/status" | jq -e '.state == "Ready"' > /dev/null

echo "First-run SQLite setup and local sign-in smoke test passed."

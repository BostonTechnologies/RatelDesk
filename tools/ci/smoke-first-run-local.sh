#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -eq 0 ]]; then
  compose_files=(docker/docker-compose.yml)
else
  compose_files=("$@")
fi
compose_arguments=()
for compose_file in "${compose_files[@]}"; do
  compose_arguments+=(-f "$compose_file")
done

api_base_url="${RATELDESK_API_URL:-http://127.0.0.1:8222}"
web_base_url="${RATELDESK_WEB_URL:-http://127.0.0.1:8111}"
setup_provider="${RATELDESK_SETUP_PROVIDER:-Sqlite}"
work_directory="$(mktemp -d)"
cookie_jar="$work_directory/cookies.txt"
web_cookie_jar="$work_directory/web-cookies.txt"
self_service_cookie_jar="$work_directory/self-service-cookies.txt"

cleanup() {
  rm -rf "$work_directory"
}
trap cleanup EXIT

curl --retry 6 --retry-all-errors --retry-delay 2 --fail --silent --show-error \
  "$web_base_url/setup" > /dev/null

setup_code="$(docker compose "${compose_arguments[@]}" exec -T api sh -c 'cat /var/lib/rateldesk/bootstrap/setup-code')"
password="Rc4-$(openssl rand -hex 24)"

session_payload="$(jq -nc --arg setupCode "$setup_code" '{setupCode: $setupCode}')"
session="$(curl --fail --silent --show-error \
  --header 'Content-Type: application/json' \
  --data "$session_payload" \
  "$api_base_url/api/v1/setup/session" | jq -er '.session')"

case "$setup_provider" in
  Sqlite)
    storage_payload='{"provider":"Sqlite"}'
    ;;
  PostgreSql)
    postgre_sql_password="${RATELDESK_SETUP_POSTGRES_PASSWORD:?Set RATELDESK_SETUP_POSTGRES_PASSWORD for PostgreSQL setup validation}"
    storage_payload="$(jq -nc \
      --arg host "${RATELDESK_SETUP_POSTGRES_HOST:-postgres}" \
      --arg database "${RATELDESK_SETUP_POSTGRES_DATABASE:-rateldesk}" \
      --arg username "${RATELDESK_SETUP_POSTGRES_USERNAME:-rateldesk}" \
      --arg password "$postgre_sql_password" \
      '{provider: "PostgreSql", postgreSqlHost: $host, postgreSqlPort: 5432, postgreSqlDatabase: $database, postgreSqlUsername: $username, postgreSqlPassword: $password, postgreSqlUseTls: false}')"
    ;;
  *)
    echo "Unsupported setup provider: $setup_provider" >&2
    exit 2
    ;;
esac
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
    --header 'X-Requested-With: XMLHttpRequest' --data "$login_payload" \
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

# A fresh Web-only jar proves the browser login establishes its own session.
curl --fail --silent --show-error --cookie-jar "$web_cookie_jar" \
  "$web_base_url/login" > "$work_directory/login.html"
antiforgery_token="$(python3 - "$work_directory/login.html" <<'TOKEN'
from html.parser import HTMLParser
import sys
class TokenParser(HTMLParser):
    token = None
    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        if tag == 'input' and values.get('name') == '__RequestVerificationToken':
            self.token = values.get('value')
parser = TokenParser()
parser.feed(open(sys.argv[1]).read())
assert parser.token, 'Login form must contain an antiforgery token'
print(parser.token)
TOKEN
)"
web_login_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --dump-header "$work_directory/web-login.headers" \
  --cookie-jar "$web_cookie_jar" --cookie "$web_cookie_jar" \
  --data-urlencode "__RequestVerificationToken=$antiforgery_token" \
  --data-urlencode 'email=admin@example.test' \
  --data-urlencode "password=$password" \
  "$web_base_url/local-login")"
[[ "$web_login_status" == "302" ]]
rg -qi '^location: /home\r?$' "$work_directory/web-login.headers"
curl --fail --silent --show-error --cookie "$web_cookie_jar" \
  "$web_base_url/api/v1/auth/me" | jq -e '.isAuthenticated == true and .isHelpdeskAdmin == true' > /dev/null

access_payload="$(curl --fail --silent --show-error \
  --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  "$api_base_url/api/v1/auth/me")"
organization_id="$(jq -er '.primaryOrganizationId' <<< "$access_payload")"
administrator_id="$(curl --fail --silent --show-error \
  --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  "$api_base_url/api/v1/users/by-email/admin%40example.test" | jq -er '.id')"

customer_payload="$(jq -nc --arg organizationId "$organization_id" '{name: "RC4 Smoke Customer", email: "rc4-smoke-customer@example.test", organizationId: $organizationId}')"
customer_id="$(curl --fail --silent --show-error \
  --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  --header 'Content-Type: application/json' \
  --data "$customer_payload" \
  "$api_base_url/api/v1/customers" | jq -er '.id')"

incident_payload="$(jq -nc --arg customerId "$customer_id" --arg organizationId "$organization_id" '{title: "RC4 smoke incident", description: "Production first-run incident verification.", priority: 0, customerId: $customerId, organizationId: $organizationId}')"
incident_id="$(curl --fail --silent --show-error \
  --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  --header 'Content-Type: application/json' \
  --data "$incident_payload" \
  "$api_base_url/api/v1/incidents" | jq -er '.id')"
curl --fail --silent --show-error --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  "$api_base_url/api/v1/incidents/$incident_id" | jq -e --arg id "$incident_id" '.id == $id' > /dev/null
curl --fail --silent --show-error --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  --request PUT --header 'Content-Type: application/json' \
  --data '{"state":3,"priority":1}' \
  "$api_base_url/api/v1/incidents/$incident_id" > /dev/null

printf 'attachment durability fixture\n' > "$work_directory/attachment.txt"
attachment_id="$(curl --fail --silent --show-error \
  --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  --form "files=@$work_directory/attachment.txt;filename=rc4-smoke.txt;type=text/plain" \
  "$api_base_url/api/v1/tickets/$incident_id/attachments/" | jq -er '.[0].id')"
curl --fail --silent --show-error --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  "$api_base_url/api/v1/tickets/$incident_id/attachments/" | jq -e --arg id "$attachment_id" 'any(.[]; .id == $id)' > /dev/null
curl --fail --silent --show-error --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  "$api_base_url/api/v1/attachments/$attachment_id" > /dev/null

request_payload="$(jq -nc --arg customerId "$customer_id" --arg organizationId "$organization_id" '{title: "RC4 smoke request", description: "Production first-run request verification.", priority: 0, customerId: $customerId, organizationId: $organizationId}')"
request_id="$(curl --fail --silent --show-error \
  --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  --header 'Content-Type: application/json' \
  --data "$request_payload" \
  "$api_base_url/api/v1/requests" | jq -er '.id')"
curl --fail --silent --show-error --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  "$api_base_url/api/v1/requests/$request_id" | jq -e --arg id "$request_id" '.id == $id' > /dev/null
curl --fail --silent --show-error --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  --request PUT --header 'Content-Type: application/json' \
  --data '{"state":3,"priority":1}' \
  "$api_base_url/api/v1/requests/$request_id" > /dev/null

change_payload="$(jq -nc --arg organizationId "$organization_id" --arg administratorId "$administrator_id" '{title: "RC4 smoke change", description: "Production first-run change verification.", priority: 0, organizationId: $organizationId, requestedForUserId: $administratorId, implementorUserId: $administratorId, changeType: "Standard", implementationStartAt: "2030-01-02T10:00:00Z", implementationEndAt: "2030-01-02T11:00:00Z", changeTemplate: {isPreApproved: false, existingRunbookReference: "RC4-SMOKE", scopeOfChange: "Smoke validation", affectedSystems: ["RatelDesk"], implementationSteps: ["Verify startup"], validationSteps: ["Verify ticket creation"], rollbackPlan: "Revert the smoke change"}}')"
change_id="$(curl --fail --silent --show-error \
  --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  --header 'Content-Type: application/json' \
  --data "$change_payload" \
  "$api_base_url/api/v1/changes" | jq -er '.id')"
curl --fail --silent --show-error --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  "$api_base_url/api/v1/changes/$change_id" | jq -e --arg id "$change_id" '.id == $id' > /dev/null
curl --fail --silent --show-error --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  --request PUT --header 'Content-Type: application/json' \
  --data '{"state":3,"priority":1}' \
  "$api_base_url/api/v1/changes/$change_id" > /dev/null

self_service_email="rc4-self-service@example.test"
self_service_password="Rc4-$(openssl rand -hex 24)"
self_service_account_payload="$(jq -nc --arg email "$self_service_email" --arg organizationId "$organization_id" '{displayName: "RC4 Self-service User", email: $email, organizationId: $organizationId}')"
self_service_activation="$(curl --fail --silent --show-error \
  --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  --header 'Content-Type: application/json' \
  --data "$self_service_account_payload" \
  "$api_base_url/api/v1/local-auth/users")"
self_service_activation_token="$(jq -er '.activationToken' <<< "$self_service_activation")"
self_service_activate_payload="$(jq -nc --arg email "$self_service_email" --arg token "$self_service_activation_token" --arg password "$self_service_password" '{email: $email, activationToken: $token, newPassword: $password}')"
curl --fail --silent --show-error \
  --header 'Content-Type: application/json' \
  --data "$self_service_activate_payload" \
  "$api_base_url/api/v1/local-auth/activate" > /dev/null

self_service_login_payload="$(jq -nc --arg email "$self_service_email" --arg password "$self_service_password" '{email: $email, password: $password, rememberMe: false}')"
curl --fail --silent --show-error \
  --cookie-jar "$self_service_cookie_jar" \
  --header 'Content-Type: application/json' \
  --header 'X-Requested-With: XMLHttpRequest' --data "$self_service_login_payload" \
  "$api_base_url/api/v1/local-auth/login" > /dev/null

self_service_form_payload="$(jq -nc --arg organizationId "$organization_id" '{serviceId: "rc4-self-service", title: "RC4 self-service request", description: "Production local account validation.", jsonSchema: "{\"title\":\"RC4 self-service request\",\"description\":\"Production local account validation.\",\"fields\":[]}", organizationId: $organizationId, allowedOrganizationIds: [$organizationId], releaseStatus: 1}')"
self_service_form_id="$(curl --fail --silent --show-error \
  --cookie "$cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  --header 'Content-Type: application/json' \
  --data "$self_service_form_payload" \
  "$api_base_url/api/v1/request-forms" | jq -er '.id')"
self_service_request_payload="$(jq -nc --arg requestFormId "$self_service_form_id" '{requestFormId: $requestFormId, payloadJson: "{}"}')"
self_service_request_id="$(curl --fail --silent --show-error \
  --cookie "$self_service_cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  --header 'Content-Type: application/json' \
  --data "$self_service_request_payload" \
  "$api_base_url/api/v1/self-service/requests" | jq -er '.requestId')"
curl --fail --silent --show-error --cookie "$self_service_cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  "$api_base_url/api/v1/self-service/requests/$self_service_request_id" | jq -e --arg id "$self_service_request_id" '.id == $id' > /dev/null

self_service_attachment_id="$(curl --fail --silent --show-error \
  --cookie "$self_service_cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  --form 'files=@/dev/null;filename=rc4-self-service.txt;type=text/plain' \
  "$api_base_url/api/v1/tickets/$self_service_request_id/attachments/" | jq -er '.[0].id')"
curl --fail --silent --show-error --cookie "$self_service_cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  "$api_base_url/api/v1/tickets/$self_service_request_id/attachments/" | jq -e --arg id "$self_service_attachment_id" 'any(.[]; .id == $id)' > /dev/null
curl --fail --silent --show-error --cookie "$self_service_cookie_jar" --header 'X-Requested-With: XMLHttpRequest' \
  "$api_base_url/api/v1/attachments/$self_service_attachment_id" > /dev/null

# Exercise provider-specific timeline SQL, then verify durable files and cookies after recreation.
for ticket_route in "incidents/$incident_id" "requests/$request_id" "changes/$change_id"; do
  curl --fail --silent --show-error --cookie "$cookie_jar" \
    "$api_base_url/api/v1/$ticket_route/timeline" | jq -e 'type == "array"' > /dev/null
done
docker compose "${compose_arguments[@]}" up --detach --no-deps --force-recreate api
curl --retry 30 --retry-all-errors --retry-delay 2 --fail --silent --show-error \
  --cookie "$cookie_jar" "$api_base_url/api/v1/attachments/$attachment_id" > "$work_directory/restored.txt"
cmp "$work_directory/attachment.txt" "$work_directory/restored.txt"
curl --fail --silent --show-error --cookie "$web_cookie_jar" \
  "$web_base_url/api/v1/auth/me" | jq -e '.isAuthenticated == true and .isHelpdeskAdmin == true' > /dev/null

curl --fail --silent --show-error "$api_base_url/api/v1/setup/status" | jq -e '.state == "Ready"' > /dev/null

echo "First-run $setup_provider setup, local sign-in, core ticket CRUD, self-service request, and attachment smoke test passed."

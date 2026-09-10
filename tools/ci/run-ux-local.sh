#!/usr/bin/env bash

set -euo pipefail

repo_root=$(git rev-parse --show-toplevel)
cd "$repo_root"

api_port=${HELPDESK_E2E_API_PORT:-5158}
web_port=${HELPDESK_E2E_WEB_PORT:-5157}
database_port=${HELPDESK_E2E_DATABASE_PORT:-55432}
api_url="http://127.0.0.1:${api_port}"
web_url="https://127.0.0.1:${web_port}"
artifact_dir=${HELPDESK_E2E_ARTIFACT_DIR:-artifacts/e2e}
compose_project="helpdesk-e2e-${RANDOM}${RANDOM}"
compose_file=docker/docker-compose.yml
mkdir -p "$artifact_dir"

e2e_system_secret=$(openssl rand -hex 32)
api_log="$artifact_dir/api.log"
web_log="$artifact_dir/web.log"
database_log="$artifact_dir/postgres.log"

api_pid=''
web_pid=''
compose_started=false

cleanup() {
  local status=$?

  for pid in "$web_pid" "$api_pid"; do
    if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null || true
      wait "$pid" 2>/dev/null || true
    fi
  done

  if [[ "$compose_started" == true ]]; then
    HELPDESK_POSTGRES_PORT="$database_port" docker compose -p "$compose_project" -f "$compose_file" logs --no-color >"$database_log" 2>&1 || true
    HELPDESK_POSTGRES_PORT="$database_port" docker compose -p "$compose_project" -f "$compose_file" down --volumes --remove-orphans >/dev/null 2>&1 || true
  fi

  exit "$status"
}
trap cleanup EXIT INT TERM

wait_for_health() {
  local name=$1
  local pid=$2
  local url=$3
  local log=$4

  for _ in $(seq 1 90); do
    if curl --insecure --fail --silent --show-error --max-time 2 "$url/health" >/dev/null 2>&1; then
      return 0
    fi

    if ! kill -0 "$pid" 2>/dev/null; then
      echo "$name exited before becoming healthy."
      sed -n '1,240p' "$log"
      return 1
    fi

    sleep 1
  done

  echo "$name did not become healthy at $url/health."
  sed -n '1,240p' "$log"
  return 1
}

wait_for_database() {
  for _ in $(seq 1 90); do
    if HELPDESK_POSTGRES_PORT="$database_port" docker compose -p "$compose_project" -f "$compose_file" exec -T postgres pg_isready --username postgres --dbname helpdesk >/dev/null 2>&1; then
      return 0
    fi

    sleep 1
  done

  echo "PostgreSQL did not become ready."
  HELPDESK_POSTGRES_PORT="$database_port" docker compose -p "$compose_project" -f "$compose_file" logs --no-color || true
  return 1
}

compose_started=true
HELPDESK_POSTGRES_PORT="$database_port" docker compose -p "$compose_project" -f "$compose_file" up --detach
wait_for_database

ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="$api_url" \
ConnectionStrings__HelpdeskDb="Host=127.0.0.1;Port=${database_port};Database=helpdesk;Username=postgres;Password=postgres" \
EmailIngestion__Enabled=false \
Helpdesk__E2eSeedData=true \
StorageOptions__ImageSigningSecret="$e2e_system_secret" \
ExchangeEmail__TenantId=00000000-0000-0000-0000-000000000000 \
ExchangeEmail__ClientId=00000000-0000-0000-0000-000000000001 \
ExchangeEmail__ClientSecret="$e2e_system_secret" \
ExchangeEmail__MailboxAddress=helpdesk-e2e@example.invalid \
SYSTEM_TOKEN_SECRET="$e2e_system_secret" \
dotnet run --project src/Helpdesk.API/Helpdesk.API.csproj --configuration Release --no-build --no-launch-profile >"$api_log" 2>&1 &
api_pid=$!
wait_for_health 'Helpdesk API' "$api_pid" "$api_url" "$api_log"

ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="$web_url" \
ApiBaseUrl="${api_url}/" \
AUTHENTIK_CLIENT_SECRET="$e2e_system_secret" \
Authentication__Authentik__Authority=https://127.0.0.1:5999/application/o/helpdesk-e2e/ \
Authentication__Authentik__ClientId=helpdesk-e2e \
Authentication__Authentik__ApiScope=helpdesk-api \
SYSTEM_TOKEN_SECRET="$e2e_system_secret" \
dotnet run --project src/HelpDesk.NewWeb/HelpDesk.NewWeb.csproj --configuration Release --no-build --no-launch-profile >"$web_log" 2>&1 &
web_pid=$!
wait_for_health 'Helpdesk web' "$web_pid" "$web_url" "$web_log"

HELPDESK_E2E_AUTH_MODE=development \
HELPDESK_E2E_BASE_URL="$web_url" \
HELPDESK_E2E_IGNORE_HTTPS_ERRORS=true \
npm run test:ux -- "$@"

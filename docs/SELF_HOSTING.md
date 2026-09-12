# Self-hosting RatelDesk

## Architecture

RatelDesk has an ASP.NET Core API and a Blazor web application. The default installation uses embedded SQLite and local accounts; PostgreSQL, OIDC, and MCP hosting are optional. The Web application proxies `/api` traffic to the API. Run both application services behind a TLS-terminating reverse proxy in production.

For built-in roles, scoped custom roles, and the bounded tenant-member delegation workflow, see [Roles and tenant-scoped access](rbac.md).

## First use and Docker

Use [docker/docker-compose.yml](../docker/docker-compose.yml) as the default starting point. It starts only Web and API in Production mode and persists four distinct concerns: bootstrap state, data-protection keys, SQLite data, and attachments. After the first API start, retrieve the operator-only setup code from `/var/lib/rateldesk/bootstrap/setup-code` in the API container and complete `http://localhost:8111/setup`. The code is consumed at completion and is never returned by HTTP APIs. On success, the API exits its restricted setup host and Compose restarts it into the normal application host; wait briefly for the sign-in page to become available.

An operator can prefill and lock non-secret interactive values with
`Bootstrap__Interactive__OrganizationName`,
`Bootstrap__Interactive__ApplicationName`,
`Bootstrap__Interactive__ApplicationUrl`, and
`Bootstrap__Interactive__TimeZoneId`. The setup page labels these as
deployment-managed and renders them read-only; the API applies the same values
at completion, so modifying the browser request cannot override them. Keep
database passwords and administrator passwords out of these values and use the
operator-controlled setup flow or unattended secret inputs instead.

Before setup completes, an operator can replace a lost or exposed setup code without reopening a completed instance:

```bash
docker compose -f docker/docker-compose.yml exec api dotnet Helpdesk.API.dll --rotate-setup-code
```

The replacement is printed once to the operator's terminal, written to the protected `setup-code` file, and invalidates existing setup sessions. The command refuses to run after setup is ready or when recovery is required.

### Operator-invoked unattended setup

An operator can initialize a new instance without rendering the wizard by
supplying the same first-run values through protected deployment configuration,
then invoking the explicit command once. It is never performed automatically
at API startup. For SQLite, set `Bootstrap__Unattended__Provider=Sqlite`,
`Bootstrap__Unattended__Email`, `Bootstrap__Unattended__DisplayName`,
`Bootstrap__Unattended__Password`, and
`Bootstrap__Unattended__OrganizationName`; optional
`Bootstrap__Unattended__ApplicationName` and
`Bootstrap__Unattended__ApplicationUrl` use the same branding behavior as the
wizard. `Bootstrap__Unattended__TimeZoneId` accepts an installed IANA time-zone
ID and defaults to `UTC`. The password belongs in an operator-controlled secret
input, not a committed Compose file.

Run the command in the API container:

```bash
docker compose -f docker/docker-compose.yml exec api \
  dotnet Helpdesk.API.dll --initialize-unattended
```

For PostgreSQL, set `Bootstrap__Unattended__Provider=PostgreSql` and
`Bootstrap__Unattended__PostgreSqlConnectionString` instead. The same bounded
preflight accepts only an empty target with `vector` and `pg_trgm` available.
Completion writes the normal durable marker and descriptor. A restricted API
host observes that descriptor, exits, and Compose restarts it into the normal
runtime; it does not retain the unattended password.

For the documented localhost HTTP profile, both services set `Authentication__AllowInsecureLocalhost=true`; this intentionally uses the `RatelDesk.Local` cookie rather than a `__Host-` cookie. Public deployments must use HTTPS, set a durable shared `DataProtection__KeyRingPath`, and remove that localhost-only setting.

Back up the SQLite data directory, including both `rateldesk.db` and the durable `rateldesk.hangfire.db` scheduler database, together with the bootstrap state directory, shared key ring, and attachment directory as one recovery set. Restoring a SQLite file without its initialization descriptor or data-protection key material can require operator recovery.

SQLite uses its own EF Core migration assembly; setup and every normal API startup apply that migration history rather than using `EnsureCreated`. To upgrade, take a verified backup, deploy one versioned image release, and allow the API to start. If migration fails, stop the new image and restore the complete backup set before retrying. Changing a running instance from SQLite to PostgreSQL (or the reverse) is not an in-place upgrade: provision the target database and migrate data through an explicit export/import plan.

## PostgreSQL

An existing PostgreSQL deployment continues to use `ConnectionStrings__HelpdeskDb`; select `Database__Provider=PostgreSql` explicitly for new deployment-managed PostgreSQL configuration. Preserve the database and shared data-protection keys before an upgrade. PostgreSQL uses the existing provider-specific migration chain and requires the extensions used by it, including `vector` and `pg_trgm`; do not point RatelDesk at an unrelated or non-empty database.

### Bundled PostgreSQL

For a fresh local or single-host deployment, combine the default stack with [docker/docker-compose.postgres.yml](../docker/docker-compose.postgres.yml). It runs the compatible `pgvector/pgvector:pg16` image, creates `vector` and `pg_trgm` only when its new PostgreSQL volume is initialized, and keeps database data in a separate named volume:

```bash
RATELDESK_POSTGRES_PASSWORD='replace-with-a-secret' \
  docker compose -f docker/docker-compose.yml -f docker/docker-compose.postgres.yml up --build
```

Complete `/setup` with host `postgres`, port `5432`, database/user `rateldesk`, and the supplied password. The bundled database uses a local password for this recipe; use an externally managed PostgreSQL service with backups, restricted credentials, TLS, and managed extension lifecycle for public or multi-host deployments. The sidecar is not an automatic SQLite-to-PostgreSQL migration path.

### External PostgreSQL

For an interactive first run, start the default source or release Compose stack and select PostgreSQL at `/setup`. The API remains in the restricted setup host until it has preflighted the external connection and committed the one-time initialization. The external database must therefore be reachable from the API container, but its credential is supplied only in the setup request; it is never placed in the normal runtime environment.

For deployment-managed unattended initialization, use [docker/docker-compose.external-postgres.yml](../docker/docker-compose.external-postgres.yml) with either base Compose file. It maps operator-supplied values only to the `Bootstrap__Unattended__*` settings; it does not set `ConnectionStrings__HelpdeskDb`, which is reserved for already-established deployments and their conservative adoption path.

```bash
RATELDESK_POSTGRES_CONNECTION_STRING='Host=db.example.test;Port=5432;Database=rateldesk;Username=rateldesk;Password=replace-with-a-secret;Ssl Mode=Require' \
RATELDESK_BOOTSTRAP_ADMIN_EMAIL='admin@example.test' \
RATELDESK_BOOTSTRAP_ADMIN_DISPLAY_NAME='Initial Administrator' \
RATELDESK_BOOTSTRAP_ADMIN_PASSWORD='replace-with-a-long-passphrase' \
RATELDESK_BOOTSTRAP_ORGANIZATION_NAME='Example Organization' \
  docker compose -f docker/docker-compose.release.yml -f docker/docker-compose.external-postgres.yml up -d

docker compose -f docker/docker-compose.release.yml -f docker/docker-compose.external-postgres.yml exec api \
  dotnet Helpdesk.API.dll --initialize-unattended
```

For a new instance, the target must be empty. RatelDesk tests the connection with bounded timeouts before it creates schema. It identifies a historical RatelDesk schema separately from an unrelated non-empty database, but refuses setup for both so an existing installation cannot be modified by a first-run session. The setup principal must be able to apply the existing migrations but does not need superuser access. A database administrator must install the required `vector` and `pg_trgm` extensions in the target database before setup, and should enforce TLS, least-privilege credentials, and provider-managed backups. Deployment-managed `ConnectionStrings__HelpdeskDb` remains the compatibility path for established installations.

Back up an external deployment as a complete recovery set: a consistent PostgreSQL backup (including required extensions and roles according to the database provider's procedure), the bootstrap state directory, the shared data-protection key ring, and attachments. Test restoring that set into an isolated target before relying on it. To upgrade, take and verify this backup, deploy one versioned image release, and let the API apply its existing migration chain. If the migration fails, stop the new image and restore the complete recovery set; never point the original initialized descriptor at a new empty database to recover it.

## Reverse proxy and Traefik

Any standards-compliant reverse proxy can front RatelDesk. A generic Traefik deployment should route the public host to the web service and preserve HTTPS, forwarded headers, and WebSocket/SSE support. Keep deployment-specific routers, DNS names, and certificates outside this repository.

## Identity

Local accounts are the first-run default and public self-registration is disabled. OIDC is supported with Authentik and Microsoft Entra ID through `Authentication__Mode=Oidc` or `Hybrid`. Configure each provider with an issuer under a domain you control, a client ID, a client secret supplied outside source control, and public callback URLs. Use `id.example.com` and `helpdesk.example.com` only as documentation examples.

### Local administrator recovery

When SMTP is unavailable, an operator with container access can issue a one-time local-account recovery token without resetting the instance or opening setup:

```bash
docker compose -f docker/docker-compose.yml exec api \
  dotnet Helpdesk.API.dll --recover-local-admin admin@example.com
```

The command only succeeds for an existing instance-administrator account in the selected identity store. It enables that account, invalidates old sessions, and writes a one-time token only to the command output. Give the administrator the `/activate` page and token through an approved secure channel; the token is consumed when a new passphrase is set.

### Local account security

Signed-in local users can change their passphrase at `/account/change-password` and set up a time-based authenticator at `/account/authenticator`. The authenticator page gives the user a shared key to enter or scan in a TOTP application, requires its current six-digit code to confirm setup, and displays ten recovery codes exactly once. Store recovery codes separately from the authenticator device. After TOTP is enabled, local sign-in requires either an authenticator code or an unused recovery code. An administrator can issue a fresh one-time activation token for a local account through the user administration UI; this is the no-SMTP password-reset path.

## Email

Microsoft Graph delivery is disabled by default. Enable it only after configuring a tenant ID, client ID, client secret, and mailbox. SMTP and IMAP settings are likewise deployment-owned credentials. Use a mailbox dedicated to RatelDesk and configure sender-domain controls deliberately.

## AI, MCP, and orchestration

AI providers are configurable and should use provider-specific credentials from a secret store. MCP hosts use isolated `RATELDESK_MCP_CONFIG` configuration files and `RATELDESK_MCP_<INSTANCE>_API_BASE_URL` endpoint pinning. External orchestration is an optional integration: configure a provider endpoint and credentials only when required by your deployment.

### AI Assistant chat recovery

An AI Assistant turn is `Processing` only while RatelDesk has recent, durable provider activity. Set `AiAssistantChat__TurnInactivityTimeout` to the maximum acceptable silent interval (default `00:05:00`) and `AiAssistantChat__ActivityHeartbeatInterval` to the bounded activity checkpoint period (default `00:00:15`). Both must be positive and the heartbeat must be shorter than the timeout.

When no activity is checkpointed before the timeout, RatelDesk changes the turn to `DeliveryUnknown`. This is intentionally not a retry state: the provider could have admitted the message or already executed a tool, so RatelDesk never resends it automatically. Operators can choose **Stop waiting** while a turn is processing and confirm the same safe local transition. They can then check/reconcile the saved remote session, or acknowledge the uncertainty and archive the transcript to start a blank conversation.

An API restart is different from a silent live connection: RatelDesk immediately marks any persisted `Processing` turn as `DeliveryUnknown`, because the prior in-memory transport owner no longer exists. It records that restart boundary and never resends the turn.

## Observability

OpenTelemetry is enabled through standard `OTEL_*` settings. Export to an OTLP endpoint you operate and avoid placing user, ticket, or credential data in telemetry attributes.

## Troubleshooting

- Verify the selected database; PostgreSQL additionally needs its required extensions.
- Confirm the API is reachable from the web service's configured reverse-proxy destination.
- Check OIDC issuer, callback, audience, and client-secret configuration when login fails.
- Confirm `DataProtection__KeyRingPath` is writable and durable in production.
- Use health endpoints and structured logs before changing application state.

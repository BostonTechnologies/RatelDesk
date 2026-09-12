# Self-hosting RatelDesk

## Architecture

RatelDesk has an ASP.NET Core API and a Blazor web application. The default installation uses embedded SQLite and local accounts; PostgreSQL, OIDC, and MCP hosting are optional. The Web application proxies `/api` traffic to the API. Run both application services behind a TLS-terminating reverse proxy in production.

## First use and Docker

Use [docker/docker-compose.yml](../docker/docker-compose.yml) as the default starting point. It starts only Web and API in Production mode and persists four distinct concerns: bootstrap state, data-protection keys, SQLite data, and attachments. After the first API start, retrieve the operator-only setup code from `/var/lib/rateldesk/bootstrap/setup-code` in the API container and complete `http://localhost:8111/setup`. The code is consumed at completion and is never returned by HTTP APIs.

For the documented localhost HTTP profile, both services set `Authentication__AllowInsecureLocalhost=true`; this intentionally uses the `RatelDesk.Local` cookie rather than a `__Host-` cookie. Public deployments must use HTTPS, set a durable shared `DataProtection__KeyRingPath`, and remove that localhost-only setting.

Back up the SQLite data directory, bootstrap state directory, shared key ring, and attachment directory as one recovery set. Restoring a SQLite file without its initialization descriptor or data-protection key material can require operator recovery.

## PostgreSQL

An existing PostgreSQL deployment continues to use `ConnectionStrings__HelpdeskDb`; select `Database__Provider=PostgreSql` explicitly for new deployment-managed PostgreSQL configuration. Preserve the database and shared data-protection keys before an upgrade. PostgreSQL requires the extensions used by the existing migration chain, including `vector` and `pg_trgm`; do not point RatelDesk at an unrelated or non-empty database.

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

# Self-hosting RatelDesk

## Architecture

RatelDesk has an ASP.NET Core API, a Blazor web application, PostgreSQL with `pgvector`, and optional MCP HTTP hosting. The web application proxies `/api` traffic to the API. Run both application services behind a TLS-terminating reverse proxy in production.

## PostgreSQL and Docker

Use [docker/docker-compose.yml](../docker/docker-compose.yml) as a local starting point. Set `ConnectionStrings__HelpdeskDb` to a PostgreSQL connection string and keep database volumes outside the container lifecycle. Apply schema migrations as part of the application deployment process after backing up the database.

## Reverse proxy and Traefik

Any standards-compliant reverse proxy can front RatelDesk. A generic Traefik deployment should route the public host to the web service and preserve HTTPS, forwarded headers, and WebSocket/SSE support. Keep deployment-specific routers, DNS names, and certificates outside this repository.

## Identity

OIDC is supported with Authentik and Microsoft Entra ID. Configure each provider with an issuer under a domain you control, a client ID, a client secret supplied outside source control, and public callback URLs. Use `id.example.com` and `helpdesk.example.com` only as documentation examples.

## Email

Microsoft Graph delivery is disabled by default. Enable it only after configuring a tenant ID, client ID, client secret, and mailbox. SMTP and IMAP settings are likewise deployment-owned credentials. Use a mailbox dedicated to RatelDesk and configure sender-domain controls deliberately.

## AI, MCP, and orchestration

AI providers are configurable and should use provider-specific credentials from a secret store. MCP hosts use isolated `RATELDESK_MCP_CONFIG` configuration files and `RATELDESK_MCP_<INSTANCE>_API_BASE_URL` endpoint pinning. External orchestration is an optional integration: configure a provider endpoint and credentials only when required by your deployment.

### AI Assistant chat recovery

An AI Assistant turn is `Processing` only while RatelDesk has recent, durable provider activity. Set `AiAssistantChat__TurnInactivityTimeout` to the maximum acceptable silent interval (default `00:05:00`) and `AiAssistantChat__ActivityHeartbeatInterval` to the bounded activity checkpoint period (default `00:00:15`). Both must be positive and the heartbeat must be shorter than the timeout.

When no activity is checkpointed before the timeout, RatelDesk changes the turn to `DeliveryUnknown`. This is intentionally not a retry state: the provider could have admitted the message or already executed a tool, so RatelDesk never resends it automatically. Operators can choose **Stop waiting** while a turn is processing and confirm the same safe local transition. They can then check/reconcile the saved remote session, or acknowledge the uncertainty and archive the transcript to start a blank conversation.

## Observability

OpenTelemetry is enabled through standard `OTEL_*` settings. Export to an OTLP endpoint you operate and avoid placing user, ticket, or credential data in telemetry attributes.

## Troubleshooting

- Verify the database connection and `pgvector` extension first.
- Confirm the API is reachable from the web service's configured reverse-proxy destination.
- Check OIDC issuer, callback, audience, and client-secret configuration when login fails.
- Confirm `DataProtection__KeyRingPath` is writable and durable in production.
- Use health endpoints and structured logs before changing application state.

<p align="center"><img src=".github/assets/rateldesk-wordmark.png" alt="RatelDesk" width="800" /></p>

# RatelDesk

RatelDesk is a self-hosted, multi-tenant service-desk application built with ASP.NET Core and Blazor. It manages incidents, requests, changes, work logs, knowledge, email workflows, and optional AI-assisted operations.

It is designed to run with PostgreSQL and can integrate with OIDC providers (including Authentik and Microsoft Entra ID), Microsoft Graph, SMTP/IMAP, OpenTelemetry, MCP clients, AI providers, and external orchestration providers.

## Quick start

Prerequisites: Docker with Compose, or the .NET SDK specified by [global.json](global.json). PostgreSQL must have the `vector` extension available.

```bash
docker compose -f docker/docker-compose.yml up --build
```

For local .NET development, start PostgreSQL and then run:

```bash
dotnet restore Helpdesk.sln
dotnet run --project src/Helpdesk.API
dotnet run --project src/HelpDesk.NewWeb
```

The Compose stack provides local PostgreSQL, API, and web services at `http://localhost:8111`. It enables the local development operator only; do not use it as a production identity configuration. Its OIDC and signed-link defaults are intentionally non-secret placeholders so the containers can start; replace `RATELDESK_OIDC_CLIENT_ID`, `RATELDESK_OIDC_CLIENT_SECRET`, and `RATELDESK_IMAGE_SIGNING_SECRET` before exposing the application. The application configuration files contain only example values. Put real credentials in environment variables, a secret manager, or .NET user secrets; do not commit them.

## Configuration

The main public configuration surfaces are:

- `ConnectionStrings__HelpdeskDb` for PostgreSQL.
- `DataProtection__KeyRingPath` for a persistent, shared key-ring directory in production.
- `Authentication__Authentik__*` or `Authentication__Azure__*` for OIDC.
- `ExchangeEmail__*` for Microsoft Graph email delivery; set `ExchangeEmail__Enabled=true` only after supplying credentials.
- `ImapEmail__*` and SMTP configuration for inbound/outbound email workflows.
- `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_RESOURCE_ATTRIBUTES` for telemetry export.
- `RATELDESK_MCP_*` for isolated MCP clients.
- `Branding__*` for deployment-owned instance identity. See [branding](docs/branding.md).
- AI-provider and orchestration configuration through the administration UI or documented environment configuration.

`/tmp/rateldesk/keys` is a safe local fallback for data-protection keys. Production deployments must override it with a durable mounted volume or managed key store.

## Documentation

See [self-hosting guidance](docs/SELF_HOSTING.md) for configuration, Docker, reverse-proxy, identity, email, AI, MCP, orchestration, telemetry, and troubleshooting guidance.
See [instance branding](docs/branding.md) to customise the customer-facing identity without forking RatelDesk.

## Contributing and security

Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a change. Report vulnerabilities privately according to [SECURITY.md](SECURITY.md).

## License

RatelDesk is licensed under the [Apache License 2.0](LICENSE).

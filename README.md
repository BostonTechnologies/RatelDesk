<p align="center"><img src=".github/assets/rateldesk-wordmark.png" alt="RatelDesk" width="800" /></p>

# RatelDesk

RatelDesk is a self-hosted, multi-tenant service-desk application built with ASP.NET Core and Blazor. It manages incidents, requests, changes, work logs, knowledge, email workflows, and optional AI-assisted operations.

It starts with embedded SQLite and local accounts, with PostgreSQL, OIDC providers (including Authentik and Microsoft Entra ID), email, AI, MCP, telemetry, and orchestration available as optional deployment integrations.

## Quick start

Prerequisites: Docker with Compose, or the .NET SDK specified by [global.json](global.json). No database or identity-provider service is required for a new installation.

```bash
docker compose -f docker/docker-compose.yml up --build
```

Open `http://localhost:8111/setup`. Retrieve the one-time, per-installation setup code only from the API container:

```bash
docker compose -f docker/docker-compose.yml exec api cat /var/lib/rateldesk/bootstrap/setup-code
```

The default Compose stack runs Web and API in Production mode with persistent SQLite, bootstrap, data-protection, and attachment volumes. It is explicitly configured for localhost HTTP so local-account cookies use the valid `RatelDesk.Local` name. Deploy HTTPS and remove `Authentication__AllowInsecureLocalhost` for public hosting.

For local .NET development, run the API and Web projects with their configuration pointed at durable local paths. The same `/setup` flow initializes a fresh local database.

## Released containers

To run published images rather than build from source, select an exact published version and use the release Compose file:

```bash
RATELDESK_VERSION=0.1.0-rc.4 docker compose -f docker/docker-compose.release.yml up -d
```

Use an exact SemVer tag in production, or preferably replace tags with the published image digests. `latest` advances only for stable releases; prereleases never move it. See [release engineering](docs/releases.md) for versioning, build metadata, and release instructions.

## Bundled PostgreSQL

For a fresh installation with PostgreSQL and the required `vector` and `pg_trgm` extensions, add the bundled sidecar overlay. Choose a unique password outside source control:

```bash
RATELDESK_POSTGRES_PASSWORD='replace-with-a-secret' \
  docker compose -f docker/docker-compose.yml -f docker/docker-compose.postgres.yml up --build
```

At `/setup`, select PostgreSQL and enter host `postgres`, port `5432`, database `rateldesk`, user `rateldesk`, and the password supplied above. For published images, replace the first Compose file with `docker/docker-compose.release.yml` and set `RATELDESK_VERSION` to an exact release tag.

## Configuration

The main public configuration surfaces are:

- `Database__Provider=Sqlite|PostgreSql`; an explicit PostgreSQL provider uses `ConnectionStrings__HelpdeskDb`, while a fresh SQLite installation is initialized through `/setup`.
- `Database__Sqlite__Path` for the SQLite file location when it is deployment-managed.
- `Authentication__Mode=Local|Oidc|Hybrid`; Local is the first-run default.
- `DataProtection__KeyRingPath` for a persistent, shared key-ring directory in production.
- `Authentication__Authentik__*` or `Authentication__Azure__*` for OIDC.
- `ExchangeEmail__*` for Microsoft Graph email delivery; set `ExchangeEmail__Enabled=true` only after supplying credentials.
- `ImapEmail__*` and SMTP configuration for inbound/outbound email workflows.
- `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_RESOURCE_ATTRIBUTES` for telemetry export.
- `RATELDESK_MCP_*` for isolated MCP clients.
- `Branding__*` for deployment-owned instance identity. See [branding](docs/branding.md).
- AI-provider and orchestration configuration through the administration UI or documented environment configuration.

`/tmp/rateldesk/keys` is a safe local fallback for data-protection keys. Production deployments must override it with a durable mounted volume or managed key store. Back up the SQLite data directory, bootstrap state, data-protection key ring, and attachments together.

## Documentation

See [self-hosting guidance](docs/SELF_HOSTING.md) for configuration, Docker, reverse-proxy, identity, email, AI, MCP, orchestration, telemetry, and troubleshooting guidance.
See [instance branding](docs/branding.md) to customise the customer-facing identity without forking RatelDesk.

## Contributing and security

Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a change. Report vulnerabilities privately according to [SECURITY.md](SECURITY.md).

## License

RatelDesk is licensed under the [Apache License 2.0](LICENSE).

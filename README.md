<p align="center"><img src=".github/assets/rateldesk-wordmark.png" alt="RatelDesk" width="800" /></p>

# RatelDesk

RatelDesk is a self-hosted, multi-tenant service-desk application built with ASP.NET Core and Blazor. It manages incidents, requests, changes, work logs, knowledge, email workflows, and optional AI-assisted operations.

It starts with embedded SQLite and local accounts, with PostgreSQL, OIDC providers (including Authentik and Microsoft Entra ID), email, AI, MCP, telemetry, and orchestration available as optional deployment integrations.

## Quick start

Prerequisites: Docker with Compose, or the .NET SDK specified by [global.json](global.json). No database or identity-provider service is required for a new installation. Run these Compose commands from the root of a checked-out copy of this repository:

```bash
docker compose -f docker/docker-compose.yml up --build
```

Open `http://localhost:8111/`. A fresh instance opens the seven-step setup wizard automatically. Retrieve its setup code from the API container; this command finds the code using the API's configuration and does not change it:

```bash
docker compose -f docker/docker-compose.yml exec api dotnet /app/Helpdesk.API.dll --show-setup-code
```

Paste that code into **Unlock setup**, then choose storage, set the instance details, create the first administrator, optionally add branding, review, and finish. Sign in with the administrator email and password you just created. No authenticator or recovery code is needed for the initial login. Two-factor verification appears only for accounts that explicitly enabled it later in account settings.

**Already deployed through a container manager?** You do not need a local Compose file or an interactive terminal. Run these commands on the Docker host, replacing `rateldesk-api-1` with the actual API container name from the first command:

```sh
docker ps --format 'table {{.Names}}\t{{.Image}}'
docker exec rateldesk-api-1 dotnet /app/Helpdesk.API.dll --show-setup-code
```

If you are already inside the API container's `sh` terminal, run `dotnet /app/Helpdesk.API.dll --show-setup-code`. The Alpine image includes `sh`; Bash is not required. Open your application's public URL to continue setup. If retrieval fails, run the same command with `--setup-status` for the API version, configured state directory, and setup state without printing secrets. See [setup troubleshooting](docs/SELF_HOSTING.md#setup-code-and-container-troubleshooting) for missing state or an older image. These read commands require rc.5 or later.

The default Compose stack runs Web and API in Production mode with persistent SQLite, bootstrap, data-protection, and attachment volumes. It is explicitly configured for localhost HTTP so local-account cookies use the valid `RatelDesk.Local` name. Deploy HTTPS and remove `Authentication__AllowInsecureLocalhost` for public hosting.

For local .NET development, run the API and Web projects with their configuration pointed at durable local paths. The same `/setup` flow initializes a fresh local database.

## Released containers

To run published images rather than build from source, select an exact published version and use the release Compose file:

```bash
RATELDESK_VERSION=0.1.0 docker compose -f docker/docker-compose.release.yml up -d
```

Use an exact SemVer tag in production, or preferably replace tags with the published image digests. `latest` advances only for stable releases; prereleases never move it. See [release engineering](docs/releases.md) for versioning, build metadata, and release instructions.

## Bundled PostgreSQL

For a fresh installation with PostgreSQL, add the bundled sidecar overlay. Choose a unique password outside source control:

```bash
RATELDESK_POSTGRES_PASSWORD='replace-with-a-secret' \
  docker compose -f docker/docker-compose.yml -f docker/docker-compose.postgres.yml up --build
```

At `/setup`, select PostgreSQL and enter host `postgres`, port `5432`, database `rateldesk`, user `rateldesk`, and the password supplied above. For published images, replace the first Compose file with `docker/docker-compose.release.yml` and set `RATELDESK_VERSION` to an exact release tag.

## External PostgreSQL

For an externally managed PostgreSQL database, start the normal source or release stack and enter its connection details at `/setup`; the API must be able to reach the database network. The target must be empty for a new setup.

```bash
docker compose -f docker/docker-compose.yml up --build
```

Complete `/setup` with the external host, port, database, user, password, and TLS preference. For published images, replace the first Compose file with `docker/docker-compose.release.yml` and use an exact `RATELDESK_VERSION`. The included external overlay is for explicit unattended initialization and requires operator-supplied secrets; it never maps a fresh-install connection string directly into the normal runtime. See [self-hosting guidance](docs/SELF_HOSTING.md#external-postgresql) for preflight, unattended setup, upgrade, backup, and recovery requirements.

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

## API, CLI, and MCP

The Web-hosted [RatelDesk API reference](/api/docs) groups the public API by product area and sends Try It requests through the established `/api` proxy. Local account login is a browser-cookie flow with CSRF protection; a browser cookie is not a CLI or MCP Bearer credential.

For automation, sign in normally, complete configured MFA, then create a bounded API integration credential through `POST /api/v1/integration-credentials`. The secret is returned only by the create response. Store it in a protected configuration file or secret mount. Its effective access is always the intersection of the account's current authorization, the credential's selected permissions, and its organization scope; revocation and account disablement take effect on later requests.

GitHub Releases contain self-contained `rateldesk` CLI and `rateldesk-mcp` stdio MCP archives for Linux x64/arm64 and Windows x64. The Linux archives target glibc distributions, not Alpine/musl. Both executables support offline `--help` and `--version` before loading credentials.

HTTP MCP is optional and never joins the base Web/API stack. The source and release overlays are documented in [the HTTP MCP example](docker/examples/mcp-http/README.md). The existing Authentik HTTP MCP mode remains a separately configured external identity integration; it is not a fallback for local credentials.

## Contributing and security

Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a change. Report vulnerabilities privately according to [SECURITY.md](SECURITY.md).

## License

RatelDesk is licensed under the [Apache License 2.0](LICENSE).

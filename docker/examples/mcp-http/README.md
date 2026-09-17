# Optional HTTP MCP transport

This example is an optional, stateless HTTP MCP transport for an existing
RatelDesk API. It does not start an API or a database. This external-mode
example requires the configured Authentik MCP authorization server, audience,
scope, and group settings in `.env`. The local-account paired gateway flow is
not represented by this container configuration yet; do not label an ordinary
API integration credential as an MCP credential.

```sh
cp .env.example .env
cp config.example.json config.json
chmod 600 config.json
docker compose up -d
```

Set `RATELDESK_API_BASE_URL` to the exact API target,
`RATELDESK_MCP_PUBLIC_RESOURCE_URI` to the public HTTPS `/mcp` URL. Configure
`RATELDESK_MCP_ALLOWED_ORIGIN` to the exact browser origin (not a path), and
the `RATELDESK_MCP_AUTHENTIK_*` settings to the external MCP client contract.
The MCP container receives only its protected configuration file and does not
mount the API database or data-protection key ring.

For a source checkout, run from the repository root:

```sh
docker compose -f docker/docker-compose.yml -f docker/docker-compose.mcp.yml up --build
```

For release images, supply the one selected version to both files:

```sh
RATELDESK_VERSION=0.1.0-rc.9 \
  docker compose -f docker/docker-compose.release.yml -f docker/docker-compose.mcp.release.yml up -d
```

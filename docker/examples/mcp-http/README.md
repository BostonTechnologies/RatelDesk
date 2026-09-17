# Optional HTTP MCP transport

This example is an optional, stateless HTTP MCP transport for an existing
RatelDesk API. It does not start an API or a database. This example uses the
local-account paired gateway flow. Configure a local MCP
credential in RatelDesk for this exact public resource URI, then manually
configure that credential as a Bearer token in the MCP client. The gateway
exchanges it for a request-scoped, short-lived execution credential; it never
forwards the credential to ordinary API operations.

```sh
cp .env.example .env
cp config.example.json config.json
chmod 600 config.json
docker compose up -d
```

Set `RATELDESK_API_BASE_URL` to the exact API target,
`RATELDESK_MCP_PUBLIC_RESOURCE_URI` to the public HTTPS `/mcp` URL. Configure
`RATELDESK_MCP_ALLOWED_ORIGIN` to the exact browser origin (not a path).
The MCP container receives only its protected configuration file and does not
mount the API database or data-protection key ring.

For an external OAuth deployment, use the source or release compose overlay
with `Helpdesk__Mcp__AuthenticationMode=authentik` and its required
`Authentication__AuthentikMcp__*` settings. Authentik mode is explicit and is
never used as a fallback for local paired credentials.

For a source checkout, run from the repository root:

```sh
docker compose -f docker/docker-compose.yml -f docker/docker-compose.mcp.yml up --build
```

For release images, supply the one selected version to both files:

```sh
RATELDESK_VERSION=0.1.0-rc.9 \
  docker compose -f docker/docker-compose.release.yml -f docker/docker-compose.mcp.release.yml up -d
```

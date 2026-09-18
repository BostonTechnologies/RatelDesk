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
mount the API database or data-protection key ring. The one-shot
`mcp-config-init` service reads the host file as root, copies it into a named
volume with owner `10001:10001` and mode `0400`, then exits. The running MCP
gateway remains non-root and mounts only that copied file read-only.

For the combined local Web/API/MCP stack, use the gateway-specific overlay;
it has no Authentik variables. The API target is internal (`http://api:8222/`)
while `RATELDESK_MCP_PUBLIC_RESOURCE_URI` is the URI configured in the MCP
client and paired credential.

```sh
cp docker/examples/mcp-http/config.example.json config.gateway.json
chmod 600 config.gateway.json
RATELDESK_MCP_CONFIG_FILE="$PWD/config.gateway.json" \
docker compose -f docker/docker-compose.yml -f docker/docker-compose.mcp.gateway.yml up --build
```

For an extracted release deployment, replace the source overlay with
`docker-compose.release.yml` and `docker-compose.mcp.gateway.release.yml`,
set `RATELDESK_VERSION` to the exact release version, and use the same setup
sequence: initialize the API, create an account-owned paired credential, then
connect the MCP client. Revoke that credential to invalidate future exchanges.

For an external OAuth deployment, use the source or release compose overlay
with `Helpdesk__Mcp__AuthenticationMode=authentik`. Copy
`config.authentik.example.json` to a protected file and replace every
placeholder: it supplies the downstream service-account configuration, while
the Compose settings below supply the separate ingress issuer, audience,
scope, and group checks. Authentik mode is explicit and is never used as a
fallback for local paired credentials.

For a source checkout, run from the repository root:

```sh
cp docker/examples/mcp-http/config.authentik.example.json config.authentik.json
chmod 600 config.authentik.json
RATELDESK_MCP_CONFIG_FILE="$PWD/config.authentik.json" \
RATELDESK_MCP_AUTHENTIK_AUTHORITY=https://auth.example.test/ \
docker compose --env-file docker/examples/mcp-http/.env \
  -f docker/docker-compose.yml -f docker/docker-compose.mcp.yml up --build
```

For release images, supply the one selected version to both files:

```sh
cp docker/examples/mcp-http/config.authentik.example.json config.authentik.json
chmod 600 config.authentik.json
RATELDESK_MCP_CONFIG_FILE="$PWD/config.authentik.json" \
RATELDESK_MCP_AUTHENTIK_AUTHORITY=https://auth.example.test/ \
docker compose --env-file docker/examples/mcp-http/.env \
  -f docker/docker-compose.release.yml -f docker/docker-compose.mcp.release.yml up -d
```

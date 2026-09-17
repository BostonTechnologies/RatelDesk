# Optional HTTP MCP transport

This example is an optional, stateless HTTP MCP transport for an existing
RatelDesk API. It does not start an API or a database. Create an MCP-targeted
integration credential after completing normal RatelDesk setup and sign-in,
then save it in `config.json` with owner-only permissions:

```sh
cp .env.example .env
cp config.example.json config.json
chmod 600 config.json
docker compose up -d
```

Set `RATELDESK_API_BASE_URL` to the exact API target and
`RATELDESK_MCP_PUBLIC_RESOURCE_URI` to the public HTTPS `/mcp` URL. Configure
the remote MCP client with that URL and its Bearer credential; local account
cookies are browser sessions, not MCP credentials. The MCP container receives
only its protected configuration file and does not mount the API database or
data-protection key ring.

For a source checkout, run from the repository root:

```sh
docker compose -f docker/docker-compose.yml -f docker/docker-compose.mcp.yml up --build
```

For release images, supply the one selected version to both files:

```sh
RATELDESK_VERSION=0.1.0-rc.9 \
  docker compose -f docker/docker-compose.release.yml -f docker/docker-compose.mcp.release.yml up -d
```

# Helpdesk UX Playwright checks

Run against a local Development app with `HELPDESK_E2E_AUTH_MODE=development`, or an Authentik-backed instance with `HELPDESK_E2E_AI_TOKEN`.

```bash
HELPDESK_E2E_BASE_URL=https://helpdesk.example.com \
HELPDESK_E2E_AI_TOKEN=<token> \
npm run test:ux
```

The suite deliberately has no authentication bypass. It uses the existing `/auth/ai-agent/exchange` endpoint for public dev and the local-only `/auth/development` endpoint only when explicitly selected.

## AiAssistant presentation fixture

Build `src/HelpDesk.NewWeb`, then run `npm run test:ux:ai-assistant`. The separate
configuration starts a loopback-only deterministic API fixture and the actual
feature NewWeb host. It uses local Development authentication and a local HTTPS
certificate (the existing `__Host-` cookie requires HTTPS). Test-only placeholder
credentials never reach a real API. Screenshots and computed styles are saved
under `test-results/`.

This suite exercises UI grouping, streaming, keyboard/IME, replay, approval
presentation, archive, responsive layout and themes. It is not live AiAssistant,
attachment ingestion or authorization evidence. It is deliberately excluded
from the ordinary live-instance UX configuration.

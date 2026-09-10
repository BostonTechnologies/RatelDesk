import { defineConfig } from '@playwright/test';
import base from './playwright.config';

export default defineConfig(base, {
  testMatch: '**/ai-assistant-chat.spec.ts',
  testIgnore: [],
  retries: 0,
  use: { ...base.use, baseURL: 'https://127.0.0.1:5158', ignoreHTTPSErrors: true },
  webServer: [
    { command: 'node tests/ux/ai-assistant-fixture-api.mjs', url: 'http://127.0.0.1:18299/fixture/health', reuseExistingServer: !process.env.CI },
    {
      command: 'dotnet run --project src/HelpDesk.NewWeb --no-build --no-launch-profile',
      url: 'https://127.0.0.1:5158/health', ignoreHTTPSErrors: true, timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'https://127.0.0.1:5158',
        ApiBaseUrl: 'http://127.0.0.1:18299/', DevelopmentOperator__Enabled: 'true',
        SYSTEM_TOKEN_SECRET: 'local-ux-fixture', Authentication__Authentik__ClientId: 'ux-local',
        AUTHENTIK_CLIENT_SECRET: 'ux-local', Authentication__Authentik__ApiScope: 'helpdesk'
      }
    }
  ]
});

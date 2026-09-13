import { expect, test, type Page } from '@playwright/test';
import { createHmac } from 'node:crypto';

test.describe.configure({ retries: 0 });
test.use({ actionTimeout: 15_000, navigationTimeout: 30_000 });

const setupCode = process.env.HELPDESK_E2E_SETUP_CODE;

test('first-run setup initializes, survives restart, and supports isolated scoped accounts', async ({ page, browser, baseURL }) => {
  test.setTimeout(180_000);
  if (!setupCode) {
    throw new Error('HELPDESK_E2E_SETUP_CODE is required for first-run setup validation.');
  }

  const interactiveConnection = page.waitForResponse(response =>
    response.url().includes('/_blazor/negotiate') && response.ok());

  await page.goto('/setup');
  await interactiveConnection;
  await expect(page.getByRole('heading', { name: 'Set up RatelDesk' })).toBeVisible();
  await expect(page.getByText('operator-only setup code')).toBeVisible();

  await expect(page.getByTestId('setup-wizard')).toHaveAttribute('data-interactive', 'true');
  const setupCodeInput = page.getByLabel('Setup code');
  await setupCodeInput.pressSequentially(setupCode);
  await setupCodeInput.press('Tab');
  await expect(setupCodeInput).toHaveValue(setupCode);
  await page.getByRole('button', { name: 'Continue' }).click();
  const prepareStorage = page.getByRole('button', { name: 'Prepare storage' });
  await expect(prepareStorage).toBeVisible();
  await prepareStorage.click();
  await expect(page.getByLabel('Initial organization')).toBeVisible();
  await page.getByLabel('Initial organization').fill('Browser Wizard Organization');
  await page.getByLabel('Application name').fill('Browser Wizard RatelDesk');
  await page.getByLabel('Public application URL').fill(baseURL!);
  await page.getByLabel('Time zone').fill('Africa/Johannesburg');
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByLabel('Display name')).toBeVisible();
  await page.getByLabel('Display name').fill('Browser Wizard Administrator');
  await page.getByLabel('Email').fill('browser.wizard.admin@example.test');
  await page.getByRole('textbox', { name: 'Passphrase*', exact: true }).fill('browser-wizard-setup-passphrase');
  await page.getByRole('textbox', { name: 'Confirm passphrase*', exact: true }).fill('browser-wizard-setup-passphrase');
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByLabel('Keep RatelDesk defaults')).toBeChecked();
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByText('Embedded SQLite', { exact: true })).toBeVisible();
  await expect(page.getByText('Browser Wizard Organization', { exact: true })).toBeVisible();
  await expect(page.getByText('Browser Wizard Administrator (browser.wizard.admin@example.test)', { exact: true })).toBeVisible();
  await expect(page.getByText('Browser Wizard RatelDesk', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Initialize instance' }).click();
  await expect(page.getByText('RatelDesk is restarting into its normal application host.')).toBeVisible({ timeout: 60_000 });
  await expect.poll(async () => {
    try {
      const response = await page.request.get('/api/v1/setup/status');
      return response.ok() ? (await response.json()).state : 'Restarting';
    } catch { return 'Restarting'; }
  }, { timeout: 60_000 }).toBe('Ready');

  await expect.poll(async () => {
    try {
      const response = await page.request.get('/api/v1/branding');
      return response.ok() ? (await response.json()).applicationName : 'Restarting';
    } catch { return 'Restarting'; }
  }, { timeout: 60_000 }).toBe('Browser Wizard RatelDesk');

  // Login starts with an empty browser cookie jar. An earlier API login cannot mask failure.
  await page.context().clearCookies();
  await page.goto('/login');
  await expect(page.getByTestId('local-login-form')).toHaveAttribute('data-interactive', 'true');
  await page.getByLabel('Email', { exact: false }).fill('browser.wizard.admin@example.test');
  await page.getByLabel('Password', { exact: false }).fill('incorrect-browser-test-passphrase');
  await page.getByRole('button', { name: 'Sign in to Browser Wizard RatelDesk', exact: true }).click();
  await expect(page.getByTestId('local-login-error')).toHaveText('Sign-in failed. Check your email, passphrase and authenticator or recovery code.');
  await signIn(page, 'browser.wizard.admin@example.test');
  const admin = await (await page.request.get('/api/v1/auth/me')).json();
  expect(admin.isHelpdeskAdmin).toBe(true);
  expect(admin.email).toBe('browser.wizard.admin@example.test');
  const branding = await (await page.request.get('/api/v1/branding')).json();
  expect(branding.applicationName).toBe('Browser Wizard RatelDesk');
  const locked = await page.request.post('/api/v1/setup/session', { data: { setupCode } });
  expect(locked.status()).toBeGreaterThanOrEqual(400);

  const headers = { 'X-Requested-With': 'XMLHttpRequest' };
  const organizations = await (await page.request.get('/api/v1/ticketing/organizations?module=incident')).json();
  const organizationId = organizations[0].id;
  const accounts: { email: string; id: string; role: string }[] = [];
  for (const role of ['IncidentReader', 'IncidentWriter']) {
    const email = `${role.toLowerCase()}@example.test`;
    const created = await page.request.post('/api/v1/local-auth/users', {
      headers, data: { displayName: role, email, role: 'User', organizationId }
    });
    expect(created.ok(), await created.text()).toBe(true);
    const account = await created.json();
    const activate = await page.request.post('/api/v1/local-auth/activate', {
      headers, data: { email, activationToken: account.activationToken, newPassword: passphrase }
    });
    expect(activate.ok(), await activate.text()).toBe(true);
    const assign = await page.request.put(`/api/v1/local-auth/users/${account.userId}/assignments`, {
      headers, data: { assignments: [{ roleKey: role, organizationId }] }
    });
    expect(assign.ok(), await assign.text()).toBe(true);
    accounts.push({ email, id: account.userId, role });
  }
  const customers = await (await page.request.get(`/api/v1/ticketing/customers?module=incident&organizationId=${organizationId}`)).json();
  const owner = customers.find((customer: { email: string }) => customer.email === accounts[1].email);
  expect(owner).toBeTruthy();
  const createdIncident = await page.request.post('/api/v1/incidents', {
    headers, data: { title: 'Colleague incident for reader acceptance', description: 'Browser RBAC acceptance', organizationId, customerId: owner.id }
  });
  expect(createdIncident.ok(), await createdIncident.text()).toBe(true);
  const incident = await createdIncident.json();

  const readerContext = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
  const writerContext = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
  try {
    const reader = await readerContext.newPage();
    const writer = await writerContext.newPage();
    await signIn(reader, accounts[0].email);
    await signIn(writer, accounts[1].email);
    expect((await (await reader.request.get('/api/v1/auth/me')).json()).email).toBe(accounts[0].email);
    expect((await (await writer.request.get('/api/v1/auth/me')).json()).email).toBe(accounts[1].email);
    expect((await (await page.request.get('/api/v1/auth/me')).json()).email).toBe('browser.wizard.admin@example.test');
    await reader.goto(`/incidents/${incident.id}`);
    await expect(reader.getByText('Colleague incident for reader acceptance', { exact: true })).toBeVisible();
    await expect(reader.getByRole('button', { name: 'Save Changes', exact: true })).toBeDisabled();
    await expect(reader.getByLabel('Priority', { exact: true })).toBeDisabled();
    await expect(reader.getByRole('link', { name: 'Changes', exact: true })).toHaveCount(0);
    await writer.goto(`/incidents/${incident.id}`);
    await expect(writer.getByText('Colleague incident for reader acceptance', { exact: true })).toBeVisible();
    await expect(writer.getByLabel('Priority', { exact: true })).toBeEnabled();
    const denied = await reader.request.put(`/api/v1/incidents/${incident.id}`, { headers, data: { priority: 1 } });
    expect(denied.status()).toBe(403);
    const permitted = await writer.request.put(`/api/v1/incidents/${incident.id}`, { headers, data: { priority: 1 } });
    expect(permitted.ok(), await permitted.text()).toBe(true);
    const revoke = await page.request.put(`/api/v1/local-auth/users/${accounts[0].id}/assignments`, { headers, data: { assignments: [] } });
    expect(revoke.ok(), await revoke.text()).toBe(true);
    // The open circuit must shed revoked navigation and page access without a logout.
    await expect(reader).toHaveURL(/\/login(?:[?#].*)?$/, { timeout: 45_000 });
    expect((await reader.request.get('/api/v1/auth/me')).status()).toBe(401);
    await signIn(reader, accounts[0].email);
    const accessAfterRemoval = await reader.request.get(`/api/v1/incidents/${incident.id}`);
    expect([403, 404]).toContain(accessAfterRemoval.status());
  } finally {
    await readerContext.close();
    await writerContext.close();
  }

  // Enroll MFA through the UI without an invisible login between setup and enable.
  await page.goto('/account/authenticator');
  await page.getByLabel('Current passphrase').fill(passphrase);
  await page.getByRole('button', { name: 'Set up authenticator', exact: true }).click();
  const sharedKey = await page.getByLabel('Authenticator key', { exact: true }).inputValue();
  await page.getByLabel('Authenticator code', { exact: true }).fill(totp(sharedKey));
  await page.getByRole('button', { name: 'Enable authenticator', exact: true }).click();
  await expect(page.getByLabel('Recovery codes', { exact: true })).toBeVisible();
  expect((await page.request.get('/api/v1/auth/me')).ok()).toBe(true);
  await page.getByRole('button', { name: 'I have saved my recovery codes' }).click();
  await expect(page).toHaveURL(/\/home(?:[?#].*)?$/);
});

const passphrase = 'browser-wizard-setup-passphrase';
async function signIn(page: Page, email: string) {
  await page.goto('/login');
  await expect(page.getByTestId('local-login-form')).toHaveAttribute('data-interactive', 'true');
  await page.getByLabel('Email', { exact: false }).fill(email);
  await page.getByLabel('Password', { exact: false }).fill(passphrase);
  await page.getByRole('button', { name: 'Sign in to Browser Wizard RatelDesk', exact: true }).click();
  await expect(page).toHaveURL(/\/home(?:[?#].*)?$/);
}
function totp(key: string): string {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  const bits = [...key.replace(/\s/g, '').toUpperCase()].map(char => alphabet.indexOf(char).toString(2).padStart(5, '0')).join('');
  const secret = Buffer.from((bits.match(/.{8}/g) ?? []).map(byte => parseInt(byte, 2)));
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30_000)));
  const digest = createHmac('sha1', secret).update(counter).digest();
  const offset = digest[digest.length - 1] & 15;
  return ((digest.readUInt32BE(offset) & 0x7fffffff) % 1_000_000).toString().padStart(6, '0');
}

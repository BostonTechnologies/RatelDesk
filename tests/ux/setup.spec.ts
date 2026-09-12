import { expect, test } from '@playwright/test';

const setupCode = process.env.HELPDESK_E2E_SETUP_CODE;

test('first-run setup unlocks embedded storage and reaches review', async ({ page }) => {
  if (!setupCode) {
    throw new Error('HELPDESK_E2E_SETUP_CODE is required for first-run setup validation.');
  }

  const interactiveConnection = page.waitForResponse(response =>
    response.url().includes('/_blazor/negotiate') && response.ok());

  await page.goto('/setup');
  await interactiveConnection;
  await expect(page.getByRole('heading', { name: 'Set up RatelDesk' })).toBeVisible();
  await expect(page.getByText('operator-only setup code')).toBeVisible();

  const setupCodeInput = page.getByLabel('Setup code');
  await setupCodeInput.fill(setupCode);
  await setupCodeInput.press('Tab');
  await expect(setupCodeInput).toHaveValue(setupCode);
  await page.getByRole('button', { name: 'Continue' }).click();
  const prepareStorage = page.getByRole('button', { name: 'Prepare storage' });
  await expect(prepareStorage).toBeVisible();
  await prepareStorage.click();
  await expect(page.getByLabel('Initial organization')).toBeVisible();
  await page.getByLabel('Initial organization').fill('Browser Wizard Organization');
  await page.getByLabel('Application name').fill('Browser Wizard RatelDesk');
  await page.getByLabel('Public application URL').fill('https://127.0.0.1:5167');
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
  await expect(page.getByRole('button', { name: 'Initialize instance' })).toBeVisible();
});

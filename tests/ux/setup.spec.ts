import { expect, test } from '@playwright/test';

const setupCode = process.env.HELPDESK_E2E_SETUP_CODE;

test('first-run setup unlocks embedded storage and reaches review', async ({ page }) => {
  if (!setupCode) {
    throw new Error('HELPDESK_E2E_SETUP_CODE is required for first-run setup validation.');
  }

  await page.goto('/setup');
  await expect(page.getByRole('heading', { name: 'Set up RatelDesk' })).toBeVisible();
  await expect(page.getByText('operator-only setup code')).toBeVisible();

  await page.getByLabel('Setup code').fill('incorrect-setup-code');
  await page.getByRole('button', { name: 'Continue' }).click();
  await expect(page.getByText('The setup code was not accepted.')).toBeVisible();

  await page.getByLabel('Setup code').fill(setupCode);
  await page.getByRole('button', { name: 'Continue' }).click();
  await expect(page.getByLabel('Storage provider')).toBeVisible();
  await expect(page.getByText('Embedded SQLite — no database service required')).toBeVisible();

  await page.getByRole('button', { name: 'Prepare storage' }).click();
  await expect(page.getByLabel('Initial organization')).toBeVisible();
  await page.getByLabel('Initial organization').fill('Browser Wizard Organization');
  await page.getByLabel('Application name').fill('Browser Wizard RatelDesk');
  await page.getByLabel('Public application URL').fill('https://127.0.0.1:5167');
  await page.getByLabel('Time zone').fill('Africa/Johannesburg');
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByLabel('Display name')).toBeVisible();
  await page.getByLabel('Display name').fill('Browser Wizard Administrator');
  await page.getByLabel('Email').fill('browser.wizard.admin@example.test');
  await page.getByLabel('Passphrase').fill('browser-wizard-setup-passphrase');
  await page.getByLabel('Confirm passphrase').fill('browser-wizard-setup-passphrase');
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByLabel('Keep RatelDesk defaults')).toBeChecked();
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByText('Browser Wizard Organization', { exact: true })).toBeVisible();
  await expect(page.getByText('Browser Wizard Administrator (browser.wizard.admin@example.test)', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Initialize instance' })).toBeVisible();
});

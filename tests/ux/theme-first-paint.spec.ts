import { expect, test } from '@playwright/test';

type ThemeCase = {
  colorScheme: 'dark' | 'light';
  expected: 'dark' | 'light';
  preference: string;
  colors: { background: string; surface: string; text: string };
};

const cases: ThemeCase[] = [
  {
    colorScheme: 'dark',
    expected: 'dark',
    preference: ' system ',
    colors: { background: 'rgb(12, 15, 19)', surface: 'rgb(23, 28, 35)', text: 'rgb(237, 241, 245)' }
  },
  {
    colorScheme: 'light',
    expected: 'dark',
    preference: ' DARK ',
    colors: { background: 'rgb(12, 15, 19)', surface: 'rgb(23, 28, 35)', text: 'rgb(237, 241, 245)' }
  },
  {
    colorScheme: 'dark',
    expected: 'light',
    preference: ' light ',
    colors: { background: 'rgb(245, 247, 250)', surface: 'rgb(255, 255, 255)', text: 'rgb(21, 34, 51)' }
  }
];

for (const themeCase of cases) {
  test(`keeps the ${themeCase.expected} palette through a paused runtime (${themeCase.preference.trim()})`, async ({ browser }) => {
    const context = await browser.newContext({
      colorScheme: themeCase.colorScheme,
      ignoreHTTPSErrors: process.env.HELPDESK_E2E_IGNORE_HTTPS_ERRORS === 'true'
    });
    let releaseRuntime: (() => void) | undefined;
    const runtimeRelease = new Promise<void>((resolve) => { releaseRuntime = resolve; });

    await context.addInitScript((preference) => {
      localStorage.setItem('helpdesk.theme.preference', preference);
      const samples: Array<{ theme: string | undefined; colorScheme: string; background: string; surface: string; text: string }> = [];
      let observing = true;
      const capture = () => {
        const portal = document.querySelector<HTMLElement>('.helpdesk-portal-page');
        const card = document.querySelector<HTMLElement>('.helpdesk-portal-action');
        const heading = document.querySelector<HTMLElement>('h1');
        if (portal && card && heading && portal.getBoundingClientRect().height > 0) {
          samples.push({
            theme: document.documentElement.dataset.helpdeskTheme,
            colorScheme: getComputedStyle(document.documentElement).colorScheme,
            background: getComputedStyle(document.body).backgroundColor,
            surface: getComputedStyle(card).backgroundColor,
            text: getComputedStyle(heading).color
          });
        }

        if (observing) requestAnimationFrame(capture);
      };

      requestAnimationFrame(capture);
      (window as typeof window & { __helpdeskThemeFirstPaint?: { samples: typeof samples; stop: () => void } }).__helpdeskThemeFirstPaint = {
        samples,
        stop: () => { observing = false; }
      };
    }, themeCase.preference);
    await context.route('**/_framework/blazor.web.js', async (route) => {
      await runtimeRelease;
      await route.continue();
    });

    const page = await context.newPage();
    try {
      await page.goto('/', { waitUntil: 'commit' });
      await page.waitForFunction(() => document.querySelector('.helpdesk-portal-action')?.getBoundingClientRect().height);
      await page.waitForFunction(() =>
        (window as typeof window & { __helpdeskThemeFirstPaint: { samples: unknown[] } }).__helpdeskThemeFirstPaint.samples.length > 0);

      const pausedSamples = await page.evaluate(() =>
        (window as typeof window & { __helpdeskThemeFirstPaint: { samples: unknown[] } }).__helpdeskThemeFirstPaint.samples);
      expect(pausedSamples, 'the paused runtime must leave visible prerendered content to observe').not.toHaveLength(0);
      expect(pausedSamples).toEqual(expect.arrayContaining([
        expect.objectContaining({ theme: themeCase.expected, colorScheme: themeCase.expected, ...themeCase.colors })
      ]));

      releaseRuntime!();
      await page.waitForLoadState('domcontentloaded');
      await expect(page.locator('html')).toHaveAttribute('data-helpdesk-theme', themeCase.expected);
      await expect.poll(() => page.evaluate(() => {
        const card = document.querySelector<HTMLElement>('.helpdesk-portal-action')!;
        const heading = document.querySelector<HTMLElement>('h1')!;
        return {
          background: getComputedStyle(document.body).backgroundColor,
          surface: getComputedStyle(card).backgroundColor,
          text: getComputedStyle(heading).color
        };
      })).toEqual(themeCase.colors);
    } finally {
      releaseRuntime?.();
      await page.evaluate(() =>
        (window as typeof window & { __helpdeskThemeFirstPaint?: { stop: () => void } }).__helpdeskThemeFirstPaint?.stop()).catch(() => undefined);
      await context.close();
    }
  });
}

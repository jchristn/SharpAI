import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

// The sidebar (brand + navigation) collapses below ~820px; those checks run only on wide viewports.
const WIDE = 900;

test.describe('dashboard shell', () => {
  test('renders semantic landmarks', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('header')).toBeVisible(); // topbar (banner)
    await expect(page.locator('main')).toBeVisible(); // routed content
    await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
  });

  test('navigates between routes via the sidebar', async ({ page }, testInfo) => {
    test.skip((testInfo.project.use.viewport?.width ?? 0) < WIDE, 'sidebar hidden on small viewports');
    await page.goto('/');
    await expect(page.getByText('SharpAI').first()).toBeVisible();

    await page.getByRole('link', { name: 'Request History' }).click();
    await expect(page).toHaveURL(/\/request-history$/);
    await expect(page.getByRole('heading', { name: 'Request History' })).toBeVisible();

    await page.getByRole('link', { name: 'Settings' }).click();
    await expect(page).toHaveURL(/\/settings$/);
  });

  test('theme toggle flips the document theme', async ({ page }) => {
    await page.goto('/');
    const before = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
    await page.getByRole('button', { name: 'Toggle theme' }).click();
    const after = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
    expect(after).not.toBe(before);
    expect(['light', 'dark']).toContain(after);
  });

  test('the page body does not scroll horizontally', async ({ page }) => {
    await page.goto('/');
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow).toBeLessThanOrEqual(1);
  });

  test('has no critical accessibility violations', async ({ page }) => {
    await page.goto('/');
    const results = await new AxeBuilder({ page }).analyze();
    const critical = results.violations.filter((v) => v.impact === 'critical');
    expect(critical, JSON.stringify(critical, null, 2)).toEqual([]);
  });
});

import { libraryFixture } from './library-fixture';
import { authenticatedPage } from './session-fixture';
import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test.describe('dashboard shell', () => {
  test.beforeEach(async ({page}) => {
    await authenticatedPage(page);
    const library = libraryFixture();
    await page.route('**/*', async route => {
      if (await library(route)) return;
      const path = new URL(route.request().url()).pathname;
      if (path === '/v1.0/token') return route.fallback();
      if (path === '/v1.0/auth/context') return route.fulfill({json:{isAdmin:true,tenantId:'tenant-a',userId:'user-a',capabilities:{}}});
      if (path.startsWith('/api/') || path.startsWith('/v1.0/')) return route.fulfill({json:{models:[],Objects:[],Buckets:[],TotalCount:0}});
      return route.fallback();
    });
  });
  test('renders semantic landmarks', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('header')).toBeVisible(); // topbar (banner)
    await expect(page.locator('main')).toBeVisible(); // routed content
    await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
  });

  test('navigates between routes via the sidebar', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', {name:'Overview',exact:true})).toBeVisible();
    const menu = page.getByRole('button', { name: 'Toggle navigation' });
    if (await menu.isVisible()) await menu.click();
    await expect(page.getByRole('link', { name: 'SharpAI Inference workspace', exact: true })).toBeVisible();

    await page.getByRole('link', { name: 'Request History' }).click();
    await expect(page).toHaveURL(/\/request-history$/);
    await expect(page.getByRole('heading', { name: 'Request History' })).toBeVisible();

    if (await menu.isVisible()) await menu.click();
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

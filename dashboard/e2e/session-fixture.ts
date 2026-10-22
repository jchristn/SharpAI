import type { Page } from '@playwright/test';
/** A persisted session must pass server validation before any protected view mounts. */
export async function authenticatedPage(page: Page): Promise<void> {
  await page.addInitScript(() => {
    if (!localStorage.getItem('sharpai.session')) localStorage.setItem('sharpai.session', JSON.stringify({token:'fixture-session',userId:'user-a',tenantId:'tenant-a'}));
  });
  await page.route('**/v1.0/token', route => route.fulfill({json:{token:'fixture-session',userId:'user-a',tenantId:'tenant-a',sessionId:'session-a',expiresUtc:'2099-01-01T00:00:00Z'}}));
}

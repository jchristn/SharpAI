import { test, expect } from '@playwright/test';
import { authenticatedPage } from './session-fixture';

for (const path of ['/', '/settings', '/access/users', '/playground']) {
  test(`anonymous users cannot open ${path}`, async ({page}) => {
    const protectedRequests: string[] = [];
    await page.route('**/*', route => {
      const pathname = new URL(route.request().url()).pathname;
      if (/^\/(api\/|v1\/|v1\.0\/|metrics$|engine-metrics$|openapi\.json$)/.test(pathname)) {
        protectedRequests.push(pathname);
        return route.fulfill({status:401,json:{error:'Bearer authentication is required.'}});
      }
      return route.continue();
    });
    await page.goto(path);
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('admin@sharpai.local', {exact:true})).toBeVisible();
    const footer = dialog.locator('.sa-login-footer');
    const actions = await footer.locator('.sa-login-actions').boundingBox();
    const credentials = await footer.locator('.sa-login-defaults').boundingBox();
    expect(credentials!.y).toBeGreaterThan(actions!.y + actions!.height);
    await dialog.getByRole('button',{name:'Cancel',exact:true}).click();
    await expect(page.locator('.sa-app')).toHaveCount(0);
    await expect(page.getByRole('navigation')).toHaveCount(0);
    expect(protectedRequests).toEqual([]);
  });
}

test('a forged persisted session cannot mount protected content', async ({page}) => {
  await page.addInitScript(() => localStorage.setItem('sharpai.session', JSON.stringify({token:'forged',userId:'admin',tenantId:'default'})));
  await page.route('**/v1.0/token', route => route.fulfill({status:401,json:{error:'Invalid bearer token'}}));
  await page.goto('/settings');
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.locator('.sa-app')).toHaveCount(0);
  expect(await page.evaluate(() => localStorage.getItem('sharpai.session'))).toBeNull();
});

for (const section of ['api','inference','embeddings']) {
  test(`${section} history controls use equal heights and vector icons`, async ({page}) => {
    await authenticatedPage(page);
    await page.route('**/v1.0/auth/context',route => route.fulfill({json:{isAdmin:false,tenantId:'tenant-a',userId:'user-a',capabilities:{}}}));
    await page.route('**/v1.0/api/request-history**',route => route.fulfill({json:{Objects:[],TotalCount:0,TotalRecords:0,Buckets:[]}}));
    await page.goto(`/request-history/${section}`);
    const method = page.getByRole('combobox',{name:'Method',exact:true});
    await expect(method).toBeVisible();
    const methodHeight = await method.evaluate(element => element.getBoundingClientRect().height);
    const controls = await page.locator('.sa-filterbar__field input,.sa-filterbar__field select').evaluateAll(elements => elements.map(element => element.getBoundingClientRect().height));
    expect(methodHeight).toBe(32);
    expect(controls.every(height => height === methodHeight)).toBe(true);
    await expect(page.locator('.sa-sidebar__brand img')).toHaveAttribute('src','/images/sharp-logo.svg');
    const iconSizes=await page.locator('.sa-topbar__actions svg').evaluateAll(elements=>elements.map(element=>({width:element.getBoundingClientRect().width,height:element.getBoundingClientRect().height})));
    expect(iconSizes.length).toBeGreaterThanOrEqual(3);
    expect(iconSizes.every(size=>size.width===24 && size.height===24)).toBe(true);
  });
}

test('sign-in fits short viewports without an internal scrollbar', async ({page}) => {
  for (const viewport of [{width:1280,height:600},{width:390,height:667}]) {
    await page.setViewportSize(viewport);
    await page.goto('/');
    const dialog=page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    const dimensions=await dialog.evaluate(element=>{
      const body=element.querySelector('.sa-modal__body')!;
      return {height:element.getBoundingClientRect().height,bodyHeight:body.clientHeight,bodyContent:body.scrollHeight};
    });
    expect(dimensions.height).toBeLessThanOrEqual(viewport.height-24);
    expect(dimensions.bodyContent).toBeLessThanOrEqual(dimensions.bodyHeight+1);
    await expect(dialog.getByRole('button',{name:'Sign in',exact:true})).toBeInViewport();
    await expect(dialog.locator('.sa-login-defaults')).toBeInViewport();
    await dialog.locator('summary').click();
    expect(await dialog.locator('.sa-modal__body').evaluate(element=>getComputedStyle(element).overflowY)).toBe('visible');
  }
});

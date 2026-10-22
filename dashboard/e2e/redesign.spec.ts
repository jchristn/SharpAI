import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import {libraryFixture} from './library-fixture';

const configuration = {
  SoftwareVersion: '4.0.1', SchemaVersion: '2.0.0',
  Storage: { ModelsDirectory: './models', TempDirectory: './temp' },
  Runtime: { ForceBackend: null, EnableNativeLogging: false },
  Auth: { Enabled: false, AdminApiKeys: ['keep-this-key'], SessionTtlMinutes: 60 },
  Telemetry: { Enable: true, OtlpEndpoint: 'http://collector:4317' },
  RequestHistory: { Enabled: true, RetentionDays: 30 },
  FutureSetting: { Preserved: 'keep-me' },
};

test.beforeEach(async ({ page }) => {
  const library=libraryFixture();
  await page.route('**/*', async route => {
    if(await library(route))return;
    const path = new URL(route.request().url()).pathname;
    let body: unknown;
    if (path === '/api/settings') body = route.request().method() === 'PUT' ? route.request().postDataJSON() : configuration;
    else if (path === '/health' || path === '/ready') body = { version: '4.0.1', backend: 'cpu' };
    else if (path === '/api/tags' || path === '/api/ps') body = { models: [] };
    else if (path === '/openapi.json') body = { paths: { '/health': { get: { tags: ['Health'], summary: 'Server health' } } } };
    else if (path.startsWith('/v1.0/api/request-history')) body = { Objects: [], TotalCount: 0, Buckets: [] };
    else if (path === '/metrics' || path === '/engine-metrics') { await route.fulfill({ contentType: 'text/plain', body: 'sharpai_models_resident 0\nsharpai_inference_requests_total 0\n' }); return; }
    else { await route.fallback(); return; }
    await route.fulfill({ json: body });
  });
});

for (const {path, heading} of [
  {path:'/',heading:'Overview'},
  {path:'/models',heading:'Models'},
  {path:'/playground',heading:'Playground'},
  {path:'/request-history',heading:'Request History'},
  {path:'/inference-history',heading:'Inference & Embeddings History'},
  {path:'/api-explorer',heading:'API Explorer'},
  {path:'/observability',heading:'Observability'},
  {path:'/settings',heading:'Settings'},
]) {
  test(`${heading} fits and remains accessible`, async ({ page }) => {
    await page.goto(path);
    await expect(page.getByRole('heading', { name: heading, exact: true })).toBeVisible();
    await expect(page.locator('main')).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
    const violations = (await new AxeBuilder({ page }).analyze()).violations.filter(v => v.impact === 'critical' || v.impact === 'serious');
    expect(violations, JSON.stringify(violations, null, 2)).toEqual([]);
    await page.screenshot({ path: `test-results/screenshots/${test.info().project.name}-${path.replaceAll('/', '') || 'overview'}.png`, fullPage: true });
  });
}

test('settings saves typed fields while preserving unrelated configuration', async ({ page }) => {
  await page.goto('/settings');
  const save = page.getByRole('button', { name: 'Save', exact: true });
  await expect(save).toBeDisabled();
  await expect(page.getByRole('textbox', { name: 'Raw configuration' })).toHaveCount(0);
  await page.getByLabel('Models Directory', { exact: true }).fill('./new-models');
  await page.getByLabel('Retention Days', { exact: true }).fill('14');
  await page.getByRole('combobox', { name: 'Force Backend', exact: true }).selectOption('cpu');
  const request = page.waitForRequest(req => req.method() === 'PUT' && req.url().endsWith('/api/settings'));
  await save.click();
  expect((await request).postDataJSON()).toMatchObject({ ...configuration, Storage: { ...configuration.Storage, ModelsDirectory: './new-models' }, RequestHistory: { ...configuration.RequestHistory, RetentionDays: 14 }, Runtime: { ...configuration.Runtime, ForceBackend: 'cpu' } });
  await expect(save).toBeDisabled();
});

test('navigation works on phones and closes after selecting a page', async ({ page }) => {
  await page.goto('/');
  const toggle = page.getByRole('button', { name: 'Toggle navigation' });
  if (await toggle.isVisible()) await toggle.click();
  await page.getByRole('link', { name: 'Settings', exact: true }).click();
  await expect(page).toHaveURL(/\/settings$/);
  if (await toggle.isVisible()) await expect(toggle).toHaveAttribute('aria-expanded', 'false');
});

test('branded login validates fields and traps keyboard focus', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('img', { name: 'SharpAI' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Sign in', exact: true }).click();
  expect(await dialog.locator('input[type=email]').evaluate((el: HTMLInputElement) => el.validity.valid)).toBe(false);
  await dialog.getByRole('button', { name: 'Sign in', exact: true }).focus();
  await page.keyboard.press('Tab');
  await expect(dialog.getByRole('button', { name: 'Close', exact: true })).toBeFocused();
  await page.keyboard.press('Shift+Tab');
  await expect(dialog.getByRole('button', { name: 'Sign in', exact: true })).toBeFocused();
  await page.screenshot({ path: `test-results/screenshots/${test.info().project.name}-login.png` });
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
});

test('playground streams a response and generates an embedding', async ({ page }) => {
  await page.route('**/api/tags', route=>route.fulfill({json:{models:[{name:'test-model'}]}}));
  await page.route('**/api/chat', route => route.fulfill({ contentType: 'application/x-ndjson', body: '{"message":{"content":"Hello from your model"},"done":true}\n' }));
  await page.route('**/api/embed', route => route.fulfill({ json: { embeddings: [[0.1, 0.2, 0.3]] } }));
  await page.goto('/playground');
  await page.getByLabel('Model', { exact: true }).selectOption('test-model');
  await page.getByLabel('Prompt', { exact: true }).fill('Hello');
  await page.getByRole('button', { name: 'Send', exact: true }).click();
  await expect(page.getByText('Hello from your model')).toBeVisible();
  await page.getByRole('button', { name: 'Embeddings', exact: true }).click();
  await page.getByLabel('Input',{exact:true}).fill('Hello');
  await page.getByRole('button', { name: 'Embed', exact: true }).click();
  await expect(page.getByText('[0.1000, 0.2000, 0.3000]')).toBeVisible();
});

test('settings reports save errors and keeps the editable draft', async ({ page }) => {
  await page.route('**/api/settings', async route => {
    await route.fulfill(route.request().method() === 'PUT' ? { status: 500, json: { message: 'Unable to save configuration' } } : { json: configuration });
  });
  await page.goto('/settings');
  await page.getByLabel('Models Directory', { exact: true }).fill('./draft');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText(/500:/).first()).toBeVisible();
  await expect(page.getByLabel('Models Directory', { exact: true })).toHaveValue('./draft');
  await expect(page.getByRole('button', { name: 'Save', exact: true })).toBeEnabled();
});

test('API explorer selects and executes a request', async ({ page }) => {
  await page.goto('/api-explorer');
  await page.getByRole('combobox', {name:'API operation'}).selectOption({label:'GET /health'});
  await page.getByRole('button', { name: 'Send', exact: true }).click();
  await expect(page.getByText('200', { exact: true })).toBeVisible();
  await expect(page.locator('pre').filter({ hasText: '4.0.1' })).toBeVisible();
});

test('model actions open confirmation and report server errors', async ({ page }) => {
  await page.route('**/api/tags', route => route.fulfill({ json: { models: [{ name: 'test-model', size: 1048576 }] } }));
  await page.route('**/api/delete', route => route.fulfill({ status: 500, json: { message: 'Model is busy' } }));
  await page.goto('/models');
  await expect(page.getByText('1 MB')).toBeVisible();
  await page.getByRole('button', { name: 'Actions' }).click();
  await page.getByRole('menuitem', { name: 'Delete' }).click();
  await expect(page.getByRole('dialog', { name: 'Delete model' })).toBeVisible();
  await page.getByRole('dialog').getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(page.getByText(/500:/).first()).toBeVisible();
});

test('model pull shows progress', async ({ page }) => {
  await page.route('**/api/pull', route => route.fulfill({ contentType: 'application/x-ndjson', body: '{"status":"Downloading model"}\n{"status":"success"}\n' }));
  await page.goto('/models');
  await page.getByRole('button', { name: 'Pull model' }).click();
  await page.getByRole('dialog').getByRole('textbox', { name: 'Model name or full URL' }).fill('org/test-model');
  await page.getByRole('dialog').getByRole('button', { name: 'Pull model' }).click();
  await expect(page.getByRole('dialog').getByText('Model ready',{exact:true})).toBeVisible();
  await expect(page.getByRole('progressbar')).toHaveAttribute('value','1');
  await expect(page.getByRole('dialog').locator('pre')).toHaveCount(0);
});

test('login submits on Enter and sign out clears identity', async ({ page }) => {
  await page.route('**/v1.0/token', route => route.fulfill({ json: { token: 'test-token', userId: 'test-operator', tenantId: 'tenant', sessionId: 'session', expiresUtc: '2099-01-01' } }));
  await page.goto('/');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('dialog').getByLabel('Email', { exact: true }).fill('operator@example.com');
  await page.getByRole('dialog').getByLabel('Password', { exact: true }).fill('test-password');
  await page.keyboard.press('Enter');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByText('test-operator')).toBeVisible();
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page.getByText('test-operator')).toHaveCount(0);
});

test('history filters requests and opens request details', async ({ page }) => {
  await page.route('**/v1.0/api/request-history**', route => route.fulfill({ json: new URL(route.request().url()).pathname.endsWith('/request-1') ? {Id:'request-1',Method:'GET',Path:'/health',StatusCode:200,RequestHeaders:{accept:'application/json'},ResponseBody:'{"status":"healthy"}',ResponseBodyBytes:20,CreatedUtc:'2026-10-21T12:00:00Z'} : route.request().url().includes('/summary') ? { TotalCount: 1, TotalSuccess: 1, TotalFailure: 0, Buckets: [] } : { Objects: [{ Id: 'request-1', Method: 'GET', Path: '/health', StatusCode: 200, DurationMs: 4, SourceIp: '127.0.0.1', CreatedUtc: '2026-10-21T12:00:00Z' }], TotalCount: 1 } }));
  await page.goto('/request-history');
  await page.getByRole('combobox', { name: 'Method', exact: true }).selectOption('GET');
  const filtered = page.waitForRequest(req => req.url().includes('/request-history?') && req.url().includes('method=GET'));
  await page.getByRole('button', { name: 'Apply', exact: true }).click();
  await filtered;
  await page.getByRole('button', { name: 'Details', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('request-1');
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Clear', exact: true }).click();
  await expect(page.getByRole('combobox', { name: 'Method', exact: true })).toHaveValue('');
});

for (const locale of ['en','es','pt','fr','it','de','zh','ar','ru','ms','hi','ja','en-XA','ar-XB']) {
  test(`locale ${locale} persists and renders the settings workflow`, async ({page}) => {
    await page.goto('/settings');
    await page.getByRole('combobox', {name:'Language',exact:true}).selectOption(locale);
    await expect(page.locator('html')).toHaveAttribute('lang',locale);
    await expect(page.locator('html')).toHaveAttribute('dir',locale === 'ar' || locale === 'ar-XB' ? 'rtl' : 'ltr');
    await expect(page.locator('main input').first()).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('lang',locale);
    await expect(page.getByRole('heading',{level:1})).toBeVisible();
    const heading = await page.getByRole('heading',{level:1}).innerText();
    await expect(page).toHaveTitle(heading + ' · SharpAI');
    const text = await page.locator('main').innerText();
    expect(text).not.toMatch(/text\.[a-z]|settings\.[a-z]|fields\.[A-Z]/);
    await page.screenshot({path:`test-results/screenshots/${test.info().project.name}-${locale}-settings.png`});
  });
}

test('HTML settings fallback is an error, not a configuration form', async ({page}) => {
  await page.route('**/api/settings', route => route.fulfill({contentType:'text/html',body:'<!doctype html><html>SPA fallback</html>'}));
  await page.goto('/settings');
  await expect(page.getByText(/502:/)).toBeVisible();
  await expect(page.locator('.sa-settings-fields')).toHaveCount(0);
  await expect(page.getByRole('button',{name:'Save',exact:true})).toBeDisabled();
});

test('pull accepts a Hugging Face page URL and sends the repository ID', async ({page}) => {
  await page.route('**/api/pull',route => route.fulfill({contentType:'application/x-ndjson',body:'{"status":"success"}\n'}));
  await page.goto('/models');
  await page.getByRole('button',{name:'Pull model',exact:true}).click();
  await page.getByRole('dialog').getByRole('textbox').fill('https://huggingface.co/Qwen/Qwen3-8B-GGUF/tree/main');
  const request = page.waitForRequest(req=> req.url().endsWith('/api/pull'));
  await page.getByRole('dialog').getByRole('button',{name:'Pull model',exact:true}).click();
  expect((await request).postDataJSON()).toEqual({name:'Qwen/Qwen3-8B-GGUF'});
});


test('observability exposes all four configured service links', async ({page}) => {
  await page.goto('/observability');
  for (const {name,port,path} of [
  {name:'Grafana',port:9400,path:'/dashboards/f/sharpai'},
  {name:'Prometheus',port:9090,path:'/graph'},
  {name:'Tempo',port:3200,path:'/ready'},
  {name:'Loki',port:3100,path:'/ready'},
]) {
    await expect(page.getByRole('link',{name:'Open '+name,exact:true})).toHaveAttribute('href',`http://localhost:${port}${path}`);
  }
});

test('conversation sends parameters and history, trims initial newlines, and displays usage',async({page})=>{
  await page.route('**/api/tags',route=>route.fulfill({json:{models:[{name:'test-model'}]}}));
  const requests: Record<string,unknown>[]=[];
  await page.route('**/api/chat',async route=>{
    requests.push(route.request().postDataJSON());
    await route.fulfill({contentType:'application/x-ndjson',body:'{"message":{"content":"\\n\\nHello"}}\n{"message":{"content":" world"},"done":true,"prompt_eval_count":12,"eval_count":3,"eval_duration":1000000000}\n'});
  });
  await page.goto('/playground');
  await page.getByLabel('Model',{exact:true}).selectOption('test-model');
  await page.getByRole('button',{name:'Edit selected system prompt',exact:true}).click();
  await page.getByRole('dialog').getByLabel('System prompt',{exact:true}).fill('Be concise.');
  await page.getByRole('dialog').getByRole('button',{name:'Save',exact:true}).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.getByRole('button',{name:'Edit selected model configuration',exact:true}).click();
  await page.getByLabel('Top-p',{exact:true}).fill('0.5');
  await page.getByLabel('Random seed',{exact:true}).fill('42');
  await page.getByRole('dialog').getByRole('button',{name:'Save',exact:true}).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.getByLabel('Prompt',{exact:true}).fill('Hello');
  await page.getByRole('button',{name:'Send',exact:true}).click();
  await expect(page.locator('.sa-chat-message--assistant .sa-chat-content')).toHaveText('Hello world');
  await expect(page.getByRole('button',{name:'Response telemetry'})).toBeVisible();
  await page.getByRole('button',{name:'Response telemetry'}).focus();
  await expect(page.locator('.sa-chat-metrics')).toContainText('Input tokens12');
  await expect(page.locator('.sa-chat-metrics')).toContainText('Output tokens3');
  expect(requests[0]).toMatchObject({model:'test-model',options:{top_p:0.5,seed:42},messages:[{role:'system',content:'Be concise.'},{role:'user',content:'Hello'}]});
  await page.getByLabel('Prompt',{exact:true}).fill('Continue');
  await page.getByRole('button',{name:'Send',exact:true}).click();
  await expect(page.locator('.sa-chat-message--assistant')).toHaveCount(2);
  expect((requests[1].messages as unknown[]).length).toBe(4);
  await page.getByRole('button',{name:'New conversation',exact:true}).click();
  await expect(page.locator('.sa-chat-message')).toHaveCount(0);
});

test('overview offers quick actions without the activity and failures panels',async({page})=>{
  await page.goto('/');
  await expect(page.locator('.sa-quick-actions a')).toHaveCount(6);
  await expect(page.locator('.sa-chart')).toHaveCount(0);
  await expect(page.getByText('Recent failures',{exact:true})).toHaveCount(0);
  await expect(page.getByText('Local intelligence. Your control.',{exact:true})).toHaveCount(0);
});

test('chat reports a terminal server error without treating it as a successful reply',async({page})=>{
  await page.route('**/api/tags',route=>route.fulfill({json:{models:[{name:'test-model'}]}}));
  await page.route('**/api/chat',route=>route.fulfill({contentType:'application/x-ndjson',body:'{"error":"Context limit exceeded"}\n'}));
  await page.goto('/playground');
  await page.getByLabel('Prompt',{exact:true}).fill('Hello');
  await page.getByRole('button',{name:'Send',exact:true}).click();
  await expect(page.getByRole('alert')).toContainText('Context limit exceeded');
  await expect(page.getByRole('button',{name:'Stop generating',exact:true})).toHaveCount(0);
  await expect(page.getByRole('button',{name:'Response telemetry'})).toHaveCount(0);
});

test('invalid sampling values prevent submission',async({page})=>{
  await page.route('**/api/tags',route=>route.fulfill({json:{models:[{name:'test-model'}]}}));
  let requests=0;
  await page.route('**/api/chat',route=>{requests++;return route.fulfill({contentType:'application/x-ndjson',body:'{"done":true}\n'});});
  await page.goto('/playground');
  await page.getByLabel('Prompt',{exact:true}).fill('Hello');
  await page.getByRole('button',{name:'Edit selected model configuration',exact:true}).click();
  await page.getByLabel('Temperature',{exact:true}).fill('3');
  await page.getByRole('dialog').getByRole('button',{name:'Save',exact:true}).click();
  expect(await page.getByLabel('Temperature',{exact:true}).evaluate((input:HTMLInputElement)=>input.validity.rangeOverflow)).toBe(true);
  expect(requests).toBe(0);
  await expect(page.locator('.sa-chat-message')).toHaveCount(0);
});

test('chat streaming keeps its composer and page position stable',async({page})=>{
  await page.route('**/api/tags',route=>route.fulfill({json:{models:[{name:'test-model'}]}}));
  await page.route('**/api/chat',route=>route.fulfill({contentType:'application/x-ndjson',body:JSON.stringify({message:{content:'A response line.\n'.repeat(80)},done:true})+'\n'}));
  await page.goto('/playground');
  await page.getByLabel('Prompt',{exact:true}).fill('Hello');
  await page.getByRole('button',{name:'Send',exact:true}).click();
  await expect(page.locator('.sa-chat-content').last()).toContainText('A response line.');
  expect(await page.locator('main').evaluate(element=>element.scrollTop)).toBeLessThan(30);
  const send=await page.getByRole('button',{name:'Send',exact:true}).boundingBox();
  expect(send!.y+send!.height).toBeLessThanOrEqual(page.viewportSize()!.height);
});

test('compact playground uses reusable selectors and no initial telemetry',async({page})=>{
  await page.goto('/playground');
  await expect(page.locator('.sa-chat-settings')).toHaveCount(0);
  await expect(page.getByRole('combobox',{name:'Model configuration',exact:true})).toBeVisible();
  await expect(page.getByLabel('Temperature',{exact:true})).not.toBeVisible();
  await expect(page.getByRole('button',{name:'Response telemetry'})).toHaveCount(0);
  await expect(page.getByText('AI can make mistakes. Please check answers.')).toBeVisible();
  const input=await page.getByLabel('Prompt',{exact:true}).boundingBox();
  const send=await page.getByRole('button',{name:'Send',exact:true}).boundingBox();
  expect(input!.height).toBeLessThanOrEqual(34);
  expect(Math.abs(input!.y-send!.y)).toBeLessThan(2);
  await page.getByRole('button',{name:'Edit selected model configuration',exact:true}).click();
  await expect(page.getByLabel('Temperature',{exact:true})).toBeVisible();
});

test('copy icons confirm successful insecure-context fallback without text buttons',async({page})=>{
  await page.addInitScript(()=>{
    Object.defineProperty(navigator,'clipboard',{configurable:true,value:undefined});
    const copy=document.execCommand.bind(document);
    document.execCommand=(command:string)=>{
      if(command==='copy') (window as unknown as {copied:string}).copied=(document.activeElement as HTMLTextAreaElement).value;
      return copy(command);
    };
  });
  await page.goto('/api-explorer');
  const button=page.locator('.sa-request-actions .sa-copy-button');
  await expect(button).toHaveText('');
  await button.click();
  await expect(button).toHaveAccessibleName('Copied');
  await expect(button).toHaveClass(/sa-copy-button--copied/);
  expect(await page.evaluate(()=>(window as unknown as {copied:string}).copied)).toContain('curl');
});

for(const origin of ['http://sharpai.test','https://sharpai.test']) {
 test(`copy works on ${origin} without localhost assumptions`,async({page,context})=>{
  await context.route(`${origin}/**`,async route=>{
    const url=new URL(route.request().url());
    const response=await route.fetch({url:`http://localhost:3017${url.pathname}${url.search}`});
    await route.fulfill({response});
  });
  if(origin.startsWith('https:'))await context.grantPermissions(['clipboard-read','clipboard-write'],{origin});
  await page.goto(`${origin}/api-explorer`);
  const button=page.locator('.sa-request-actions .sa-copy-button');
  await button.click();
  await expect(button).toHaveAccessibleName('Copied');
  await expect(button).toHaveClass(/sa-copy-button--copied/);
  if(origin.startsWith('https:'))expect(await page.evaluate(()=>navigator.clipboard.readText())).toContain('curl');
  await context.unrouteAll({behavior:'wait'});
 });
}

for(const locale of ['en','es','pt','fr','it','de','zh','ar','ru','ms','hi','ja']) {
 test(`useful control help covers ${locale} forms tables and dialogs`,async({page})=>{
  await page.goto('/settings');await page.getByRole('combobox',{name:'Language',exact:true}).selectOption(locale);
  for(const path of ['/settings','/playground','/models','/request-history','/api-explorer']) {
   await page.goto(path);await expect(page.locator('main h1')).toBeVisible();
   if(path==='/playground')await page.locator('.sa-library-picker button').last().click();
   if(path==='/models')await page.locator('button:has(svg)').filter({has:page.locator('svg path[d="M12 3v12m-5-5 5 5 5-5M4 16v5h16v-5"]')}).click();
   await expect.poll(()=>page.evaluate(()=>Array.from(document.querySelectorAll<HTMLElement>('main button,main label,main th,main select,main input,main textarea,[role=dialog] button,[role=dialog] input')).filter(element=>!element.title || element.title.length<8 || element.title===element.textContent?.trim()).map(element=>element.outerHTML.slice(0,160)))).toEqual([]);
   expect(await page.locator('body').innerText()).not.toMatch(/help\.[a-z]|samplingHelp\./);
   if(path==='/models'||path==='/playground')await page.keyboard.press('Escape');
  }
 });
}

test('non-streaming playground submits false and shows the complete reply',async({page})=>{
 await page.route('**/api/tags',route=>route.fulfill({json:{models:[{name:'test-model'}]}}));
 await page.route('**/api/chat',route=>route.fulfill({json:{message:{content:'Complete answer'},done:true,prompt_eval_count:5,eval_count:2,first_token_duration:12000000}}));
 await page.goto('/playground');await page.getByRole('button',{name:'Edit selected model configuration',exact:true}).click();
 await page.getByLabel('Stream response',{exact:true}).uncheck();
 await page.getByRole('dialog').getByRole('button',{name:'Save',exact:true}).click();
 await expect(page.getByRole('dialog')).toHaveCount(0);
 await page.getByLabel('Prompt',{exact:true}).fill('Hello');
 const request=page.waitForRequest(req=>req.url().endsWith('/api/chat'));await page.getByRole('button',{name:'Send',exact:true}).click();
 expect((await request).postDataJSON().stream).toBe(false);
 await expect(page.locator('.sa-chat-message--assistant')).toContainText('Complete answer');
 await page.getByRole('button',{name:'Response telemetry'}).focus();
 await expect(page.getByRole('tooltip')).toContainText('0.01 s');
});

test('full history detail fetch shows formatted payloads and durable inference phases',async({page})=>{
 const row={Id:'req-rich',Method:'POST',Path:'/api/chat',StatusCode:200,DurationMs:800,CreatedUtc:'2026-10-21T12:00:00Z'};
 await page.route('**/v1.0/api/request-history**',route=>route.fulfill({json:route.request().url().includes('/summary')?{TotalCount:1,Buckets:[]}:route.request().url().includes('/req-rich')?{...row,InputTokens:12,OutputTokens:4,CachedTokens:null,TtftMs:120,GenerationMs:600,QueueMs:2,RequestHeaders:{'x-api-key':'[redacted]'},RequestBody:JSON.stringify({messages:[{role:'user',content:'Hello world'}]}),ResponseBody:JSON.stringify({message:{role:'assistant',content:'A useful answer'}}),ResponseBodyBytes:80}:{Objects:[row],TotalCount:1}}));
 await page.goto('/inference-history');await expect(page.locator('thead th').first()).toHaveText('Created (UTC)');
 await page.getByRole('cell',{name:'/api/chat',exact:true}).click();
 const dialog=page.getByRole('dialog');await expect(dialog).toHaveClass(/sa-modal--wide/);
 await expect(dialog).toContainText('Hello world');await expect(dialog).toContainText('A useful answer');
 await expect(dialog).toContainText('Cached tokens');await expect(dialog).toContainText('Not available');
 await expect(dialog.locator('meter')).toHaveCount(7);
 await expect(dialog.locator('.sa-json-viewer')).toHaveCount(0);
 await page.screenshot({path:`test-results/screenshots/${test.info().project.name}-rich-request.png`});
});

test('streamed history reconstructs readable output and preserves frame inspection',async({page})=>{
 const row={Id:'req-stream',Method:'POST',Path:'/api/chat',StatusCode:200,DurationMs:300,CreatedUtc:'2026-10-21T12:00:00Z'};
 await page.route('**/v1.0/api/request-history**',route=>route.fulfill({json:route.request().url().includes('/summary')?{TotalCount:1,Buckets:[]}:route.request().url().includes('/req-stream')?{...row,ResponseBody:'{"message":{"content":"Hello "}}\n{"message":{"content":"world"},"done":true}\n',ResponseBodyBytes:80}:{Objects:[row],TotalCount:1}}));
 await page.goto('/request-history');await page.getByRole('cell',{name:'/api/chat',exact:true}).click();
 await expect(page.getByRole('dialog')).toContainText('Hello world');
 await page.getByText('Inspect captured stream frames (2)',{exact:true}).click();
 await expect(page.getByRole('dialog')).toContainText('done');
});

test('settings provider choices preserve connections and compact syslog has real gutters',async({page})=>{
 await page.route('**/api/settings',route=>route.fulfill({json:{...configuration,Database:{Type:'Sqlite',Filename:'./sharpai.db',Hostname:'db.internal',Port:0,DatabaseName:'sharpai',Username:'dbuser',Password:''},Logging:{Servers:[{Hostname:'127.0.0.1',Port:514,RandomizePorts:false,MinimumPort:65000,MaximumPort:65535}],LogDirectory:'./logs/',LogFilename:'sharpai.log',MinimumSeverity:0},Rest:{Headers:{DefaultHeaders:{'Access-Control-Allow-Origin':'*'}}},Telemetry:{OtlpProtocol:'grpc'}}}));
 await page.goto('/settings');
 const database=page.locator('fieldset[data-setting-path="Database"]');
 await expect(database.getByRole('combobox',{name:'Type',exact:true})).toHaveValue('Sqlite');
 await expect(database.getByLabel('Filename',{exact:true})).toBeVisible();
 await expect(database.getByLabel('Hostname',{exact:true})).toHaveCount(0);
 await database.getByRole('combobox',{name:'Type',exact:true}).selectOption('Postgresql');
 await expect(database.getByLabel('Filename',{exact:true})).toHaveCount(0);
 await expect(database.getByLabel('Hostname',{exact:true})).toHaveValue('db.internal');
 await expect(database.getByLabel('Port',{exact:true})).toHaveAttribute('title',/5432/);
 await database.getByRole('combobox',{name:'Type',exact:true}).selectOption('Sqlite');
 await expect(database.getByLabel('Filename',{exact:true})).toHaveValue('./sharpai.db');
 await expect(page.locator('.sa-syslog-row').getByRole('combobox',{name:'Randomize Ports',exact:true})).toHaveAttribute('title',/destination port/);
 const logging=page.locator('fieldset[data-setting-path="Logging"]');
 const syslogInputs=await logging.locator('.sa-syslog-row input,.sa-syslog-row select').evaluateAll(nodes=>nodes.map(node=>{const r=node.getBoundingClientRect();return {x:r.x,y:r.y,width:r.width,height:r.height};}));
 for(let i=0;i<syslogInputs.length;i++)for(let j=i+1;j<syslogInputs.length;j++)if(Math.abs(syslogInputs[i].y-syslogInputs[j].y)<4)expect(Math.abs(syslogInputs[j].x-syslogInputs[i].x)).toBeGreaterThanOrEqual(Math.min(syslogInputs[i].width,syslogInputs[j].width)+8);
 await expect(logging.getByRole('combobox',{name:'Minimum Severity'})).toHaveValue('0');
 await expect(logging.getByRole('combobox',{name:'Minimum Severity'})).toHaveAttribute('title',/0 debug/);
 const dir=await logging.getByLabel('Log Directory',{exact:true}).boundingBox();
 const file=await logging.getByLabel('Log Filename',{exact:true}).boundingBox();
 expect(dir && file && (file.y>=dir.y+dir.height+8 || file.x>=dir.x+dir.width+8)).toBeTruthy();
 if(test.info().project.name.startsWith('desktop')) {
  const row=logging.locator('.sa-syslog-row');
  const host=await row.getByLabel('Hostname',{exact:true}).boundingBox();
  const port=await row.getByLabel('Port',{exact:true}).boundingBox();
  const min=await row.getByLabel('Minimum Port',{exact:true}).boundingBox();
  const trash=await row.getByRole('button').boundingBox();
  expect(Math.abs(host!.y-port!.y)).toBeLessThan(2);expect(min!.y).toBeGreaterThan(host!.y);
  expect(Math.abs(trash!.y+trash!.height-min!.y-min!.height)).toBeLessThan(3);
 }
 await logging.scrollIntoViewIfNeeded();
 await page.screenshot({path:`test-results/screenshots/${test.info().project.name}-compact-settings.png`,fullPage:true});
});

test('history retains raw request whitespace and copies headers as JSON',async({page})=>{
 const row={Id:'req-raw-copy',Method:'POST',Path:'/api/chat',StatusCode:200,DurationMs:20,CreatedUtc:'2026-10-21T12:00:00Z'};
 const raw='{ "messages": [\n  {"role":"user", "content":"hello"}\n] }';
 await page.route('**/v1.0/api/request-history**',route=>route.fulfill({json:route.request().url().includes('/summary')?{TotalCount:1,Buckets:[]}:route.request().url().includes('/req-raw-copy')?{...row,RequestHeaders:{'content-type':'application/json','x-secret':'[redacted]'},ResponseHeaders:{'content-type':'application/json','X-Empty':'','X-Literal':'{\"enabled\":true}'},RequestBody:raw,ResponseBody:'{"message":"hello"}'}:{Objects:[row],TotalCount:1}}));
 await page.addInitScript(()=>Object.defineProperty(navigator,'clipboard',{value:{writeText:async(text:string)=>{(window as unknown as {copied:string}).copied=text;}}}));
 await page.goto('/request-history');await page.getByRole('cell',{name:'/api/chat',exact:true}).click();
 const request=page.getByRole('dialog').locator('.sa-detail-exchange > section').first();
 expect(await request.locator(':scope > pre').textContent()).toBe(raw);
 await request.locator('.sa-payload-heading').first().getByRole('button').click();
 await expect.poll(()=>page.evaluate(()=>(window as unknown as {copied:string}).copied)).toBe(JSON.stringify({'content-type':'application/json','x-secret':'[redacted]'},null,2));
 await expect(request.locator('.sa-copy-button--copied')).toHaveCount(1);
 const response=page.getByRole('dialog').locator('.sa-detail-exchange > section').last();
 await expect(response.locator('.sa-http-headers')).not.toContainText('No content retained');
 await expect(response.locator('.sa-http-headers')).toContainText('{"enabled":true}');
});

test('playground uses a red square stop control and content-sized replies',async({page})=>{
 let release:()=>void=()=>{};
 const pending=new Promise<void>(resolve=>{release=resolve;});
 await page.route('**/api/tags',route=>route.fulfill({json:{models:[{name:'test-model'}]}}));
 await page.route('**/api/chat',async route=>{await pending;await route.fulfill({contentType:'application/x-ndjson',body:'{"message":{"content":"Hi!"},"done":true}\n'});});
 await page.goto('/playground');await page.getByRole('button',{name:'Edit selected model configuration',exact:true}).click();
 await expect(page.getByLabel('Stream response',{exact:true})).toHaveAttribute('title',/tokens as they arrive/);
 await page.getByRole('dialog').getByRole('button',{name:'Cancel',exact:true}).click();
 await page.getByLabel('Prompt',{exact:true}).fill('Hello');await page.getByRole('button',{name:'Send',exact:true}).click();
 const stop=page.getByRole('button',{name:'Stop generating',exact:true});await expect(stop).toBeVisible();
 await expect(stop.locator('rect')).toHaveAttribute('fill','currentColor');
 await page.mouse.move(0,0);await expect.poll(()=>stop.evaluate(node=>getComputedStyle(node).backgroundColor)).toBe('rgb(198, 40, 40)');
 release();await expect(page.locator('.sa-chat-message--assistant')).toContainText('Hi!');
 const bubble=await page.locator('.sa-chat-message--assistant').boundingBox();
 const chat=await page.locator('.sa-chat-messages').boundingBox();
 expect(bubble!.width).toBeLessThan(chat!.width-30);
});

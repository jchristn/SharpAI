import {test,expect,type Page} from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import type {LibraryItem} from '../src/api/library';

async function install(page:Page,role:'User'|'Tenant'|'System'='User'){
 const config={Temperature:.7,MaxTokens:512,TopP:.8,TopK:20,MinP:.05,RepeatPenalty:1.1,RepeatLastN:64,PresencePenalty:0,FrequencyPenalty:0,Seed:0,Stop:['END'],StreamResponse:false,JsonOutput:true,DisplayThinking:true};
 const common={Description:'Reusable instructions',CreatedUtc:'2026-10-22T00:00:00Z',UpdatedUtc:'2026-10-22T00:00:00Z',CanDelete:false};
 const prompts:LibraryItem[]=[
  {...common,Id:'system-starter-prompt',Kind:'SystemPrompt',Name:'System starter',Scope:'System',TenantId:null,UserId:null,Content:'System instructions',Configuration:null,CanEdit:role==='System'},
  {...common,Id:'tenant-prompt',Kind:'SystemPrompt',Name:'Tenant starter',Scope:'Tenant',TenantId:'tenant-a',UserId:null,Content:'Tenant instructions',Configuration:null,CanEdit:role!=='User'},
  {...common,Id:'personal-prompt',Kind:'SystemPrompt',Name:'Personal instructions',Scope:'User',TenantId:'tenant-a',UserId:'user-a',Content:'My private instructions',Configuration:null,CanEdit:true,CanDelete:true},
 ];
 const configurations:LibraryItem[]=[{...common,Id:'system-starter-configuration',Kind:'ModelConfiguration',Name:'Saved sampling',Scope:'System',TenantId:null,UserId:null,Content:null,Configuration:config,CanEdit:role==='System'}];
 await page.route('**/*',async route=>{
  const req=route.request(),url=new URL(req.url()),path=url.pathname;
  if(path==='/v1.0/auth/context')return route.fulfill({json:{isAdmin:role==='System',isTenantAdmin:role==='Tenant',tenantId:'tenant-a',userId:'user-a',capabilities:{}}});
  if(path==='/v1.0/tenants')return route.fulfill({json:{Objects:[{Guid:'tenant-a',Name:'Alpha'},{Guid:'tenant-b',Name:'Beta'}],EndOfResults:true}});
  if(path==='/v1.0/library-context')return route.fulfill({json:{IsAdmin:role==='System',IsTenantAdmin:role==='Tenant',TenantId:'tenant-a',UserTenantId:'tenant-a',UserId:'user-a'}});
  if(path==='/api/tags')return route.fulfill({json:{models:[{name:'test-model'}]}});
  if(path==='/api/ps')return route.fulfill({json:{models:[]}});
  if(path==='/health'||path==='/ready')return route.fulfill({json:{version:'4.0.1'}});
  if(path.includes('/request-history'))return route.fulfill({json:{TotalCount:0,Objects:[],Buckets:[]}});
  const match=path.match(/^\/v1\.0\/(system-prompts|model-configurations)(?:\/([^/]+))?(\/duplicate)?$/);
  if(!match)return route.fallback();
  const rows=match[1]==='system-prompts'?prompts:configurations;
  if(req.method()==='GET')return route.fulfill({json:{Objects:rows,TotalRecords:rows.length,EndOfResults:true}});
  const index=rows.findIndex(row=>row.Id===match[2]);
  if(req.method()==='DELETE'){rows.splice(index,1);return route.fulfill({json:{Deleted:true}});}
  const body=req.postDataJSON();const item={...common,...(index>=0?rows[index]:{}),...body,Id:req.method()==='PUT'?match[2]:'new-copy',CanEdit:true,CanDelete:true,UserId:body.Scope==='User'?'user-a':null,TenantId:body.Scope==='System'?null:'tenant-a'};
  if(req.method()==='PUT')rows[index]=item;else rows.push(item);
  return route.fulfill({json:item});
 });
 return {prompts,configurations};
}
for(const kind of ['system-prompts','model-configurations'] as const){
 test(`${kind} management fits and is accessible`,async({page})=>{
  await install(page);await page.goto('/'+kind);await expect(page.locator('main h1')).toHaveText(kind==='system-prompts'?'System prompts':'Model configurations');
  await expect(page.locator('tbody tr')).not.toHaveCount(0);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
  const violations=(await new AxeBuilder({page}).analyze()).violations.filter(v=>['critical','serious'].includes(v.impact??''));expect(violations).toEqual([]);
  await page.screenshot({path:`test-results/screenshots/${test.info().project.name}-${kind}.png`,fullPage:true});
 });
}
test('regular users inspect and duplicate shared prompts into private editable items',async({page})=>{
 const {prompts}=await install(page);await page.goto('/system-prompts');
 const row=page.getByRole('row').filter({has:page.getByText('System starter',{exact:true})});await row.getByRole('button',{name:'Actions',exact:true}).click();
 await expect(page.getByRole('menuitem',{name:'Edit',exact:true})).toBeDisabled();
 await page.getByRole('menuitem',{name:'Duplicate',exact:true}).click();
 const dialog=page.getByRole('dialog');await expect(dialog.getByRole('combobox',{name:'Visibility'})).toHaveValue('User');
 await expect(dialog.getByRole('option',{name:'System default'})).toHaveCount(0);await expect(dialog.getByRole('option',{name:'Tenant default'})).toHaveCount(0);
 await dialog.getByLabel('Name',{exact:true}).fill('My customized prompt');await dialog.getByRole('textbox',{name:'System prompt',exact:true}).fill('My changed instructions');
 await dialog.getByRole('button',{name:'Save',exact:true}).click();await expect(dialog).toHaveCount(0);await expect(page.getByRole('cell',{name:'My customized prompt',exact:true})).toBeVisible();
 expect(prompts[0].Content).toBe('System instructions');expect(prompts.find(row=>row.Name==='My customized prompt')?.Scope).toBe('User');
});
for(const role of ['Tenant','System'] as const){
 test(`${role} administrators see only authorized publication scopes`,async({page})=>{
  await install(page,role);await page.goto('/model-configurations');await page.getByRole('button',{name:'Create',exact:true}).click();
  const scope=page.getByRole('dialog').getByRole('combobox',{name:'Visibility'});await expect(scope).toHaveValue('User');
  await scope.selectOption('Tenant');await expect(scope).toHaveValue('Tenant');
  await expect(scope.getByRole('option',{name:'System default'})).toHaveCount(role==='System'?1:0);
 });
}
test('Playground submits selected saved values and edits a shared selection as a private duplicate',async({page})=>{
 await install(page);let submitted:Record<string,unknown>={};
 await page.route('**/api/chat',route=>{submitted=route.request().postDataJSON();return route.fulfill({json:{message:{content:'A complete answer'},done:true}});});
 await page.goto('/playground');await page.getByRole('combobox',{name:'System prompt',exact:true}).selectOption('tenant-prompt');
 await page.getByLabel('Prompt',{exact:true}).fill('Hello');await page.getByRole('button',{name:'Send',exact:true}).click();
 await expect(page.getByText('A complete answer',{exact:true})).toBeVisible();
 expect(submitted).toMatchObject({stream:false,format:'json',messages:[{role:'system',content:'Tenant instructions'},{role:'user',content:'Hello'}],options:{temperature:.7,num_predict:512,top_p:.8,top_k:20,min_p:.05,repeat_penalty:1.1,repeat_last_n:64,presence_penalty:0,frequency_penalty:0,seed:0,stop:['END'],display_thinking:true}});
 await page.getByRole('button',{name:'Edit selected system prompt',exact:true}).click();const dialog=page.getByRole('dialog');await expect(dialog).toHaveAccessibleName('Duplicate');
 await dialog.getByLabel('Name',{exact:true}).fill('My new prompt');await dialog.getByRole('button',{name:'Save',exact:true}).click();
 await expect(page.getByRole('combobox',{name:'System prompt',exact:true})).toHaveValue('new-copy');
 await page.reload();await expect(page.getByRole('combobox',{name:'System prompt',exact:true})).toHaveValue('new-copy');
});
test('saved configuration editor retains every parameter and all output toggles',async({page})=>{
 const {configurations}=await install(page,'System');configurations[0].Configuration!.Stop=['END','line one\nline two'];await page.goto('/playground');await page.getByRole('button',{name:'Edit selected model configuration',exact:true}).click();
 const dialog=page.getByRole('dialog');await expect(dialog.getByLabel('Random seed',{exact:true})).toHaveValue('0');
 await expect(dialog.getByLabel('Stream response',{exact:true})).not.toBeChecked();await expect(dialog.getByLabel('Constrain output to JSON',{exact:true})).toBeChecked();
 await dialog.getByLabel('Temperature',{exact:true}).fill('0.2');await dialog.getByRole('button',{name:'Save',exact:true}).click();await expect(dialog).toHaveCount(0);
 expect(configurations[0].Configuration).toMatchObject({Temperature:.2,MaxTokens:512,TopP:.8,TopK:20,MinP:.05,RepeatPenalty:1.1,RepeatLastN:64,PresencePenalty:0,FrequencyPenalty:0,Seed:0,Stop:['END','line one\nline two'],StreamResponse:false,JsonOutput:true,DisplayThinking:true});
});
test('home cards include library entry points and colored icon KPIs',async({page})=>{
 await install(page);await page.goto('/');await expect(page.locator('.sa-quick-card')).toHaveCount(6);await expect(page.getByRole('link',{name:'Manage prompts',exact:false})).toBeVisible();
 await expect(page.locator('.sa-home-kpis .sa-kpi__label svg')).toHaveCount(5);
 const colors=await page.locator('.sa-home-kpis .sa-kpi__value').evaluateAll(nodes=>nodes.map(node=>getComputedStyle(node).color));expect(new Set(colors).size).toBe(5);
});

test('row actions and ID copying do not open the inspector, while inspection and deletion work',async({page})=>{
 await install(page);await page.goto('/system-prompts');
 const row=page.getByRole('row').filter({has:page.getByText('Personal instructions',{exact:true})});
 await row.getByRole('button',{name:'Copy',exact:true}).first().click();await expect(page.getByRole('dialog')).toHaveCount(0);
 await row.getByRole('button',{name:'Actions',exact:true}).click();await expect(page.getByRole('dialog')).toHaveCount(0);
 await page.getByRole('menuitem',{name:'Details',exact:true}).click();await expect(page.getByRole('dialog')).toContainText('My private instructions');
 await page.keyboard.press('Escape');await row.getByRole('button',{name:'Actions',exact:true}).click();await page.getByRole('menuitem',{name:'Delete',exact:true}).click();
 await page.getByRole('dialog').getByRole('button',{name:'Confirm',exact:true}).click();await expect(page.getByRole('cell',{name:'Personal instructions',exact:true})).toHaveCount(0);
});
test('opting out of saved defaults persists and sends genuine engine defaults',async({page})=>{
 await install(page);let submitted:Record<string,unknown>={};await page.route('**/api/chat',route=>{submitted=route.request().postDataJSON();return route.fulfill({contentType:'application/x-ndjson',body:'{"message":{"content":"Done"},"done":true}\n'});});
 await page.goto('/playground');await page.getByRole('combobox',{name:'System prompt',exact:true}).selectOption('');await page.getByRole('combobox',{name:'Model configuration',exact:true}).selectOption('');
 await page.reload();await expect(page.getByRole('combobox',{name:'Model configuration',exact:true})).toHaveValue('');await page.getByLabel('Prompt',{exact:true}).fill('Hello');await page.getByRole('button',{name:'Send',exact:true}).click();await expect(page.getByText('Done',{exact:true})).toBeVisible();
 expect(submitted.messages).toEqual([{role:'user',content:'Hello'}]);expect(submitted.options).toEqual({display_thinking:false});expect(submitted.stream).toBe(true);
});

test('expired stored sessions recover without leaving the library stuck',async({page})=>{
 await install(page);
 await page.addInitScript(()=>localStorage.setItem('sharpai.session',JSON.stringify({token:'expired',userId:'old-user',tenantId:'tenant-a'})));
 await page.route('**/v1.0/library-context',route=>route.request().headers().authorization?route.fulfill({status:401,json:{Message:'Session expired'}}):route.fulfill({json:{IsAdmin:true,IsTenantAdmin:false,TenantId:'tenant-a'}}));
 await page.goto('/system-prompts');await expect(page.getByRole('button',{name:'Sign in',exact:true})).toBeVisible();
 await expect(page.getByRole('button',{name:'Create',exact:true})).toBeEnabled();
 expect(await page.evaluate(()=>localStorage.getItem('sharpai.session'))).toBeNull();
});

test('signing out during generation cannot restore the previous account conversation',async({page})=>{
 await install(page);await page.addInitScript(()=>localStorage.setItem('sharpai.session',JSON.stringify({token:'valid',userId:'user-a',tenantId:'tenant-a'})));
 await page.route('**/v1.0/token',route=>route.fulfill({json:{revoked:true}}));
 let release:()=>void=()=>{};const hold=new Promise<void>(resolve=>{release=resolve;});
 await page.route('**/api/chat',async route=>{await hold;try{await route.fulfill({json:{message:{content:'Old private reply'},done:true}});}catch{/* The old request is cancelled when its identity changes. */}});
 await page.goto('/playground');await page.getByLabel('Prompt',{exact:true}).fill('Sensitive old turn');await page.getByRole('button',{name:'Send',exact:true}).click();
 await expect(page.getByRole('button',{name:'Stop generating',exact:true})).toBeVisible();await page.getByRole('button',{name:'Sign out',exact:true}).click();await expect(page.getByRole('button',{name:'Sign in',exact:true})).toBeVisible();
 release();await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
 await expect(page.locator('.sa-chat-message')).toHaveCount(0);await expect(page.getByText('Old private reply',{exact:true})).toHaveCount(0);
});

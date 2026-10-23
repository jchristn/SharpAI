import { authenticatedPage } from './session-fixture';
import {test,expect,type Page} from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import type {LibraryItem,LibraryKind} from '../src/api/library';

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
for(const kind of ['system-prompts','model-configurations'] satisfies LibraryKind[]){
 test(`${kind} management fits and is accessible`,async({page})=>{
  await install(page);await page.goto('/'+kind);await expect(page.locator('main h1')).toHaveText(kind==='system-prompts'?'System Prompts':'Model Configurations');
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
for(const role of ['Tenant','System'] satisfies Array<'Tenant'|'System'>){
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
 await page.goto('/system-prompts');await expect(page.getByRole('button',{name:'Sign in',exact:true}).first()).toBeVisible();
 await expect(page.getByRole('button',{name:'Create',exact:true})).toHaveCount(0);
 expect(await page.evaluate(()=>localStorage.getItem('sharpai.session'))).toBeNull();
});

test('signing out during generation cannot restore the previous account conversation',async({page})=>{
 await install(page);await page.addInitScript(()=>localStorage.setItem('sharpai.session',JSON.stringify({token:'valid',userId:'user-a',tenantId:'tenant-a'})));
 await page.route('**/v1.0/token',route=>route.fulfill({json:{revoked:true}}));
 let release:()=>void=()=>{};const hold=new Promise<void>(resolve=>{release=resolve;});
 await page.route('**/api/chat',async route=>{await hold;try{await route.fulfill({json:{message:{content:'Old private reply'},done:true}});}catch{/* The old request is cancelled when its identity changes. */}});
 await page.goto('/playground');await page.getByLabel('Prompt',{exact:true}).fill('Sensitive old turn');await page.getByRole('button',{name:'Send',exact:true}).click();
 await expect(page.getByRole('button',{name:'Stop generating',exact:true})).toBeVisible();await page.getByRole('button',{name:'Sign out',exact:true}).click();await expect(page.getByRole('button',{name:'Sign in',exact:true}).first()).toBeVisible();
 release();await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
 await expect(page.locator('.sa-chat-message')).toHaveCount(0);await expect(page.getByText('Old private reply',{exact:true})).toHaveCount(0);
});

test('library selectors align with their edit icons',async({page})=>{
 await install(page);await page.goto('/playground');
 for(const picker of await page.locator('.sa-library-picker').all()){
  const select=await picker.locator('select').boundingBox();const button=await picker.locator('button').boundingBox();
  expect(select).not.toBeNull();expect(button).not.toBeNull();expect(Math.abs(select!.height-button!.height)).toBeLessThan(1);expect(Math.abs(select!.y-button!.y)).toBeLessThan(1);
 }
});
for(const kind of ['system-prompts','model-configurations'] satisfies Array<'system-prompts'|'model-configurations'>){
 test(`${kind} preview offers edit and all editor modes use matching control heights`,async({page})=>{
  await install(page,'System');await page.goto('/'+kind);
  const row=page.getByRole('row').filter({has:page.getByRole('cell',{name:kind==='system-prompts'?'System starter':'Saved sampling',exact:true})});await expect(row).toBeVisible();await row.getByRole('cell',{name:kind==='system-prompts'?'System starter':'Saved sampling',exact:true}).click();let dialog=page.getByRole('dialog');await expect(dialog.getByRole('button',{name:'Edit',exact:true})).toBeEnabled();await dialog.getByRole('button',{name:'Edit',exact:true}).click();
  async function aligned(){const modal=page.getByRole('dialog');const name=await modal.getByLabel('Name',{exact:true}).boundingBox();const scope=await modal.getByRole('combobox',{name:'Visibility'}).boundingBox();expect(Math.abs(name!.height-scope!.height)).toBeLessThan(1);await modal.getByRole('button',{name:'Cancel',exact:true}).click();}
  await aligned();await page.getByRole('button',{name:'Create',exact:true}).click();await aligned();
  await row.getByRole('cell',{name:kind==='system-prompts'?'System starter':'Saved sampling',exact:true}).click();dialog=page.getByRole('dialog');await dialog.getByRole('button',{name:'Duplicate',exact:true}).click();await aligned();
  await expect(page.getByRole('combobox',{name:'Selected tenant',exact:true})).toBeVisible();await expect(page.getByText('Entire tenant',{exact:true})).toHaveCount(0);
 });
}
test('model configuration numeric inputs clamp every range and preserve fractional sampling',async({page})=>{
 const {configurations}=await install(page,'System');await page.goto('/model-configurations');await page.getByRole('button',{name:'Create',exact:true}).click();const dialog=page.getByRole('dialog');await dialog.getByLabel('Name',{exact:true}).fill('Clamped configuration');
 for(const input of await dialog.locator('input[type=number]').all()){
  const min=Number(await input.getAttribute('min'));const max=Number(await input.getAttribute('max'));
  await input.fill(String(max+100));await expect(input).toHaveValue(String(max));await input.fill(String(min-100));await expect(input).toHaveValue(String(min));
 }
 await dialog.getByLabel('Top-k',{exact:true}).fill('2.6');await expect(dialog.getByLabel('Top-k',{exact:true})).toHaveValue('3');
 await dialog.getByLabel('Top-p',{exact:true}).fill('0.333');await dialog.getByRole('button',{name:'Save',exact:true}).click();await expect(dialog).toHaveCount(0);
 expect(configurations.find(item=>item.Name==='Clamped configuration')?.Configuration).toMatchObject({TopK:3,TopP:.333,Temperature:0,MaxTokens:1,Seed:0});
});
test('waiting quips sit beneath the response and turn telemetry forms aligned rows',async({page})=>{
 await install(page);let release:()=>void=()=>{};const hold=new Promise<void>(resolve=>{release=resolve;});
 await page.route('**/api/chat',async route=>{await hold;await route.fulfill({json:{message:{content:'A tidy answer'},done:true,prompt_eval_count:12,eval_count:4,eval_duration:1000000000,total_duration:1300000000}});});
 await page.goto('/playground');await page.getByLabel('Prompt',{exact:true}).fill('Hello');await page.getByRole('button',{name:'Send',exact:true}).click();const quip=page.locator('.sa-chat-quip');await expect(quip).toBeVisible();expect(await quip.evaluate(element=>element.closest('article')===null)).toBe(true);expect(await quip.locator('em').evaluate(element=>getComputedStyle(element).fontStyle)).toBe('italic');
 release();await expect(quip).toHaveCount(0);await expect(page.getByText('A tidy answer',{exact:true})).toBeVisible();await page.getByRole('button',{name:'Response telemetry',exact:true}).hover();const tooltip=page.getByRole('tooltip');await expect(tooltip).toBeVisible();
 const rows=await tooltip.locator('dl > div').all();expect(rows).toHaveLength(7);const starts:number[]=[];const ends:number[]=[];
 for(const row of rows){const label=await row.locator('dt').boundingBox();const value=await row.locator('dd').boundingBox();expect(label!.x+label!.width).toBeLessThan(value!.x);starts.push(label!.x);ends.push(value!.x+value!.width);}
 expect(Math.max(...starts)-Math.min(...starts)).toBeLessThan(1);expect(Math.max(...ends)-Math.min(...ends)).toBeLessThan(1);
 await page.screenshot({path:`test-results/screenshots/${test.info().project.name}-turn-telemetry.png`});
});

// Every protected workflow starts with a validated sign-in.
test.beforeEach(async ({page}) => authenticatedPage(page));

for (const streaming of [true,false]) for (const finish of ['stop','length']) {
 test(`chat ${streaming?'streaming':'nonstreaming'} retains final-frame text and reports ${finish}`,async({page})=>{
  const {configurations}=await install(page);configurations[0].Configuration!.StreamResponse=streaming;
  const content=Array.from({length:48},(_,index)=>`Sentence ${index+1} is present.\n\n`).join('');
  const ending='Final sentence is complete. 你好 🌍';
  await page.route('**/api/chat',route=>route.fulfill({contentType:'application/x-ndjson',body:streaming?JSON.stringify({message:{role:'assistant',content},done:false})+'\n'+JSON.stringify({message:{role:'assistant',content:ending},done:true,done_reason:finish}):JSON.stringify({message:{role:'assistant',content:content+ending},done:true,done_reason:finish})}));
  await page.goto('/playground');await page.getByLabel('Prompt',{exact:true}).fill('Tell a story');await page.getByRole('button',{name:'Send',exact:true}).click();
  const response=page.locator('.sa-chat-message--assistant .sa-chat-content');
  await expect(response).toContainText(ending);await expect(response).toContainText('Sentence 1 is present.');await expect(response).toContainText('Sentence 48 is present.');
  await expect(page.getByLabel('Prompt',{exact:true})).toBeEnabled();
  if(finish==='length')await expect(page.getByText('Token limit reached.',{exact:false})).toBeVisible();
  const tail=response.locator('p').last();await expect(tail).toBeInViewport();
 });
}

for (const mode of ['inference','fallback','unknown','empty']) {
 test(`playground automatically selects the first ${mode} model`,async({page})=>{
  await install(page);
  const models=mode==='empty'?[]:mode==='unknown'?[{name:'first-unknown'},{name:'second-unknown'}]:[
   {name:'first-embedding',capabilities:{embeddings:true,completions:false}},
   {name:'first-inference',capabilities:{completions:mode==='inference'}},
   {name:'second-inference',capabilities:{completions:mode==='inference'}},
  ];
  await page.route('**/api/tags',route=>route.fulfill({json:{models}}));
  await page.goto('/playground');
  await expect(page.getByRole('combobox',{name:'Model',exact:true})).toHaveValue(mode==='inference'?'first-inference':mode==='fallback'?'first-embedding':mode==='unknown'?'first-unknown':'');
  if(mode==='inference'){
   await page.getByRole('combobox',{name:'Model',exact:true}).selectOption('second-inference');
   await page.getByLabel('Prompt',{exact:true}).fill('Hello');
   await expect(page.getByRole('combobox',{name:'Model',exact:true})).toHaveValue('second-inference');
  }
 });
}

async function chatCommand(page:Page,text:string):Promise<void>{await page.getByLabel('Prompt',{exact:true}).fill(text);await page.getByLabel('Prompt',{exact:true}).press('Enter');}

test('slash help, unknown commands, model selection, status and clear stay local',async({page})=>{
 await install(page);let requests=0;
 await page.route('**/api/tags',route=>route.fulfill({json:{models:[{name:'first-model'},{name:'second-model'}]}}));
 await page.route('**/api/chat',route=>{requests++;return route.fulfill({json:{message:{content:'Unexpected'},done:true}});});
 await page.goto('/playground');await expect(page.getByRole('combobox',{name:'Model',exact:true})).toHaveValue('first-model');
 await chatCommand(page,'/?');await expect(page.locator('.sa-chat-command-notice')).toContainText('/compact');
 await chatCommand(page,'/not-a-command');await expect(page.locator('.sa-chat-command-notice')).toContainText('Unknown command');
 await chatCommand(page,'/model second-model');await expect(page.getByRole('combobox',{name:'Model',exact:true})).toHaveValue('second-model');
 await chatCommand(page,'/status');await expect(page.locator('.sa-chat-command-notice')).toContainText('second-model');
 await chatCommand(page,'/clear');await expect(page.locator('.sa-chat-command-notice')).toContainText('Conversation cleared');expect(requests).toBe(0);
});

test('compact summarizes older turns, preserves visible chat and recent context, and clear resets both',async({page})=>{
 await install(page);const requests:Record<string,unknown>[]=[];
 await page.route('**/api/chat',route=>{
  const body=route.request().postDataJSON();requests.push(body);
  const compact=body.messages[0]?.content.startsWith('Summarize the quoted conversation');
  return route.fulfill({json:{message:{role:'assistant',content:compact?'Earlier summary: the user prefers violet.':'Original answer.'},done:true,done_reason:'stop'}});
 });
 await page.goto('/playground');await expect(page.getByRole('combobox',{name:'Model',exact:true})).toHaveValue('test-model');
 await chatCommand(page,'remember violet');await expect(page.locator('.sa-chat-message--assistant')).toHaveCount(1);await expect(page.getByLabel('Prompt',{exact:true})).toBeEnabled();
 await chatCommand(page,'add detail');await expect(page.locator('.sa-chat-message--assistant')).toHaveCount(2);await expect(page.getByLabel('Prompt',{exact:true})).toBeEnabled();
 await chatCommand(page,'/compact');await expect(page.locator('.sa-chat-command-notice')).toContainText('Context compacted');
 await expect(page.locator('.sa-chat-message--user')).toHaveCount(2);await expect(page.locator('.sa-chat-message--assistant')).toHaveCount(2);
 const compact=requests[2] as {model:string;stream:boolean;format?:string;messages:{role:string;content:string}[]};expect(compact.model).toBe('test-model');expect(compact.stream).toBe(false);expect(compact.format).toBeUndefined();
 expect(JSON.parse(compact.messages[1].content).earlierMessages).toEqual([{role:'user',content:'remember violet'},{role:'assistant',content:'Original answer.'}]);
 await chatCommand(page,'next');await expect(page.locator('.sa-chat-message--assistant')).toHaveCount(3);
 const next=requests[3].messages as {role:string;content:string}[];
 expect(next.some(message=>message.content.startsWith('Summary of earlier conversation'))).toBe(true);expect(next.some(message=>message.content==='remember violet')).toBe(false);expect(next.some(message=>message.content==='add detail')).toBe(true);
 await chatCommand(page,'/compact');await expect(page.locator('.sa-chat-command-notice')).toContainText('Context compacted');
 const repeated=requests[4].messages as {role:string;content:string}[];expect(JSON.parse(repeated[1].content).previousSummary).toContain('prefers violet');
 await chatCommand(page,'/new');await chatCommand(page,'//literal');await expect(page.locator('.sa-chat-message--assistant')).toHaveCount(1);
 const fresh=requests[5].messages as {role:string;content:string}[];expect(fresh.some(message=>message.content.startsWith('Summary of earlier conversation'))).toBe(false);expect(fresh.at(-1)?.content).toBe('/literal');expect(fresh.length).toBe(2);
});

for(const failure of ['http','length','cancel'])test(`compact ${failure} preserves original context`,async({page})=>{
 await install(page);const requests:Record<string,unknown>[]=[];
 await page.route('**/api/chat',async route=>{
  const body=route.request().postDataJSON();requests.push(body);
  if(body.messages[0]?.content.startsWith('Summarize the quoted conversation')){
   if(failure==='http')return route.fulfill({status:503,json:{error:'Summary unavailable'}});
   if(failure==='cancel')await new Promise(resolve=>setTimeout(resolve,1000));
   return route.fulfill({json:{message:{content:'Partial summary'},done:true,done_reason:failure==='length'?'length':'stop'}}).catch(()=>{});
  }
  return route.fulfill({json:{message:{content:'Original answer.'},done:true,done_reason:'stop'}});
 });
 await page.goto('/playground');await expect(page.getByRole('combobox',{name:'Model',exact:true})).toHaveValue('test-model');
 await chatCommand(page,'keep original context');await expect(page.locator('.sa-chat-message--assistant')).toHaveCount(1);
 await chatCommand(page,'/compact');
 if(failure==='cancel'){await expect(page.locator('.sa-chat-command-notice')).toContainText('Summarizing conversation');await chatCommand(page,'/stop');}
 await expect(page.locator('.sa-chat-command-notice')).toContainText('original context is unchanged');
 await chatCommand(page,'continue');await expect(page.locator('.sa-chat-message--assistant')).toHaveCount(2);
 const next=requests.at(-1)?.messages as {role:string;content:string}[];
 expect(next.some(message=>message.content==='keep original context')).toBe(true);expect(next.some(message=>message.content.startsWith('Summary of earlier conversation'))).toBe(false);
});

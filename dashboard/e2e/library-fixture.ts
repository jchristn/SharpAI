import type {Route} from '@playwright/test';
export function libraryFixture(){
 const base={Description:'A reusable starting point.',Scope:'System',TenantId:null,UserId:null,CreatedUtc:'2026-10-22T00:00:00Z',UpdatedUtc:'2026-10-22T00:00:00Z',CanEdit:true,CanDelete:true};
 const prompts=[{...base,Id:'system-starter-prompt',Kind:'SystemPrompt',Name:'Helpful assistant',Content:'You are a helpful assistant.',Configuration:null}];
 const configurations=[{...base,Id:'system-starter-configuration',Kind:'ModelConfiguration',Name:'Balanced',Content:null,Configuration:{Temperature:.6,MaxTokens:2048,Stop:[],StreamResponse:true,JsonOutput:false,DisplayThinking:false}}];
 return async(route:Route):Promise<boolean>=>{
  const request=route.request();const path=new URL(request.url()).pathname;
  if(path==='/v1.0/library-context'){await route.fulfill({json:{IsAdmin:true,IsTenantAdmin:false,TenantId:'default',UserTenantId:'default',UserId:'user-a'}});return true;}
  const match=path.match(/^\/v1\.0\/(system-prompts|model-configurations)(?:\/([^/]+))?(\/duplicate)?$/);if(!match)return false;
  const rows=match[1]==='system-prompts'?prompts:configurations;
  if(request.method()==='GET'){await route.fulfill({json:{Objects:rows,TotalRecords:rows.length,EndOfResults:true}});return true;}
  const index=rows.findIndex(row=>row.Id===match[2]);
  if(request.method()==='DELETE'){rows.splice(index,1);await route.fulfill({json:{Deleted:true}});return true;}
  const body=request.postDataJSON();const item={...base,...(index>=0?rows[index]:{}),...body,Id:request.method()==='PUT'?match[2]:`copy-${rows.length}`,CanEdit:true,CanDelete:true};
  if(request.method()==='PUT')rows[index]=item;else rows.push(item);
  await route.fulfill({json:item});return true;
 };
}

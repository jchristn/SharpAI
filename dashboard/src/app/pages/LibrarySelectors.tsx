import {useEffect,useRef,useState} from 'react';
import {useTranslation} from 'react-i18next';
import type {GenerationConfiguration,LibraryContext,LibraryItem,LibraryKind} from '../../api/library';
import {engineGeneration} from '../../api/library';
import {useApi} from '../ApiProvider';
import {Icon} from '../Icon';
import {LibraryEditor} from './LibraryEditor';

/** Select reusable instructions and generation parameters without mutating shared defaults. */
export function LibrarySelectors({busy,onPromptChange,onConfigurationChange,onReady}:{busy:boolean;onPromptChange:(text:string)=>void;onConfigurationChange:(value:GenerationConfiguration)=>void;onReady:(ready:boolean)=>void}):JSX.Element {
 const {t}=useTranslation();const api=useApi();
 const [state,setState]=useState<{owner:typeof api;context:LibraryContext;prompts:LibraryItem[];configurations:LibraryItem[];prompt:string;configuration:string}|null>(null);
 const [error,setError]=useState('');const [revision,setRevision]=useState(0);
 const [editor,setEditor]=useState<{kind:LibraryKind;source?:LibraryItem;mode:'create'|'edit'|'duplicate'}|null>(null);
 const pending=useRef<{kind:LibraryKind;id:string}|null>(null);
 useEffect(()=>{let active=true;
  async function all(kind:LibraryKind):Promise<LibraryItem[]>{const rows:LibraryItem[]=[];for(let page=1;;page++){const result=await api.libraryList(kind,{pageNumber:page,pageSize:1000});rows.push(...result.Objects);if(result.EndOfResults||!result.Objects.length)return rows;}}
  void Promise.resolve().then(()=>{if(!active)return null;onReady(false);setError('');setEditor(null);return Promise.all([api.libraryContext(),all('system-prompts'),all('model-configurations')]);}).then(result=>{
   if(!active||!result)return;const [context,prompts,configurations]=result;
   const remembered=(kind:string):string|null=>{try{return localStorage.getItem(`sharpai.library.${context.UserId??'open'}.${context.TenantId??''}.${kind}`)??null;}catch{return null;}};
   const choose=(kind:LibraryKind,rows:LibraryItem[],fallback:string)=>{const id=pending.current?.kind===kind?pending.current.id:remembered(kind);return id===''?'':rows.some(row=>row.Id===id)?id!:rows.some(row=>row.Id===fallback)?fallback:'';};
   const prompt=choose('system-prompts',prompts,'system-starter-prompt');const configuration=choose('model-configurations',configurations,'system-starter-configuration');
   setState({owner:api,context,prompts,configurations,prompt,configuration});
   onPromptChange(prompts.find(row=>row.Id===prompt)?.Content??'');onConfigurationChange(configurations.find(row=>row.Id===configuration)?.Configuration??engineGeneration());onReady(true);pending.current=null;
  }).catch(error=>{if(active){setState(null);setError(error instanceof Error?error.message:t('library.error'));}});
  return()=>{active=false;};
 },[api,onPromptChange,onConfigurationChange,onReady,revision,t]);
 const visible=state?.owner===api?state:null;
 const select=(kind:LibraryKind,id:string)=>{
  if(!visible)return;
  setState({...visible,[kind==='system-prompts'?'prompt':'configuration']:id});
  if(kind==='system-prompts')onPromptChange(visible.prompts.find(row=>row.Id===id)?.Content??'');
  else onConfigurationChange(visible.configurations.find(row=>row.Id===id)?.Configuration??engineGeneration());
  try{localStorage.setItem(`sharpai.library.${visible.context.UserId??'open'}.${visible.context.TenantId??''}.${kind}`,id);}catch{/* browser storage is optional */}
 };
 return <><div className="sa-library-selectors">{(['system-prompts','model-configurations'] as LibraryKind[]).map(kind=>{
  const rows=kind==='system-prompts'?visible?.prompts:visible?.configurations;const id=kind==='system-prompts'?visible?.prompt:visible?.configuration;
  const selected=rows?.find(row=>row.Id===id);const label=t(kind==='system-prompts'?'library.promptSelector':'library.configurationSelector');
  return <div className="sa-library-picker" key={kind}><label data-library-help className="sa-filterbar__field" title={t(kind==='system-prompts'?'library.promptSelectorHelp':'library.configurationSelectorHelp')}><span>{label}</span><select disabled={busy||!visible} value={id??''} onChange={event=>select(kind,event.target.value)}>
   <option value="">{t(kind==='system-prompts'?'library.noPrompt':'library.engineDefaults')}</option>
   {(['System','Tenant','User'] as const).map(scope=>{const items=rows?.filter(row=>row.Scope===scope)??[];return items.length?<optgroup key={scope} label={t(`library.${scope}`)}>{items.map(row=><option key={row.Id} value={row.Id}>{row.Name}</option>)}</optgroup>:null;})}
  </select></label><button className="sa-btn sa-btn--icon" type="button" disabled={busy||!visible} title={t('library.editSelectionHelp')} aria-label={t(kind==='system-prompts'?'library.editPrompt':'library.editConfiguration')} onClick={()=>setEditor({kind,source:selected,mode:!selected?'create':selected.CanEdit?'edit':'duplicate'})}><Icon name="edit"/></button></div>;
 })}</div>{error&&<div className="sa-library-error"><p className="sa-error" role="alert">{error}</p><button className="sa-btn sa-btn--icon" aria-label={t('common.refresh')} title={t('help.refresh')} onClick={()=>setRevision(value=>value+1)}><Icon name="refresh"/></button></div>}
 {visible&&editor&&<LibraryEditor {...editor} context={visible.context} onClose={()=>setEditor(null)} onSaved={item=>{setEditor(null);try{localStorage.setItem(`sharpai.library.${visible.context.UserId??'open'}.${visible.context.TenantId??''}.${editor.kind}`,item.Id);}catch{/* optional preference */}pending.current={kind:editor.kind,id:item.Id};setRevision(value=>value+1);}}/>}</>;
}

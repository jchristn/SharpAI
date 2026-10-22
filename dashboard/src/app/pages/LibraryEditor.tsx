import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Modal } from '../../components/ui';
import { defaultGeneration, generationFields } from '../../api/library';
import type { GenerationConfiguration,LibraryContext,LibraryItem,LibraryKind,LibraryScope } from '../../api/library';
import { useApi } from '../ApiProvider';
import { useAdministration } from '../AdministrationProvider';

/** Shared structured editor. Duplication always defaults to the caller's private scope when signed in. */
export function LibraryEditor({kind,context,source,mode,onClose,onSaved}:{kind:LibraryKind;context:LibraryContext;source?:LibraryItem;mode:'create'|'edit'|'duplicate';onClose:()=>void;onSaved:(item:LibraryItem)=>void}):JSX.Element {
 const {t}=useTranslation();const api=useApi();const {tenants}=useAdministration();
 const suffix=mode==='duplicate'?` (${t('library.copySuffix')})`:'';
 const [name,setName]=useState(source?source.Name.slice(0,256-suffix.length)+suffix:'');
 const [description,setDescription]=useState(source?.Description??'');const [content,setContent]=useState(source?.Content??'');
 const [scope,setScope]=useState<LibraryScope>(mode==='edit'&&source?source.Scope:context.UserId?'User':context.IsAdmin?'System':'Tenant');
 const [tenant,setTenant]=useState(source?.TenantId??context.TenantId??'');
 const [configuration,setConfiguration]=useState<GenerationConfiguration>(source?.Configuration?{...source.Configuration,Stop:[...source.Configuration.Stop]}:defaultGeneration());
 const [stops,setStops]=useState(configuration.Stop.join('\n'));const [saving,setSaving]=useState(false);const [error,setError]=useState('');
 const change=(key:keyof GenerationConfiguration,value:unknown)=>setConfiguration(current=>({...current,[key]:value}));
 const save=async()=>{
  if(saving)return;setSaving(true);setError('');
  try {
   const result=await api.librarySave(kind,{Name:name.trim(),Description:description,Scope:scope,TenantId:scope==='System'?null:scope==='User'?context.UserTenantId:tenant,
    Content:kind==='system-prompts'?content:null,Configuration:kind==='model-configurations'?{...configuration,Stop:stops===(source?.Configuration?.Stop??[]).join('\n')?[...(source?.Configuration?.Stop??[])]:stops.split('\n').filter(value=>value.length>0)}:null},source?.Id,mode==='duplicate');
   onSaved(result);
  }catch(error){setError(error instanceof Error?error.message:t('library.error'));}finally{setSaving(false);}
 };
 const hint=(key:string)=>t(`library.${key}`);
 return <Modal wide open title={t(`library.${mode}`)} onClose={()=>{if(!saving)onClose();}} footer={<><button type="button" className="sa-btn" disabled={saving} onClick={onClose}>{t('common.cancel')}</button><button form="sa-library-editor" type="submit" className="sa-btn sa-btn--primary" disabled={saving}>{t('common.save')}</button></>}>
  <form id="sa-library-editor" className="sa-library-form" onSubmit={event=>{event.preventDefault();void save();}}>
   {mode==='duplicate'&&<p className="sa-settings-note sa-wide">{hint('readOnlyHelp')}</p>}
   {error&&<p className="sa-error sa-wide" role="alert">{error}</p>}
   <label data-library-help className="sa-filterbar__field" title={hint('nameHelp')}><span>{t('admin.name')}</span><input required maxLength={256} value={name} onChange={event=>setName(event.target.value)} disabled={saving}/></label>
   <label data-library-help className="sa-filterbar__field" title={hint('scopeHelp')}><span>{hint('scope')}</span><select value={scope} onChange={event=>setScope(event.target.value as LibraryScope)} disabled={saving||mode==='edit'}>
    {context.UserId&&<option value="User">{hint('User')}</option>}{(context.IsAdmin||context.IsTenantAdmin)&&<option value="Tenant">{hint('Tenant')}</option>}{context.IsAdmin&&<option value="System">{hint('System')}</option>}
   </select></label>
   {scope==='Tenant'&&context.IsAdmin&&<label data-library-help className="sa-filterbar__field sa-wide" title={hint('scopeHelp')}><span>{t('admin.tenantScope')}</span><select required value={tenant} onChange={event=>setTenant(event.target.value)} disabled={saving||mode==='edit'}>{tenants.length?tenants.map(item=><option key={item.Guid} value={item.Guid}>{item.Name??item.Guid}</option>):<option value={context.TenantId??''}>{context.TenantId??'—'}</option>}</select></label>}
   <label data-library-help className="sa-filterbar__field sa-wide" title={hint('descriptionHelp')}><span>{hint('description')}</span><input maxLength={4096} value={description} onChange={event=>setDescription(event.target.value)} disabled={saving}/></label>
   {kind==='system-prompts'?<label data-library-help className="sa-filterbar__field sa-wide" title={hint('contentHelp')}><span>{t('chat.system')}</span><textarea aria-label={t('chat.system')} rows={12} maxLength={1048576} value={content} onChange={event=>setContent(event.target.value)} disabled={saving}/></label>:<>
    {generationFields.map(({field:key,key:label,min,max,step})=><label data-library-help key={key} className="sa-filterbar__field" title={t(`samplingHelp.${label}`)}><span>{t(`sampling.${label}`)}</span><input type="number" min={min} max={max} step={step} placeholder={t('chat.serverDefault')} value={configuration[key]??''} onChange={event=>change(key,event.target.value===''?null:Number(event.target.value))} disabled={saving}/></label>)}
    <label data-library-help className="sa-filterbar__field sa-wide" title={t('help.stop')}><span>{t('chat.stop')}</span><textarea aria-label={t('chat.stop')} rows={3} value={stops} onChange={event=>setStops(event.target.value)} disabled={saving}/></label>
    {(['StreamResponse','JsonOutput','DisplayThinking'] satisfies Array<'StreamResponse'|'JsonOutput'|'DisplayThinking'>).map((key,index)=><label data-library-help key={key} title={t(`help.${['stream','format','format'][index]}`)}><input type="checkbox" checked={configuration[key]} onChange={event=>change(key,event.target.checked)} disabled={saving}/> {t(['chat.streamToggle','chat.json','chat.thinking'][index])}</label>)}
   </>}
  </form>
 </Modal>;
}

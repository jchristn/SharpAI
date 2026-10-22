import { useCallback,useEffect,useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionMenu,ConfirmModal,CopyableId,DataTable } from '../../components/ui';
import type { Column } from '../../components/ui';
import type { LibraryContext,LibraryItem,LibraryKind } from '../../api/library';
import { formatDateTime } from '../../i18n/format';
import { Icon } from '../Icon';
import { useApi } from '../ApiProvider';
import { useAdministration } from '../AdministrationProvider';
import {LibraryPreview} from './LibraryPreview';
import { LibraryEditor } from './LibraryEditor';

/** Managed libraries show shared defaults alongside the current user's private records. */
export function LibraryView({kind}:{kind:LibraryKind}):JSX.Element {
 const {t,i18n}=useTranslation();const api=useApi();const {tenants}=useAdministration();
 const [preview,setPreview]=useState<LibraryItem|null>(null);
 const [owner,setOwner]=useState<{api:typeof api;kind:LibraryKind;tenant:string}|null>(null);
 const [context,setContext]=useState<LibraryContext|null>(null);const [tenant,setTenant]=useState('');const [rows,setRows]=useState<LibraryItem[]>([]);
 const [page,setPage]=useState(1);const [size,setSize]=useState(25);const [total,setTotal]=useState(0);const [loading,setLoading]=useState(false);const [error,setError]=useState<string|null>(null);const [revision,setRevision]=useState(0);
 const [editor,setEditor]=useState<{source?:LibraryItem;mode:'create'|'edit'|'duplicate'}|null>(null);const [deleting,setDeleting]=useState<LibraryItem|null>(null);
 const load=useCallback(async()=>{
  const ctx=await api.libraryContext();
  const selected=ctx.IsAdmin?tenant:ctx.TenantId;
  const response=await api.libraryList(kind,{pageNumber:page,pageSize:size,...(selected?{tenantId:selected}:{})});
  return {ctx,response};
 },[api,kind,tenant,page,size]);
 useEffect(()=>{let active=true;
  void Promise.resolve().then(()=>{if(!active)return null;setLoading(true);setError(null);setRows([]);setEditor(null);setDeleting(null);setPreview(null);return load();}).then(result=>{if(!result)return;const {ctx,response}=result;if(active){setContext(ctx);setOwner({api,kind,tenant});setRows(response.Objects);setTotal(Number(response.TotalRecords));}}).catch(error=>{if(active){setContext(null);setError(error instanceof Error?error.message:t('library.error'));}}).finally(()=>{if(active)setLoading(false);});
  return()=>{active=false;};
 },[api,kind,tenant,load,revision,t]);
 const visible=owner?.api===api&&owner.kind===kind&&owner.tenant===tenant;
 const columns:Column<LibraryItem>[]=[
  {key:'id',header:t('admin.id'),render:row=><CopyableId value={row.Id}/>},
  {key:'name',header:t('admin.name'),help:t('library.nameHelp'),render:row=><strong>{row.Name}</strong>},
  {key:'scope',header:t('library.scope'),help:t('library.scopeHelp'),render:row=><span className={`sa-scope sa-scope--${row.Scope.toLowerCase()}`}>{t(`library.${row.Scope}`)}</span>},
  {key:'description',header:t('library.description'),help:t('library.descriptionHelp'),render:row=>row.Description||'—'},
  {key:'tenantId',header:t('details.TenantId'),render:row=>row.TenantId?<CopyableId value={row.TenantId}/>:'—'},
  {key:'userId',header:t('details.UserId'),render:row=>row.UserId?<CopyableId value={row.UserId}/>:'—'},
  {key:'modified',header:t('library.updated'),render:row=>formatDateTime(row.UpdatedUtc,i18n.language)},
  {key:'actions',header:'',render:row=><ActionMenu items={[
   {label:t('requestHistory.details'),help:t('help.inspect'),onClick:()=>setPreview(row)},
   {label:t('library.edit'),disabled:!row.CanEdit,onClick:()=>setEditor({source:row,mode:'edit'})},
   {label:t('library.duplicate'),help:t('library.duplicateHelp'),disabled:!context,onClick:()=>setEditor({source:row,mode:'duplicate'})},
   {label:t('common.delete'),danger:true,disabled:!row.CanDelete,onClick:()=>setDeleting(row)},
  ]}/>},
 ];
 const remove=async()=>{if(!deleting)return;try{await api.libraryDelete(kind,deleting.Id);setDeleting(null);setRevision(value=>value+1);}catch(error){setError(error instanceof Error?error.message:t('library.error'));setDeleting(null);}};
 return <div className="sa-library-page">
  <div className="sa-page__toolbar"><div><h1 className="sa-page__title">{t(kind==='system-prompts'?'library.promptsTitle':'library.configurationsTitle')}</h1><p className="sa-heading-description">{t(kind==='system-prompts'?'library.promptsHelp':'library.configurationsHelp')}</p></div>
   <div className="sa-library-actions"><button className="sa-btn sa-btn--icon" title={t('help.refresh')} aria-label={t('common.refresh')} disabled={loading} onClick={()=>setRevision(value=>value+1)}><Icon name="refresh"/></button><button className="sa-btn sa-btn--primary" disabled={!visible||!context||loading} onClick={()=>setEditor({mode:'create'})}>{t('library.create')}</button></div>
  </div>
  {visible&&context?.IsAdmin&&<label data-library-help className="sa-filterbar__field sa-library-tenant" title={t('library.tenantFilterHelp')}><span>{t('admin.selectedTenant')}</span><select value={tenant||context.TenantId||''} onChange={event=>{setTenant(event.target.value);setPage(1);}}>{tenants.map(item=><option key={item.Guid} value={item.Guid}>{item.Name??item.Guid}</option>)}</select></label>}
  <DataTable columns={columns} rows={visible?rows:[]} onRowClick={setPreview} rowKey={row=>row.Id} loading={loading} error={error} emptyMessage={t('library.empty')} pagination={{pageNumber:page,pageSize:size,totalRecords:visible?total:0,onPageChange:setPage,onPageSizeChange:value=>{setSize(value);setPage(1);}}}/>
  {visible&&context&&editor&&<LibraryEditor kind={kind} context={context} {...editor} onClose={()=>setEditor(null)} onSaved={()=>{setEditor(null);setRevision(value=>value+1);}}/>}
  {visible&&preview&&<LibraryPreview item={preview} onEdit={()=>{setEditor({source:preview,mode:'edit'});setPreview(null);}} onClose={()=>setPreview(null)} onDuplicate={()=>{setEditor({source:preview,mode:'duplicate'});setPreview(null);}}/>}
  <ConfirmModal open={!!deleting} title={t('common.delete')} message={t('library.deleteConfirm',{name:deleting?.Name??''})} danger onCancel={()=>setDeleting(null)} onConfirm={()=>void remove()}/>
 </div>;
}

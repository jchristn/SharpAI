import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ConfirmModal, CopyButton } from '../components/ui';
import { ApiError } from '../api/ApiClient';
import { useApi } from './ApiProvider';
import { Icon } from './Icon';

/** Fetch secrets only after an explicit authorized reveal; never include them in list responses. */
export function CredentialTokenControl({tenant, id, canManage, protectedCredential=false}: {tenant:string;id:string;canManage:boolean;protectedCredential?:boolean}): JSX.Element {
 const api=useApi();const {t}=useTranslation();const [token,setToken]=useState<string|null>(null);const [busy,setBusy]=useState(false);const [error,setError]=useState<string|null>(null);const [confirm,setConfirm]=useState(false);const version=useRef({active:true});
 useEffect(()=>{const request=version.current;request.active=true;return()=>{request.active=false;};},[]);
 const load=async(rotate=false):Promise<void>=>{
  const revision=version.current;setBusy(true);setError(null);
  try{const result=await api.administration<{token:string}>(rotate?'POST':'GET',tenant,`credentials/${encodeURIComponent(id)}/token${rotate?'/rotate':''}`);if(revision.active)setToken(result.token);}
  catch(reason){if(revision.active)setError(reason instanceof ApiError && reason.status===409?t('admin.tokenUnavailable'):t('admin.tokenFailed'));}
  finally{if(revision.active)setBusy(false);}
 };
 return <div className="sa-credential-token" onClick={event=>event.stopPropagation()}>
  <div><code>{token??'••••••••'}</code>{canManage&&<>
   <button type="button" className="sa-btn sa-btn--ghost sa-btn--icon" disabled={busy} title={t(token?'admin.hideToken':'admin.revealToken')} aria-label={t(token?'admin.hideToken':'admin.revealToken')} onClick={()=>token?setToken(null):void load()}><Icon name={token?'eye-off':'eye'}/></button>
   {token&&<CopyButton value={token} label={t('admin.copyBearerToken')}/>}
   {!protectedCredential&&<button type="button" className="sa-btn sa-btn--ghost sa-btn--icon" disabled={busy} title={t('admin.regenerateToken')} aria-label={t('admin.regenerateToken')} onClick={()=>setConfirm(true)}><Icon name="refresh"/></button>}
  </>}</div>
  {error&&<small role="alert">{error}</small>}
  <ConfirmModal open={confirm} danger title={t('admin.regenerateToken')} message={t('admin.regenerateTokenConfirm')} confirmLabel={t('admin.regenerateToken')} onCancel={()=>setConfirm(false)} onConfirm={()=>{setConfirm(false);setToken(null);void load(true);}}/>
 </div>;
}

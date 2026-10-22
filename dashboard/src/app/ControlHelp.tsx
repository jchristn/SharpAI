import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import en from '../i18n/en.json';

interface HelpEntry {key:string;value:string;}
function entries(value:unknown,prefix=''):HelpEntry[] {
  if(typeof value==='string')return [{key:prefix,value}];
  if(!value || typeof value!=='object')return [];
  return Object.entries(value).flatMap(([key,item])=>entries(item,prefix?`${prefix}.${key}`:key));
}
const sources=entries(en).filter(({key})=>!key.startsWith('help.'));
/** Backfills meaningful localized help for dynamic forms, tables, modals and icon controls. */
export function ControlHelp():null {
  const {t,i18n}=useTranslation();
  useEffect(()=>{
    const labels=new Map<string,string>();
    for(const {key,value} of sources)labels.set(t(key),`${key} ${value}`);
    const identify=(element:HTMLElement):string=>{
      const label=element.closest('label');
      return element.getAttribute('aria-label') || (label?.querySelector('span')?.textContent || label?.textContent?.trim()) || (element.tagName==='LABEL'?element.querySelector('span')?.textContent:'') || element.textContent?.trim() || '';
    };
    const apply=():void=>{
      document.querySelectorAll<HTMLElement>('button,label,legend,dt,th,select,input,textarea,summary,a,meter,[role=menuitem],.sa-kpi__label,.sa-detail-tokens span,.sa-detail-timings span').forEach(element=>{
        if(element instanceof HTMLInputElement && element.type==='hidden')return;
        // Form authors own explicit help. Do not overwrite it with a label heuristic.
        const authored=element.closest<HTMLElement>('label[data-library-help]');
        if(authored?.title){element.title=authored.title;delete element.dataset.autoHelp;return;}
        const setting=element.closest<HTMLElement>('label[data-setting-path]');
        if(setting?.title){element.title=setting.title;delete element.dataset.autoHelp;return;}
        if(element.title && !element.dataset.autoHelp && element.title!==identify(element) && !element.classList.contains('sa-copy-button'))return;
        const label=identify(element);
        const semantic=(labels.get(label)||label)+' '+(element.getAttribute('data-help')||'');
        let kind='text';
        if(element.tagName==='METER')kind='timing';
        else if(element.tagName==='SUMMARY' && element.closest('.sa-chat-settings'))kind='sampling';
        else if(element.tagName==='LEGEND' && element.closest('fieldset[data-setting-path]')){element.title=element.closest<HTMLElement>('fieldset[data-setting-path]')!.title;return;}
        else if(element.tagName==='DT' && !element.closest('.sa-payload-fields'))kind='readonly';
        else if(element.matches('.sa-kpi__label,.sa-detail-tokens span,.sa-detail-timings span'))kind='readonly';
        else if(/stopGenerating|stop generating/i.test(semantic))kind='stopGeneration';
        else if(element.tagName==='TEXTAREA' && window.location.pathname==='/api-explorer')kind='requestBody';
        else if(element.tagName==='TH') {
          const key=element.dataset.columnKey || '';
          const history=window.location.pathname.includes('history');
          if(/created|modified|expires|duration|time/i.test(key))kind='timing';
          else if(key==='method')kind='httpMethod';
          else if(history && key==='status')kind='httpStatus';
          else if(history && key==='path')kind='httpPath';
          else if(history && key==='ip')kind='sourceIP';
          else if(/guid|^id$/i.test(key))kind='id';
          else if(key==='actions')kind='inspect';
          else kind='column';
        }
        else if(element.closest('.sa-http-headers'))kind='headers';
        else if(element.closest('.sa-payload-fields'))kind='body';
        else if(element.closest('.sa-pagination'))kind='pagination';
        else if(element.closest('.sa-filterbar'))kind='filter';
        else if(element.closest('[data-help=embeddingText]'))kind='embeddingText';
        else if(element.matches('a[href],[role=tab]'))kind='navigate';
        else if(element.closest('.sa-settings-fields') && element.tagName==='BUTTON')kind=element.dataset.help==='delete'?'listRemove':element.dataset.help==='create'?'listAdd':'save';
        else if(element.tagName==='LABEL' && element.querySelector('textarea') && window.location.pathname==='/api-explorer')kind='requestBody';
        else if(window.location.pathname==='/api-explorer' && /apiExplorer\.(send|sending)/.test(semantic))kind='save';
        else if(/chat\.clear\b/.test(semantic))kind='clearConversation';
        else if(/models\.unload\b/.test(semantic))kind='unload';
        else if(/playground\.chat\b/.test(semantic))kind='chatMode';
        else if(/playground\.embeddings\b/.test(semantic))kind='embeddingMode';
        else if(/admin\.platformAdmin\b/.test(semantic))kind='platformAdmin';
        else if(/admin\.tenantAdmin\b/.test(semantic))kind='tenantAdmin';
        else if(element.closest('.sa-admin-form') && /login\.email\b/.test(semantic))kind='text';
        else if(/refresh|reload/i.test(semantic))kind='refresh';
        else if(/pull|download/i.test(semantic))kind='download';
        else if(/copy|copied/i.test(semantic))kind='copy';
        else if(/delete|remove|revoke/i.test(semantic))kind='delete';
        else if(/save|submit|apply/i.test(semantic))kind='save';
        else if(/cancel|close/i.test(semantic))kind='cancel';
        else if(/\b(create|add|new conversation)\b/i.test(semantic))kind='create';
        else if(/sign.?in|login\.(email|password|tenant)/i.test(semantic))kind='signin';
        else if(/sign.?out/i.test(semantic))kind='signout';
        else if(/password|secret|api.?key|signing.?key/i.test(semantic) || element.getAttribute('type')==='password')kind='secret';
        else if(/theme|dark|light|language/i.test(semantic))kind='preferences';
        else if(/streamToggle/i.test(semantic))kind='stream';
        else if(/system prompt|chat.system/i.test(semantic))kind='system';
        else if(/stop sequences|chat.stop/i.test(semantic))kind='stop';
        else if(/json|chat.thinking/i.test(semantic))kind='format';
        else if(/sampling\./i.test(semantic))kind='sampling';
        else if(/prompt|playground.input|send|embed/i.test(semantic))kind='prompt';
        else if(/filter|range|last hour|last day|last week|last month|clear/i.test(semantic))kind='filter';
        else if(/tenant|admin.owner|principal|admin.user/i.test(semantic))kind='tenant';
        else if(/role|permission|scope|inherit|admin.active|admin.status|admin.account/i.test(semantic))kind='security';
        else if(/token|cached/i.test(semantic))kind='tokens';
        else if(/duration|time|ttft|latency|queue|generation|speed|created|expires|utc/i.test(semantic))kind='timing';
        else if(/bytes|size|memory|budget/i.test(semantic))kind='size';
        else if(/headers/i.test(semantic))kind='headers';
        else if(/body|payload/i.test(semantic))kind='body';
        else if(/url|hostname|endpoint|port|path|directory/i.test(semantic))kind='endpoint';
        else if(/model/i.test(semantic))kind='model';
        else if(/details|inspect|view|effective|telemetry|actions|more/i.test(semantic))kind='inspect';
        else if(/edit|reset/i.test(semantic))kind='edit';
        else if(/\bid\b|guid|identifier/i.test(semantic))kind='id';
        else if(element.tagName==='A')kind='navigate';
        else if(element.tagName==='TH' || element.tagName==='DT')kind='column';
        else if(element.tagName==='SELECT' || element.querySelector('select'))kind='select';
        else if(element.getAttribute('type')==='number' || element.querySelector('input[type=number]'))kind='number';
        else if(element.getAttribute('type')==='checkbox' || element.querySelector('input[type=checkbox]'))kind='boolean';
        else if(element.tagName==='BUTTON')kind='inspect';
        if(element.tagName==='LABEL') {
          const input=element.querySelector<HTMLElement>('input,select,textarea');
          if(input?.title && !input.dataset.autoHelp){element.title=input.title;return;}
        }
        const samplingKey=semantic.match(/sampling\.([a-z_]+)/)?.[1];
        const title=samplingKey ? t(`samplingHelp.${samplingKey}`) : t(`help.${kind}`);
        const bounds=element instanceof HTMLInputElement && element.type==='number' ? t('help.bounds',{min:element.min||'−∞',max:element.max||'∞',step:element.step||1}) : '';
        element.title=title+(bounds?` ${bounds}`:'');
        element.dataset.autoHelp=kind;
      });
    };
    apply();
    const observer=new MutationObserver(apply);
    observer.observe(document.body,{childList:true,subtree:true});
    return ()=>observer.disconnect();
  },[t,i18n.language]);
  return null;
}

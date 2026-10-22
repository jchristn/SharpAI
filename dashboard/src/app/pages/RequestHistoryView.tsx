import { NavLink, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { RequestHistoryPanel } from './RequestHistoryPanel';

type HistorySection='api'|'inference'|'embeddings';
interface HistoryTab {id:HistorySection;label:string;help:string;category?:string;}
const tabs:HistoryTab[]=[
 {id:'api',label:'history.api',help:'history.apiHelp'},
 {id:'inference',label:'history.inference',help:'history.inferenceHelp',category:'generation'},
 {id:'embeddings',label:'history.embeddings',help:'history.embeddingsHelp',category:'embeddings'},
];

/** One history workspace with deep-linkable tabs and separate server-side generation/embedding filters. */
export function RequestHistoryView():JSX.Element {
 const {t}=useTranslation();const navigate=useNavigate();const {section='api'}=useParams<{section:string}>();
 const selected=tabs.find(tab=>tab.id===section)??tabs[0];
 const endpointOptions=selected.id==='inference'?[
  {label:t('inferenceHistory.endpointChat'),value:'chat'},
  {label:t('inferenceHistory.endpointGenerate'),value:'/generate'},
  {label:t('history.completions'),value:'/v1/completions'},
 ]:undefined;
 return <div className="sa-history-workspace"><nav className="sa-access-tabs sa-history-tabs" role="tablist" aria-label={t('history.tabs')} onKeyDown={event=>{
  const current=tabs.findIndex(tab=>tab.id===selected.id);let next:number;
  if(event.key==='Home')next=0;else if(event.key==='End')next=tabs.length-1;
  else if(event.key==='ArrowRight'||event.key==='ArrowLeft'){
   const direction=(event.key==='ArrowRight'?1:-1)*(document.documentElement.dir==='rtl'?-1:1);
   next=(current+direction+tabs.length)%tabs.length;
  }else return;
  event.preventDefault();navigate(`/request-history/${tabs[next].id}`);document.getElementById(`history-tab-${tabs[next].id}`)?.focus();
 }}>
  {tabs.map(tab=><NavLink key={tab.id} id={`history-tab-${tab.id}`} role="tab" aria-selected={selected.id===tab.id} aria-controls="history-panel" tabIndex={selected.id===tab.id?0:-1} title={t(tab.help)} to={`/request-history/${tab.id}`} className={`sa-access-tab ${selected.id===tab.id?'sa-access-tab--active':''}`}>{t(tab.label)}</NavLink>)}
 </nav><section id="history-panel" role="tabpanel" aria-labelledby={`history-tab-${selected.id}`}>
  <RequestHistoryPanel key={selected.id} titleKey="requestHistory.title" category={selected.category} endpointOptions={endpointOptions}/>
 </section></div>;
}

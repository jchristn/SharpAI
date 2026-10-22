import { useTranslation } from 'react-i18next';
import { CopyButton, CopyableId } from '../../components/ui';
import { formatDateTime, formatDurationMs, formatNumber } from '../../i18n/format';

/** Structured payload inspection keeps message content readable and arbitrary properties discoverable. */
function Payload({value,raw=false}:{value:unknown;raw?:boolean}):JSX.Element {
  const {t}=useTranslation();
  if(value===null || value===undefined || value==='') return <p className="sa-page-description">{t('details.notRetained')}</p>;
  if(raw)return <pre className="sa-payload-text">{String(value)}</pre>;
  if(typeof value==='string') {
    let parsed:unknown;
    let valid=false;
    try {parsed=JSON.parse(value);valid=true;} catch { /* Plain text and truncated payloads remain readable. */ }
    if(valid)return <Payload value={parsed}/>;
    const lines=value.split('\n').map(line=>line.replace(/^data:\s*/, '').trim()).filter(line=>line && line!=='[DONE]');
    if(lines.length>1) {
      const frames:Record<string,unknown>[]=[];
      for(const line of lines) {try {const frame=JSON.parse(line);if(frame && typeof frame==='object')frames.push(frame);} catch { /* A truncated final frame stays visible below. */ }}
      if(frames.length) {
        const output=frames.map(frame=>{
          const message=frame.message as {content?:string}|undefined;
          const choices=frame.choices as Array<{delta?:{content?:string};text?:string}>|undefined;
          return message?.content ?? frame.response as string ?? choices?.map(choice=>choice.delta?.content??choice.text??'').join('') ?? '';
        }).join('');
        return <div>{output && <pre className="sa-payload-text">{output}</pre>}<details><summary>{t('details.frames',{count:frames.length})}</summary><Payload value={frames}/>{frames.length<lines.length && <pre className="sa-payload-text">{lines.slice(frames.length).join('\n')}</pre>}</details></div>;
      }
    }
    return <pre className="sa-payload-text">{value}</pre>;
  }
  if(Array.isArray(value))return <ol className="sa-payload-list">{value.map((item,index)=><li key={index}><Payload value={item}/></li>)}</ol>;
  if(typeof value==='object')return <dl className="sa-payload-fields">{Object.entries(value).map(([key,item])=><div key={key}><dt>{t(`fields.${key}`,{defaultValue:key})}</dt><dd><Payload value={item}/></dd></div>)}</dl>;
  return <span>{typeof value==='boolean'?t(value?'admin.yes':'admin.no'):String(value)}</span>;
}

/** Complete captured HTTP exchange with persisted inference phases, token usage and payloads. */
export function RequestDetails({row,inference}:{row:Record<string,unknown>;inference:boolean}):JSX.Element {
  const {t,i18n}=useTranslation();
  const get=(key:string):unknown=>row[key]??row[key[0].toLowerCase()+key.slice(1)];
  const duration=Number(get('DurationMs')??0);
  const timing=[['TtftMs','chat.ttft'],['StreamDurationMs','chat.streaming'],['QueueMs','details.queue'],['LoadMs','details.load'],['PromptMs','details.prompt'],['GenerationMs','details.generation'],['EngineTotalMs','details.engineTotal']] as const;
  const counts=[['InputTokens','chat.inputTokens'],['OutputTokens','chat.outputTokens'],['CachedTokens','details.cached']] as const;
  return <div className="sa-request-detail">
    {get('DetailUnavailable')===true && <p role="alert">{t('details.loadFailed')}</p>}
    <header className="sa-request-detail-summary"><strong>{String(get('Method')??'')} {String(get('Path')??'')}</strong><span>{t('requestHistory.colStatus')}: {String(get('StatusCode')??'')}</span><span>{formatDurationMs(duration,i18n.language)}</span><CopyableId value={String(get('Id')??'')}/></header>
    <dl className="sa-detail-metadata">{['CreatedUtc','CompletedUtc','Url','SourceIp','TenantId','UserId','PrincipalName','ModelName','IsStreaming','TraceId','SpanId','Cancelled','PhaseTimingsApproximate'].map(key=><div key={key}><dt>{t(`details.${key}`)}</dt><dd>{get(key)==null?t('details.unavailable'):key.endsWith('Utc')?formatDateTime(String(get(key)),i18n.language,'UTC'):typeof get(key)==='boolean'?t(get(key)?'admin.yes':'admin.no'):String(get(key))}</dd></div>)}</dl>
    {(inference || get('InputTokens')!=null) && <section><h2>{t('chat.telemetry')}</h2><div className="sa-detail-timings">{timing.map(([key,label])=><div key={key}><span>{t(label)}</span><strong>{get(key)==null?t('details.unavailable'):formatDurationMs(Number(get(key)),i18n.language)}</strong><meter aria-label={t(label)} min={0} max={Math.max(duration,...timing.map(([name])=>Number(get(name)??0)),1)} value={Number(get(key)??0)}/></div>)}</div><div className="sa-detail-tokens">{counts.map(([key,label])=><div key={key}><span>{t(label)}</span><strong>{get(key)==null?t('details.unavailable'):formatNumber(Number(get(key)),i18n.language)}</strong></div>)}<div><span>{t('chat.totalTokens')}</span><strong>{get('InputTokens')==null || get('OutputTokens')==null?t('details.unavailable'):formatNumber(Number(get('InputTokens'))+Number(get('OutputTokens')),i18n.language)}</strong></div></div><p className="sa-page-description">{t('details.measurementNote')}</p></section>}
    <div className="sa-detail-exchange">{(['Request','Response'] as const).map(side=><section key={side}><h2>{t(`details.${side}`)}</h2><header className="sa-payload-heading"><h3>{t('details.headers')}</h3><CopyButton value={JSON.stringify(get(`${side}Headers`)??{},null,2)}/></header><div className="sa-http-headers"><Payload value={get(`${side}Headers`)}/></div><header className="sa-payload-heading"><h3>{t('details.body')}</h3>{get(`${side}Body`)!=null && <CopyButton value={String(get(`${side}Body`))}/>}</header><p className="sa-page-description">{formatNumber(Number(get(`${side}BodyBytes`)??0),i18n.language)} {t('details.bytes')} · {t(get(`${side}BodyTruncated`)?'details.truncated':'details.complete')}</p><Payload value={get(`${side}Body`)} raw={side==='Request'}/></section>)}</div>
  </div>;
}

import { Icon } from '../Icon';
import { lazy, Suspense, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { InfoPopover } from '../../components/ui/InfoPopover';
import { ApiError } from '../../api/ApiClient';
import { CopyButton } from '../../components/ui';
import { useApi } from '../ApiProvider';

const ChatMarkdown=lazy(()=>import('./ChatMarkdown'));

type Mode = 'chat' | 'embeddings';
type Message = {role:'user'|'assistant';content:string; metrics?: Metrics};
type Metrics = {ttft?:number;elapsed:number;input?:number;output?:number;generation?:number};
const numericOptions = [
  ['temperature',0,2,0.1],['num_predict',1,32768,1],['top_p',0,1,0.05],['top_k',0,100,1],['min_p',0,1,0.05],
  ['repeat_penalty',0,2,0.05],['repeat_last_n',0,32768,1],['presence_penalty',-2,2,0.1],['frequency_penalty',-2,2,0.1],['seed',0,2147483647,1],
] as const;

/** Conversation playground with per-turn timing, usage, and request-scoped sampling controls. */
export function PlaygroundView(): JSX.Element {
  const { t, i18n } = useTranslation();
  const api = useApi();
  const controller = useRef<AbortController | null>(null);
  const log = useRef<HTMLDivElement>(null);
  const [available, setAvailable] = useState<string[]>([]);
  const [model, setModel] = useState('');
  const [modelError, setModelError] = useState(false);
  const [mode, setMode] = useState<Mode>('chat');
  const [prompt, setPrompt] = useState('');
  const [messages, setMessages] = useState<Message[]>([]);
  const [vector, setVector] = useState<number[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [system, setSystem] = useState('');
  const [options, setOptions] = useState<Record<string,string>>({temperature:'0.6',num_predict:'128'});
  const [stops, setStops] = useState('');
  const [json, setJson] = useState(false);
  const [thinking, setThinking] = useState(false);
  const [streaming,setStreaming]=useState(true);
  const [quip,setQuip]=useState(0);
  useEffect(()=>{if(!busy)return;const interval=window.setInterval(()=>setQuip(current=>(current+1)%6),4000);return ()=>window.clearInterval(interval);},[busy]);
  useEffect(() => {
    void api.listModels().then((value) => {
      const names = ((value as {models?:{name:string}[]}).models ?? []).map(item=>item.name);
      setAvailable(names); setModel(current => names.includes(current) ? current : names[0] ?? '');
    }).catch(()=>setModelError(true));
    return () => controller.current?.abort();
  }, [api]);
  useEffect(()=>{const element=log.current;if(element) element.scrollTop=element.scrollHeight;},[messages]);
  const number = (n:number) => new Intl.NumberFormat(i18n.language,{maximumFractionDigits:2}).format(n);
  const send = async (): Promise<void> => {
    if (!model || !prompt.trim() || busy) return;
    controller.current = new AbortController();
    setBusy(true); setQuip(current=>(current+1+Math.floor(Math.random()*5))%6); setError(null); setVector(null);
    const form = document.getElementById('sa-chat-form') as HTMLFormElement | null;
    if (form && !form.reportValidity()) {setBusy(false);return;}
    const input = prompt.trim();
    const conversation = [...messages.filter(m=>m.content),{role:'user' as const,content:input}];
    const started = performance.now();
    let first: number | undefined;
    let response = '';
    let stats: Metrics = {elapsed:0};
    let done=false;
    const timer=mode==='chat'?window.setInterval(()=>setMessages([...conversation,{role:'assistant',content:response,metrics:{...stats,elapsed:performance.now()-started}}]),200):undefined;
    if(mode==='chat') {setMessages([...conversation,{role:'assistant',content:'',metrics:stats}]);setPrompt('');}
    try {
      if (mode === 'chat') {
        const submitted = Object.fromEntries(Object.entries(options).filter(([,v])=>v!=='').map(([k,v])=>[k,Number(v)]));
        const request={model,messages:[...(system.trim() ? [{role:'system',content:system}] : []),...conversation.map(({role,content})=>({role,content}))],
          options:{...submitted,display_thinking:thinking,...(stops.trim()?{stop:stops.split('\n').filter(Boolean)}:{})},
          ...(json?{format:'json'}:{})};
        const receive = (event:unknown):void => {
          const record = event as Record<string,unknown>;
          if(record.done===true) done=true;
          if(record.error) throw new Error(String(record.error));
          const delta = (record.message as {content?:string}|undefined)?.content ?? '';
          if(delta && first===undefined) first=performance.now()-started;
          // Suppress only presentation whitespace before the first visible character; retain internal newlines.
          response=(response+delta).replace(/^\s*\n+/, '');
          stats={elapsed:performance.now()-started,ttft:typeof record.first_token_duration==='number' && record.first_token_duration>0 ? record.first_token_duration/1e6 : streaming?first:undefined,
            input:typeof record.prompt_eval_count==='number'?record.prompt_eval_count:stats.input,
            output:typeof record.eval_count==='number'?record.eval_count:stats.output,
            generation:typeof record.eval_duration==='number'?record.eval_duration/1e9:stats.generation};
          setMessages([...conversation,{role:'assistant',content:response,metrics:{...stats}}]);
        };
        if(streaming)await api.chatStream(request,receive,controller.current.signal);
        else receive(await api.chat({...request,stream:false},controller.current.signal));
        if(!done) throw new Error(t('chat.incomplete'));
      } else {
        const result=await api.embeddings({model,input},controller.current.signal);
        const record=result as {embedding?:number[];embeddings?:number[][]};
        setVector(record.embedding ?? record.embeddings?.[0] ?? null);
      }
    } catch (err) {
      if (!(err instanceof DOMException && err.name==='AbortError')) setError(err instanceof ApiError ? `${err.status}: ${err.message}` : err instanceof Error ? err.message : t('playground.failed'));
    } finally {
      window.clearInterval(timer);
      if(mode==='chat') setMessages([...conversation,{role:'assistant',content:response,metrics:{...stats,elapsed:performance.now()-started}}]);
      setBusy(false);
    }
  };
  return <div className="sa-playground-page">
    <div className="sa-page__toolbar"><div><h1 className="sa-page__title">{t('playground.title')}</h1><p className="sa-heading-description">{t('text.tryAPromptStreamAResponseOr')}</p></div>
      <div className="sa-chat-tabs">{(['chat','embeddings'] as Mode[]).map(item=><button key={item} className={`sa-btn ${mode===item?'sa-btn--primary':''}`} disabled={busy} aria-pressed={mode===item} onClick={()=>setMode(item)}>{t(`playground.${item}`)}</button>)}<button className="sa-btn" disabled={busy || !messages.length} onClick={()=>{setMessages([]);setVector(null);setError(null);}}>{t('chat.clear')}</button></div>
    </div>
    <div className="sa-chat-layout">
      <details className="sa-chat-settings"><summary>{t('chat.parameters')}</summary><p>{t('chat.defaults')}</p><fieldset disabled={busy || mode!=='chat'}><label className="sa-filterbar__field"><span>{t('chat.system')}</span><textarea value={system} onChange={e=>setSystem(e.target.value)}/></label><div className="sa-chat-options">{numericOptions.map(([key,min,max,step])=><label className="sa-filterbar__field" key={key}><span>{t(`sampling.${key}`)}</span><input form="sa-chat-form" type="number" min={min} max={max} step={step} value={options[key]??''} placeholder={t('chat.serverDefault')} onChange={e=>setOptions({...options,[key]:e.target.value})}/></label>)}</div><label className="sa-filterbar__field"><span>{t('chat.stop')}</span><textarea value={stops} onChange={e=>setStops(e.target.value)}/></label><label><input type="checkbox" checked={streaming} onChange={event=>setStreaming(event.target.checked)}/> {t('chat.streamToggle')}</label><label><input type="checkbox" checked={json} onChange={e=>setJson(e.target.checked)}/> {t('chat.json')}</label><label><input type="checkbox" checked={thinking} onChange={e=>setThinking(e.target.checked)}/> {t('chat.thinking')}</label></fieldset></details>
      <section className="sa-chat-panel">
        <header className="sa-chat-header"><label className="sa-filterbar__field"><span className="sa-filterbar__label">{t('playground.model')}</span><select aria-label={t('playground.model')} disabled={busy} value={model} onChange={e=>{setModel(e.target.value);setMessages([]);}}><option value="" disabled>{t('chat.selectModel')}</option>{available.map(name=><option key={name} value={name}>{name}</option>)}</select></label></header>
        {modelError && <p role="alert" className="sa-error">{t('models.loadError')}</p>}
        {!modelError && !available.length && <p className="sa-settings-note">{t('chat.noModels')}</p>}
        <div ref={log} tabIndex={0} className="sa-chat-messages" role="log" aria-label={t('chat.conversation')}>
          {mode==='chat' && !messages.length && <div className="sa-chat-empty"><img src="/images/sharp-logo.png" alt="SharpAI"/><h2>{t('chat.start')}</h2><p>{t('chat.startHelp')}</p></div>}
          {mode==='chat' && messages.map((message,index)=><article key={index} className={`sa-chat-message sa-chat-message--${message.role}`}><header><strong>{t(message.role==='user'?'chat.you':'playground.response')}</strong>{message.content && <CopyButton value={message.content}/>}</header><div className="sa-chat-content">{message.role==='assistant' && message.content ? <Suspense fallback={message.content}><ChatMarkdown text={message.content}/></Suspense> : message.content || (busy?t(`quips.${quip}`):'—')}</div>
            {message.metrics && message.content && <InfoPopover label={t('chat.telemetry')}><dl className="sa-chat-metrics">{[
              ['chat.ttft',message.metrics.ttft===undefined?'—':`${number(message.metrics.ttft/1000)} s`],
              ['chat.totalTime',`${number(message.metrics.elapsed/1000)} s`],
              ['chat.streaming',message.metrics.ttft===undefined?'—':`${number(Math.max(0,message.metrics.elapsed-message.metrics.ttft)/1000)} s`],
              ['chat.inputTokens',message.metrics.input===undefined?'—':number(message.metrics.input)],
              ['chat.outputTokens',message.metrics.output===undefined?'—':number(message.metrics.output)],
              ['chat.totalTokens',message.metrics.input===undefined || message.metrics.output===undefined?'—':number(message.metrics.input+message.metrics.output)],
              ['chat.speed',message.metrics.generation && message.metrics.output!==undefined?number(message.metrics.output/message.metrics.generation):'—'],
            ].map(([label,value])=><div key={label}><dt>{t(label)}</dt><dd>{value}</dd></div>)}</dl></InfoPopover>}
          </article>)}
          {mode==='embeddings' && vector && <article className="sa-chat-message"><strong>{t('playground.dimensions')}: {number(vector.length)} — {t('playground.preview')}</strong><pre>{`[${vector.slice(0,12).map(n=>n.toFixed(4)).join(', ')}${vector.length>12?', …':''}]`}</pre></article>}
        </div>
        {error && <p role="alert" className="sa-error">{error}</p>}
        <form id="sa-chat-form" onInvalidCapture={()=>{const parameters=document.querySelector<HTMLDetailsElement>('.sa-chat-settings');if(parameters)parameters.open=true;}} className="sa-chat-composer" onSubmit={e=>{e.preventDefault();void send();}}><label className="sa-filterbar__field"><textarea rows={1} aria-label={t(mode==='chat'?'playground.prompt':'playground.input')} disabled={busy} value={prompt} onChange={e=>setPrompt(e.target.value)} placeholder={t('chat.promptHelp')} onKeyDown={e=>{if(!e.shiftKey && e.key==='Enter'){e.preventDefault();void send();}}}/></label>{busy?<button type="button" className="sa-btn sa-btn--icon sa-chat-stop" aria-label={t('text.stopGenerating')} onClick={()=>controller.current?.abort()}><Icon name="stop"/></button>:<button className="sa-btn sa-btn--primary" disabled={!model || !prompt.trim()}>{t(mode==='chat'?'playground.send':'playground.embed')}</button>}<small className="sa-chat-warning">{t('chat.warning')}</small></form>
      </section>

    </div>
  </div>;
}

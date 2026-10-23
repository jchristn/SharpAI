import {chatCommands,parseChatCommand,compactInstruction,compactMaxTokens,summaryPrefix,type ChatCommand} from '../../api/chatCommands';
import { LibrarySelectors } from './LibrarySelectors';
import { defaultGeneration, generationRequest } from '../../api/library';
import { Icon } from '../Icon';
import { lazy, Suspense, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { InfoPopover } from '../../components/ui/InfoPopover';
import { ApiError } from '../../api/ApiClient';
import { CopyButton } from '../../components/ui';
import { useApi } from '../ApiProvider';

const ChatMarkdown=lazy(()=>import('./ChatMarkdown'));

type Mode = 'chat' | 'embeddings';
type Message = {role:'user'|'assistant';content:string; metrics?: Metrics};
type Metrics = {ttft?:number;elapsed:number;input?:number;output?:number;generation?:number};
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
  const [context,setContext]=useState<Message[]>([]);
  const [summary,setSummary]=useState('');
  const [notice,setNotice]=useState('');
  const [vector, setVector] = useState<number[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [system, setSystem] = useState('');
  const [configuration,setConfiguration]=useState(defaultGeneration);
  const [libraryReady,setLibraryReady]=useState(false);
  const streaming=configuration.StreamResponse;
  const [quip,setQuip]=useState(0);
  const ownerRef=useRef(api);useLayoutEffect(()=>{ownerRef.current=api;},[api]);
  const [owner,setOwner]=useState(api);
  if(owner!==api){setOwner(api);setMessages([]);setContext([]);setSummary('');setNotice('');setSystem('');setConfiguration(defaultGeneration());setLibraryReady(false);setBusy(false);setPrompt('');setVector(null);setError(null);}
  useEffect(()=>{if(!busy)return;const interval=window.setInterval(()=>setQuip(current=>(current+1)%6),4000);return ()=>window.clearInterval(interval);},[busy]);
  useEffect(() => {
    let active=true;
    setModelError(false);
    void api.listModels().then((value) => {
      if(!active)return;
      const models = ((value as {models?:{name?:string;model?:string;capabilities?:{completions?:boolean}}[]}).models ?? [])
        .map(item=>({...item,name:item.name || item.model || ''})).filter(item=>item.name.trim());
      const preferred=models.find(item=>item.capabilities?.completions===true) ?? models[0];
      const names=models.map(item=>item.name);
      setAvailable(names); setModel(preferred?.name ?? '');
    }).catch(()=>{if(active){setModelError(true);setAvailable([]);setModel('');}});
    return () => {active=false;controller.current?.abort();};
  }, [api]);
  useEffect(()=>{
    const element=log.current;if(!element)return;
    const follow=():void=>{element.scrollTop=element.scrollHeight;};
    follow();
    // Lazy Markdown and wrapped content can change height after the message update commits.
    const observer=new ResizeObserver(follow);
    observer.observe(element);
    if(element.lastElementChild)observer.observe(element.lastElementChild);
    return ()=>observer.disconnect();
  },[messages,notice]);
  const number = (n:number) => new Intl.NumberFormat(i18n.language,{maximumFractionDigits:2}).format(n);
  const clearConversation=():void=>{setMessages([]);setContext([]);setSummary('');setNotice('');setVector(null);setError(null);};
  const runCommand=async(command:ChatCommand):Promise<void>=>{
    if(busy && command.id!=='stop'){setNotice(t('chatCommands.busy'));return;}
    setPrompt('');
    if(command.arguments && command.id!=='model'){setNotice(t('chatCommands.unknown'));return;}
    if(command.id==='help'){setNotice(chatCommands.map(item=>`${item.usage} — ${t(item.description)}`).join('\n')+'\n'+t('chatCommands.literal'));return;}
    if(command.id==='clear'){clearConversation();setNotice(t('chatCommands.cleared'));return;}
    if(command.id==='stop'){if(busy)controller.current?.abort();else setNotice(t('chatCommands.idle'));return;}
    if(command.id==='status'){setNotice(`${t('playground.model')}: ${model || '—'} · MaxTokens: ${configuration.MaxTokens ?? '—'}\n${t('chatCommands.context')}: ${context.length}${summary?' + '+t('chatCommands.summary'):''}`);return;}
    if(command.id==='model'){
      if(!command.arguments){setNotice('/model [name]\n'+available.join('\n'));return;}
      const selected=available.find(name=>name.toLowerCase()===command.arguments.toLowerCase());
      if(selected){setModel(selected);setNotice(`${t('playground.model')}: ${selected}`);}else setNotice(t('chatCommands.modelUnknown'));
      return;
    }
    if(command.id!=='compact'){setNotice(t('chatCommands.unknown'));return;}
    if(!context.length){setNotice(t('chatCommands.noConversation'));return;}
    if(!model){setNotice(t('chat.selectModel'));return;}
    const requestClient=api;
    const keep=context.length>2?2:0;
    const earlier=context.slice(0,context.length-keep).map(({role,content})=>({role,content}));
    const recent=keep?context.slice(-keep):[];
    controller.current=new AbortController();setBusy(true);setError(null);setNotice(t('chatCommands.compacting'));
    try{
      const result=await api.chat({model,stream:false,messages:[{role:'system',content:compactInstruction},{role:'user',content:JSON.stringify({previousSummary:summary,earlierMessages:earlier})}],options:{temperature:0.2,num_predict:compactMaxTokens,display_thinking:false,stop:[]}},controller.current.signal) as {done?:boolean;cancelled?:boolean;done_reason?:string;message?:{content?:string}};
      if(result.done!==true || result.cancelled || result.done_reason==='length' || !result.message?.content?.trim())throw new Error(t('chatCommands.summaryFailed'));
      if(ownerRef.current!==requestClient || controller.current.signal.aborted)return;
      setSummary(result.message.content);setContext(recent);setNotice(t('chatCommands.compacted'));
    }catch(err){
      if(ownerRef.current===requestClient){setNotice(t('chatCommands.summaryFailed'));if(!(err instanceof DOMException && err.name==='AbortError'))setError(err instanceof Error?err.message:t('chatCommands.summaryFailed'));}
    }finally{if(ownerRef.current===requestClient)setBusy(false);}
  };
  const send = async (): Promise<void> => {
    const command=parseChatCommand(prompt);
    if(mode==='chat' && command.isCommand){await runCommand(command);return;}
    if (!model || !prompt.trim() || busy || (mode==='chat'&&!libraryReady)) return;
    const requestClient=api;
    controller.current = new AbortController();
    setNotice('');setBusy(true); setQuip(current=>(current+1+Math.floor(Math.random()*5))%6); setError(null); setVector(null);
    const form = document.getElementById('sa-chat-form') as HTMLFormElement | null;
    if (form && !form.reportValidity()) {setBusy(false);return;}
    const input = (mode==='chat'?command.prompt:prompt).trim();
    const conversation = [...messages.filter(m=>m.content),{role:'user' as const,content:input}];
    const requestConversation=[...context,{role:'user' as const,content:input}];
    const started = performance.now();
    let first: number | undefined;
    let response = '';
    let stats: Metrics = {elapsed:0};
    let done=false;
    let tokenLimit=false;
    const timer=mode==='chat'?window.setInterval(()=>{if(ownerRef.current===requestClient)setMessages([...conversation,{role:'assistant',content:response,metrics:{...stats,elapsed:performance.now()-started}}]);},200):undefined;
    if(mode==='chat') {setMessages([...conversation,{role:'assistant',content:'',metrics:stats}]);setPrompt('');}
    try {
      if (mode === 'chat') {
        const request={model,messages:[...(system.trim() ? [{role:'system',content:system}] : []),...(summary?[{role:'system',content:summaryPrefix+summary}]:[]),...requestConversation.map(({role,content})=>({role,content}))],...generationRequest(configuration)};
        const receive = (event:unknown):void => {
          if(ownerRef.current!==requestClient)return;
          const record = event as Record<string,unknown>;
          if(record.done===true) {done=true;tokenLimit=record.done_reason==='length';}
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
        if(ownerRef.current===requestClient)setContext([...requestConversation,{role:'assistant',content:response}]);
        if(tokenLimit && ownerRef.current===requestClient) setError(t('chat.tokenLimit'));
      } else {
        const result=await api.embeddings({model,input},controller.current.signal);
        const record=result as {embedding?:number[];embeddings?:number[][]};
        if(ownerRef.current===requestClient)setVector(record.embedding ?? record.embeddings?.[0] ?? null);
      }
    } catch (err) {
      if (ownerRef.current===requestClient && !(err instanceof DOMException && err.name==='AbortError')) setError(err instanceof ApiError ? `${err.status}: ${err.message}` : err instanceof Error ? err.message : t('playground.failed'));
    } finally {
      window.clearInterval(timer);
      if(ownerRef.current===requestClient){
        if(mode==='chat') setMessages([...conversation,{role:'assistant',content:response,metrics:{...stats,elapsed:performance.now()-started}}]);
        setBusy(false);
      }
    }
  };
  return <div className="sa-playground-page">
    <div className="sa-page__toolbar"><div><h1 className="sa-page__title">{t('playground.title')}</h1><p className="sa-heading-description">{t('text.tryAPromptStreamAResponseOr')}</p></div>
      <div className="sa-chat-tabs">{(['chat','embeddings'] as Mode[]).map(item=><button key={item} className={`sa-btn ${mode===item?'sa-btn--primary':''}`} disabled={busy} aria-pressed={mode===item} onClick={()=>setMode(item)}>{t(`playground.${item}`)}</button>)}<button className="sa-btn" disabled={busy || !messages.length} onClick={clearConversation}>{t('chat.clear')}</button></div>
    </div>
    <div className="sa-chat-layout">
      {mode==='chat'&&<LibrarySelectors busy={busy} onPromptChange={setSystem} onConfigurationChange={setConfiguration} onReady={setLibraryReady}/>}
      <section className="sa-chat-panel">
        <header className="sa-chat-header"><label className="sa-filterbar__field"><span className="sa-filterbar__label">{t('playground.model')}</span><select aria-label={t('playground.model')} disabled={busy} value={model} onChange={e=>{setModel(e.target.value);clearConversation();}}><option value="" disabled>{t('chat.selectModel')}</option>{available.map(name=><option key={name} value={name}>{name}</option>)}</select></label></header>
        {modelError && <p role="alert" className="sa-error">{t('models.loadError')}</p>}
        {!modelError && !available.length && <p className="sa-settings-note">{t('chat.noModels')}</p>}
        <div ref={log} tabIndex={0} className="sa-chat-messages" role="log" aria-label={t('chat.conversation')}>
          {mode==='chat' && !messages.length && <div className="sa-chat-empty"><img src="/images/sharp-logo.png" alt="SharpAI"/><h2>{t('chat.start')}</h2><p>{t('chat.startHelp')}</p></div>}
          {mode==='chat' && messages.map((message,index)=><div key={index} className={`sa-chat-turn sa-chat-turn--${message.role}`}><article className={`sa-chat-message sa-chat-message--${message.role}`}><header><strong>{t(message.role==='user'?'chat.you':'playground.response')}</strong>{message.content && <CopyButton value={message.content}/>}</header><div className="sa-chat-content">{message.role==='assistant' && message.content ? <Suspense fallback={message.content}><ChatMarkdown text={message.content}/></Suspense> : message.content || (busy&&index===messages.length-1?'':'—')}</div>
            {message.metrics && message.content && <InfoPopover label={t('chat.telemetry')}><strong className="sa-metrics-title">{t('chat.telemetry')}</strong><dl className="sa-chat-metrics">{[
  {label:'chat.ttft',value:message.metrics.ttft===undefined?'—':`${number(message.metrics.ttft/1000)} s`},
  {label:'chat.totalTime',value:`${number(message.metrics.elapsed/1000)} s`},
  {label:'chat.streaming',value:message.metrics.ttft===undefined?'—':`${number(Math.max(0,message.metrics.elapsed-message.metrics.ttft)/1000)} s`},
  {label:'chat.inputTokens',value:message.metrics.input===undefined?'—':number(message.metrics.input)},
  {label:'chat.outputTokens',value:message.metrics.output===undefined?'—':number(message.metrics.output)},
  {label:'chat.totalTokens',value:message.metrics.input===undefined || message.metrics.output===undefined?'—':number(message.metrics.input+message.metrics.output)},
  {label:'chat.speed',value:message.metrics.generation && message.metrics.output!==undefined?number(message.metrics.output/message.metrics.generation):'—'},
].map(({label,value})=><div key={label}><dt>{t(label)}</dt><dd>{value}</dd></div>)}</dl></InfoPopover>}
          </article>{busy&&index===messages.length-1&&message.role==='assistant'&&!message.content&&<p className="sa-chat-quip" role="status"><em>{t(`quips.${quip}`)}</em></p>}</div>)}
          {mode==='embeddings' && vector && <article className="sa-chat-message"><strong>{t('playground.dimensions')}: {number(vector.length)} — {t('playground.preview')}</strong><pre>{`[${vector.slice(0,12).map(n=>n.toFixed(4)).join(', ')}${vector.length>12?', …':''}]`}</pre></article>}
          {mode==='chat' && notice && <div className="sa-chat-command-notice" role="status"><pre>{notice}</pre></div>}
        </div>
        {error && <p role="alert" className="sa-error">{error}</p>}
        <form id="sa-chat-form" className="sa-chat-composer" onSubmit={e=>{e.preventDefault();void send();}}><label className="sa-filterbar__field" data-help={mode==='embeddings'?'embeddingText':undefined}><textarea rows={1} aria-label={t(mode==='chat'?'playground.prompt':'playground.input')} disabled={busy && mode!=='chat'} value={prompt} onChange={e=>setPrompt(e.target.value)} placeholder={t('chat.promptHelp')+(mode==='chat'?' /?':'')} onKeyDown={e=>{if(!e.shiftKey && e.key==='Enter'){e.preventDefault();void send();}}}/></label>{busy?<button type="button" className="sa-btn sa-btn--icon sa-chat-stop" aria-label={t('text.stopGenerating')} onClick={()=>controller.current?.abort()}><Icon name="stop"/></button>:<button className="sa-btn sa-btn--primary" disabled={!prompt.trim() || (!(mode==='chat'&&parseChatCommand(prompt).isCommand) && (!model || (mode==='chat'&&!libraryReady)))}>{t(mode==='chat'?'playground.send':'playground.embed')}</button>}<small className="sa-chat-warning">{t('chat.warning')}{mode==='chat' && <> · /?</>}</small></form>
      </section>

    </div>
  </div>;
}

// SharpAI contract fixtures from dashboard operations.json and docs/SCOPED_LIBRARIES.md.
// Synthetic transport endpoints live under /fixture; they make no inference compatibility claim.
const http = require('http');
const json = (response, status, body) => { response.writeHead(status, { 'Content-Type': 'application/json' }); response.end(JSON.stringify(body)); };
let mutations = 0;
const historyQueries = [];
const chatRequests = [];
let records = [
  { Id: 'system-starter-prompt', Name: 'Helpful assistant', Scope: 'System', Content: 'Be helpful.', CanEdit: true, CanDelete: false },
];
http.createServer((request, response) => {
  const url = new URL(request.url, 'http://127.0.0.1');
  const path = url.pathname;
  if (path === '/fixture/delayed401') return setTimeout(() => json(response, 401, { error: 'expired previous session' }), 150);
  if (path === '/fixture/history-queries') return json(response, 200, historyQueries);
  if (path.includes('/request-history')) { historyQueries.push({ path, query: Object.fromEntries(url.searchParams) }); if (historyQueries.length > 64) historyQueries.shift(); }
  if (path === '/fixture/chat-requests') return json(response,200,chatRequests.filter(item=>item.mode===String(request.headers.authorization || '').replace('Bearer fixture-', '')));
  if (path === '/fixture/count') return json(response, 200, { mutations });
  if (path === '/fixture/mutation') { mutations++; return json(response, 503, { error: 'ambiguous fixture mutation' }); }
  if (path.startsWith('/fixture/status/')) return json(response, Number(path.split('/').pop()), { error: 'fixture failure' });
  if (path === '/fixture/echo') return json(response, 200, { path: request.url, locale: request.headers['accept-language'], authorization: Boolean(request.headers.authorization) });
  if (path === '/fixture/large') { response.writeHead(200); return response.end('x'.repeat(65536)); }
  if (path === '/fixture/hang') return setTimeout(() => json(response, 200, {}), 3000);
  if (path.startsWith('/fixture/stream/')) {
    const mode = path.split('/').pop();
    const sse = mode === 'sse';
    response.writeHead(200, { 'Content-Type': sse ? 'text/event-stream' : 'application/x-ndjson' });
    const content = mode === 'burst' ? Array.from({length: 2000}, () => '{"message":{"content":"x"}}\n').join('') + '{"done":true}\n' : mode === 'malformed' ? '{oops}\n' : mode === 'error' ? '{"message":{"content":"partial"}}\n{"error":"failed"}\n' : mode === 'oversize' ? 'x'.repeat(4096) : sse ? ': heartbeat\r\ndata: {"text":\r\ndata: "你好 🌍"}\r\n\r\ndata: [DONE]\r\n\r\n' : '{"message":{"content":"你好 🌍"}}\n{"done":true}\n';
    if (mode === 'burst') return response.end(content);
    const bytes = Buffer.from(content); let offset = 0;
    const send = () => { if (response.destroyed) return; if (offset === bytes.length) return response.end(); response.write(bytes.subarray(offset, ++offset)); setImmediate(send); };
    return send();
  }
  if (path === '/v1.0/auth/context') return json(response, 200, { authEnabled: true, isAdmin: true, isTenantAdmin: false, tenantId: 'tenant-fixture', userId: null, capabilities: {} });
  const mode = String(request.headers.authorization || '').replace('Bearer fixture-', '');
  if (mode === 'forbidden') return json(response, 403, { error: 'fixture permission denied' });
  if (mode === 'failed') return json(response, 503, { error: 'fixture unavailable' });
  if (mode === 'empty' && (path === '/api/tags' || path === '/api/ps')) return json(response, 200, { models: [] });
  if (mode === 'empty' && path.endsWith('/request-history/summary')) return json(response, 200, {TotalCount:0, TotalSuccess:0, TotalFailure:0, AverageDurationMs:0, Buckets:[]});
  if (mode === 'empty' && path !== '/v1.0/library-context' && path.startsWith('/v1.0/')) return json(response, 200, { Objects: [], TotalRecords: 0, TotalCount: 0, EndOfResults: true });
  if (path === '/v1.0/library-context') return json(response, 200, { IsAdmin: true, TenantId: 'tenant-fixture' });
  if (path === '/health' || path === '/ready') return json(response, 200, { status: 'ok' });
  if (path === '/metrics') { response.writeHead(200, { 'Content-Type': 'text/plain' }); return response.end('http_requests_total 42\n'); }
  if (path === '/api/chat') {
    let body='';
    request.on('data',chunk=>{body+=chunk.toString();});
    request.on('end',()=>{
      let payload;try{payload=JSON.parse(body);}catch{return json(response,400,{error:'Invalid JSON'});}
      chatRequests.push({mode,body:payload});if(chatRequests.length>128)chatRequests.shift();
      const compact=payload.messages?.[0]?.content?.startsWith('Summarize the quoted conversation');
      if(compact){
        if(mode.startsWith('compact-failure'))return json(response,503,{error:'fixture summary failure'});
        const result={message:{role:'assistant',content:'The user prefers violet. Continue with the requested task.'},done:true,done_reason:mode.startsWith('compact-length')?'length':'stop'};
        if(mode.startsWith('compact-cancel'))return setTimeout(()=>json(response,200,result),2000);
        return json(response,200,result);
      }
      const text=mode.startsWith('chat-markdown')?'# Heading example\n\nA **bold phrase** and [docs](https://example.test).\n\n- first item\n- second item\n\n| Label | Value |\n| --- | --- |\n| Table A | 42 |\n\n```csharp\nstring sample = "violet";\n```\n\n':mode.startsWith('chat-commands')||mode.startsWith('compact-')?'The reply is complete. ':Array.from({length:48},(_,index)=>`Sentence ${index+1} is present.\n\n`).join('');
      const terminal={message:{role:'assistant',content:'Final sentence is complete. 你好 🌍'},done:true,done_reason:mode==='chat-limit'?'length':'stop',eval_count:2048};
      const lines=[{message:{role:'assistant',content:text},done:false}];
      if(mode!=='chat-incomplete')lines.push(terminal);
      response.writeHead(200,{'Content-Type':'application/x-ndjson'});
      const bytes=Buffer.from(lines.map(line=>JSON.stringify(line)).join('\n'));
      let offset=0;
      const send=()=>{if(response.destroyed)return;if(offset===bytes.length)return response.end();response.write(bytes.subarray(offset,++offset));setImmediate(send);};
      send();
    });
    return;
  }
  if (path === '/api/tags' && mode.startsWith('models-')) {
    const models = mode === 'models-empty' ? [] : mode === 'models-unknown' ? [{name:'first-unknown'},{name:'second-unknown'}] : [
      {name:'first-embedding',capabilities:{embeddings:true,completions:false}},
      {name:'first-inference',capabilities:{completions:mode === 'models-inference'}},
      {name:'second-inference',capabilities:{completions:mode === 'models-inference'}},
    ];
    return json(response,200,{models});
  }
  if (path === '/api/tags' || path === '/api/ps') return json(response, 200, { models: [{ name: 'fixture-model', size: 1024, modified_at: '2026-01-01T00:00:00Z' }] });
  if (path === '/v1.0/system-prompts' && request.method === 'GET') return json(response, 200, { Objects: records, TotalRecords: records.length, EndOfResults: true });
  if (path === '/v1.0/system-prompts' && request.method === 'POST') { const input = request.body; if (!input || !input.Name) return json(response, 400, { error: 'Name required' }); const record = { ...input, Id: 'fixture-' + (++mutations), CanEdit: true, CanDelete: true }; records.push(record); return json(response, 201, record); }
  if (path === '/v1.0/model-configurations') return json(response, 200, { Objects: [{ Id: 'system-balanced-configuration', Name: 'Balanced', Scope: 'System', CanEdit: true, CanDelete: false, Configuration: { Temperature: 0.6, MaxTokens: 2048, Stop: [], StreamResponse: true, JsonOutput: false, DisplayThinking: false } }], TotalRecords: 1 });
  if (path.endsWith('/request-history/summary')) {
    const from = Date.parse(url.searchParams.get('fromUtc') || '2026-01-01T00:00:00Z');
    const to = Date.parse(url.searchParams.get('toUtc') || '2026-01-02T00:00:00Z');
    const minutes = Number(url.searchParams.get('bucketMinutes') || 15);
    const count = Math.max(1, Math.min(1000, Math.ceil((to - from) / (minutes * 60000))));
    const peak = url.searchParams.get('category') === 'generation' ? 7 : url.searchParams.get('category') === 'embeddings' ? 3 : 12;
    const buckets = Array.from({length:count}, (_, index) => ({
      BucketStartUtc: new Date(from + index * minutes * 60000).toISOString(),
      BucketEndUtc: new Date(Math.min(to, from + (index + 1) * minutes * 60000)).toISOString(),
      SuccessCount: index === Math.floor(count / 2) ? peak : index === count - 3 ? 4 : 0,
      FailureCount: index === Math.floor(count / 2) ? 2 : 0,
    }));
    const totalSuccess = buckets.reduce((total, bucket) => total + bucket.SuccessCount, 0);
    return json(response, 200, {TotalCount:totalSuccess + 2, TotalSuccess:totalSuccess, TotalFailure:2, AverageDurationMs:12.5, Buckets:buckets});
  }
  if (path.endsWith('/request-history')) return json(response, 200, { Objects: [{ Id: 'req_fixture', Method: 'POST', Path: '/api/chat', StatusCode: 200, DurationMs: 50, CreatedUtc: '2026-01-01T00:00:00Z' }], TotalRecords: 1 });
  if (path.endsWith('/request-history/req_fixture')) return json(response, 200, { Id: 'req_fixture', Method: 'POST', Path: '/api/chat', RequestBody: null, RequestBodyUnavailable: true, ResponseBody: '{"message":{"content":"fixture"}}', InputTokens: 2, OutputTokens: 1, CachedTokens: null });
  if (path === '/api/settings') return json(response, 200, { HuggingFace: { ApiKey: '[redacted]' }, Runtime: { MaxTokens: 2048 }, UnknownFixture: { Preserve: true } });
  if (path.startsWith('/v1.0/tenants') || path === '/v1.0/api/audit') return json(response, 200, { Objects: [{ Guid: 'fixture-record', Name: 'Fixture record', Email: 'fixture@example.test', Active: true, UserGuid: 'fixture-owner', RoleGuid: 'fixture-role', CreatedUtc: '2026-01-01T00:00:00Z', ExpiresUtc: null, EventType: 'fixture', Outcome: 'Success' }], TotalRecords: 1 });
  return json(response, 501, { error: 'Unmodeled fixture operation' });
}).listen(process.env.PORT);

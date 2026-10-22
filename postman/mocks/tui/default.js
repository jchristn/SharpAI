// SharpAI contract fixtures from dashboard operations.json and docs/SCOPED_LIBRARIES.md.
// Synthetic transport endpoints live under /fixture; they make no inference compatibility claim.
const http = require('http');
const json = (response, status, body) => { response.writeHead(status, { 'Content-Type': 'application/json' }); response.end(JSON.stringify(body)); };
let mutations = 0;
let records = [
  { Id: 'system-starter-prompt', Name: 'Helpful assistant', Scope: 'System', Content: 'Be helpful.', CanEdit: true, CanDelete: false },
];
http.createServer((request, response) => {
  const url = new URL(request.url, 'http://127.0.0.1');
  const path = url.pathname;
  if (path === '/fixture/delayed401') return setTimeout(() => json(response, 401, { error: 'expired previous session' }), 150);
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
  if (mode === 'empty' && path !== '/v1.0/library-context' && path.startsWith('/v1.0/')) return json(response, 200, { Objects: [], TotalRecords: 0, TotalCount: 0, EndOfResults: true });
  if (path === '/v1.0/library-context') return json(response, 200, { IsAdmin: true, TenantId: 'tenant-fixture' });
  if (path === '/health' || path === '/ready') return json(response, 200, { status: 'ok' });
  if (path === '/metrics') { response.writeHead(200, { 'Content-Type': 'text/plain' }); return response.end('http_requests_total 42\n'); }
  if (path === '/api/tags' || path === '/api/ps') return json(response, 200, { models: [{ name: 'fixture-model', size: 1024, modified_at: '2026-01-01T00:00:00Z' }] });
  if (path === '/v1.0/system-prompts' && request.method === 'GET') return json(response, 200, { Objects: records, TotalRecords: records.length, EndOfResults: true });
  if (path === '/v1.0/system-prompts' && request.method === 'POST') { const input = request.body; if (!input || !input.Name) return json(response, 400, { error: 'Name required' }); const record = { ...input, Id: 'fixture-' + (++mutations), CanEdit: true, CanDelete: true }; records.push(record); return json(response, 201, record); }
  if (path === '/v1.0/model-configurations') return json(response, 200, { Objects: [{ Id: 'system-balanced-configuration', Name: 'Balanced', Scope: 'System', CanEdit: true, CanDelete: false, Configuration: { Temperature: 0.6, MaxTokens: 128, Stop: [], StreamResponse: true, JsonOutput: false, DisplayThinking: false } }], TotalRecords: 1 });
  if (path.endsWith('/request-history/summary')) return json(response, 200, { TotalCount: 1, Buckets: [{ BucketStartUtc: '2026-01-01T00:00:00Z', BucketEndUtc: '2026-01-01T00:15:00Z', SuccessCount: 1, FailureCount: 0 }] });
  if (path.endsWith('/request-history')) return json(response, 200, { Objects: [{ Id: 'req_fixture', Method: 'POST', Path: '/api/chat', StatusCode: 200, DurationMs: 50, CreatedUtc: '2026-01-01T00:00:00Z' }], TotalRecords: 1 });
  if (path.endsWith('/request-history/req_fixture')) return json(response, 200, { Id: 'req_fixture', Method: 'POST', Path: '/api/chat', RequestBody: null, RequestBodyUnavailable: true, ResponseBody: '{"message":{"content":"fixture"}}', InputTokens: 2, OutputTokens: 1, CachedTokens: null });
  if (path === '/api/settings') return json(response, 200, { HuggingFace: { ApiKey: '[redacted]' }, Runtime: { MaxTokens: 128 }, UnknownFixture: { Preserve: true } });
  if (path.startsWith('/v1.0/tenants') || path === '/v1.0/api/audit') return json(response, 200, { Objects: [{ Guid: 'fixture-record', Name: 'Fixture record', Email: 'fixture@example.test', Active: true, UserGuid: 'fixture-owner', RoleGuid: 'fixture-role', CreatedUtc: '2026-01-01T00:00:00Z', ExpiresUtc: null, EventType: 'fixture', Outcome: 'Success' }], TotalRecords: 1 });
  return json(response, 501, { error: 'Unmodeled fixture operation' });
}).listen(process.env.PORT);

// Contract fixture for the Hugging Face tree/resolve and HTTP Range paths SharpAI consumes.
// Deliberately synthetic GGUF metadata, not inference weights. No real upstream is contacted.
const http = require('http');
const crypto = require('crypto');
const fs = require('fs');
const fixtureDir = process.env.SHARPAI_TEST_SPLIT_DIRECTORY;
const fixturePaths = fixtureDir ? fs.readdirSync(fixtureDir).filter(p => p.endsWith('.gguf')).sort() : [];
const data = Buffer.from('verified model content');
function u64(n) { const b = Buffer.alloc(8); b.writeBigUInt64LE(BigInt(n)); return b; }
function u32(n) { const b = Buffer.alloc(4); b.writeUInt32LE(n); return b; }
function str(s) { const b = Buffer.from(s); return Buffer.concat([u64(b.length), b]); }
function gguf(arch = 'llama') {
  const entries = [['general.architecture', arch], ['llama.block_count', 4], ['llama.context_length', 2048], ['llama.attention.head_count', 8], ['llama.attention.head_count_kv', 2], ['llama.embedding_length', 256]];
  return Buffer.concat([Buffer.from('GGUF'), u32(3), u64(0), u64(entries.length), ...entries.map(([key, value]) => Buffer.concat([str(key), typeof value === 'string' ? Buffer.concat([u32(8), str(value)]) : Buffer.concat([u32(4), u32(value)])]))]);
}
const hash = b => crypto.createHash('sha256').update(b).digest('hex');
function json(res, code, body) { res.writeHead(code, {'Content-Type':'application/json'}); res.end(JSON.stringify(body)); }
http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  const path = url.pathname;
  if (path === '/health') return json(res, 200, {status:'ok'});
  if (path.startsWith('/file/')) {
    const mode = path.split('/')[2];
    if (mode === 'crash') { const b = Buffer.alloc(1024*1024,120); const offset = req.headers.range ? Number(req.headers.range.match(/bytes=(\d+)-/)[1]) : 0; const headers = {'ETag':'"crash1"','Content-Length':b.length-offset}; if (offset) { if (req.headers['if-range'] !== '"crash1"') return json(res,400,{error:'missing If-Range'}); headers['Content-Range'] = `bytes ${offset}-${b.length-1}/${b.length}`; } res.writeHead(offset ? 206 : 200,headers); return res.end(b.subarray(offset)); }
    if (mode === 'error') return json(res, 503, {error:'unavailable'});
    if (mode === 'html') { res.writeHead(500, {'Content-Type':'text/html'}); return res.end('<h1>upstream error</h1>'); }
    if (mode === 'empty') { res.writeHead(204); return res.end(); }
    if (mode === 'slow') { res.writeHead(200, {'ETag':'"v1"','Content-Length':data.length}); res.write(data.subarray(0,4)); return setTimeout(() => res.end(data.subarray(4)), 1500); }
    if (mode === 'short') { res.writeHead(200, {'ETag':'"v1"'}); return res.end(data.subarray(0,4)); }
    let body = mode === 'corrupt' ? Buffer.alloc(data.length, 120) : mode === 'oversize' ? Buffer.concat([data,data]) : data;
    const headers = {'ETag': mode === 'changed' ? '"v2"' : '"v1"'};
    const range = req.headers.range;
    if (mode === 'unexpected206') { headers['Content-Range'] = `bytes 0-${data.length-1}/${data.length}`; res.writeHead(206, headers); return res.end(body); }
    if (range && mode !== 'ignore' && mode !== 'changed') {
      const offset = Number(range.match(/bytes=(\d+)-/)[1]);
      if (req.headers['if-range'] !== '"v1"') return json(res, 400, {error:'missing If-Range'});
      headers['Content-Range'] = `bytes ${mode === 'wrongrange' ? offset+1 : offset}-${data.length-1}/${data.length}`;
      if (mode === 'wrongetag') headers.ETag = '"v2"';
      if (mode === 'wrongtotal') headers['Content-Range'] = `bytes ${offset}-${data.length-1}/${data.length+1}`;
      if (mode === 'wrongend') headers['Content-Range'] = `bytes ${offset}-${data.length-2}/${data.length}`;
      if (mode === 'missingrange') delete headers['Content-Range'];
      if (mode === 'wrongunit') headers['Content-Range'] = `items ${offset}-${data.length-1}/${data.length}`;
      body = body.subarray(offset);
      headers['Content-Length'] = body.length;
      res.writeHead(206, headers); return res.end(body);
    }
    if (mode !== 'oversize' && mode !== 'short') headers['Content-Length'] = mode === 'badlength' ? data.length + 1 : body.length;
    res.writeHead(200, headers); return res.end(body);
  }
  const api = path.match(/^\/api\/models\/org\/([^/]+)(?:\/tree\/([^/]+))?$/);
  if (api) {
    const scenario = api[1];
    if (scenario === 'notfound') return json(res, 404, {error:'not found'});
    if (!api[2]) return json(res, 200, {id:`org/${scenario}`,sha:scenario === 'norevision' ? null : 'revision1',safetensors:{total:1000}});
    if (api[2] !== 'revision1') return json(res, 409, {error:'tree must be pinned'});
    if (scenario === 'nogguf') return json(res, 200, []);
    if (scenario === 'pageescape') res.setHeader('Link', '<https://invalid.example/api/models/org/model>; rel="next"');
    if (scenario === 'pageloop') res.setHeader('Link', '<?recursive=true>; rel="next"');
    if (scenario === 'paged' && !url.searchParams.has('cursor')) res.setHeader('Link', '<?cursor=2>; rel="next"');
    if (scenario === 'real' && fixturePaths.length) return json(res, 200, fixturePaths.map(path => { const b = fs.readFileSync(require('path').join(fixtureDir, path)); return {type:'file', path, size:b.length, lfs:{oid:hash(b),size:b.length}}; }));
    const split = scenario === 'split' || scenario === 'incomplete' || scenario === 'paged';
    const paths = split ? ['model-Q4_K_M-00001-of-00002.gguf', ...(scenario === 'incomplete' ? [] : ['model-Q4_K_M-00002-of-00002.gguf'])] : [scenario === 'projector' ? 'mmproj.gguf' : 'model-Q4_K_M.gguf'];
    const model = gguf(scenario === 'unsupported' ? 'clip' : 'llama');
    return json(res, 200, paths.filter((path, index) => scenario !== 'paged' || index === (url.searchParams.has('cursor') ? 1 : 0)).map(path => ({type:'file', path, size:model.length, oid:'this-is-a-file-oid-not-a-commit',lfs:scenario === 'missinghash' ? null : {oid:hash(model),size:model.length}})));
  }
  const resolve = path.match(/^\/org\/([^/]+)\/resolve\/revision1\/(.+)$/);
  if (resolve) {
    if (resolve[1] === 'real' && fixturePaths.includes(resolve[2])) { const b = fs.readFileSync(require('path').join(fixtureDir, resolve[2])); res.writeHead(200, {'ETag':'"revision1"', 'Content-Length':b.length}); return res.end(b); }
    const model = gguf(resolve[1] === 'unsupported' ? 'clip' : 'llama');
    const body = resolve[1] === 'corrupt' ? Buffer.alloc(model.length, 0) : model;
    res.writeHead(200, {'ETag':'"revision1"','Content-Length':body.length}); return res.end(body);
  }
  return json(res, 501, {error:'endpoint not modeled',path});
}).listen(process.env.PORT || 4907);

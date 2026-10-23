import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError } from './ApiClient';

describe('ApiClient', () => {
  const originalFetch = globalThis.fetch;

  function mockFetch(status: number, body: unknown): ReturnType<typeof vi.fn> {
    const fn = vi.fn().mockResolvedValue({
      ok: status >= 200 && status < 300,
      status,
      statusText: 'x',
      text: () => Promise.resolve(typeof body === 'string' ? body : JSON.stringify(body)),
    });
    globalThis.fetch = fn as unknown as typeof fetch;
    return fn;
  }

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it('retains final-frame content at EOF across every UTF-8 byte boundary', async () => {
    const bytes = new TextEncoder().encode(JSON.stringify({message:{content:'First '},done:false})+'\n'+JSON.stringify({message:{content:'final sentence. 你好 🌍'},done:true,done_reason:'stop'}));
    globalThis.fetch = vi.fn().mockResolvedValue(new Response(new ReadableStream<Uint8Array>({
      start(controller) { for (const byte of bytes) controller.enqueue(Uint8Array.of(byte)); controller.close(); },
    }), {headers:{'content-type':'application/x-ndjson'}}));
    const events: unknown[] = [];
    await new ApiClient('http://localhost:8000').chatStream({}, event=>events.push(event));
    expect(events).toEqual([{message:{content:'First '},done:false},{message:{content:'final sentence. 你好 🌍'},done:true,done_reason:'stop'}]);
  });

  it.skipIf(!process.env.SHARPAI_TUI_TEST_URL)('reads the final content through the Postman HTTP simulation', async () => {
    const client = new ApiClient(process.env.SHARPAI_TUI_TEST_URL!);
    let answer = '';
    let completed = false;
    await client.chatStream({model:'fixture-model',messages:[{role:'user',content:'Tell a story'}]}, event=>{
      const frame = event as {message?:{content?:string};done?:boolean};
      answer += frame.message?.content ?? '';
      completed = frame.done === true;
    });
    expect(completed).toBe(true);
    expect(answer).toBe(Array.from({length:48},(_,index)=>`Sentence ${index+1} is present.\n\n`).join('')+'Final sentence is complete. 你好 🌍');
  });

  it('strips a trailing slash and builds query strings', async () => {
    const fetchMock = mockFetch(200, { TotalRecords: 0, Objects: [] });
    const client = new ApiClient('http://127.0.0.1:8000/');
    await client.requestHistory({ pageSize: 25, pageNumber: 2, method: 'GET' });
    const url = fetchMock.mock.calls[0][0] as string;
    expect(url).toContain('http://127.0.0.1:8000/v1.0/api/request-history?');
    expect(url).toContain('pageSize=25');
    expect(url).toContain('pageNumber=2');
    expect(url).toContain('method=GET');
  });

  it('sends a bearer token when configured', async () => {
    const fetchMock = mockFetch(200, {});
    const client = new ApiClient('http://127.0.0.1:8000', { kind: 'bearer', token: 'abc' });
    await client.session();
    const headers = (fetchMock.mock.calls[0][1] as RequestInit).headers as Record<string, string>;
    expect(headers.authorization).toBe('Bearer abc');
  });

  it('sends access-key/secret-key headers when configured', async () => {
    const fetchMock = mockFetch(200, {});
    const client = new ApiClient('http://127.0.0.1:8000', { kind: 'accessKey', accessKey: 'access_x', secretKey: 'secret_y' });
    await client.listModels();
    const headers = (fetchMock.mock.calls[0][1] as RequestInit).headers as Record<string, string>;
    expect(headers['x-access-key']).toBe('access_x');
    expect(headers['x-secret-key']).toBe('secret_y');
  });

  it('puts login credentials in headers, not the body', async () => {
    const fetchMock = mockFetch(200, { token: 't', sessionId: 's', tenantId: 'ten', userId: 'usr', expiresUtc: 'z' });
    const client = new ApiClient('http://127.0.0.1:8000');
    const result = await client.login('a@b.c', 'pw', 'ten_1');
    const init = fetchMock.mock.calls[0][1] as RequestInit;
    const headers = init.headers as Record<string, string>;
    expect(headers['x-email']).toBe('a@b.c');
    expect(headers['x-password']).toBe('pw');
    expect(headers['x-tenant-guid']).toBe('ten_1');
    expect(init.body).toBeUndefined();
    expect(result.token).toBe('t');
  });

  it('sends Accept-Language from the locale provider', async () => {
    const fetchMock = mockFetch(200, {});
    const client = new ApiClient('http://127.0.0.1:8000', { kind: 'none' }, () => 'ar');
    await client.listModels();
    const headers = (fetchMock.mock.calls[0][1] as RequestInit).headers as Record<string, string>;
    expect(headers['accept-language']).toBe('ar');
  });

  it('throws ApiError with the server status and message on failure', async () => {
    mockFetch(403, { error: { type: 'forbidden', message: 'nope' } });
    const client = new ApiClient('http://127.0.0.1:8000');
    await expect(client.getSettings()).rejects.toBeInstanceOf(ApiError);
    await expect(client.getSettings()).rejects.toMatchObject({ status: 403, message: 'nope' });
  });
  it('rejects a successful HTML fallback instead of treating it as API data', async () => {
    mockFetch(200, '<!doctype html><html>Dashboard</html>');
    const client = new ApiClient('http://localhost:3000');
    await expect(client.getSettings()).rejects.toMatchObject({ status: 502, body: { code: 'invalid_api_response' } });
    await expect(client.health()).rejects.toBeInstanceOf(ApiError);
  });

});

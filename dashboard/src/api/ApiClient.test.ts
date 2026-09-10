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
});

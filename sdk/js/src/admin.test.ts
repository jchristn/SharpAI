import { afterEach, describe, expect, it, vi } from 'vitest';
import { SharpAISdk } from './SharpAISdk';

describe('AdminMethods', () => {
  const originalFetch = globalThis.fetch;

  function mockFetch(status: number, body: unknown): ReturnType<typeof vi.fn> {
    const fn = vi.fn().mockResolvedValue({
      ok: status >= 200 && status < 300,
      status,
      text: () => Promise.resolve(typeof body === 'string' ? body : JSON.stringify(body)),
    });
    globalThis.fetch = fn as unknown as typeof fetch;
    return fn;
  }

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it('sends a bearer token on authenticated requests', async () => {
    const fetchMock = mockFetch(200, {});
    const sdk = new SharpAISdk('http://127.0.0.1:8000', { token: 'abc' });
    await sdk.admin.session();
    const [, init] = fetchMock.mock.calls[0];
    expect((init as RequestInit).headers).toMatchObject({ Authorization: 'Bearer abc' });
  });

  it('logs in via headers and stores the returned token', async () => {
    const fetchMock = mockFetch(200, { token: 'tok123', sessionId: 's', tenantId: 'ten', userId: 'usr', expiresUtc: 'z' });
    const sdk = new SharpAISdk('http://127.0.0.1:8000');
    const result = await sdk.admin.login('a@b.c', 'pw', 'ten_1');
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://127.0.0.1:8000/v1.0/token');
    expect((init as RequestInit).headers).toMatchObject({ 'x-email': 'a@b.c', 'x-password': 'pw', 'x-tenant-guid': 'ten_1' });
    expect(result?.token).toBe('tok123');
    expect(sdk.token).toBe('tok123');
  });

  it('encodes query parameters for request history', async () => {
    const fetchMock = mockFetch(200, { Objects: [], TotalRecords: 0 });
    const sdk = new SharpAISdk('http://127.0.0.1:8000');
    await sdk.admin.requestHistory({ pageSize: 25, method: 'GET' });
    const url = fetchMock.mock.calls[0][0] as string;
    expect(url).toContain('/v1.0/api/request-history?');
    expect(url).toContain('pageSize=25');
    expect(url).toContain('method=GET');
  });

  it('creates a tenant with the expected body', async () => {
    const fetchMock = mockFetch(200, { Guid: 'ten_1', Name: 'acme' });
    const sdk = new SharpAISdk('http://127.0.0.1:8000', { apiKey: 'key' });
    await sdk.admin.createTenant('acme');
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://127.0.0.1:8000/v1.0/tenants');
    expect((init as RequestInit).method).toBe('POST');
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({ Name: 'acme' });
    expect((init as RequestInit).headers).toMatchObject({ 'x-api-key': 'key' });
  });
});

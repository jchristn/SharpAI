import { afterEach, describe, expect, it, vi } from 'vitest';
import { SharpAISdk } from './SharpAISdk';

describe('OllamaMethods import + presets', () => {
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

  it('imports a local model by path', async () => {
    const fetchMock = mockFetch(200, { name: 'm', model: 'm' });
    const sdk = new SharpAISdk('http://127.0.0.1:8000');
    await sdk.ollama.importModel({ path: 'C:/models/m.gguf', name: 'm' });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://127.0.0.1:8000/api/import');
    expect((init as RequestInit).method).toBe('POST');
    expect(JSON.parse((init as RequestInit).body as string)).toMatchObject({ path: 'C:/models/m.gguf', name: 'm' });
  });

  it('creates a preset', async () => {
    const fetchMock = mockFetch(200, { Name: 'pirate' });
    const sdk = new SharpAISdk('http://127.0.0.1:8000');
    await sdk.ollama.createPreset({ name: 'pirate', model: 'qwen-base', system: 'Arr', temperature: 0.3 });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://127.0.0.1:8000/v1.0/models/presets');
    expect(JSON.parse((init as RequestInit).body as string)).toMatchObject({ name: 'pirate', model: 'qwen-base' });
  });

  it('deletes a preset by name using a DELETE to the named route', async () => {
    const fetchMock = mockFetch(200, { deleted: true });
    const sdk = new SharpAISdk('http://127.0.0.1:8000');
    await sdk.ollama.deletePreset('pirate');
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://127.0.0.1:8000/v1.0/models/presets/pirate');
    expect((init as RequestInit).method).toBe('DELETE');
  });
});

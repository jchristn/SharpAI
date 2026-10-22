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
    const call = fetchMock.mock.calls[0]!;
    const url = call[0]; const init = call[1];
    expect(url).toBe('http://127.0.0.1:8000/api/import');
    expect((init as RequestInit).method).toBe('POST');
    expect(JSON.parse((init as RequestInit).body as string)).toMatchObject({ path: 'C:/models/m.gguf', name: 'm' });
  });

  it('creates a preset', async () => {
    const fetchMock = mockFetch(200, { Name: 'pirate' });
    const sdk = new SharpAISdk('http://127.0.0.1:8000');
    await sdk.ollama.createPreset({ name: 'pirate', model: 'qwen-base', system: 'Arr', temperature: 0.3 });
    const call = fetchMock.mock.calls[0]!;
    const url = call[0]; const init = call[1];
    expect(url).toBe('http://127.0.0.1:8000/v1.0/models/presets');
    expect(JSON.parse((init as RequestInit).body as string)).toMatchObject({ name: 'pirate', model: 'qwen-base' });
  });

  it('deletes a preset by name using a DELETE to the named route', async () => {
    const fetchMock = mockFetch(200, { deleted: true });
    const sdk = new SharpAISdk('http://127.0.0.1:8000');
    await sdk.ollama.deletePreset('pirate');
    const call = fetchMock.mock.calls[0]!;
    const url = call[0]; const init = call[1];
    expect(url).toBe('http://127.0.0.1:8000/v1.0/models/presets/pirate');
    expect((init as RequestInit).method).toBe('DELETE');
  });
  it('preserves a full GGUF URL including its revision through streaming transport', async () => {
    const source = 'https://huggingface.co/org/model/resolve/commit123/folder/model.gguf';
    const sdk = new SharpAISdk('http://127.0.0.1:8000');
    const stream = vi.spyOn(sdk, 'postStreamAsync').mockImplementation(async function* () { yield { status: 'success' }; });
    for await (const progress of sdk.ollama.pullModel({ model: source })) expect(progress.status).toBe('success');
    expect(stream).toHaveBeenCalledWith('http://127.0.0.1:8000/api/pull', { model: source }, undefined);
  });

});

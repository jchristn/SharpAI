import { describe, expect, it } from 'vitest';
import { normalizeRepository } from './repository';
describe('repository input', () => {
  it.each(['Qwen/Qwen3-8B-GGUF', 'https://huggingface.co/Qwen/Qwen3-8B-GGUF'])('normalizes repository %s', input => {
    expect(normalizeRepository(input)).toBe('Qwen/Qwen3-8B-GGUF');
  });
  it.each([
    'https://huggingface.co/Qwen/Qwen3-8B-GGUF/tree/main',
    'https://huggingface.co/Qwen/model/tree/commit123',
    'https://huggingface.co/Qwen/model/resolve/commit123/folder/model.gguf',
    'https://huggingface.co/Qwen/model/blob/commit123/model.gguf',
  ])('preserves revision and exact file selection %s', input => expect(normalizeRepository(input)).toBe(input));
  it.each(['Qwen3-8B-GGUF', 'https://evil.example/Qwen/model', '../model', 'https://user:pass@huggingface.co/Qwen/model',
    'https://huggingface.co/Qwen/model?token=secret', 'https://huggingface.co/Qwen/model/tree/main/subdir',
    'https://huggingface.co/Qwen/model/resolve/main/model.bin', 'https://huggingface.co/Qwen/../model',
    'https://huggingface.co/Qwen/model/resolve/main/folder%2fmodel.gguf', 'https://huggingface.co/Qwen/model#fragment',
  ])('rejects ambiguous or unsupported input %s', input => expect(() => normalizeRepository(input)).toThrow());
});

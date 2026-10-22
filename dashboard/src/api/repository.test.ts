import { describe, expect, it } from 'vitest';
import { normalizeRepository } from './repository';
describe('repository input', () => {
  it.each(['Qwen/Qwen3-8B-GGUF', 'https://huggingface.co/Qwen/Qwen3-8B-GGUF/tree/main', 'https://huggingface.co/Qwen/Qwen3-8B-GGUF'])('normalizes %s', input => {
    expect(normalizeRepository(input)).toBe('Qwen/Qwen3-8B-GGUF');
  });
  it.each(['Qwen3-8B-GGUF','https://evil.example/Qwen/model','https://huggingface.co/Qwen/model/resolve/main/file.gguf','../model'])('rejects ambiguous or unsupported input %s', input => expect(() => normalizeRepository(input)).toThrow());
});

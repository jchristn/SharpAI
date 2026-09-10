import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import type { ReactNode } from 'react';
import { AuthProvider, useAuth } from './AuthProvider';

describe('AuthProvider', () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    window.localStorage.clear();
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'ok',
      text: () => Promise.resolve(JSON.stringify({ token: 'tok123', userId: 'usr_1', tenantId: 'ten_1' })),
    }) as unknown as typeof fetch;
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  const wrapper = ({ children }: { children: ReactNode }): JSX.Element => (
    <AuthProvider baseUrl="http://127.0.0.1:8000">{children}</AuthProvider>
  );

  it('starts unauthenticated', () => {
    const { result } = renderHook(() => useAuth(), { wrapper });
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('logs in, persists the session, then clears on logout', async () => {
    const { result } = renderHook(() => useAuth(), { wrapper });

    await act(async () => {
      await result.current.login('admin@sharpai.local', 'pw', 'ten_1');
    });
    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.session?.token).toBe('tok123');
    expect(window.localStorage.getItem('sharpai.session')).toContain('tok123');

    await act(async () => {
      await result.current.logout();
    });
    expect(result.current.isAuthenticated).toBe(false);
    expect(window.localStorage.getItem('sharpai.session')).toBeNull();
  });
});

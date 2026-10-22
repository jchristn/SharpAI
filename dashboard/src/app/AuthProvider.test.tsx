import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import {useApi} from './ApiProvider';
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

  it('clears only a rejected stored session so library requests can recover after redeployment', async()=>{
    localStorage.setItem('sharpai.session',JSON.stringify({token:'stale',userId:'user-old',tenantId:'default'}));
    globalThis.fetch=vi.fn().mockResolvedValue({ok:false,status:401,statusText:'Unauthorized',text:async()=>JSON.stringify({Message:'Expired'})}) as unknown as typeof fetch;
    const {result}=renderHook(()=>({auth:useAuth(),api:useApi()}),{wrapper});
    await act(async()=>{await expect(result.current.api.libraryContext()).rejects.toThrow();});
    await waitFor(()=>expect(result.current.auth.isAuthenticated).toBe(false));expect(localStorage.getItem('sharpai.session')).toBeNull();
  });

  it('an old request cannot clear a newer sign-in session',async()=>{
    localStorage.setItem('sharpai.session',JSON.stringify({token:'old',userId:'user-old',tenantId:'default'}));
    const {result}=renderHook(()=>({auth:useAuth(),api:useApi()}),{wrapper});const old=result.current.api;
    await act(async()=>{await result.current.auth.login('admin@sharpai.local','pw');});
    globalThis.fetch=vi.fn().mockResolvedValue({ok:false,status:401,statusText:'Unauthorized',text:async()=>JSON.stringify({Message:'Expired'})}) as unknown as typeof fetch;
    await act(async()=>{await expect(old.libraryContext()).rejects.toThrow();});
    expect(result.current.auth.session?.token).toBe('tok123');
  });

  it('does not authenticate a persisted token until the server verifies it', async () => {
    localStorage.setItem('sharpai.session', JSON.stringify({token:'stored',userId:'user',tenantId:'default'}));
    let complete: (value: unknown) => void = () => {};
    globalThis.fetch = vi.fn().mockReturnValue(new Promise(resolve => {complete=resolve;})) as unknown as typeof fetch;
    const {result} = renderHook(() => useAuth(), {wrapper});
    expect(result.current.isAuthenticated).toBe(false);
    expect(result.current.isCheckingSession).toBe(true);
    await act(async () => complete({ok:true,status:200,text:async()=>JSON.stringify({userId:'user',tenantId:'default'})}));
    await waitFor(() => expect(result.current.isAuthenticated).toBe(true));
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

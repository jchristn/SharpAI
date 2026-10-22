import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { ApiClient } from '../api/ApiClient';
import type { Credentials } from '../api/types';
import i18n from '../i18n/config';
import { ApiContext } from './ApiProvider';

interface StoredSession {
  token: string;
  userId: string;
  tenantId: string;
}

const STORAGE_KEY = 'sharpai.session';

function loadSession(): StoredSession | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as StoredSession;
    return parsed.token ? parsed : null;
  } catch {
    return null;
  }
}

function saveSession(session: StoredSession | null): void {
  try {
    if (session) window.localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    else window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    // storage unavailable; the session still lives in memory for this tab
  }
}

export interface AuthState {
  session: StoredSession | null;
  isAuthenticated: boolean;
  isCheckingSession: boolean;
  login: (email: string, password: string, tenantGuid?: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

export interface AuthProviderProps {
  baseUrl: string;
  children: ReactNode;
}

/**
 * Owns the required dashboard session and token-bound API client. Persisted sessions are validated
 * before protected pages mount. Unauthorized responses clear the matching session.
 */
export function AuthProvider({ baseUrl, children }: AuthProviderProps): JSX.Element {
  const [session, setSession] = useState<StoredSession | null>(() => loadSession());
  const [verifiedToken, setVerifiedToken] = useState<string | null>(null);

  const credentials: Credentials = useMemo(
    () => (session ? { kind: 'bearer', token: session.token } : { kind: 'none' }),
    [session],
  );

  const client = useMemo(
    () => new ApiClient(baseUrl, credentials, () => i18n.language, () => {
      if(credentials.kind!=='bearer')return;
      const rejectedToken=credentials.token;
      setSession(current=>{
        if(current?.token!==rejectedToken)return current;
        saveSession(null);return null;
      });
    }),
    [baseUrl, credentials],
  );

  const login = useCallback(
    async (email: string, password: string, tenantGuid?: string): Promise<void> => {
      const result = await client.login(email, password, tenantGuid);
      const next: StoredSession = { token: result.token, userId: result.userId, tenantId: result.tenantId };
      setVerifiedToken(next.token);
      saveSession(next);
      setSession(next);
    },
    [client],
  );

  const logout = useCallback(async (): Promise<void> => {
    saveSession(null);
    setSession(null);
    setVerifiedToken(null);
    try {
      await client.logout();
    } catch {
      // revoke best-effort; clear locally regardless
    }
  }, [client]);

  useEffect(() => {
    if (!session || verifiedToken === session.token) return;
    let cancelled = false;
    client.session().then(() => {
      if (!cancelled) setVerifiedToken(session.token);
    }).catch(() => {
      if (!cancelled) setSession(current => { if (current?.token !== session.token) return current; saveSession(null); return null; });
    });
    return () => { cancelled = true; };
  }, [client, session, verifiedToken]);

  const authValue: AuthState = useMemo(
    () => ({ session, isAuthenticated: session !== null && verifiedToken === session.token, isCheckingSession: session !== null && verifiedToken !== session.token, login, logout }),
    [session, verifiedToken, login, logout],
  );

  return (
    <ApiContext.Provider value={client}>
      <AuthContext.Provider value={authValue}>{children}</AuthContext.Provider>
    </ApiContext.Provider>
  );
}

/** Access the authentication state. Throws if used outside an AuthProvider. */
export function useAuth(): AuthState {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within an AuthProvider');
  return value;
}

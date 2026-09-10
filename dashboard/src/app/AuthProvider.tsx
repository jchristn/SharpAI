import { createContext, useCallback, useContext, useMemo, useState } from 'react';
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
  login: (email: string, password: string, tenantGuid?: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

export interface AuthProviderProps {
  baseUrl: string;
  children: ReactNode;
}

/**
 * Owns the dashboard's authentication session and the ApiClient bound to it. Auth is optional — when the
 * server runs open (auth disabled) the dashboard works with no session; when the operator signs in, the
 * bearer token flows into every request. The session is persisted so a refresh keeps the user signed in.
 */
export function AuthProvider({ baseUrl, children }: AuthProviderProps): JSX.Element {
  const [session, setSession] = useState<StoredSession | null>(() => loadSession());

  const credentials: Credentials = useMemo(
    () => (session ? { kind: 'bearer', token: session.token } : { kind: 'none' }),
    [session],
  );

  const client = useMemo(
    () => new ApiClient(baseUrl, credentials, () => i18n.language),
    [baseUrl, credentials],
  );

  const login = useCallback(
    async (email: string, password: string, tenantGuid?: string): Promise<void> => {
      const result = await client.login(email, password, tenantGuid);
      const next: StoredSession = { token: result.token, userId: result.userId, tenantId: result.tenantId };
      saveSession(next);
      setSession(next);
    },
    [client],
  );

  const logout = useCallback(async (): Promise<void> => {
    try {
      await client.logout();
    } catch {
      // revoke best-effort; clear locally regardless
    }
    saveSession(null);
    setSession(null);
  }, [client]);

  const authValue: AuthState = useMemo(
    () => ({ session, isAuthenticated: session !== null, login, logout }),
    [session, login, logout],
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

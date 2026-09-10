import { createContext, useContext, useMemo } from 'react';
import type { ReactNode } from 'react';
import { ApiClient } from '../api/ApiClient';
import type { Credentials } from '../api/types';

export const ApiContext = createContext<ApiClient | null>(null);

export interface ApiProviderProps {
  baseUrl: string;
  credentials?: Credentials;
  children: ReactNode;
}

/** Provides a single hand-rolled ApiClient to the rebuilt dashboard tree. */
export function ApiProvider({ baseUrl, credentials, children }: ApiProviderProps): JSX.Element {
  const client = useMemo(() => new ApiClient(baseUrl, credentials ?? { kind: 'none' }), [baseUrl, credentials]);
  return <ApiContext.Provider value={client}>{children}</ApiContext.Provider>;
}

/** Access the shared ApiClient. Throws if used outside an ApiProvider. */
export function useApi(): ApiClient {
  const client = useContext(ApiContext);
  if (!client) throw new Error('useApi must be used within an ApiProvider');
  return client;
}

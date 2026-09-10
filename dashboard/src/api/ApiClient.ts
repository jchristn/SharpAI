// Hand-rolled fetch-based SharpAI API client (no axios), per FRONTEND_ARCHITECTURE. One typed client
// carries auth, builds URLs, serializes JSON, and normalizes errors into ApiError. Every rebuilt
// dashboard view depends on this rather than calling fetch directly.

import type {
  Credentials,
  CreatedCredential,
  CreateUserRequest,
  Credential,
  EffectivePermissions,
  EnumerationQuery,
  EnumerationResult,
  HealthStatus,
  LoginResponse,
  SessionDetails,
  Tenant,
  User,
} from './types';

export class ApiError extends Error {
  public readonly status: number;
  public readonly body: unknown;

  constructor(status: number, message: string, body: unknown) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }
}

type QueryValue = string | number | boolean | undefined | null;

export class ApiClient {
  private baseUrl: string;
  private credentials: Credentials;
  private getLocale?: () => string;

  constructor(baseUrl: string, credentials: Credentials = { kind: 'none' }, getLocale?: () => string) {
    this.baseUrl = baseUrl.replace(/\/+$/, '');
    this.credentials = credentials;
    this.getLocale = getLocale;
  }

  private localeHeader(): Record<string, string> {
    const locale = this.getLocale?.();
    return locale ? { 'accept-language': locale } : {};
  }

  // ---- auth state ----

  setCredentials(credentials: Credentials): void {
    this.credentials = credentials;
  }

  clearCredentials(): void {
    this.credentials = { kind: 'none' };
  }

  private authHeaders(): Record<string, string> {
    switch (this.credentials.kind) {
      case 'bearer':
        return { authorization: `Bearer ${this.credentials.token}` };
      case 'apiKey':
        return { 'x-api-key': this.credentials.apiKey };
      case 'accessKey':
        return {
          'x-access-key': this.credentials.accessKey,
          'x-secret-key': this.credentials.secretKey,
        };
      default:
        return {};
    }
  }

  private buildUrl(path: string, query?: Record<string, QueryValue>): string {
    const url = `${this.baseUrl}${path.startsWith('/') ? path : `/${path}`}`;
    if (!query) return url;
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') params.append(key, String(value));
    }
    const qs = params.toString();
    return qs ? `${url}?${qs}` : url;
  }

  private async request<T>(
    method: string,
    path: string,
    options?: { query?: Record<string, QueryValue>; body?: unknown; headers?: Record<string, string> },
  ): Promise<T> {
    const headers: Record<string, string> = { ...this.localeHeader(), ...this.authHeaders(), ...(options?.headers ?? {}) };
    let body: string | undefined;
    if (options?.body !== undefined) {
      headers['content-type'] = 'application/json';
      body = JSON.stringify(options.body);
    }

    const response = await fetch(this.buildUrl(path, options?.query), { method, headers, body });
    const text = await response.text();
    const parsed: unknown = text.length > 0 ? safeJsonParse(text) : undefined;

    if (!response.ok) {
      throw new ApiError(response.status, extractError(parsed) ?? response.statusText, parsed);
    }
    return parsed as T;
  }

  // ---- health / settings ----

  health(): Promise<HealthStatus> {
    return this.request<HealthStatus>('GET', '/health');
  }

  ready(): Promise<HealthStatus> {
    return this.request<HealthStatus>('GET', '/ready');
  }

  getSettings(): Promise<unknown> {
    return this.request<unknown>('GET', '/api/settings');
  }

  updateSettings(settings: unknown): Promise<unknown> {
    return this.request<unknown>('PUT', '/api/settings', { body: settings });
  }

  // ---- models ----

  listModels(): Promise<unknown> {
    return this.request<unknown>('GET', '/api/tags');
  }

  runningModels(): Promise<unknown> {
    return this.request<unknown>('GET', '/api/ps');
  }

  showModel(name: string): Promise<unknown> {
    return this.request<unknown>('POST', '/api/show', { body: { name } });
  }

  /** Pull a model, streaming newline-delimited progress lines to the callback. */
  async pullModel(name: string, onLine: (line: string) => void): Promise<void> {
    const response = await fetch(this.buildUrl('/api/pull'), {
      method: 'POST',
      headers: { ...this.localeHeader(), ...this.authHeaders(), 'content-type': 'application/json' },
      body: JSON.stringify({ name }),
    });
    if (!response.ok) {
      const text = await response.text();
      throw new ApiError(response.status, extractError(safeJsonParse(text)) ?? response.statusText, text);
    }
    const reader = response.body?.getReader();
    if (!reader) return;
    const decoder = new TextDecoder();
    let buffer = '';
    for (;;) {
      const chunk = await reader.read();
      if (chunk.done) break;
      buffer += decoder.decode(chunk.value, { stream: true });
      let index = buffer.indexOf('\n');
      while (index >= 0) {
        const line = buffer.slice(0, index).trim();
        buffer = buffer.slice(index + 1);
        if (line) onLine(line);
        index = buffer.indexOf('\n');
      }
    }
    if (buffer.trim()) onLine(buffer.trim());
  }

  openapi(): Promise<unknown> {
    return this.request<unknown>('GET', '/openapi.json');
  }

  /**
   * Execute an arbitrary request (for the API Explorer). Carries inherited auth, never throws on non-2xx —
   * the caller renders the status and body itself.
   */
  async execute(
    method: string,
    path: string,
    body?: unknown,
  ): Promise<{ status: number; ok: boolean; body: unknown }> {
    const headers: Record<string, string> = { ...this.localeHeader(), ...this.authHeaders() };
    let payload: string | undefined;
    if (body !== undefined && body !== null && body !== '') {
      headers['content-type'] = 'application/json';
      payload = typeof body === 'string' ? body : JSON.stringify(body);
    }
    const response = await fetch(this.buildUrl(path), { method, headers, body: payload });
    const text = await response.text();
    return { status: response.status, ok: response.ok, body: text.length > 0 ? safeJsonParse(text) : null };
  }

  /** Fetch the Prometheus text exposition from /metrics (raw text, not JSON). */
  async metrics(): Promise<string> {
    const response = await fetch(this.buildUrl('/metrics'), {
      headers: { ...this.localeHeader(), ...this.authHeaders() },
    });
    if (!response.ok) {
      throw new ApiError(response.status, response.statusText, undefined);
    }
    return response.text();
  }

  deleteModel(name: string): Promise<unknown> {
    return this.request<unknown>('DELETE', '/api/delete', { body: { name } });
  }

  unloadModel(name?: string): Promise<unknown> {
    return this.request<unknown>('POST', '/api/unload', { body: name ? { name } : {} });
  }

  // ---- inference ----

  chat(body: unknown): Promise<unknown> {
    return this.request<unknown>('POST', '/api/chat', { body });
  }

  /** Stream a chat completion, invoking onEvent with each parsed newline-delimited JSON chunk. */
  async chatStream(body: Record<string, unknown>, onEvent: (event: unknown) => void): Promise<void> {
    const response = await fetch(this.buildUrl('/api/chat'), {
      method: 'POST',
      headers: { ...this.localeHeader(), ...this.authHeaders(), 'content-type': 'application/json' },
      body: JSON.stringify({ ...body, stream: true }),
    });
    if (!response.ok) {
      const text = await response.text();
      throw new ApiError(response.status, extractError(safeJsonParse(text)) ?? response.statusText, text);
    }
    const reader = response.body?.getReader();
    if (!reader) return;
    const decoder = new TextDecoder();
    let buffer = '';
    for (;;) {
      const chunk = await reader.read();
      if (chunk.done) break;
      buffer += decoder.decode(chunk.value, { stream: true });
      let index = buffer.indexOf('\n');
      while (index >= 0) {
        const line = buffer.slice(0, index).trim();
        buffer = buffer.slice(index + 1);
        if (line) onEvent(safeJsonParse(line));
        index = buffer.indexOf('\n');
      }
    }
    if (buffer.trim()) onEvent(safeJsonParse(buffer.trim()));
  }

  generate(body: unknown): Promise<unknown> {
    return this.request<unknown>('POST', '/api/generate', { body });
  }

  embeddings(body: unknown): Promise<unknown> {
    return this.request<unknown>('POST', '/api/embed', { body });
  }

  // ---- request history ----

  requestHistory(query?: EnumerationQuery & Record<string, QueryValue>): Promise<EnumerationResult<unknown>> {
    return this.request<EnumerationResult<unknown>>('GET', '/v1.0/api/request-history', { query });
  }

  requestHistorySummary(query?: Record<string, QueryValue>): Promise<unknown> {
    return this.request<unknown>('GET', '/v1.0/api/request-history/summary', { query });
  }

  requestHistoryEntry(id: string): Promise<unknown> {
    return this.request<unknown>('GET', `/v1.0/api/request-history/${encodeURIComponent(id)}`);
  }

  // ---- authentication ----

  login(email: string, password: string, tenantGuid?: string): Promise<LoginResponse> {
    const headers: Record<string, string> = { 'x-email': email, 'x-password': password };
    if (tenantGuid) headers['x-tenant-guid'] = tenantGuid;
    return this.request<LoginResponse>('POST', '/v1.0/token', { headers });
  }

  session(): Promise<SessionDetails> {
    return this.request<SessionDetails>('GET', '/v1.0/token');
  }

  logout(): Promise<unknown> {
    return this.request<unknown>('DELETE', '/v1.0/token');
  }

  audit(query?: EnumerationQuery & Record<string, QueryValue>): Promise<EnumerationResult<unknown>> {
    return this.request<EnumerationResult<unknown>>('GET', '/v1.0/api/audit', { query });
  }

  // ---- management (RBAC) ----

  listTenants(query?: EnumerationQuery & Record<string, QueryValue>): Promise<EnumerationResult<Tenant>> {
    return this.request<EnumerationResult<Tenant>>('GET', '/v1.0/tenants', { query });
  }

  createTenant(name: string): Promise<Tenant> {
    return this.request<Tenant>('POST', '/v1.0/tenants', { body: { Name: name } });
  }

  listUsers(tenantGuid: string, query?: EnumerationQuery & Record<string, QueryValue>): Promise<EnumerationResult<User>> {
    return this.request<EnumerationResult<User>>('GET', `/v1.0/tenants/${tenantGuid}/users`, { query });
  }

  createUser(tenantGuid: string, body: CreateUserRequest): Promise<User> {
    return this.request<User>('POST', `/v1.0/tenants/${tenantGuid}/users`, { body });
  }

  deleteUser(tenantGuid: string, userGuid: string): Promise<unknown> {
    return this.request<unknown>('DELETE', `/v1.0/tenants/${tenantGuid}/users/${userGuid}`);
  }

  listCredentials(tenantGuid: string, query?: EnumerationQuery & Record<string, QueryValue>): Promise<EnumerationResult<Credential>> {
    return this.request<EnumerationResult<Credential>>('GET', `/v1.0/tenants/${tenantGuid}/credentials`, { query });
  }

  createCredential(tenantGuid: string, userGuid: string, name: string): Promise<CreatedCredential> {
    return this.request<CreatedCredential>('POST', `/v1.0/tenants/${tenantGuid}/credentials`, {
      body: { UserGuid: userGuid, Name: name },
    });
  }

  listRoles(tenantGuid: string, query?: EnumerationQuery & Record<string, QueryValue>): Promise<EnumerationResult<unknown>> {
    return this.request<EnumerationResult<unknown>>('GET', `/v1.0/tenants/${tenantGuid}/roles`, { query });
  }

  userPermissions(tenantGuid: string, userGuid: string): Promise<EffectivePermissions> {
    return this.request<EffectivePermissions>('GET', `/v1.0/tenants/${tenantGuid}/users/${userGuid}/permissions`);
  }

  credentialPermissions(tenantGuid: string, credentialGuid: string): Promise<EffectivePermissions> {
    return this.request<EffectivePermissions>(
      'GET',
      `/v1.0/tenants/${tenantGuid}/credentials/${credentialGuid}/permissions`,
    );
  }
}

function safeJsonParse(text: string): unknown {
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

function extractError(parsed: unknown): string | undefined {
  if (parsed && typeof parsed === 'object') {
    const record = parsed as Record<string, unknown>;
    const error = record.error;
    if (typeof error === 'string') return error;
    if (error && typeof error === 'object') {
      const message = (error as Record<string, unknown>).message;
      if (typeof message === 'string') return message;
    }
    if (typeof record.message === 'string') return record.message;
  }
  return undefined;
}

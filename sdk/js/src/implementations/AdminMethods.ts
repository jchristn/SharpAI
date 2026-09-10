import type { SharpAISdk } from '../SharpAISdk';
import type {
  CreateAssignmentRequest,
  CreateUserRequest,
  CreatedCredential,
  IAdminMethods,
  LoginResponse,
  QueryParams,
} from '../interfaces/IAdminMethods';

/**
 * Implementation of administrative methods: settings, request history, authentication, audit, and
 * account/RBAC management.
 */
export class AdminMethods implements IAdminMethods {
  private readonly sdk: SharpAISdk;

  constructor(sdk: SharpAISdk) {
    this.sdk = sdk;
  }

  private url(path: string, query?: QueryParams): string {
    let url = `${this.sdk.endpoint}${path}`;
    if (query) {
      const params = new URLSearchParams();
      for (const [key, value] of Object.entries(query)) {
        if (value !== undefined && value !== null && value !== '') params.append(key, String(value));
      }
      const qs = params.toString();
      if (qs) url = `${url}?${qs}`;
    }
    return url;
  }

  // ---- settings ----

  getSettings(signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('GET', this.url('/api/settings'), undefined, signal);
  }

  updateSettings(settings: unknown, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('PUT', this.url('/api/settings'), { body: settings }, signal);
  }

  // ---- request history ----

  requestHistory(query?: QueryParams, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('GET', this.url('/v1.0/api/request-history', query), undefined, signal);
  }

  requestHistorySummary(query?: QueryParams, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('GET', this.url('/v1.0/api/request-history/summary', query), undefined, signal);
  }

  requestHistoryEntry(id: string, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('GET', this.url(`/v1.0/api/request-history/${encodeURIComponent(id)}`), undefined, signal);
  }

  // ---- authentication ----

  async login(email: string, password: string, tenantGuid?: string, signal?: AbortSignal): Promise<LoginResponse | null> {
    const headers: Record<string, string> = { 'x-email': email, 'x-password': password };
    if (tenantGuid) headers['x-tenant-guid'] = tenantGuid;
    const result = await this.sdk.sendAsync<LoginResponse>('POST', this.url('/v1.0/token'), { headers }, signal);
    if (result?.token) this.sdk.token = result.token;
    return result;
  }

  session(signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('GET', this.url('/v1.0/token'), undefined, signal);
  }

  async logout(signal?: AbortSignal): Promise<unknown | null> {
    const result = await this.sdk.sendAsync('DELETE', this.url('/v1.0/token'), undefined, signal);
    this.sdk.token = null;
    return result;
  }

  audit(query?: QueryParams, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('GET', this.url('/v1.0/api/audit', query), undefined, signal);
  }

  // ---- account / RBAC management ----

  listTenants(query?: QueryParams, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('GET', this.url('/v1.0/tenants', query), undefined, signal);
  }

  createTenant(name: string, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('POST', this.url('/v1.0/tenants'), { body: { Name: name } }, signal);
  }

  listUsers(tenantGuid: string, query?: QueryParams, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('GET', this.url(`/v1.0/tenants/${tenantGuid}/users`, query), undefined, signal);
  }

  createUser(tenantGuid: string, request: CreateUserRequest, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('POST', this.url(`/v1.0/tenants/${tenantGuid}/users`), { body: request }, signal);
  }

  deleteUser(tenantGuid: string, userGuid: string, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('DELETE', this.url(`/v1.0/tenants/${tenantGuid}/users/${userGuid}`), undefined, signal);
  }

  listCredentials(tenantGuid: string, query?: QueryParams, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('GET', this.url(`/v1.0/tenants/${tenantGuid}/credentials`, query), undefined, signal);
  }

  createCredential(tenantGuid: string, userGuid: string, name: string, signal?: AbortSignal): Promise<CreatedCredential | null> {
    return this.sdk.sendAsync<CreatedCredential>(
      'POST',
      this.url(`/v1.0/tenants/${tenantGuid}/credentials`),
      { body: { UserGuid: userGuid, Name: name } },
      signal
    );
  }

  listRoles(tenantGuid: string, query?: QueryParams, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('GET', this.url(`/v1.0/tenants/${tenantGuid}/roles`, query), undefined, signal);
  }

  createAssignment(tenantGuid: string, request: CreateAssignmentRequest, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('POST', this.url(`/v1.0/tenants/${tenantGuid}/assignments`), { body: request }, signal);
  }

  userPermissions(tenantGuid: string, userGuid: string, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('GET', this.url(`/v1.0/tenants/${tenantGuid}/users/${userGuid}/permissions`), undefined, signal);
  }

  credentialPermissions(tenantGuid: string, credentialGuid: string, signal?: AbortSignal): Promise<unknown | null> {
    return this.sdk.sendAsync('GET', this.url(`/v1.0/tenants/${tenantGuid}/credentials/${credentialGuid}/permissions`), undefined, signal);
  }
}

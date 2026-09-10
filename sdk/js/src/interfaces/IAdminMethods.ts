/**
 * Query parameter map for paginated/filterable endpoints.
 */
export type QueryParams = Record<string, string | number | boolean | undefined | null>;

/**
 * Response from a successful login.
 */
export interface LoginResponse {
  token: string;
  sessionId: string;
  tenantId: string;
  userId: string;
  expiresUtc: string;
}

/**
 * Request body to create a user.
 */
export interface CreateUserRequest {
  Email: string;
  Password: string;
  FirstName?: string;
  LastName?: string;
  IsAdmin?: boolean;
  IsTenantAdmin?: boolean;
}

/**
 * Request body to assign a role to a user.
 */
export interface CreateAssignmentRequest {
  UserGuid: string;
  RoleGuid?: string;
  RoleName?: string;
  ResourceScope?: 'Tenant' | 'Resource';
  ResourceGuid?: string;
  InheritsToChildren?: boolean;
}

/**
 * The one-time credential creation response (includes the plaintext secret key, shown only once).
 */
export interface CreatedCredential {
  guid: string;
  userId: string;
  tenantId: string;
  name: string;
  accessKey: string;
  secretKey: string;
  expiresUtc: string | null;
}

/**
 * Administrative methods: settings, request history, authentication, audit, and account/RBAC management.
 * Methods return the parsed response, or null on a non-success response.
 */
export interface IAdminMethods {
  getSettings(signal?: AbortSignal): Promise<unknown | null>;
  updateSettings(settings: unknown, signal?: AbortSignal): Promise<unknown | null>;

  requestHistory(query?: QueryParams, signal?: AbortSignal): Promise<unknown | null>;
  requestHistorySummary(query?: QueryParams, signal?: AbortSignal): Promise<unknown | null>;
  requestHistoryEntry(id: string, signal?: AbortSignal): Promise<unknown | null>;

  login(email: string, password: string, tenantGuid?: string, signal?: AbortSignal): Promise<LoginResponse | null>;
  session(signal?: AbortSignal): Promise<unknown | null>;
  logout(signal?: AbortSignal): Promise<unknown | null>;
  audit(query?: QueryParams, signal?: AbortSignal): Promise<unknown | null>;

  listTenants(query?: QueryParams, signal?: AbortSignal): Promise<unknown | null>;
  createTenant(name: string, signal?: AbortSignal): Promise<unknown | null>;
  listUsers(tenantGuid: string, query?: QueryParams, signal?: AbortSignal): Promise<unknown | null>;
  createUser(tenantGuid: string, request: CreateUserRequest, signal?: AbortSignal): Promise<unknown | null>;
  deleteUser(tenantGuid: string, userGuid: string, signal?: AbortSignal): Promise<unknown | null>;
  listCredentials(tenantGuid: string, query?: QueryParams, signal?: AbortSignal): Promise<unknown | null>;
  createCredential(tenantGuid: string, userGuid: string, name: string, signal?: AbortSignal): Promise<CreatedCredential | null>;
  listRoles(tenantGuid: string, query?: QueryParams, signal?: AbortSignal): Promise<unknown | null>;
  createAssignment(tenantGuid: string, request: CreateAssignmentRequest, signal?: AbortSignal): Promise<unknown | null>;
  userPermissions(tenantGuid: string, userGuid: string, signal?: AbortSignal): Promise<unknown | null>;
  credentialPermissions(tenantGuid: string, credentialGuid: string, signal?: AbortSignal): Promise<unknown | null>;
}

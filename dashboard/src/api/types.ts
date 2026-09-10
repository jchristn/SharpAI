// Shared API types for the hand-rolled SharpAI client (no axios). These mirror the server's
// EnumerationQuery/EnumerationResult envelopes and the authentication/RBAC surface so every rebuilt
// dashboard view consumes one typed client.

export interface EnumerationQuery {
  pageNumber?: number;
  pageSize?: number;
  order?: string;
  createdAfter?: string;
  createdBefore?: string;
  name?: string;
}

export interface EnumerationResult<T> {
  MaxResults: number;
  Skip: number;
  TotalRecords: number;
  RecordsRemaining: number;
  EndOfResults: boolean;
  Objects: T[];
}

export interface LoginResponse {
  token: string;
  sessionId: string;
  tenantId: string;
  userId: string;
  expiresUtc: string;
}

export interface SessionDetails {
  sessionId: string;
  tenantId: string;
  userId: string;
  createdUtc: string;
  expiresUtc: string;
}

export interface Tenant {
  Guid: string;
  Name: string;
  Active: boolean;
  IsProtected: boolean;
  CreatedUtc: string;
  LastUpdateUtc: string;
}

export interface User {
  Guid: string;
  TenantGuid: string;
  FirstName: string;
  LastName: string;
  Email: string;
  IsAdmin: boolean;
  IsTenantAdmin: boolean;
  Active: boolean;
  IsProtected: boolean;
  CreatedUtc: string;
  LastUpdateUtc: string;
}

export interface CreateUserRequest {
  Email: string;
  FirstName?: string;
  LastName?: string;
  Password: string;
  IsAdmin?: boolean;
  IsTenantAdmin?: boolean;
}

export interface Credential {
  Guid: string;
  UserGuid: string;
  TenantGuid: string;
  Name: string;
  AccessKey: string;
  Active: boolean;
  IsProtected: boolean;
  CreatedUtc: string;
  ExpiresUtc: string | null;
}

export interface CreatedCredential {
  guid: string;
  userId: string;
  tenantId: string;
  name: string;
  accessKey: string;
  secretKey: string;
  expiresUtc: string | null;
}

export interface EffectivePermission {
  resourceType: string;
  operation: string;
  effect: string;
  scope: string;
  resourceGuid: string | null;
  inheritsToChildren: boolean;
}

export interface EffectivePermissions {
  tenantId: string;
  principalType: string;
  principalId: string;
  count: number;
  permissions: EffectivePermission[];
}

export interface HealthStatus {
  status?: string;
  [key: string]: unknown;
}

export type Credentials =
  | { kind: 'none' }
  | { kind: 'bearer'; token: string }
  | { kind: 'apiKey'; apiKey: string }
  | { kind: 'accessKey'; accessKey: string; secretKey: string };

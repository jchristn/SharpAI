export interface AdministrationContext {
  authEnabled: boolean;
  isAdmin: boolean;
  isTenantAdmin: boolean;
  tenantId: string | null;
  tenantName?: string;
  userId: string | null;
  capabilities: Record<string, { read: boolean; write: boolean }>;
}
export type AdministrationSection =
  | "tenants"
  | "users"
  | "credentials"
  | "roles"
  | "permissions"
  | "assignments"
  | "sessions"
  | "audit";
export type SecurityRecord = Record<string, unknown> & {
  Guid: string;
  Name?: string;
  Active?: boolean;
  IsProtected?: boolean;
  IsBuiltIn?: boolean;
};
export const administrationResources: Record<AdministrationSection, string> = {
  tenants: "Tenant",
  users: "User",
  credentials: "Credential",
  roles: "Role",
  permissions: "Permission",
  assignments: "Assignment",
  sessions: "Session",
  audit: "Audit",
};
export function records(value: unknown): SecurityRecord[] {
  if (Array.isArray(value)) return value as SecurityRecord[];
  return (value as { Objects?: SecurityRecord[] })?.Objects ?? [];
}
export function securityName(row: SecurityRecord): string {
  return String(row.Name || row.Email || row.RoleName || row.Guid);
}
export function safeRecord(row: SecurityRecord): SecurityRecord {
  return Object.fromEntries(
    Object.entries(row).filter(([key]) => !/password|secret|token/i.test(key)),
  ) as SecurityRecord;
}

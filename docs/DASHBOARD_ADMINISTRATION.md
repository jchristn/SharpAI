# Dashboard administration

Sign-in is required before any dashboard page or management data is loaded. Initial credentials are
`admin@sharpai.local` / `password`, tenant `default`, until changed. See
[API bearer credentials](../DEPLOYMENT_GUIDE.md#api-bearer-credentials) for bootstrap token provisioning.

The dashboard's **Access** navigation item opens a tabbed workspace for tenant-scoped security management. Its tenant selector
chooses the target of management requests; it does not impersonate another user or change a session's tenant.
Platform administrators can choose any tenant. Other principals are bound to their authenticated tenant.

| Route | Workflow | Backend |
| --- | --- | --- |
| `/access/tenants` | List, create, edit, deactivate, delete unprotected tenants | `/v1.0/tenants` |
| `/access/users` | List, create, edit account details/status, reset passwords, inspect effective permissions | `/v1.0/tenants/{tenant}/users` |
| `/access/credentials` | List, create, reveal/copy/hide bearer tokens, regenerate, edit status/expiration, revoke, inspect permissions | `/v1.0/tenants/{tenant}/credentials` |
| `/access/roles` | List built-in/custom roles, create/rename/delete custom roles, manage permission mappings | `/v1.0/tenants/{tenant}/roles` |
| `/access/permissions` | List, create permit/deny rules, delete custom permissions | `/v1.0/tenants/{tenant}/permissions` |
| `/access/assignments` | Grant/revoke user roles and credential scopes at tenant/resource scope | User and credential assignment APIs |
| `/access/sessions` | List tenant sessions and revoke active sessions | Tenant session APIs |
| `/access/audit` | Inspect tenant security events | `/v1.0/api/audit?tenantGuid=...` |

Legacy routes such as `/users` remain available. Access tabs preserve the selected tenant.

All routes use the shared API client, tables, pagination, form dialogs, confirmation dialogs, and localized
copy. Lists have explicit loading, empty, access-denied, and failure states. Row actions respect protected
and built-in records. Raw passwords/hashes are never displayed in record details. A newly issued credential
token is shown in the creation-result dialog and can be revealed later with Credential Admin permission.
Revealed values are cleared when hidden, when navigating away, or when tenant/permission context changes. Passwords are accepted by a separate reset form and are never read back.

Server authorization remains authoritative. The dashboard obtains identity and per-resource capabilities
from `/v1.0/auth/context`; restricted actions are hidden/disabled, and direct API calls re-enforce the rules.
Global administrator grants and platform tenant operations require platform administration. Built-in roles
and permissions are immutable. Resource assignments require a resource ID, and all referenced principals,
roles, permissions, and mappings must belong to the selected tenant or be explicitly global built-ins.
Credential grants remain subject to the existing owning-user permission ceiling; deny overrides permit.

Reference patterns reviewed: AssistantHub's tenant forms; NotDory's user forms and tenant context;
Armada's tenant-isolation and RBAC patterns; the Agents authentication and dashboard usability requirements.

## Operator workflow

1. Open **Administration → Tenants** to create an organization or copy its tenant ID.
2. Select the organization in **Selected tenant** on an administration page. Selection persists within
   the browser tab and is checked against the caller's accessible tenants after sign-in or refresh.
3. Open **Users** to add accounts, edit active/admin flags, or reset passwords. A reset revokes all of
   the user's current sessions. Disabling a user or tenant also blocks existing sessions and credentials.
4. Open **Credentials**, choose an owning user, and create a named integration key. Reveal, hide, copy or regenerate its
   bearer token using the icons in its token column. API calls require `Authorization: Bearer <token>`; these are separate from dashboard passwords.
5. In **Permissions**, define resource/operation rules with Permit or Deny. In **Roles**, create a custom
   role and use **Role permissions** to map rules. Built-in roles and permissions are read-only.
6. In **Assignments**, choose Users or Credentials and the principal, then grant a role over the entire
   tenant or one exact resource ID. Inspect the result with **Effective permissions** on the principal.
7. Use **Sessions** to revoke a sign-in and **Security audit** to inspect security changes and denials.

Lists use server pagination. The labeled **Filter this page** field is a client-side filter over the
current page because the existing security enumeration APIs do not implement general field search/sort.
Secret/password fields are omitted from record JSON, and sensitive request-body fields are redacted before
request-history persistence. Creating a tenant does not automatically create an administrator in that tenant;
select the new tenant and add its first account on Users.

## Validation

`python3 scripts/test-administration.py` starts its own temporary, authenticated server and SQLite database;
it never modifies a running deployment. It verifies tenant isolation, admin escalation prevention, built-in
immutability, role mappings, permission effects, credential owner ceilings, bearer token reveal, regeneration and redaction,
account/tenant deactivation, password resets, and session revocation. Build the server first or pass
`--server-dll /absolute/path/to/SharpAI.Server.dll`. This test also runs in dashboard CI.

Dashboard browser coverage is in `dashboard/e2e/administration.spec.ts`, including all routes at desktop,
tablet and mobile sizes, accessibility, tenant switches and stale responses, typed forms, credential secret
lifetime, readonly controls, role permission mappings, and exact credential resource scopes.

See [reveal and regeneration contracts](../DEPLOYMENT_GUIDE.md#reveal-and-regenerate-api-bearer-tokens) for permissions, error codes, legacy credential recovery and encrypted token backups.

Playground retains content carried in the final chat frame, including responses without a
trailing newline. It follows the latest response as Markdown layout changes. A token-limit
notice means generation reached its output budget; increase `MaxTokens` in the selected
model configuration for a longer answer. This notice preserves the generated text and
conversation context.

Playground supports `/?` (`/help`, `/commands`, `/`), `/clear` (`/new`, `/reset`),
`/compact`, `/model [name]` (`/models`), `/status`, and `/stop` (`/cancel`). Use `//`
for literal text beginning with a slash. `/compact` uses the selected model to summarize
earlier turns, preserves the visible transcript and latest exchange, and changes model
context only after successful completion. Failed, truncated, or cancelled summaries leave
the original context intact. These composer commands share their catalog with the TUI;
see [TUI chat commands](TUI.md#chat-commands-and-rendering) for details.

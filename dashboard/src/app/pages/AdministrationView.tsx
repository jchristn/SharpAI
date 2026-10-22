import { CredentialTokenControl } from '../CredentialTokenControl';
import { Icon } from '../Icon';
import { Fragment, useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type {
  AdministrationSection,
  SecurityRecord,
} from "../../api/administration";
import {
  administrationResources,
  records,
  safeRecord,
  securityName,
} from "../../api/administration";
import { ApiError } from "../../api/ApiClient";
import {
  ActionMenu,
  CopyButton,
  CopyableId,
  DataTable,
  JsonViewer,
  Modal,
  StatusBadge,
  type Column,
} from "../../components/ui";
import { formatDateTime } from "../../i18n/format";
import { useAdministration } from "../AdministrationProvider";
import { useAuth } from "../AuthProvider";
import { useApi } from "../ApiProvider";
import { AdministrationForm } from "./AdministrationForm";

export function AdministrationView({
  section,
}: {
  section: AdministrationSection;
}): JSX.Element {
  const { t } = useTranslation();
  const state = useAdministration();
  const canRead =
    state.access?.isAdmin ||
    state.access?.capabilities[administrationResources[section]]?.read;
  return (
    <div>
      <div className="sa-page__toolbar">
        <div>
          <h1 className="sa-page__title">{t(`admin.${section}`)}</h1>
          <p className="sa-heading-description">{t(`admin.help.${section}`)}</p>
        </div>
      </div>
      {state.loading ? (
        <p role="status">{t("text.loading")}</p>
      ) : state.error ? (
        <div role="alert" className="sa-settings-note">
          <p>{t("admin.accessFailed")}</p>
          <p>{state.error}</p>
          <button className="sa-btn--icon sa-btn" onClick={state.reload} aria-label={t('common.refresh')}><Icon name="refresh"/></button>
        </div>
      ) : (
        <>
          <div className="sa-admin-scope">
            <label className="sa-filterbar__field">
              <span>{t("admin.selectedTenant")}</span>
              <select
                aria-label={t("admin.selectedTenant")}
                value={state.tenant}
                onChange={(e) => state.selectTenant(e.target.value)}
                disabled={!state.access?.isAdmin || !state.tenants.length}
              >
                <option value="" disabled>
                  {t("admin.selectTenant")}
                </option>
                {state.tenants.map((tenant) => (
                  <option key={tenant.Guid} value={tenant.Guid}>
                    {tenant.Name}
                    {tenant.Active === false ? ` (${t("admin.inactive")})` : ""}
                  </option>
                ))}
              </select>
            </label>
            <p>
              {t(
                state.access?.authEnabled
                  ? "admin.scopeHelp"
                  : "admin.openAccess",
              )}
            </p>
          </div>
          {!canRead ? (
            <div role="alert" className="sa-settings-note">
              {t("admin.denied")}
            </div>
          ) : !state.tenant && section !== "tenants" ? (
            <div className="sa-empty">{t("admin.createTenantFirst")}</div>
          ) : (
            <AdministrationRecords
              key={`${section}:${state.tenant}`}
              section={section}
            />
          )}
        </>
      )}
    </div>
  );
}

type Dialog = {
  kind:
    "create" | "edit" | "reset" | "details" | "json" | "effective" | "mappings";
  row?: SecurityRecord;
};

function AdministrationRecords({
  section,
}: {
  section: AdministrationSection;
}): JSX.Element {
  const { t, i18n } = useTranslation();
  const api = useApi();
  const { logout } = useAuth();
  const { tenant, access, reload } = useAdministration();
  const [items, setItems] = useState<SecurityRecord[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [size, setSize] = useState(25);
  const [filter, setFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const [detail, setDetail] = useState<SecurityRecord[]>([]);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [secret, setSecret] = useState<Record<string, string> | null>(null);
  const [confirm, setConfirm] = useState<SecurityRecord | null>(null);
  const [busy, setBusy] = useState(false);
  const [principalType, setPrincipalType] = useState("users");
  const [principal, setPrincipal] = useState("");
  const [principals, setPrincipals] = useState<SecurityRecord[]>([]);
  const mounted = useRef(true);
  const version = useRef(0);
  const detailVersion = useRef(0);
  const [notice, setNotice] = useState("");
  const canWrite = Boolean(
    access?.isAdmin ||
    access?.capabilities[administrationResources[section]]?.write,
  );
  const notify = useCallback(
    (err: unknown) =>
      err instanceof ApiError
        ? `${err.status}: ${err.message}`
        : err instanceof Error
          ? err.message
          : t("admin.failed"),
    [t],
  );
  const endpoint =
    section === "assignments"
      ? `${principalType}/${encodeURIComponent(principal)}/assignments`
      : section;
  const load = useCallback(async () => {
    const revision = ++version.current;
    setLoading(true);
    setError(null);
    try {
      if (section === "assignments" && !principal) {
        setItems([]);
        setTotal(0);
        return;
      }
      const value = await api.administration(
        "GET",
        tenant,
        endpoint,
        undefined,
        {
          pageNumber: page,
          pageSize: size,
          ...(section === "audit" ? { tenantGuid: tenant } : {}),
        },
      );
      if (!mounted.current || revision !== version.current) return;
      const all = records(value);
      setItems(
        Array.isArray(value) ? all.slice((page - 1) * size, page * size) : all,
      );
      setTotal(
        Array.isArray(value)
          ? all.length
          : Number(
              (value as { TotalRecords?: number }).TotalRecords ?? all.length,
            ),
      );
    } catch (err) {
      if (mounted.current && revision === version.current) {
        setItems([]);
        setTotal(0);
        setError(notify(err));
      }
    } finally {
      if (mounted.current && revision === version.current) setLoading(false);
    }
  }, [api, tenant, endpoint, section, page, size, principal, notify]);
  useEffect(() => {
    mounted.current = true;
    void load();
    return () => {
      mounted.current = false;
    };
  }, [load]);
  useEffect(() => {
    if (section !== "assignments") return;
    let active = true;
    setPrincipal("");
    setPrincipals([]);
    void (async () => {
      const all: SecurityRecord[] = [];
      for (let pageNumber = 1; ; pageNumber++) {
        const value = await api.administration(
          "GET",
          tenant,
          principalType,
          undefined,
          { pageSize: 1000, pageNumber },
        );
        all.push(...records(value));
        if (
          (value as { EndOfResults: boolean }).EndOfResults ||
          !records(value).length
        )
          break;
      }
      if (active) {
        setPrincipals(all);
        setPrincipal(all[0]?.Guid ?? "");
      }
    })().catch((err) => {
      if (active) setError(notify(err));
    });
    return () => {
      active = false;
    };
  }, [api, tenant, section, principalType, notify]);
  const showDetail = async (kind: Dialog["kind"], row: SecurityRecord) => {
    const revision = ++detailVersion.current;
    setDetail([]);
    setDetailError(null);
    setDialog({ kind, row });
    if (kind !== "effective" && kind !== "mappings") return;
    setDetailLoading(true);
    try {
      const value = await api.administration(
        "GET",
        tenant,
        `${section}/${encodeURIComponent(row.Guid)}/permissions`,
      );
      if (mounted.current && revision === detailVersion.current)
        setDetail(
          kind === "effective"
            ? ((value as { permissions: SecurityRecord[] }).permissions ?? [])
            : records(value),
        );
    } catch (err) {
      if (mounted.current && revision === detailVersion.current)
        setDetailError(notify(err));
    } finally {
      if (mounted.current && revision === detailVersion.current)
        setDetailLoading(false);
    }
  };
  const remove = async () => {
    if (!confirm || busy) return;
    setBusy(true);
    setError(null);
    try {
      let path = `${section}/${encodeURIComponent(confirm.Guid)}`;
      if (section === "tenants")
        path = `tenants/${encodeURIComponent(confirm.Guid)}`;
      if (section === "assignments")
        path =
          principalType === "users"
            ? `assignments/${encodeURIComponent(confirm.Guid)}`
            : `credentials/${encodeURIComponent(principal)}/assignments/${encodeURIComponent(confirm.Guid)}`;
      await api.administration("DELETE", tenant, path);
      if (!mounted.current) return;
      setNotice(t("admin.removed"));
      setConfirm(null);
      reload();
      if (section !== "tenants") void load();
    } catch (err) {
      if (mounted.current) {
        setError(notify(err));
        setConfirm(null);
      }
    } finally {
      if (mounted.current) setBusy(false);
    }
  };
  const status = (row: SecurityRecord) =>
    row.IsBuiltIn ||
    (!row.TenantGuid && (section === "roles" || section === "permissions")) ? (
      <StatusBadge tone="info">{t("admin.builtIn")}</StatusBadge>
    ) : (
      <StatusBadge tone={row.Active === false ? "neutral" : "success"}>
        {t(row.Active === false ? "admin.inactive" : "admin.active")}
      </StatusBadge>
    );
  const columns: Column<SecurityRecord>[] = [{key:"Guid",header:t("admin.id"),render:r=><CopyableId value={r.Guid} truncateTo={18}/> }];
  if (section === "users")
    columns.push(
      {
        key: "Email",
        header: t("login.email"),
        render: (r) => (
          <div>
            <strong>{String(r.Email)}</strong>
            <div>
              {String(r.FirstName ?? "")} {String(r.LastName ?? "")}
            </div>
          </div>
        ),
      },
      {
        key: "role",
        header: t("admin.accountType"),
        render: (r) =>
          t(
            r.IsAdmin
              ? "admin.platformAdmin"
              : r.IsTenantAdmin
                ? "admin.tenantAdmin"
                : "admin.standardUser",
          ),
      },
    );
  else if (section === "credentials")
    columns.push(
      { key: "Name", header: t("admin.name"), render: (r) => securityName(r) },
      {
        key: "owner",
        header: t("admin.owner"),
        render: (r) => <CopyableId value={String(r.UserGuid ?? "")} />,
      },
      { key:"bearerToken",header:t("admin.bearerToken"),render:r=><CredentialTokenControl key={JSON.stringify({tenant,id:r.Guid,canWrite})} tenant={tenant} id={r.Guid} canManage={canWrite} protectedCredential={r.IsProtected}/> },

    );
  else if (section === "permissions")
    columns.push(
      { key: "Name", header: t("admin.name"), render: (r) => securityName(r) },
      {
        key: "effect",
        header: t("admin.effect"),
        render: (r) => (
          <StatusBadge tone={r.Effect === "Deny" ? "danger" : "success"}>
            {t(`admin.${r.Effect === "Deny" ? "deny" : "permit"}`)}
          </StatusBadge>
        ),
      },
      {
        key: "resources",
        header: t("admin.resources"),
        render: (r) =>
          Array.isArray(r.ResourceTypes) ? r.ResourceTypes.join(", ") : "—",
      },
      {
        key: "operations",
        header: t("admin.operations"),
        render: (r) =>
          Array.isArray(r.OperationTypes) ? r.OperationTypes.join(", ") : "—",
      },
    );
  else if (section === "assignments")
    columns.push(
      {
        key: "Role",
        header: t("admin.role"),
        render: (r) => String(r.RoleName || r.RoleGuid || "—"),
      },
      {
        key: "scope",
        header: t("admin.scope"),
        render: (r) =>
          t(
            r.ResourceScope === "Resource"
              ? "admin.resourceScope"
              : "admin.tenantScope",
          ),
      },
      {
        key: "resource",
        header: t("admin.resourceId"),
        render: (r) => String(r.ResourceGuid || "—"),
      },
      {
        key: "inherit",
        header: t("admin.inherit"),
        render: (r) => t(r.InheritsToChildren ? "admin.yes" : "admin.no"),
      },
    );
  else if (section === "sessions")
    columns.push(
      {
        key: "UserGuid",
        header: t("admin.owner"),
        render: (r) => <CopyableId value={String(r.UserGuid ?? "")} />,
      },
      {
        key: "ExpiresUtc",
        header: t("admin.expires"),
        render: (r) => formatDateTime(String(r.ExpiresUtc), i18n.language),
      },
    );
  else if (section === "audit")
    columns.push(
      {
        key: "EventType",
        header: t("admin.event"),
        render: (r) => String(r.EventType),
      },
      {
        key: "PrincipalGuid",
        header: t("admin.principal"),
        render: (r) => String(r.PrincipalGuid || "—"),
      },
      {
        key: "result",
        header: t("admin.result"),
        render: (r) => (
          <StatusBadge tone={r.AuthzResult ? "success" : "danger"}>
            {t(r.AuthzResult ? "admin.allowed" : "admin.deniedResult")}
          </StatusBadge>
        ),
      },
      {
        key: "Path",
        header: t("admin.path"),
        render: (r) => String(r.Path || "—"),
      },
    );
  else
    columns.push({
      key: "Name",
      header: t("admin.name"),
      render: (r) => <strong>{securityName(r)}</strong>,
    });
  if (section !== "audit")
    columns.push({ key: "status", header: t("admin.status"), render: status });
  columns.push({
    key: "CreatedUtc",
    header: t("admin.created"),
    render: (r) => formatDateTime(String(r.CreatedUtc || ""), i18n.language),
  });
  columns.push({
    key: "actions",
    header: "",
    width: "64px",
    render: (row) => {
      const immutable =
        row.IsProtected ||
        row.IsBuiltIn ||
        (!row.TenantGuid && (section === "roles" || section === "permissions"));
      const ownsAdmin = row.IsAdmin && !access?.isAdmin;
      return (
        <ActionMenu
          items={[
            {
              label: t("admin.details"),
              onClick: () => void showDetail("details", row),
            },
            {
              label: t("admin.viewJson"),
              onClick: () => void showDetail("json", row),
            },
            ...(["users", "credentials"].includes(section) && (access?.isAdmin || access?.isTenantAdmin || access?.capabilities.Admin?.write || section === "users" && row.Guid === access?.userId)
              ? [
                  {
                    label: t("admin.effective"),
                    onClick: () => void showDetail("effective", row),
                  },
                ]
              : []),
            ...(section === "roles"
              ? [
                  {
                    label: t("admin.mappings"),
                    onClick: () => void showDetail("mappings", row),
                  },
                ]
              : []),
            ...(canWrite &&
            !["assignments", "sessions", "audit"].includes(section)
              ? [
                  {
                    label: t("admin.edit"),
                    disabled: Boolean(immutable || ownsAdmin),
                    onClick: () => setDialog({ kind: "edit", row }),
                  },
                ]
              : []),
            ...(canWrite && section === "users"
              ? [
                  {
                    label: t("admin.resetPassword"),
                    disabled: Boolean(
                      (row.IsAdmin || row.IsProtected) && !access?.isAdmin,
                    ),
                    onClick: () => setDialog({ kind: "reset", row }),
                  },
                ]
              : []),
            ...(canWrite && section !== "audit"
              ? [
                  {
                    label: t(
                      section === "sessions" || section === "assignments"
                        ? "admin.revoke"
                        : "admin.delete",
                    ),
                    danger: true,
                    disabled: Boolean(immutable || ownsAdmin),
                    onClick: () => setConfirm(row),
                  },
                ]
              : []),
          ]}
        />
      );
    },
  });
  return (
    <>
      {section === "assignments" && (
        <div className="sa-admin-toolbar">
          <label className="sa-filterbar__field">
            <span>{t("admin.principalType")}</span>
            <select
              aria-label={t("admin.principalType")}
              value={principalType}
              onChange={(e) => {
                setPrincipalType(e.target.value);
                setPrincipal("");
                setItems([]);
                setLoading(true);
                setPage(1);
              }}
            >
              <option value="users">{t("admin.users")}</option>
              <option value="credentials">{t("admin.credentials")}</option>
            </select>
          </label>
          <label className="sa-filterbar__field">
            <span>{t("admin.principal")}</span>
            <select
              aria-label={t("admin.principal")}
              value={principal}
              onChange={(e) => {
                setPrincipal(e.target.value);
                setItems([]);
                setLoading(true);
                setPage(1);
              }}
            >
              <option value="" disabled>
                {t("admin.selectPrincipal")}
              </option>
              {principals.map((row) => (
                <option key={row.Guid} value={row.Guid}>
                  {securityName(row)}
                </option>
              ))}
            </select>
          </label>
        </div>
      )}
      {section === "assignments" && (
        <p className="sa-admin-notice">{t("admin.assignmentHelp")}</p>
      )}
      <div className="sa-admin-toolbar">
        <label className="sa-filterbar__field">
          <span>{t("admin.filterPage")}</span>
          <input value={filter} onChange={(e) => setFilter(e.target.value)} />
        </label>
        <button
          className="sa-btn--icon sa-btn"
          disabled={loading}
          onClick={() => {reload();void load();}}
         aria-label={t('common.refresh')}><Icon name="refresh"/></button>
        {canWrite && !["audit", "sessions"].includes(section) && (
          <button
            className="sa-btn sa-btn--primary"
            disabled={section === "assignments" && !principal}
            onClick={() => setDialog({ kind: "create" })}
          >
            {t(section === "assignments" ? "admin.grant" : "admin.add")}
          </button>
        )}
      </div>
      {notice && (
        <p role="status" className="sa-admin-notice">
          {notice}
        </p>
      )}
      {error && (
        <p role="alert" className="sa-error">
          {error}
        </p>
      )}
      <DataTable
        columns={columns}
        rows={items.filter((row) =>
          JSON.stringify(safeRecord(row))
            .toLocaleLowerCase()
            .includes(filter.toLocaleLowerCase()),
        )}
        rowKey={(r) => r.Guid}
        loading={loading}
        error={error}
        emptyMessage={t("admin.empty")}
        pagination={{
          pageNumber: page,
          pageSize: size,
          totalRecords: total,
          onPageChange: (p) => {
            setPage(p);
            setFilter("");
          },
          onPageSizeChange: (s) => {
            setSize(s);
            setPage(1);
            setFilter("");
          },
        }}
      />
      {dialog && ["create", "edit", "reset"].includes(dialog.kind) && (
        <AdministrationForm
          section={section}
          kind={dialog.kind as "create" | "edit" | "reset"}
          row={dialog.row}
          principalType={principalType}
          principal={principal}
          onClose={() => setDialog(null)}
          onSaved={(result) => {
            setNotice(t("admin.saved"));
            if (dialog.kind === "reset" && dialog.row?.Guid === access?.userId)
              void logout();
            setDialog(null);
            if (section === "credentials" && dialog.kind === "create")
              setSecret(result as Record<string, string>);
            reload();
            if (section !== "tenants") void load();
          }}
        />
      )}
      <Modal
        open={Boolean(
          dialog &&
          ["details", "json", "effective", "mappings"].includes(dialog.kind),
        )}
        title={t(
          dialog?.kind === "effective"
            ? "admin.effective"
            : dialog?.kind === "mappings"
              ? "admin.mappings"
              : "admin.details",
        )}
        onClose={() => setDialog(null)}
      >
        {dialog?.row && (
          <>
            {dialog.kind === "json" ? (
              <JsonViewer value={safeRecord(dialog.row)} />
            ) : dialog.kind === "details" ? (
              <dl className="sa-admin-details">
                {Object.entries(safeRecord(dialog.row)).map(([key, value]) => (
                  <Fragment key={key}>
                    <dt>{t(`fields.${key}`, { defaultValue: key })}</dt>
                    <dd>
                      {typeof value === "boolean"
                        ? t(value ? "admin.yes" : "admin.no")
                        : Array.isArray(value)
                          ? value.join(", ")
                          : String(value ?? "—")}
                    </dd>
                  </Fragment>
                ))}
              </dl>
            ) : (
              <>
                {dialog.kind === "effective" && <>
                  {(dialog.row.IsAdmin || dialog.row.IsTenantAdmin) && <StatusBadge tone="info">{t(dialog.row.IsAdmin ? "admin.platformAdmin" : "admin.tenantAdmin")}</StatusBadge>}
                  <p className="sa-admin-notice">{t("admin.effectiveHelp")}</p>
                </>}
                {detailLoading && <p role="status">{t("text.loading")}</p>}
                {detailError && (
                  <p role="alert" className="sa-error">
                    {detailError}
                  </p>
                )}
                {dialog.kind === "mappings" ? (
                  <RoleMappings
                    row={dialog.row}
                    items={detail}
                    canWrite={
                      canWrite &&
                      !dialog.row.IsBuiltIn &&
                      !dialog.row.IsProtected
                    }
                    onChange={() => {reload();void showDetail("mappings", dialog.row!);}}
                  />
                ) : (
                  <DataTable
                    columns={[
                      "resourceType",
                      "operation",
                      "effect",
                      "scope",
                      "resourceGuid",
                      "inheritsToChildren",
                    ].map((key) => ({
                      key,
                      header: t(`admin.effectiveFields.${key}`),
                      render: (r: SecurityRecord) => String(r[key] ?? "—"),
                    }))}
                    rows={detail}
                    rowKey={(r) => JSON.stringify(r)}
                    emptyMessage={t("admin.noPermissions")}
                  />
                )}
              </>
            )}
          </>
        )}
      </Modal>
      <Modal
        open={Boolean(secret)}
        title={t("admin.credentialCreated")}
        onClose={() => setSecret(null)}
        footer={
          <button
            className="sa-btn sa-btn--primary"
            onClick={() => setSecret(null)}
          >
            {t("admin.savedSecret")}
          </button>
        }
      >
        <p className="sa-admin-notice">{t("admin.secretOnce")}</p>
        <div className="sa-admin-secret">
          <p>{t("admin.bearerToken")}</p>
          <code>{secret?.token}</code>
          <CopyButton value={secret?.token ?? ""} label={t("admin.copyBearerToken")} />
        </div>
      </Modal>
      <Modal
        open={Boolean(confirm)}
        title={t(
          section === "sessions" || section === "assignments"
            ? "admin.revoke"
            : "admin.delete",
        )}
        onClose={() => {
          if (!busy) setConfirm(null);
        }}
        footer={
          <>
            <button
              className="sa-btn"
              disabled={busy}
              onClick={() => setConfirm(null)}
            >
              {t("common.cancel")}
            </button>
            <button
              className="sa-btn sa-btn--danger"
              disabled={busy}
              onClick={() => void remove()}
            >
              {t(busy ? "admin.saving" : "common.confirm")}
            </button>
          </>
        }
      >
        <p>
          {t("admin.deleteConfirm", {
            name: confirm ? securityName(confirm) : "",
          })}
        </p>
        <p>
          {t(
            section === "users"
              ? "admin.deleteUserHelp"
              : section === "tenants"
                ? "admin.deleteTenantHelp"
                : "admin.irreversible",
          )}
        </p>
      </Modal>
    </>
  );
}

function RoleMappings({
  row,
  items,
  canWrite,
  onChange,
}: {
  row: SecurityRecord;
  items: SecurityRecord[];
  canWrite: boolean;
  onChange: () => void;
}): JSX.Element {
  const { t } = useTranslation();
  const api = useApi();
  const { tenant } = useAdministration();
  const [available, setAvailable] = useState<SecurityRecord[]>([]);
  const [selection, setSelection] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (!canWrite) return;
    let active = true;
    void (async () => {
      const all: SecurityRecord[] = [];
      for (let page = 1; ; page++) {
        const result = await api.administration(
          "GET",
          tenant,
          "permissions",
          undefined,
          { pageSize: 1000, pageNumber: page },
        );
        all.push(...records(result));
        if (
          (result as { EndOfResults: boolean }).EndOfResults ||
          !records(result).length
        )
          break;
      }
      if (active) setAvailable(all);
    })().catch((err) => {
      if (active) setError(String(err.message));
    });
    return () => {
      active = false;
    };
  }, [api, tenant, canWrite]);
  const mutate = async (method: string, id: string) => {
    setBusy(true);
    setError(null);
    try {
      await api.administration(
        method,
        tenant,
        `roles/${encodeURIComponent(row.Guid)}/permissions/${encodeURIComponent(id)}`,
      );
      setSelection("");
      onChange();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  };
  return (
    <>
      <p className="sa-admin-notice">{t("admin.roleHelp")}</p>
      {error && (
        <p role="alert" className="sa-error">
          {error}
        </p>
      )}
      {canWrite && (
        <div className="sa-admin-toolbar">
          <label className="sa-filterbar__field">
            <span>{t("admin.permission")}</span>
            <select
              aria-label={t("admin.permission")}
              value={selection}
              onChange={(e) => setSelection(e.target.value)}
            >
              <option value="">{t("admin.selectPermission")}</option>
              {available
                .filter(
                  (p) =>
                    p.Active !== false && !items.some((i) => i.Guid === p.Guid),
                )
                .map((p) => (
                  <option key={p.Guid} value={p.Guid}>
                    {securityName(p)}
                  </option>
                ))}
            </select>
          </label>
          <button
            className="sa-btn sa-btn--primary"
            disabled={busy || !selection}
            onClick={() => void mutate("POST", selection)}
          >
            {t("admin.map")}
          </button>
        </div>
      )}
      <DataTable
        columns={[
          {
            key: "Guid",
            header: t("admin.id"),
            render: (r) => <CopyableId value={r.Guid} truncateTo={18} />,
          },
          {
            key: "Name",
            header: t("admin.name"),
            render: (r) => securityName(r),
          },
          {
            key: "Effect",
            header: t("admin.effect"),
            render: (r) => String(r.Effect),
          },
          {
            key: "operations",
            header: t("admin.operations"),
            render: (r) =>
              Array.isArray(r.OperationTypes)
                ? r.OperationTypes.join(", ")
                : "—",
          },
          ...(canWrite
            ? [
                {
                  key: "actions",
                  header: "",
                  render: (r: SecurityRecord) => (
                    <button
                      className="sa-btn"
                      disabled={busy}
                      onClick={() => void mutate("DELETE", r.Guid)}
                    >
                      {t("admin.unmap")}
                    </button>
                  ),
                },
              ]
            : []),
        ]}
        rows={items}
        rowKey={(r) => r.Guid}
        emptyMessage={t("admin.noPermissions")}
      />
    </>
  );
}

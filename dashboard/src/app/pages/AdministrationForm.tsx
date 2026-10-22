import { useEffect, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import type {
  AdministrationSection,
  SecurityRecord,
} from "../../api/administration";
import { records, securityName } from "../../api/administration";
import { Modal } from "../../components/ui";
import { useApi } from "../ApiProvider";
import { useAdministration } from "../AdministrationProvider";

const RESOURCES = [
  "All",
  "Tenant",
  "User",
  "Credential",
  "Session",
  "Role",
  "Permission",
  "Assignment",
  "Audit",
  "Admin",
  "Model",
  "Inference",
  "Settings",
  "RequestHistory",
  "SystemPrompt",
  "ModelConfiguration",
];
const OPERATIONS = [
  "All",
  "Read",
  "Write",
  "Create",
  "Update",
  "Delete",
  "Execute",
  "Admin",
];
interface Props {
  section: AdministrationSection;
  kind: "create" | "edit" | "reset";
  row?: SecurityRecord;
  principalType: string;
  principal: string;
  onClose: () => void;
  onSaved: (result: unknown) => void;
}
/** Purpose-built security forms. Passwords are write-only; no raw security record is submitted. */
export function AdministrationForm({
  section,
  kind,
  row,
  principalType,
  principal,
  onClose,
  onSaved,
}: Props): JSX.Element {
  const { t } = useTranslation();
  const api = useApi();
  const { tenant, access } = useAdministration();
  const [name, setName] = useState(String(row?.Name ?? ""));
  const [email, setEmail] = useState(String(row?.Email ?? ""));
  const [first, setFirst] = useState(String(row?.FirstName ?? ""));
  const [last, setLast] = useState(String(row?.LastName ?? ""));
  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [active, setActive] = useState(row?.Active !== false);
  const [platform, setPlatform] = useState(Boolean(row?.IsAdmin));
  const [tenantAdmin, setTenantAdmin] = useState(Boolean(row?.IsTenantAdmin));
  const [owner, setOwner] = useState("");
  const [choices, setChoices] = useState<SecurityRecord[]>([]);
  const [resources, setResources] = useState<string[]>(
    Array.isArray(row?.ResourceTypes) ? row.ResourceTypes : [],
  );
  const [operations, setOperations] = useState<string[]>(
    Array.isArray(row?.OperationTypes) ? row.OperationTypes : [],
  );
  const [effect, setEffect] = useState(String(row?.Effect ?? "Permit"));
  const [scope, setScope] = useState("Tenant");
  const [resource, setResource] = useState("");
  const [inherit, setInherit] = useState(true);
  const [expires, setExpires] = useState(
    row?.ExpiresUtc
      ? new Date(String(row.ExpiresUtc)).toISOString().slice(0, 16)
      : "",
  );
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  useEffect(() => {
    const endpoint =
      section === "assignments"
        ? "roles"
        : section === "credentials" && kind === "create"
          ? "users"
          : null;
    if (!endpoint) return;
    let current = true;
    setLoading(true);
    void (async () => {
      const all: SecurityRecord[] = [];
      for (let pageNumber = 1; ; pageNumber++) {
        const value = await api.administration(
          "GET",
          tenant,
          endpoint,
          undefined,
          { pageNumber, pageSize: 1000 },
        );
        all.push(...records(value));
        if (
          (value as { EndOfResults: boolean }).EndOfResults ||
          !records(value).length
        )
          break;
      }
      if (current) {
        setChoices(all.filter((r) => r.Active !== false));
        setOwner(all.find((r) => r.Active !== false)?.Guid ?? "");
      }
    })()
      .catch((err) => {
        if (current) setError(err instanceof Error ? err.message : String(err));
      })
      .finally(() => {
        if (current) setLoading(false);
      });
    return () => {
      current = false;
    };
  }, [api, tenant, section, kind]);
  const toggle = (list: string[], value: string) =>
    list.includes(value)
      ? list.filter((v) => v !== value)
      : [...list.filter((v) => v !== "All"), value];
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (busy) return;
    if (
      (kind === "reset" || (section === "users" && kind === "create")) &&
      password !== confirmation
    ) {
      setError(t("admin.passwordMismatch"));
      return;
    }
    if (
      section === "permissions" &&
      (!resources.length || !operations.length)
    ) {
      setError(t("admin.permissionRequired"));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      let path =
        kind === "edit"
          ? `${section}/${encodeURIComponent(row!.Guid)}`
          : section;
      let body: Record<string, unknown> = {
        Name: name.trim(),
        ...(kind === "edit" ? { Active: active } : {}),
      };
      if (section === "users")
        body = {
          Email: email.trim(),
          FirstName: first.trim(),
          LastName: last.trim(),
          IsTenantAdmin: tenantAdmin,
          ...(access?.isAdmin ? { IsAdmin: platform } : {}),
          ...(kind === "create" ? { Password: password } : { Active: active }),
        };
      if (kind === "reset") {
        path = `users/${encodeURIComponent(row!.Guid)}/password`;
        body = { Password: password };
      }
      if (section === "credentials")
        body = {
          Name: name.trim(),
          ...(kind === "create" ? { UserGuid: owner } : { Active: active }),
          ...(expires
            ? { ExpiresUtc: new Date(expires + "Z").toISOString() }
            : { ClearExpiration: true }),
        };
      if (section === "permissions")
        body = {
          Name: name.trim(),
          ResourceTypes: resources,
          OperationTypes: operations,
          Effect: effect,
          Active: active,
        };
      if (section === "assignments") {
        path =
          principalType === "users"
            ? "assignments"
            : `credentials/${encodeURIComponent(principal)}/assignments`;
        body = {
          ...(principalType === "users" ? { UserGuid: principal } : {}),
          RoleGuid: owner,
          ResourceScope: scope,
          ...(scope === "Resource" ? { ResourceGuid: resource.trim() } : {}),
          InheritsToChildren: scope === "Tenant" && inherit,
        };
      }
      const result = await api.administration(
        kind === "create" ? "POST" : "PUT",
        tenant,
        path,
        body,
      );
      setPassword("");
      setConfirmation("");
      onSaved(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  };
  const field = (
    label: string,
    value: string,
    set: (v: string) => void,
    type = "text",
    required = false,
  ) => (
    <label className="sa-filterbar__field">
      <span>{t(label)}</span>
      <input
        type={type}
        value={value}
        required={required}
        disabled={busy}
        onChange={(e) => set(e.target.value)}
        autoComplete={type === "password" ? "new-password" : "off"}
      />
    </label>
  );
  return (
    <Modal
      open
      title={t(
        kind === "reset"
          ? "admin.resetPassword"
          : kind === "edit"
            ? "admin.edit"
            : "admin.add",
      )}
      onClose={() => {
        if (!busy) onClose();
      }}
      footer={
        <>
          <button className="sa-btn" disabled={busy} onClick={onClose}>
            {t("common.cancel")}
          </button>
          <button
            form="sa-administration-form"
            type="submit"
            className="sa-btn sa-btn--primary"
            disabled={
              busy ||
              loading ||
              ((section === "assignments" ||
                (section === "credentials" && kind === "create")) &&
                !owner)
            }
          >
            {t(busy ? "admin.saving" : "admin.save")}
          </button>
        </>
      }
    >
      <form
        id="sa-administration-form"
        className="sa-admin-form"
        onSubmit={(event) => void submit(event)}
      >
        {error && (
          <p className="sa-error sa-wide" role="alert">
            {error}
          </p>
        )}
        {loading && <p role="status">{t("text.loading")}</p>}
        {kind === "reset" ? (
          <p className="sa-admin-notice sa-wide">{t("admin.resetHelp")}</p>
        ) : section === "users" ? (
          <>
            {field("admin.firstName", first, setFirst)}
            {field("admin.lastName", last, setLast)}
            <div className="sa-wide">
              {field("login.email", email, setEmail, "email", true)}
            </div>
            <label>
              <input
                type="checkbox"
                checked={tenantAdmin}
                disabled={busy}
                onChange={(e) => setTenantAdmin(e.target.checked)}
              />{" "}
              {t("admin.tenantAdmin")}
            </label>
            {access?.isAdmin && (
              <label>
                <input
                  type="checkbox"
                  checked={platform}
                  disabled={busy}
                  onChange={(e) => setPlatform(e.target.checked)}
                />{" "}
                {t("admin.platformAdmin")}
              </label>
            )}
          </>
        ) : section === "assignments" ? (
          <>
            <label className="sa-filterbar__field sa-wide">
              <span>{t("admin.role")}</span>
              <select
                aria-label={t("admin.role")}
                required
                value={owner}
                disabled={busy || loading}
                onChange={(e) => setOwner(e.target.value)}
              >
                {choices.map((role) => (
                  <option key={role.Guid} value={role.Guid}>
                    {securityName(role)}
                  </option>
                ))}
              </select>
            </label>
            <label className="sa-filterbar__field sa-wide">
              <span>{t("admin.scope")}</span>
              <select
                aria-label={t("admin.scope")}
                value={scope}
                disabled={busy}
                onChange={(e) => setScope(e.target.value)}
              >
                <option value="Tenant">{t("admin.tenantScope")}</option>
                <option value="Resource">{t("admin.resourceScope")}</option>
              </select>
            </label>
            {scope === "Resource" ? (
              <div className="sa-wide">
                {field("admin.resourceId", resource, setResource, "text", true)}
              </div>
            ) : (
              <label className="sa-wide">
                <input
                  type="checkbox"
                  checked={inherit}
                  disabled={busy}
                  onChange={(e) => setInherit(e.target.checked)}
                />{" "}
                {t("admin.inherit")}
              </label>
            )}
            <p className="sa-admin-notice sa-wide">
              {t("admin.assignmentHelp")}
            </p>
          </>
        ) : (
          <div className="sa-wide">
            {field("admin.name", name, setName, "text", true)}
          </div>
        )}
        {(kind === "reset" || (section === "users" && kind === "create")) && (
          <>
            {field("login.password", password, setPassword, "password", true)}
            {field(
              "admin.confirmPassword",
              confirmation,
              setConfirmation,
              "password",
              true,
            )}
          </>
        )}
        {section === "credentials" && (
          <>
            {kind === "create" && (
              <label className="sa-filterbar__field sa-wide">
                <span>{t("admin.owner")}</span>
                <select
                  aria-label={t("admin.owner")}
                  required
                  value={owner}
                  disabled={busy || loading}
                  onChange={(e) => setOwner(e.target.value)}
                >
                  {choices.map((user) => (
                    <option key={user.Guid} value={user.Guid}>
                      {securityName(user)}
                    </option>
                  ))}
                </select>
              </label>
            )}
            <div className="sa-wide">
              {field("admin.expiresUtc", expires, setExpires, "datetime-local")}
            </div>
            <p className="sa-admin-notice sa-wide">
              {t("admin.credentialHelp")}
            </p>
          </>
        )}
        {section === "permissions" && (
          <>
            <label className="sa-filterbar__field sa-wide">
              <span>{t("admin.effect")}</span>
              <select
                aria-label={t("admin.effect")}
                value={effect}
                disabled={busy}
                onChange={(e) => setEffect(e.target.value)}
              >
                <option value="Permit">{t("admin.permit")}</option>
                <option value="Deny">{t("admin.deny")}</option>
              </select>
            </label>
            <fieldset className="sa-wide">
              <legend>{t("admin.resources")}</legend>
              <div className="sa-admin-checks">
                {RESOURCES.map((value) => (
                  <label key={value}>
                    <input
                      type="checkbox"
                      disabled={busy}
                      checked={resources.includes(value)}
                      onChange={() =>
                        setResources(
                          value === "All"
                            ? resources.includes(value)
                              ? []
                              : ["All"]
                            : toggle(resources, value),
                        )
                      }
                    />{" "}
                    {t(`admin.resourceTypes.${value}`)}
                  </label>
                ))}
              </div>
            </fieldset>
            <fieldset className="sa-wide">
              <legend>{t("admin.operations")}</legend>
              <div className="sa-admin-checks">
                {OPERATIONS.map((value) => (
                  <label key={value}>
                    <input
                      type="checkbox"
                      disabled={busy}
                      checked={operations.includes(value)}
                      onChange={() =>
                        setOperations(
                          value === "All"
                            ? operations.includes(value)
                              ? []
                              : ["All"]
                            : toggle(operations, value),
                        )
                      }
                    />{" "}
                    {t(`admin.operationTypes.${value}`)}
                  </label>
                ))}
              </div>
            </fieldset>
            <p className="sa-admin-notice sa-wide">{t("admin.denyHelp")}</p>
          </>
        )}
        {kind === "edit" && (
          <label className="sa-wide">
            <input
              type="checkbox"
              checked={active}
              disabled={busy}
              onChange={(e) => setActive(e.target.checked)}
            />{" "}
            {t("admin.active")}
          </label>
        )}
      </form>
    </Modal>
  );
}

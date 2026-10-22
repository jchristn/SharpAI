import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { useApi } from "./ApiProvider";
import { useAuth } from "./AuthProvider";
import type {
  AdministrationContext,
  SecurityRecord,
} from "../api/administration";
import { records } from "../api/administration";

interface State {
  access: AdministrationContext | null;
  tenants: SecurityRecord[];
  tenant: string;
  selectTenant: (id: string) => void;
  reload: () => void;
  loading: boolean;
  error: string | null;
}
const Context = createContext<State | null>(null);
/** Selection scopes administration requests without changing the authenticated principal or tenant. */
export function AdministrationProvider({
  children,
}: {
  children: ReactNode;
}): JSX.Element {
  const api = useApi();
  const { session } = useAuth();
  const [state, setState] = useState<
    Omit<State, "selectTenant" | "reload"> & { owner?: typeof api }
  >({ access: null, tenants: [], tenant: "", loading: true, error: null });
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    void (async () => {
      try {
        const access = await api.administrationContext();
        let tenants: SecurityRecord[] = [];
        if (access.isAdmin) {
          for (let pageNumber = 1; ; pageNumber++) {
            const page = await api.listTenants({ pageNumber, pageSize: 1000 });
            tenants.push(...records(page));
            if (page.EndOfResults || !page.Objects?.length) break;
          }
        } else if (access.tenantId) {
          tenants = [
            {
              Guid: access.tenantId,
              Name: access.tenantName ?? access.tenantId,
            },
          ];
        }
        let remembered="";
        try {remembered=window.sessionStorage.getItem(`sharpai.admin.tenant.${access.userId??"platform"}`)??"";} catch { /* optional persistence */ }
        if (active)
          setState((prev) => ({
            owner: api,
            access,
            tenants,
            tenant: tenants.some((t) => t.Guid === (prev.owner===api?prev.tenant:remembered))
              ? (prev.owner===api?prev.tenant:remembered)
              : (tenants.find((t) => t.Guid === access.tenantId)?.Guid ??
                tenants[0]?.Guid ??
                ""),
            loading: false,
            error: null,
          }));
      } catch (error) {
        if (active)
          setState({
            owner: api,
            access: null,
            tenants: [],
            tenant: "",
            loading: false,
            error: error instanceof Error ? error.message : String(error),
          });
      }
    })();
    return () => {
      active = false;
    };
  }, [api, session, revision]);
  useEffect(()=>{
    if(state.owner!==api || !state.access || !state.tenant)return;
    try {window.sessionStorage.setItem(`sharpai.admin.tenant.${state.access.userId??"platform"}`,state.tenant);} catch { /* optional persistence */ }
  },[api,state]);
  const visible =
    state.owner === api
      ? state
      : { access: null, tenants: [], tenant: "", loading: true, error: null };
  return (
    <Context.Provider
      value={{
        ...visible,
        selectTenant: (tenant) => setState((prev) => ({ ...prev, tenant })),
        reload: () => setRevision((r) => r + 1),
      }}
    >
      {children}
    </Context.Provider>
  );
}
export function useAdministration(): State {
  const value = useContext(Context);
  if (!value) throw new Error("AdministrationProvider missing");
  return value;
}

import type { AdministrationSection } from '../api/administration';
import '../theme/tokens.css';
import { ControlHelp } from './ControlHelp';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthenticationGate } from './AuthenticationGate';
import { AuthProvider } from './AuthProvider';
import { AppShell } from './AppShell';
import type { NavItem } from './AppShell';
import { ApiExplorerView } from './pages/ApiExplorerView';
import { HomeView } from './pages/HomeView';
import { ModelsView } from './pages/ModelsView';
import { ObservabilityView } from './pages/ObservabilityView';
import { PlaygroundView } from './pages/PlaygroundView';
import { RequestHistoryView } from './pages/RequestHistoryView';
import { AdministrationProvider } from './AdministrationProvider';
import { AccessView } from './pages/AccessView';
import { LibraryView } from './pages/LibraryView';
import { SettingsView } from './pages/SettingsView';

const NAV: NavItem[] = [
  { path: '/', labelKey: 'nav.overview', group: 'overview' },
  { path: '/models', labelKey: 'nav.models', group: 'models' },
  {path:'/system-prompts',labelKey:'library.promptsTitle',group:'models'},
  {path:'/model-configurations',labelKey:'library.configurationsTitle',group:'models'},
  { path: '/playground', labelKey: 'nav.playground', group: 'models' },
  { path: '/request-history', labelKey: 'nav.requestHistory', group: 'operations' },
  { path: '/api-explorer', labelKey: 'nav.apiExplorer', group: 'operations' },
  { path: '/observability', labelKey: 'nav.observability', group: 'operations' },
  {path:'/access',labelKey:'nav.access',group:'system'},
  { path: '/settings', labelKey: 'nav.settings', group: 'system' },
];

export interface NewAppProps {
  baseUrl?: string;
  version?: string;
}

/**
 * Root of the rebuilt, standards-aligned dashboard: AuthProvider (session + token-bound ApiClient) inside a
 * React Router 7 BrowserRouter, with the AppShell hosting the routed views. Deep links and browser
 * back/forward work; the nginx SPA fallback serves index.html for client-side routes.
 */
export function NewApp({ baseUrl, version = '4.0.1' }: NewAppProps): JSX.Element {
  const resolvedBaseUrl = baseUrl ?? (typeof window !== 'undefined' ? window.location.origin : 'http://127.0.0.1:8000');

  return (
    <AuthProvider baseUrl={resolvedBaseUrl}>
      <ControlHelp/><AuthenticationGate><AdministrationProvider><BrowserRouter>
        <AppShell nav={NAV} serverUrl={resolvedBaseUrl} version={version}>
          <Routes>
            <Route path="/" element={<HomeView />} />
            <Route path="/models" element={<ModelsView />} />
            <Route path="/system-prompts" element={<LibraryView kind="system-prompts"/>}/>
            <Route path="/model-configurations" element={<LibraryView kind="model-configurations"/>}/>
            <Route path="/playground" element={<PlaygroundView />} />
            <Route path="/request-history" element={<RequestHistoryView />} />
            <Route path="/request-history/:section" element={<RequestHistoryView />} />
            <Route path="/inference-history" element={<Navigate to="/request-history/inference" replace/>} />
            <Route path="/embeddings-history" element={<Navigate to="/request-history/embeddings" replace/>} />
            <Route path="/api-explorer" element={<ApiExplorerView />} />
            <Route path="/observability" element={<ObservabilityView />} />
            {(['tenants','users','credentials','roles','permissions','assignments','sessions','audit'] satisfies AdministrationSection[]).map(section=><Route key={section} path={`/${section}`} element={<AccessView section={section}/>}/>)}
            <Route path="/access" element={<AccessView/>}/>
            {(['tenants','users','credentials','roles','permissions','assignments','sessions','audit'] satisfies AdministrationSection[]).map(section=><Route key={section} path={`/access/${section}`} element={<AccessView section={section}/>}/>)}
            <Route path="/settings" element={<SettingsView />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </AppShell>
      </BrowserRouter></AdministrationProvider></AuthenticationGate>
    </AuthProvider>
  );
}

import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { StatusBadge } from '../components/ui';
import { useApi } from './ApiProvider';
import { useAuth } from './AuthProvider';
import { LanguageSelector } from './LanguageSelector';
import { LoginModal } from './LoginModal';
import { useTheme } from './useTheme';
import './app.css';

export interface NavItem {
  path: string;
  labelKey: string;
  group: string;
}

export interface AppShellProps {
  nav: NavItem[];
  serverUrl: string;
  version: string;
  children: ReactNode;
}

/**
 * The authenticated dashboard shell: grouped sidebar navigation (real URLs via React Router), a topbar
 * carrying the server URL, version, a live health indicator, sign-in/identity, a language selector, and a
 * theme toggle. All chrome text is localized through the i18n layer.
 */
export function AppShell({ nav, serverUrl, version, children }: AppShellProps): JSX.Element {
  const { t } = useTranslation();
  const api = useApi();
  const { theme, toggle } = useTheme();
  const { session, isAuthenticated, logout } = useAuth();
  const [healthy, setHealthy] = useState<boolean | null>(null);
  const [loginOpen, setLoginOpen] = useState<boolean>(false);

  useEffect(() => {
    let cancelled = false;
    api
      .health()
      .then(() => {
        if (!cancelled) setHealthy(true);
      })
      .catch(() => {
        if (!cancelled) setHealthy(false);
      });
    return () => {
      cancelled = true;
    };
  }, [api]);

  const groups: string[] = [];
  for (const item of nav) {
    if (!groups.includes(item.group)) groups.push(item.group);
  }

  return (
    <div className="sa-app">
      <aside className="sa-sidebar">
        <div className="sa-sidebar__brand">{t('app.brand')}</div>
        {groups.map((group) => (
          <div key={group}>
            <div className="sa-sidebar__group-label">{t(`groups.${group}`)}</div>
            {nav
              .filter((item) => item.group === group)
              .map((item) => (
                <NavLink
                  key={item.path}
                  to={item.path}
                  end={item.path === '/'}
                  className={({ isActive }) => `sa-nav-item ${isActive ? 'sa-nav-item--active' : ''}`}
                >
                  {t(item.labelKey)}
                </NavLink>
              ))}
          </div>
        ))}
      </aside>

      <header className="sa-topbar">
        <div className="sa-topbar__meta">
          <span className="sa-topbar__url">{serverUrl}</span>
          <span>v{version}</span>
          {healthy === null ? (
            <StatusBadge tone="neutral" dot>
              {t('topbar.checking')}
            </StatusBadge>
          ) : healthy ? (
            <StatusBadge tone="success" dot>
              {t('topbar.healthy')}
            </StatusBadge>
          ) : (
            <StatusBadge tone="danger" dot>
              {t('topbar.unreachable')}
            </StatusBadge>
          )}
        </div>
        <div className="sa-topbar__actions">
          {isAuthenticated ? (
            <>
              <span className="sa-topbar__url" title={session?.userId}>
                {session?.userId}
              </span>
              <button type="button" className="sa-btn sa-btn--ghost" onClick={() => void logout()}>
                {t('topbar.signOut')}
              </button>
            </>
          ) : (
            <button type="button" className="sa-btn sa-btn--ghost" onClick={() => setLoginOpen(true)}>
              {t('topbar.signIn')}
            </button>
          )}
          <LanguageSelector />
          <a className="sa-btn sa-btn--ghost" href="https://github.com/jchristn/SharpAI" target="_blank" rel="noreferrer">
            {t('topbar.repo')}
          </a>
          <a
            className="sa-btn sa-btn--ghost sa-btn--icon"
            href="https://discord.gg/tRAN8HgvK5"
            target="_blank"
            rel="noreferrer"
            aria-label={t('topbar.discord')}
            title={t('topbar.discord')}
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" focusable="false">
              <path d="M20.317 4.369a19.79 19.79 0 0 0-4.885-1.515a.074.074 0 0 0-.079.037c-.211.375-.444.865-.608 1.25a18.27 18.27 0 0 0-5.487 0a12.6 12.6 0 0 0-.617-1.25a.077.077 0 0 0-.079-.037A19.74 19.74 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057a19.9 19.9 0 0 0 5.993 3.03a.078.078 0 0 0 .084-.028c.462-.63.874-1.295 1.226-1.994a.076.076 0 0 0-.041-.106a13.1 13.1 0 0 1-1.872-.892a.077.077 0 0 1-.008-.128c.126-.094.252-.192.372-.291a.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.009c.12.099.246.198.373.292a.077.077 0 0 1-.006.127a12.3 12.3 0 0 1-1.873.891a.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028a19.84 19.84 0 0 0 6.002-3.03a.077.077 0 0 0 .032-.056c.5-5.177-.838-9.674-3.549-13.66a.06.06 0 0 0-.031-.028M8.02 15.331c-1.183 0-2.157-1.086-2.157-2.42s.955-2.42 2.157-2.42c1.211 0 2.176 1.096 2.157 2.42c0 1.334-.955 2.42-2.157 2.42m7.975 0c-1.183 0-2.157-1.086-2.157-2.42s.955-2.42 2.157-2.42c1.211 0 2.176 1.096 2.157 2.42c0 1.334-.946 2.42-2.157 2.42" />
            </svg>
          </a>
          <button type="button" className="sa-btn sa-btn--ghost" onClick={toggle} aria-label={t('topbar.toggleTheme')}>
            {theme === 'dark' ? t('topbar.themeLight') : t('topbar.themeDark')}
          </button>
        </div>
      </header>

      <main className="sa-content">{children}</main>

      <LoginModal open={loginOpen} onClose={() => setLoginOpen(false)} />
    </div>
  );
}

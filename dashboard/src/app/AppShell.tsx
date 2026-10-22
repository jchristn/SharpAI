import { HeaderIcon } from './HeaderIcon';
import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { StatusBadge } from '../components/ui';
import { useApi } from './ApiProvider';
import { useAuth } from './AuthProvider';
import { LanguageSelector } from './LanguageSelector';
import { LoginModal } from './LoginModal';
import { useTheme } from './useTheme';
import './app.css';
import { Icon } from './Icon';
import { useAdministration } from './AdministrationProvider';
import { administrationResources } from '../api/administration';

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
  const location = useLocation();
  const content = useRef<HTMLElement>(null);
  useEffect(() => { if (content.current) content.current.scrollTop = 0; }, [location.pathname]);
  useEffect(() => { document.title = `${t(nav.find(item => item.path === location.pathname || (item.path === '/access' && location.pathname.startsWith('/access/')))?.labelKey ?? 'app.title')} · SharpAI`; }, [location.pathname, nav, t]);
  const api = useApi();
  const {access} = useAdministration();
  const visibleNav = nav.filter(item=>item.path!=='/access' || (access && (access.isAdmin || Object.values(administrationResources).some(resource=>access.capabilities[resource]?.read))));
  const { theme, toggle } = useTheme();
  const { session, isAuthenticated, logout } = useAuth();
  const [menuOpen, setMenuOpen] = useState(false);
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
  for (const item of visibleNav) {
    if (!groups.includes(item.group)) groups.push(item.group);
  }

  return (
    <div className="sa-app">
      <a className="sa-skip" href="#main-content">{t('text.skipToContent')}</a>
      <aside className={`sa-sidebar ${menuOpen ? 'sa-sidebar--open' : ''}`} aria-label={t('text.mainNavigation')}>
        <NavLink to="/" className="sa-sidebar__brand" onClick={() => setMenuOpen(false)}><img src="/images/sharp-logo.svg" alt="" /><span>{t('text.sharpai')}<small>{t('text.inferenceWorkspace')}</small></span></NavLink>
        <nav aria-label={t('text.workspace')}>
        {groups.map((group) => (
          <div key={group}>
            <div className="sa-sidebar__group-label">{t(`groups.${group}`)}</div>
            {visibleNav
              .filter((item) => item.group === group)
              .map((item) => (
                <NavLink
                  key={item.path}
                  onClick={() => setMenuOpen(false)}
                  to={item.path}
                  end={item.path === '/'}
                  className={({ isActive }) => `sa-nav-item ${isActive ? 'sa-nav-item--active' : ''}`}
                >
                  <Icon name={item.path} />{t(item.labelKey)}
                </NavLink>
              ))}
          </div>
        ))}
        </nav><div className="sa-sidebar__footer"><small>{t('text.sharpaiV')}{version}</small></div>
      </aside>

      <header className="sa-topbar">
        <button className="sa-btn sa-btn--icon sa-mobile-menu" aria-label={t('text.toggleNavigation')} aria-expanded={menuOpen} onClick={() => setMenuOpen(!menuOpen)}><Icon name="menu" /></button>
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
          <a aria-label={t('topbar.repo')} title={t('topbar.repo')} className="sa-btn sa-btn--ghost sa-btn--icon" href="https://github.com/jchristn/SharpAI" target="_blank" rel="noreferrer">
            <HeaderIcon name="github" />
          </a>
          <a
            className="sa-btn sa-btn--ghost sa-btn--icon"
            href="https://discord.gg/tRAN8HgvK5"
            target="_blank"
            rel="noreferrer"
            aria-label={t('topbar.discord')}
            title={t('topbar.discord')}
          >
            <HeaderIcon name="discord" />
          </a>
          <button type="button" className="sa-btn sa-btn--ghost sa-btn--icon" title={theme === 'dark' ? t('topbar.themeLight') : t('topbar.themeDark')} onClick={toggle} aria-label={t('topbar.toggleTheme')}>
            <HeaderIcon name={theme === 'dark' ? 'sun' : 'moon'} />
          </button>
        </div>
      </header>

      <main ref={content} id="main-content" className="sa-content" tabIndex={-1}>{children}</main>

      <LoginModal open={loginOpen} onClose={() => setLoginOpen(false)} />
    </div>
  );
}

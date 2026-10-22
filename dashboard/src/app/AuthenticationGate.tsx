import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from './AuthProvider';
import { LoginModal } from './LoginModal';

/** Mount protected providers and pages only after the server validates the session. */
export function AuthenticationGate({ children }: { children: ReactNode }): JSX.Element {
  const { isAuthenticated, isCheckingSession } = useAuth();
  const { t } = useTranslation();
  const [open, setOpen] = useState(true);
  useEffect(() => { if (!isAuthenticated) document.title = `${t('login.title')} · SharpAI`; }, [isAuthenticated, t]);
  if (isCheckingSession) return <main className="sa-login-screen" aria-busy="true"><img src="/images/sharp-logo.svg" alt="SharpAI"/><p role="status">{t('login.signingIn')}</p></main>;
  if (isAuthenticated) return <>{children}</>;
  return <main className="sa-login-screen">
    <img src="/images/sharp-logo.svg" alt="SharpAI"/>
    <h1>{t('login.title')}</h1>
    <button className="sa-btn sa-btn--primary" disabled={isCheckingSession} onClick={() => setOpen(true)}>{t(isCheckingSession ? 'login.signingIn' : 'login.signIn')}</button>
    <LoginModal open={open && !isCheckingSession} onClose={() => setOpen(false)}/>
  </main>;
}

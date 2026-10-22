import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from './AuthProvider';
import { LoginModal } from './LoginModal';
import { Navigate, useLocation } from 'react-router-dom';

/** Mount protected providers and pages only after the server validates the session. */
export function AuthenticationGate({ children }: { children: ReactNode }): JSX.Element {
  const { isAuthenticated, isCheckingSession } = useAuth();
  const { t } = useTranslation();
  const location = useLocation();
  useEffect(() => { if (!isAuthenticated) document.title = `${t('login.title')} · SharpAI`; }, [isAuthenticated, t]);
  if (isCheckingSession) return <main className="sa-login-screen" aria-busy="true"><img src="/images/sharp-logo.svg" alt="SharpAI"/><p role="status">{t('login.signingIn')}</p></main>;
  if (isAuthenticated) return location.pathname === '/sign-in' ? <Navigate to={typeof location.state?.returnTo === 'string' && location.state.returnTo.startsWith('/') && !location.state.returnTo.startsWith('//') && location.state.returnTo !== '/sign-in' ? location.state.returnTo : '/'} replace/> : <>{children}</>;
  if (location.pathname !== '/sign-in') return <Navigate to="/sign-in" state={{returnTo:location.pathname + location.search}} replace/>;
  return <SignInScreen/>;
}

/** Each signed-out visit starts with the form open, independently of earlier cancellations. */
function SignInScreen(): JSX.Element {
  const {t}=useTranslation();
  const [open,setOpen]=useState(true);
  return <main className="sa-login-screen">
    <img src="/images/sharp-logo.svg" alt="SharpAI"/>
    <h1>{t('login.title')}</h1>
    {!open && <button className="sa-btn sa-btn--primary" onClick={()=>setOpen(true)}>{t('login.signIn')}</button>}
    <LoginModal open={open} onClose={()=>setOpen(false)}/>
  </main>;
}

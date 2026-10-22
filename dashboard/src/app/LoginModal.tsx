import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { LanguageSelector } from './LanguageSelector';
import { Modal } from '../components/ui';
import { ApiError } from '../api/ApiClient';
import { useAuth } from './AuthProvider';

const INITIAL_ACCOUNT = {email:'admin@sharpai.local', password:'password', tenant:'default'};

export interface LoginModalProps {
  open: boolean;
  onClose: () => void;
}

/** Sign-in dialog. Posts email/password (+ optional tenant) to obtain a bearer session token. */
export function LoginModal({ open, onClose }: LoginModalProps): JSX.Element {
  const { t } = useTranslation();
  const { login } = useAuth();
  const [email, setEmail] = useState<string>('');
  const [password, setPassword] = useState<string>('');
  const [tenant, setTenant] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<boolean>(false);

  const submit = async (): Promise<void> => {
    setBusy(true);
    setError(null);
    try {
      await login(email.trim(), password, tenant.trim() || undefined);
      setPassword('');
      onClose();
    } catch (err) {
      setError(err instanceof ApiError ? (err.status === 401 ? t('login.invalid') : `${err.status}: ${err.message}`) : t('login.failed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      className="sa-modal--login"
      open={open}
      title={t('login.title')}
      onClose={onClose}
      footer={
        <div className="sa-login-footer"><div className="sa-login-actions">
          <button type="button" className="sa-btn" onClick={onClose}>
            {t('login.cancel')}
          </button>
          <button type="submit" form="sa-login" className="sa-btn sa-btn--primary" disabled={busy}>
            {busy ? t('login.signingIn') : t('login.signIn')}
          </button>
        </div><p className="sa-login-defaults">{t('login.initialCredentials')}<br/><strong>{INITIAL_ACCOUNT.email}</strong> / <strong>{INITIAL_ACCOUNT.password}</strong><br/>{t('login.tenant')}: <strong>{INITIAL_ACCOUNT.tenant}</strong></p></div>
      }
    >
      <form id="sa-login" onSubmit={(event) => { event.preventDefault(); void submit(); }} className="sa-login-form">
        <div className="sa-login-intro"><div className="sa-login-brand"><img src="/images/sharp-logo.svg" alt="SharpAI" /><div><h2>{t('text.welcomeBack')}</h2><p>{t('text.signInToYourInferenceWorkspace')}</p></div></div><LanguageSelector /></div>
        <details className="sa-settings-note"><summary>{t('login.setupTitle')}</summary><p>{t('login.setupHelp')}</p><p>{t('login.setupConfig')}</p></details>
        <label className="sa-filterbar__field">
          <span className="sa-filterbar__label">{t('login.email')}</span>
          <input required autoFocus type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="username" />
        </label>
        <label className="sa-filterbar__field">
          <span className="sa-filterbar__label">{t('login.password')}</span>
          <input required type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
        </label>
        <label className="sa-filterbar__field">
          <span className="sa-filterbar__label">{t('login.tenant')}</span>
          <input type="text" value={tenant} placeholder={t('login.tenantPlaceholder')} onChange={(e) => setTenant(e.target.value)} />
        </label>
        {error ? <div role="alert" style={{ color: 'var(--sa-danger)' }}>{error}</div> : null}
      </form>
    </Modal>
  );
}

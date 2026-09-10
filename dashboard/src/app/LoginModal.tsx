import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Modal } from '../components/ui';
import { ApiError } from '../api/ApiClient';
import { useAuth } from './AuthProvider';

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
      open={open}
      title={t('login.title')}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="sa-btn" onClick={onClose}>
            {t('login.cancel')}
          </button>
          <button type="button" className="sa-btn sa-btn--primary" onClick={() => void submit()} disabled={busy}>
            {busy ? t('login.signingIn') : t('login.signIn')}
          </button>
        </>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--sa-space-3)' }}>
        <label className="sa-filterbar__field">
          <span className="sa-filterbar__label">{t('login.email')}</span>
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="username" />
        </label>
        <label className="sa-filterbar__field">
          <span className="sa-filterbar__label">{t('login.password')}</span>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
        </label>
        <label className="sa-filterbar__field">
          <span className="sa-filterbar__label">{t('login.tenant')}</span>
          <input type="text" value={tenant} placeholder={t('login.tenantPlaceholder')} onChange={(e) => setTenant(e.target.value)} />
        </label>
        {error ? <div style={{ color: 'var(--sa-danger)' }}>{error}</div> : null}
      </div>
    </Modal>
  );
}

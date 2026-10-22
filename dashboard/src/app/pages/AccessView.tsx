import { NavLink, Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAdministration } from '../AdministrationProvider';
import { administrationResources, type AdministrationSection } from '../../api/administration';
import { AdministrationView } from './AdministrationView';

const sections = Object.keys(administrationResources) as AdministrationSection[];
/** One access workspace, with permission-aware, deep-linkable section tabs. */
export function AccessView({section}: {section?: AdministrationSection}): JSX.Element {
  const {t} = useTranslation();
  const {access} = useAdministration();
  const available = sections.filter(key => access && (access.isAdmin || access.capabilities[administrationResources[key]]?.read));
  if (!section && available.length) return <Navigate to={`/access/${available[0]}`} replace/>;
  return <div className="sa-access-page"><nav className="sa-access-tabs" aria-label={t('nav.access')}>
    {available.map(key => <NavLink key={key} to={`/access/${key}`} className={`sa-access-tab ${section === key ? 'sa-access-tab--active' : ''}`} aria-current={section === key ? 'page' : undefined}>{t(`admin.${key}`)}</NavLink>)}
  </nav>{section ? <AdministrationView key={section} section={section}/> : <p role="status">{t('admin.denied')}</p>}</div>;
}

import React from 'react';
import ReactDOM from 'react-dom/client';
import { localeReady } from './i18n/config';
import { NewApp } from './app/NewApp';
import './app/redesign.css';
import { initTheme } from './theme/themeController';

initTheme();

void localeReady.then(() => ReactDOM.createRoot(document.getElementById('root') as HTMLElement).render(
  <React.StrictMode>
    <NewApp />
  </React.StrictMode>,
));

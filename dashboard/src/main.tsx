import React from 'react';
import ReactDOM from 'react-dom/client';
import './i18n/config';
import { NewApp } from './app/NewApp';
import { initTheme } from './theme/themeController';

initTheme();

ReactDOM.createRoot(document.getElementById('root') as HTMLElement).render(
  <React.StrictMode>
    <NewApp />
  </React.StrictMode>,
);

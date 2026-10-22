import '@testing-library/jest-dom/vitest';

import { beforeEach } from 'vitest';
import { localeReady, setActiveLocale } from '../i18n/config';
beforeEach(async () => { await localeReady; await setActiveLocale('en'); });

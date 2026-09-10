import { defineConfig } from '@playwright/test';

const DESKTOP = { width: 1280, height: 800 };
const TABLET = { width: 768, height: 1024 };
const MOBILE = { width: 390, height: 844 };

// Visual + accessibility QA (W11.T10). The dashboard is a static SPA, so the preview server is enough —
// data calls fail gracefully to empty/error states, leaving the shell, navigation, theming, and landmarks
// fully exercisable without a backend.
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:3000',
  },
  webServer: {
    command: 'npm run build && npm run start',
    url: 'http://localhost:3000',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
  projects: [
    { name: 'desktop-light', use: { viewport: DESKTOP, colorScheme: 'light' } },
    { name: 'desktop-dark', use: { viewport: DESKTOP, colorScheme: 'dark' } },
    { name: 'tablet-light', use: { viewport: TABLET, colorScheme: 'light' } },
    { name: 'mobile-dark', use: { viewport: MOBILE, colorScheme: 'dark' } },
  ],
});

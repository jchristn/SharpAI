// Framework-agnostic theme controller (W11.T2). Persists the user's choice, stamps [data-theme] on the
// document root so the CSS-variable tokens switch, and falls back to the OS preference when unset.

export type ThemeMode = 'light' | 'dark';

const STORAGE_KEY = 'sharpai.theme';

export function getStoredTheme(): ThemeMode | null {
  try {
    const value = window.localStorage.getItem(STORAGE_KEY);
    return value === 'light' || value === 'dark' ? value : null;
  } catch {
    return null;
  }
}

export function getSystemTheme(): ThemeMode {
  if (typeof window !== 'undefined' && window.matchMedia) {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
  return 'light';
}

export function getEffectiveTheme(): ThemeMode {
  return getStoredTheme() ?? getSystemTheme();
}

export function applyTheme(mode: ThemeMode): void {
  document.documentElement.setAttribute('data-theme', mode);
}

export function setTheme(mode: ThemeMode): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, mode);
  } catch {
    // storage may be unavailable (private mode); the attribute still applies for this session.
  }
  applyTheme(mode);
}

export function toggleTheme(): ThemeMode {
  const next: ThemeMode = getEffectiveTheme() === 'dark' ? 'light' : 'dark';
  setTheme(next);
  return next;
}

export function initTheme(): ThemeMode {
  const mode = getEffectiveTheme();
  applyTheme(mode);
  return mode;
}

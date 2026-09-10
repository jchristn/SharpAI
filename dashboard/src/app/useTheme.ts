import { useCallback, useEffect, useState } from 'react';
import { getEffectiveTheme, setTheme, toggleTheme } from '../theme/themeController';
import type { ThemeMode } from '../theme/themeController';

export interface UseThemeResult {
  theme: ThemeMode;
  toggle: () => void;
  set: (mode: ThemeMode) => void;
}

/** React binding over the framework-agnostic theme controller: applies on mount and exposes a toggle. */
export function useTheme(): UseThemeResult {
  const [theme, setThemeState] = useState<ThemeMode>(() => getEffectiveTheme());

  useEffect(() => {
    setTheme(theme);
  }, [theme]);

  const toggle = useCallback((): void => {
    setThemeState(toggleTheme());
  }, []);

  const set = useCallback((mode: ThemeMode): void => {
    setThemeState(mode);
  }, []);

  return { theme, toggle, set };
}

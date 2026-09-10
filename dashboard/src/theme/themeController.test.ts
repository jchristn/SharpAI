import { beforeEach, describe, expect, it } from 'vitest';
import { getStoredTheme, setTheme, toggleTheme } from './themeController';

describe('themeController', () => {
  beforeEach(() => {
    window.localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
  });

  it('persists and applies a theme', () => {
    setTheme('dark');
    expect(getStoredTheme()).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it('toggles between light and dark', () => {
    setTheme('light');
    const next = toggleTheme();
    expect(next).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });
});

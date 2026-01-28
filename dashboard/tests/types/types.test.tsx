import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import { ThemeEnum } from '#/types/types';

describe('ThemeEnum', () => {
  it('should have correct enum values', () => {
    expect(ThemeEnum.LIGHT).toBe('light');
    expect(ThemeEnum.DARK).toBe('dark');
  });

  it('should work with Object.values', () => {
    const values = Object.values(ThemeEnum);
    expect(values).toContain('light');
    expect(values).toContain('dark');
    expect(values).toHaveLength(2);
  });

  it('should work with Object.entries', () => {
    const entries = Object.entries(ThemeEnum);
    expect(entries).toContainEqual(['LIGHT', 'light']);
    expect(entries).toContainEqual(['DARK', 'dark']);
    expect(entries).toHaveLength(2);
  });

  it('should work with Object.keys', () => {
    const keys = Object.keys(ThemeEnum);
    expect(keys).toContain('LIGHT');
    expect(keys).toContain('DARK');
    expect(keys).toHaveLength(2);
  });

  it('should work in switch statements', () => {
    const getThemeValue = (theme: ThemeEnum) => {
      switch (theme) {
        case ThemeEnum.LIGHT:
          return 'light-theme';
        case ThemeEnum.DARK:
          return 'dark-theme';
        default:
          return 'unknown-theme';
      }
    };

    expect(getThemeValue(ThemeEnum.LIGHT)).toBe('light-theme');
    expect(getThemeValue(ThemeEnum.DARK)).toBe('dark-theme');
  });

  it('should work in conditional statements', () => {
    const isLightTheme = (theme: ThemeEnum) => theme === ThemeEnum.LIGHT;
    const isDarkTheme = (theme: ThemeEnum) => theme === ThemeEnum.DARK;

    expect(isLightTheme(ThemeEnum.LIGHT)).toBe(true);
    expect(isLightTheme(ThemeEnum.DARK)).toBe(false);
    expect(isDarkTheme(ThemeEnum.DARK)).toBe(true);
    expect(isDarkTheme(ThemeEnum.LIGHT)).toBe(false);
  });

  it('should work with arrays', () => {
    const themes = [ThemeEnum.LIGHT, ThemeEnum.DARK];
    expect(themes).toHaveLength(2);
    expect(themes).toContain(ThemeEnum.LIGHT);
    expect(themes).toContain(ThemeEnum.DARK);
  });

  it('should work with filtering', () => {
    const allThemes = [ThemeEnum.LIGHT, ThemeEnum.DARK, ThemeEnum.LIGHT];
    const lightThemes = allThemes.filter((theme) => theme === ThemeEnum.LIGHT);
    const darkThemes = allThemes.filter((theme) => theme === ThemeEnum.DARK);

    expect(lightThemes).toHaveLength(2);
    expect(darkThemes).toHaveLength(1);
  });

  it('should work with mapping', () => {
    const themes = [ThemeEnum.LIGHT, ThemeEnum.DARK];
    const themeLabels = themes.map((theme) => (theme === ThemeEnum.LIGHT ? 'Light Theme' : 'Dark Theme'));

    expect(themeLabels).toEqual(['Light Theme', 'Dark Theme']);
  });

  it('should work with reduce', () => {
    const themes = [ThemeEnum.LIGHT, ThemeEnum.DARK, ThemeEnum.LIGHT];
    const themeCount = themes.reduce((acc, theme) => {
      acc[theme] = (acc[theme] || 0) + 1;
      return acc;
    }, {} as Record<string, number>);

    expect(themeCount[ThemeEnum.LIGHT]).toBe(2);
    expect(themeCount[ThemeEnum.DARK]).toBe(1);
  });

  it('should work with JSON serialization', () => {
    const themeData = {
      currentTheme: ThemeEnum.LIGHT,
      availableThemes: [ThemeEnum.LIGHT, ThemeEnum.DARK],
    };

    const json = JSON.stringify(themeData);
    const parsed = JSON.parse(json);

    expect(parsed.currentTheme).toBe('light');
    expect(parsed.availableThemes).toEqual(['light', 'dark']);
  });

  it('should work with localStorage', () => {
    const mockLocalStorage = {
      getItem: jest.fn(),
      setItem: jest.fn(),
    };

    Object.defineProperty(window, 'localStorage', {
      value: mockLocalStorage,
    });

    // Test setting theme
    mockLocalStorage.setItem('theme', ThemeEnum.DARK);
    expect(mockLocalStorage.setItem).toHaveBeenCalledWith('theme', 'dark');

    // Test getting theme
    mockLocalStorage.getItem.mockReturnValue(ThemeEnum.LIGHT);
    const savedTheme = mockLocalStorage.getItem('theme');
    expect(savedTheme).toBe('light');
  });
});

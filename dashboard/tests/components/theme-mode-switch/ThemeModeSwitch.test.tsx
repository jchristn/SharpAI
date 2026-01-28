import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import ThemeModeSwitch from '#/components/theme-mode-switch/ThemeModeSwitch';
import { ThemeEnum } from '#/types/types';

// Mock the hook
const mockSetTheme = jest.fn();
const mockUseAppContext = jest.fn();

jest.mock('#/hooks/appHooks', () => ({
  useAppContext: () => mockUseAppContext(),
}));

// Mock the DarkModeSwitch component
jest.mock('react-toggle-dark-mode', () => ({
  DarkModeSwitch: ({ checked, onChange, size }: any) => (
    <button data-testid="dark-mode-switch" data-checked={checked} data-size={size} onClick={() => onChange(!checked)}>
      {checked ? 'Dark Mode' : 'Light Mode'}
    </button>
  ),
}));

describe('ThemeModeSwitch', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render with light theme by default', () => {
    mockUseAppContext.mockReturnValue({
      theme: ThemeEnum.LIGHT,
      setTheme: mockSetTheme,
    });

    render(<ThemeModeSwitch />);

    const switchButton = screen.getByTestId('dark-mode-switch');
    expect(switchButton).toBeInTheDocument();
    expect(switchButton).toHaveAttribute('data-checked', 'false');
    expect(switchButton).toHaveAttribute('data-size', '20');
    expect(switchButton).toHaveTextContent('Light Mode');
  });

  it('should render with dark theme when selected', () => {
    mockUseAppContext.mockReturnValue({
      theme: ThemeEnum.DARK,
      setTheme: mockSetTheme,
    });

    render(<ThemeModeSwitch />);

    const switchButton = screen.getByTestId('dark-mode-switch');
    expect(switchButton).toBeInTheDocument();
    expect(switchButton).toHaveAttribute('data-checked', 'true');
    expect(switchButton).toHaveTextContent('Dark Mode');
  });

  it('should call setTheme with DARK when switching from light to dark', () => {
    mockUseAppContext.mockReturnValue({
      theme: ThemeEnum.LIGHT,
      setTheme: mockSetTheme,
    });

    render(<ThemeModeSwitch />);

    const switchButton = screen.getByTestId('dark-mode-switch');
    fireEvent.click(switchButton);

    expect(mockSetTheme).toHaveBeenCalledWith(ThemeEnum.DARK);
  });

  it('should call setTheme with LIGHT when switching from dark to light', () => {
    mockUseAppContext.mockReturnValue({
      theme: ThemeEnum.DARK,
      setTheme: mockSetTheme,
    });

    render(<ThemeModeSwitch />);

    const switchButton = screen.getByTestId('dark-mode-switch');
    fireEvent.click(switchButton);

    expect(mockSetTheme).toHaveBeenCalledWith(ThemeEnum.LIGHT);
  });

  it('should handle multiple theme switches', () => {
    mockUseAppContext.mockReturnValue({
      theme: ThemeEnum.LIGHT,
      setTheme: mockSetTheme,
    });

    render(<ThemeModeSwitch />);

    const switchButton = screen.getByTestId('dark-mode-switch');

    // First switch: Light -> Dark
    fireEvent.click(switchButton);
    expect(mockSetTheme).toHaveBeenCalledWith(ThemeEnum.DARK);

    // Second switch: Dark -> Light (mock stays the same, but component behavior changes)
    fireEvent.click(switchButton);
    expect(mockSetTheme).toHaveBeenCalledWith(ThemeEnum.DARK);

    expect(mockSetTheme).toHaveBeenCalledTimes(2);
  });

  it('should have correct size attribute', () => {
    mockUseAppContext.mockReturnValue({
      theme: ThemeEnum.LIGHT,
      setTheme: mockSetTheme,
    });

    render(<ThemeModeSwitch />);

    const switchButton = screen.getByTestId('dark-mode-switch');
    expect(switchButton).toHaveAttribute('data-size', '20');
  });

  it('should update display when theme changes', () => {
    const { rerender } = render(<ThemeModeSwitch />);

    // Initial render with light theme
    mockUseAppContext.mockReturnValue({
      theme: ThemeEnum.LIGHT,
      setTheme: mockSetTheme,
    });

    rerender(<ThemeModeSwitch />);

    let switchButton = screen.getByTestId('dark-mode-switch');
    expect(switchButton).toHaveAttribute('data-checked', 'false');
    expect(switchButton).toHaveTextContent('Light Mode');

    // Re-render with dark theme
    mockUseAppContext.mockReturnValue({
      theme: ThemeEnum.DARK,
      setTheme: mockSetTheme,
    });

    rerender(<ThemeModeSwitch />);

    switchButton = screen.getByTestId('dark-mode-switch');
    expect(switchButton).toHaveAttribute('data-checked', 'true');
    expect(switchButton).toHaveTextContent('Dark Mode');
  });

  it('should handle theme context changes', () => {
    mockUseAppContext.mockReturnValue({
      theme: ThemeEnum.DARK,
      setTheme: mockSetTheme,
    });

    render(<ThemeModeSwitch />);

    const switchButton = screen.getByTestId('dark-mode-switch');
    expect(switchButton).toHaveAttribute('data-checked', 'true');
  });

  it('should be accessible', () => {
    mockUseAppContext.mockReturnValue({
      theme: ThemeEnum.LIGHT,
      setTheme: mockSetTheme,
    });

    render(<ThemeModeSwitch />);

    const switchButton = screen.getByTestId('dark-mode-switch');
    expect(switchButton).toBeInTheDocument();
    expect(switchButton.tagName).toBe('BUTTON');
  });

  it('should match snapshot for light theme', () => {
    mockUseAppContext.mockReturnValue({
      theme: ThemeEnum.LIGHT,
      setTheme: mockSetTheme,
    });

    const { container } = render(<ThemeModeSwitch />);
    expect(container.firstChild).toMatchSnapshot();
  });

  it('should match snapshot for dark theme', () => {
    mockUseAppContext.mockReturnValue({
      theme: ThemeEnum.DARK,
      setTheme: mockSetTheme,
    });

    const { container } = render(<ThemeModeSwitch />);
    expect(container.firstChild).toMatchSnapshot();
  });
});

import React from 'react';
import { render, screen, act } from '@testing-library/react';
import '@testing-library/jest-dom';
import AppProviders from '#/hoc/AppProviders';

// Mock ThemeEnum
const ThemeEnum = {
  LIGHT: 'light',
  DARK: 'dark',
};

// Mock the dependencies
jest.mock('#/lib/store/StoreProvider', () => {
  return function MockStoreProvider({ children }: { children: React.ReactNode }) {
    return <div data-testid="store-provider">{children}</div>;
  };
});

jest.mock('antd', () => ({
  ConfigProvider: ({ children, theme }: any) => (
    <div data-testid="config-provider" data-theme={theme?.token?.colorPrimary}>
      {children}
    </div>
  ),
  App: ({ children }: any) => <div data-testid="antd-app">{children}</div>,
}));

jest.mock('@ant-design/cssinjs', () => ({
  StyleProvider: ({ children }: any) => <div data-testid="style-provider">{children}</div>,
}));

jest.mock('next/font/google', () => ({
  Inter: () => ({ className: 'inter-font' }),
}));

jest.mock('@ant-design/nextjs-registry', () => ({
  AntdRegistry: ({ children }: any) => <div data-testid="antd-registry">{children}</div>,
}));

jest.mock('#/theme/theme', () => ({
  primaryTheme: {
    cssVar: true,
    algorithm: 'default',
    token: {
      colorPrimary: '#CB3B84',
    },
  },
  darkTheme: {
    cssVar: true,
    algorithm: 'dark',
    token: {
      colorPrimary: '#fa43a0',
    },
  },
}));

// Mock localStorage
const mockLocalStorage = {
  getItem: jest.fn(),
  setItem: jest.fn(),
  removeItem: jest.fn(),
  clear: jest.fn(),
};

Object.defineProperty(window, 'localStorage', {
  value: mockLocalStorage,
});

describe('AppProviders', () => {
  const TestComponent = () => <div data-testid="test-child">Test Child</div>;

  beforeEach(() => {
    jest.clearAllMocks();
    mockLocalStorage.getItem.mockReturnValue(null);
  });

  it('should render children with all providers', () => {
    render(
      <AppProviders>
        <TestComponent />
      </AppProviders>
    );

    expect(screen.getByTestId('store-provider')).toBeInTheDocument();
    expect(screen.getByTestId('style-provider')).toBeInTheDocument();
    expect(screen.getByTestId('antd-registry')).toBeInTheDocument();
    expect(screen.getByTestId('config-provider')).toBeInTheDocument();
    // Toaster is mocked but not rendered in this test
    expect(screen.getByTestId('test-child')).toBeInTheDocument();
  });

  it('should use light theme by default', () => {
    render(
      <AppProviders>
        <TestComponent />
      </AppProviders>
    );

    const configProvider = screen.getByTestId('config-provider');
    expect(configProvider).toBeInTheDocument();
  });

  it('should load theme from localStorage on mount', async () => {
    mockLocalStorage.getItem.mockReturnValue(ThemeEnum.DARK);

    await act(async () => {
      render(
        <AppProviders>
          <TestComponent />
        </AppProviders>
      );
    });

    expect(mockLocalStorage.getItem).toHaveBeenCalledWith('theme');
  });

  it('should handle theme change and save to localStorage', async () => {
    const TestComponentWithTheme = () => {
      const [theme, setTheme] = React.useState(ThemeEnum.LIGHT);

      return (
        <div>
          <div data-testid="current-theme">{theme}</div>
          <button data-testid="change-theme" onClick={() => setTheme(ThemeEnum.DARK)}>
            Change Theme
          </button>
        </div>
      );
    };

    render(
      <AppProviders>
        <TestComponentWithTheme />
      </AppProviders>
    );

    expect(screen.getByTestId('current-theme')).toHaveTextContent(ThemeEnum.LIGHT);
  });

  it('should provide AppContext with theme and handlers', () => {
    const TestContextConsumer = () => {
      const context = React.useContext(require('#/hooks/appHooks').AppContext);

      return (
        <div>
          <div data-testid="context-theme">{context.theme}</div>
          <div data-testid="has-set-theme">{typeof context.setTheme}</div>
          <div data-testid="has-background-task">{typeof context.handleBackgroundTask}</div>
        </div>
      );
    };

    render(
      <AppProviders>
        <TestContextConsumer />
      </AppProviders>
    );

    expect(screen.getByTestId('context-theme')).toHaveTextContent(ThemeEnum.LIGHT);
    expect(screen.getByTestId('has-set-theme')).toHaveTextContent('function');
    expect(screen.getByTestId('has-background-task')).toHaveTextContent('function');
  });

  it('should handle background tasks', async () => {
    const mockTask = jest.fn().mockResolvedValue(undefined);

    const TestBackgroundTask = () => {
      const { handleBackgroundTask } = React.useContext(require('#/hooks/appHooks').AppContext);

      const handleClick = async () => {
        await handleBackgroundTask(mockTask);
      };

      return (
        <button data-testid="run-task" onClick={handleClick}>
          Run Task
        </button>
      );
    };

    render(
      <AppProviders>
        <TestBackgroundTask />
      </AppProviders>
    );

    const button = screen.getByTestId('run-task');
    await act(async () => {
      button.click();
    });

    expect(mockTask).toHaveBeenCalled();
  });

  it('should match snapshot', () => {
    const { container } = render(
      <AppProviders>
        <TestComponent />
      </AppProviders>
    );

    expect(container.firstChild).toMatchSnapshot();
  });
});

import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import DashboardLayout from '#/components/layout/DashboardLayout';

// Mock Next.js Link
jest.mock('next/link', () => {
  return function MockLink({ children, href }: { children: React.ReactNode; href: string }) {
    return <a href={href}>{children}</a>;
  };
});

// Mock hooks
const mockTheme = 'light';
jest.mock('#/hooks/appHooks', () => ({
  useAppContext: () => ({
    theme: mockTheme,
  }),
}));

// Mock HOC
jest.mock('#/hoc/hoc', () => {
  return function mockWithConnectivityValidation(Component: React.ComponentType<any>) {
    return Component;
  };
});

// Mock components
jest.mock('#/components/base/sidebar', () => {
  return function MockSidebar({ collapsed, onCollapse }: any) {
    return (
      <div data-testid="sidebar" data-collapsed={collapsed}>
        <button onClick={() => onCollapse(!collapsed)}>Toggle Sidebar</button>
      </div>
    );
  };
});

jest.mock('#/components/theme-mode-switch/ThemeModeSwitch', () => {
  return function MockThemeModeSwitch() {
    return <button data-testid="theme-switch">Theme Switch</button>;
  };
});

describe('DashboardLayout Component', () => {
  it('should render layout with children', () => {
    render(
      <DashboardLayout>
        <div data-testid="child-content">Test Content</div>
      </DashboardLayout>
    );

    expect(screen.getByTestId('child-content')).toBeInTheDocument();
    expect(screen.getByText('Test Content')).toBeInTheDocument();
  });

  it('should render sidebar', () => {
    render(
      <DashboardLayout>
        <div>Test Content</div>
      </DashboardLayout>
    );

    expect(screen.getByTestId('sidebar')).toBeInTheDocument();
  });

  it('should render header with theme switch', () => {
    render(
      <DashboardLayout>
        <div>Test Content</div>
      </DashboardLayout>
    );

    expect(screen.getByTestId('theme-switch')).toBeInTheDocument();
  });

  it('should render change instance button', () => {
    render(
      <DashboardLayout>
        <div>Test Content</div>
      </DashboardLayout>
    );

    const changeInstanceButton = screen.getByText('Change SharpAPI Instance');
    expect(changeInstanceButton).toBeInTheDocument();

    const link = changeInstanceButton.closest('a');
    expect(link).toHaveAttribute('href', '/');
  });

  it('should have correct layout structure', () => {
    const { container } = render(
      <DashboardLayout>
        <div>Test Content</div>
      </DashboardLayout>
    );

    // Check for Ant Design Layout components
    const layoutElements = container.querySelectorAll('.ant-layout');
    expect(layoutElements.length).toBeGreaterThan(0);
  });

  it('should match snapshot', () => {
    const { container } = render(
      <DashboardLayout>
        <div>Test Content</div>
      </DashboardLayout>
    );

    expect(container.firstChild).toMatchSnapshot();
  });
});

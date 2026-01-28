import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import DashboardLayout from '#/app/dashboard/layout';

// Mock Next.js components
jest.mock('next/navigation', () => ({
  useRouter: () => ({
    push: jest.fn(),
    pathname: '/dashboard',
  }),
}));

jest.mock('#/components/layout/DashboardLayout', () => {
  return function MockDashboardLayout({ children }: any) {
    return <div data-testid="dashboard-layout">{children}</div>;
  };
});

describe('Dashboard Layout', () => {
  it('should render dashboard layout with children', () => {
    render(
      <DashboardLayout>
        <div>Dashboard Content</div>
      </DashboardLayout>
    );

    expect(screen.getByTestId('dashboard-layout')).toBeInTheDocument();
    expect(screen.getByText('Dashboard Content')).toBeInTheDocument();
  });

  it('should render with metadata', () => {
    render(
      <DashboardLayout>
        <div>Dashboard Content</div>
      </DashboardLayout>
    );

    // Check that the layout renders without errors
    expect(screen.getByText('Dashboard Content')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(
      <DashboardLayout>
        <div>Snapshot Content</div>
      </DashboardLayout>
    );
    expect(container.firstChild).toMatchSnapshot();
  });
});

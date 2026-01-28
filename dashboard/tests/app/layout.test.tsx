import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import RootLayout from '#/app/layout';

// Mock Next.js components
jest.mock('next/font/google', () => ({
  Inter: () => ({
    className: 'inter-font',
  }),
}));

jest.mock('@ant-design/nextjs-registry', () => ({
  AntdRegistry: ({ children }: any) => <div>{children}</div>,
}));

jest.mock('#/hoc/AppProviders', () => {
  return function MockAppProviders({ children }: any) {
    return <div data-testid="app-providers">{children}</div>;
  };
});

describe('Root Layout', () => {
  it('should render root layout with children', () => {
    render(
      <RootLayout>
        <div>Test Content</div>
      </RootLayout>
    );

    expect(screen.getByTestId('app-providers')).toBeInTheDocument();
    expect(screen.getByText('Test Content')).toBeInTheDocument();
  });

  it('should render with metadata', () => {
    render(
      <RootLayout>
        <div>Test Content</div>
      </RootLayout>
    );

    // Check that the layout renders without errors
    expect(screen.getByText('Test Content')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(
      <RootLayout>
        <div>Snapshot Content</div>
      </RootLayout>
    );
    expect(container.firstChild).toMatchSnapshot();
  });
});

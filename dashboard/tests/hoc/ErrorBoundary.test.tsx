import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import ErrorBoundary from '#/hoc/ErrorBoundary';

// Mock the FallBack component
jest.mock('#/components/base/fallback/FallBack', () => {
  return function MockFallBack({ children, style }: any) {
    return (
      <div data-testid="fallback" style={style}>
        {children}
      </div>
    );
  };
});

// Mock window.location
const mockLocation = {
  href: 'http://localhost:3000/test',
  reload: jest.fn(),
};

Object.defineProperty(window, 'location', {
  value: mockLocation,
  writable: true,
});

describe('ErrorBoundary', () => {
  const ThrowError = ({ shouldThrow }: { shouldThrow: boolean }) => {
    if (shouldThrow) {
      throw new Error('Test error message');
    }
    return <div data-testid="success">Success</div>;
  };

  beforeEach(() => {
    jest.clearAllMocks();
    // Suppress console.error for expected error boundary tests
    jest.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('should render children when there is no error', () => {
    render(
      <ErrorBoundary>
        <ThrowError shouldThrow={false} />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('success')).toBeInTheDocument();
    expect(screen.queryByTestId('fallback')).not.toBeInTheDocument();
  });

  it('should render fallback when there is an error', () => {
    render(
      <ErrorBoundary>
        <ThrowError shouldThrow={true} />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('fallback')).toBeInTheDocument();
    expect(screen.getByText('Unexpected Error: Test error message.')).toBeInTheDocument();
    expect(screen.queryByTestId('success')).not.toBeInTheDocument();
  });

  it('should render custom error component when provided', () => {
    const CustomErrorComponent = ({ errorMessage }: { errorMessage?: string }) => (
      <div data-testid="custom-error">Custom Error: {errorMessage}</div>
    );

    render(
      <ErrorBoundary errorComponent={CustomErrorComponent}>
        <ThrowError shouldThrow={true} />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('custom-error')).toBeInTheDocument();
    expect(screen.getByText('Custom Error:')).toBeInTheDocument();
    expect(screen.queryByTestId('fallback')).not.toBeInTheDocument();
  });

  it('should show reload link when allowRefresh is true', () => {
    render(
      <ErrorBoundary allowRefresh={true}>
        <ThrowError shouldThrow={true} />
      </ErrorBoundary>
    );

    const reloadLink = screen.getByText('Reload page');
    expect(reloadLink).toBeInTheDocument();
    expect(reloadLink).toHaveAttribute('href', 'http://localhost:3000/test');
  });

  it('should not show reload link when allowRefresh is false', () => {
    render(
      <ErrorBoundary allowRefresh={false}>
        <ThrowError shouldThrow={true} />
      </ErrorBoundary>
    );

    expect(screen.queryByText('Reload page')).not.toBeInTheDocument();
  });

  it('should not show reload link when allowRefresh is not provided', () => {
    render(
      <ErrorBoundary>
        <ThrowError shouldThrow={true} />
      </ErrorBoundary>
    );

    expect(screen.queryByText('Reload page')).not.toBeInTheDocument();
  });

  it('should handle error without message', () => {
    const ThrowErrorWithoutMessage = () => {
      throw new Error('');
    };

    render(
      <ErrorBoundary>
        <ThrowErrorWithoutMessage />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('fallback')).toBeInTheDocument();
    expect(screen.getByText('Unexpected Error: .')).toBeInTheDocument();
  });

  it('should log error to console', () => {
    const consoleSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

    render(
      <ErrorBoundary>
        <ThrowError shouldThrow={true} />
      </ErrorBoundary>
    );

    expect(consoleSpy).toHaveBeenCalledWith('ErrorBoundary caught an error: ', expect.any(Error), expect.any(Object));

    consoleSpy.mockRestore();
  });

  it('should handle multiple errors and show the latest one', () => {
    const { rerender } = render(
      <ErrorBoundary>
        <ThrowError shouldThrow={true} />
      </ErrorBoundary>
    );

    expect(screen.getByText('Unexpected Error: Test error message.')).toBeInTheDocument();

    // Simulate a new error by re-rendering with a different error
    const ThrowDifferentError = () => {
      throw new Error('Different error message');
    };

    rerender(
      <ErrorBoundary>
        <ThrowDifferentError />
      </ErrorBoundary>
    );

    expect(screen.getByText('Unexpected Error: Test error message.')).toBeInTheDocument();
  });

  it('should match snapshot for error state', () => {
    const { container } = render(
      <ErrorBoundary allowRefresh={true}>
        <ThrowError shouldThrow={true} />
      </ErrorBoundary>
    );

    expect(container.firstChild).toMatchSnapshot();
  });

  it('should match snapshot for success state', () => {
    const { container } = render(
      <ErrorBoundary>
        <ThrowError shouldThrow={false} />
      </ErrorBoundary>
    );

    expect(container.firstChild).toMatchSnapshot();
  });
});

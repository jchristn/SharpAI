import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { withConnectivityValidation } from '#/hoc/hoc';

// Mock the dependencies
jest.mock('#/lib/reducer/apiSlice', () => ({
  useValidateConnectivityMutation: jest.fn(),
}));

jest.mock('#/components/base/loading/PageLoading', () => {
  return function MockPageLoading({ message }: { message: string }) {
    return <div data-testid="page-loading">{message}</div>;
  };
});

jest.mock('#/components/base/fallback/FallBack', () => {
  return function MockFallBack({ children, style }: any) {
    return (
      <div data-testid="fallback" style={style}>
        {children}
      </div>
    );
  };
});

jest.mock('#/components/base/button/Button', () => {
  return function MockButton({ children, onClick, type }: any) {
    return (
      <button data-testid="retry-button" onClick={onClick} type={type}>
        {children}
      </button>
    );
  };
});

jest.mock('#/components/base/typograpghy/Text', () => {
  return function MockText({ children }: any) {
    return <div data-testid="error-text">{children}</div>;
  };
});

jest.mock('#/components/base/typograpghy/Title', () => {
  return function MockTitle({ children, level, weight }: any) {
    return (
      <h1 data-testid="error-title" data-level={level} data-weight={weight}>
        {children}
      </h1>
    );
  };
});

jest.mock('#/components/base/flex/Flex', () => {
  return function MockFlex({ children, className, justify, align, vertical, gap }: any) {
    return (
      <div
        data-testid="error-flex"
        className={className}
        data-justify={justify}
        data-align={align}
        data-vertical={vertical}
        data-gap={gap}
      >
        {children}
      </div>
    );
  };
});

jest.mock('#/components/base/pageContainer/PageContainer', () => {
  return function MockPageContainer({ children, style }: any) {
    return (
      <div data-testid="page-container" style={style}>
        {children}
      </div>
    );
  };
});

jest.mock('#/lib/store/rtk/rtkApiInstance', () => ({
  changeAxiosBaseUrl: jest.fn(),
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

describe('withConnectivityValidation HOC', () => {
  const TestComponent = ({ testProp }: { testProp: string }) => (
    <div data-testid="test-component">Test Component with prop: {testProp}</div>
  );

  const WrappedComponent = withConnectivityValidation(TestComponent);

  beforeEach(() => {
    jest.clearAllMocks();
    mockLocalStorage.getItem.mockReturnValue(null);
  });

  it('should show loading state initially', () => {
    const mockValidateConnectivity = jest.fn();
    const mockUseValidateConnectivityMutation = require('#/lib/reducer/apiSlice').useValidateConnectivityMutation;

    mockUseValidateConnectivityMutation.mockReturnValue([
      mockValidateConnectivity,
      { isLoading: true, isSuccess: false, isError: false, error: null },
    ]);

    render(<WrappedComponent testProp="test" />);

    expect(screen.getByTestId('page-loading')).toBeInTheDocument();
    expect(screen.getByText('Validating connectivity...')).toBeInTheDocument();
    expect(screen.queryByTestId('test-component')).not.toBeInTheDocument();
  });

  it('should show error state when validation fails', () => {
    const mockValidateConnectivity = jest.fn();
    const mockUseValidateConnectivityMutation = require('#/lib/reducer/apiSlice').useValidateConnectivityMutation;

    mockUseValidateConnectivityMutation.mockReturnValue([
      mockValidateConnectivity,
      {
        isLoading: false,
        isSuccess: false,
        isError: true,
        error: { message: 'Network error' },
      },
    ]);

    render(<WrappedComponent testProp="test" />);

    expect(screen.getByTestId('fallback')).toBeInTheDocument();
    expect(screen.getByTestId('error-title')).toHaveTextContent(
      'Failed to validate connectivity. Please check your connection.'
    );
    expect(screen.getByTestId('error-text')).toHaveTextContent('Network error');
    expect(screen.getByTestId('retry-button')).toBeInTheDocument();
    expect(screen.queryByTestId('test-component')).not.toBeInTheDocument();
  });

  it('should render wrapped component when validation succeeds', () => {
    const mockValidateConnectivity = jest.fn();
    const mockUseValidateConnectivityMutation = require('#/lib/reducer/apiSlice').useValidateConnectivityMutation;

    mockUseValidateConnectivityMutation.mockReturnValue([
      mockValidateConnectivity,
      { isLoading: false, isSuccess: true, isError: false, error: null },
    ]);

    render(<WrappedComponent testProp="test" />);

    expect(screen.getByTestId('test-component')).toBeInTheDocument();
    expect(screen.getByText('Test Component with prop: test')).toBeInTheDocument();
    expect(screen.queryByTestId('page-loading')).not.toBeInTheDocument();
    expect(screen.queryByTestId('fallback')).not.toBeInTheDocument();
  });

  it('should call validateConnectivity on mount', () => {
    const mockValidateConnectivity = jest.fn();
    const mockUseValidateConnectivityMutation = require('#/lib/reducer/apiSlice').useValidateConnectivityMutation;

    mockUseValidateConnectivityMutation.mockReturnValue([
      mockValidateConnectivity,
      { isLoading: false, isSuccess: true, isError: false, error: null },
    ]);

    render(<WrappedComponent testProp="test" />);

    expect(mockValidateConnectivity).toHaveBeenCalledWith({});
  });

  it('should handle retry button click', () => {
    const mockValidateConnectivity = jest.fn();
    const mockUseValidateConnectivityMutation = require('#/lib/reducer/apiSlice').useValidateConnectivityMutation;

    mockUseValidateConnectivityMutation.mockReturnValue([
      mockValidateConnectivity,
      {
        isLoading: false,
        isSuccess: false,
        isError: true,
        error: { message: 'Network error' },
      },
    ]);

    render(<WrappedComponent testProp="test" />);

    const retryButton = screen.getByTestId('retry-button');
    fireEvent.click(retryButton);

    expect(mockValidateConnectivity).toHaveBeenCalledTimes(2); // Once on mount, once on retry
  });

  it('should use custom API URL from localStorage', () => {
    const mockValidateConnectivity = jest.fn();
    const mockUseValidateConnectivityMutation = require('#/lib/reducer/apiSlice').useValidateConnectivityMutation;
    const mockChangeAxiosBaseUrl = require('#/lib/store/rtk/rtkApiInstance').changeAxiosBaseUrl;

    mockLocalStorage.getItem.mockReturnValue('http://custom-api.com');
    mockUseValidateConnectivityMutation.mockReturnValue([
      mockValidateConnectivity,
      { isLoading: false, isSuccess: true, isError: false, error: null },
    ]);

    render(<WrappedComponent testProp="test" />);

    expect(mockLocalStorage.getItem).toHaveBeenCalledWith('sharpAPIUrl');
    expect(mockChangeAxiosBaseUrl).toHaveBeenCalledWith('http://custom-api.com');
  });

  it('should use default API URL when localStorage is empty', () => {
    const mockValidateConnectivity = jest.fn();
    const mockUseValidateConnectivityMutation = require('#/lib/reducer/apiSlice').useValidateConnectivityMutation;
    const mockChangeAxiosBaseUrl = require('#/lib/store/rtk/rtkApiInstance').changeAxiosBaseUrl;

    mockLocalStorage.getItem.mockReturnValue(null);
    mockUseValidateConnectivityMutation.mockReturnValue([
      mockValidateConnectivity,
      { isLoading: false, isSuccess: true, isError: false, error: null },
    ]);

    render(<WrappedComponent testProp="test" />);

    expect(mockChangeAxiosBaseUrl).toHaveBeenCalledWith('http://localhost:8000');
  });

  it('should handle error without message', () => {
    const mockValidateConnectivity = jest.fn();
    const mockUseValidateConnectivityMutation = require('#/lib/reducer/apiSlice').useValidateConnectivityMutation;

    mockUseValidateConnectivityMutation.mockReturnValue([
      mockValidateConnectivity,
      {
        isLoading: false,
        isSuccess: false,
        isError: true,
        error: 'Simple error string',
      },
    ]);

    render(<WrappedComponent testProp="test" />);

    expect(screen.getByTestId('error-text')).toHaveTextContent('"Simple error string"');
  });

  it('should set correct display name', () => {
    expect(WrappedComponent.displayName).toBe('withConnectivityValidation(TestComponent)');
  });

  it('should handle component without display name', () => {
    const AnonymousComponent = () => <div>Anonymous</div>;
    const WrappedAnonymous = withConnectivityValidation(AnonymousComponent);

    expect(WrappedAnonymous.displayName).toBe('withConnectivityValidation(AnonymousComponent)');
  });

  it('should match snapshot for loading state', () => {
    const mockValidateConnectivity = jest.fn();
    const mockUseValidateConnectivityMutation = require('#/lib/reducer/apiSlice').useValidateConnectivityMutation;

    mockUseValidateConnectivityMutation.mockReturnValue([
      mockValidateConnectivity,
      { isLoading: true, isSuccess: false, isError: false, error: null },
    ]);

    const { container } = render(<WrappedComponent testProp="test" />);
    expect(container.firstChild).toMatchSnapshot();
  });

  it('should match snapshot for error state', () => {
    const mockValidateConnectivity = jest.fn();
    const mockUseValidateConnectivityMutation = require('#/lib/reducer/apiSlice').useValidateConnectivityMutation;

    mockUseValidateConnectivityMutation.mockReturnValue([
      mockValidateConnectivity,
      {
        isLoading: false,
        isSuccess: false,
        isError: true,
        error: { message: 'Network error' },
      },
    ]);

    const { container } = render(<WrappedComponent testProp="test" />);
    expect(container.firstChild).toMatchSnapshot();
  });

  it('should match snapshot for success state', () => {
    const mockValidateConnectivity = jest.fn();
    const mockUseValidateConnectivityMutation = require('#/lib/reducer/apiSlice').useValidateConnectivityMutation;

    mockUseValidateConnectivityMutation.mockReturnValue([
      mockValidateConnectivity,
      { isLoading: false, isSuccess: true, isError: false, error: null },
    ]);

    const { container } = render(<WrappedComponent testProp="test" />);
    expect(container.firstChild).toMatchSnapshot();
  });
});

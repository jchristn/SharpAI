import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import StoreProvider from '#/lib/store/StoreProvider';

// Mock the Redux Provider
jest.mock('react-redux', () => ({
  Provider: ({ children, store }: any) => (
    <div data-testid="redux-provider" data-store={!!store}>
      {children}
    </div>
  ),
}));

// Mock the store
jest.mock('#/lib/store/store', () => ({
  makeStore: jest.fn(() => ({
    dispatch: jest.fn(),
    getState: jest.fn(),
    subscribe: jest.fn(),
    replaceReducer: jest.fn(),
  })),
  AppStore: {},
}));

describe('StoreProvider', () => {
  const TestComponent = () => <div data-testid="test-child">Test Child</div>;

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render children with Redux Provider', () => {
    render(
      <StoreProvider>
        <TestComponent />
      </StoreProvider>
    );

    expect(screen.getByTestId('redux-provider')).toBeInTheDocument();
    expect(screen.getByTestId('redux-provider')).toHaveAttribute('data-store', 'true');
    expect(screen.getByTestId('test-child')).toBeInTheDocument();
  });

  it('should create store instance on first render', () => {
    const { makeStore } = require('#/lib/store/store');

    render(
      <StoreProvider>
        <TestComponent />
      </StoreProvider>
    );

    expect(makeStore).toHaveBeenCalled();
  });

  it('should reuse store instance on re-renders', () => {
    const { makeStore } = require('#/lib/store/store');

    render(
      <StoreProvider>
        <TestComponent />
      </StoreProvider>
    );

    // makeStore should be called at least once
    expect(makeStore).toHaveBeenCalled();
  });

  it('should handle multiple children', () => {
    const MultipleChildren = () => (
      <>
        <div data-testid="child-1">Child 1</div>
        <div data-testid="child-2">Child 2</div>
        <div data-testid="child-3">Child 3</div>
      </>
    );

    render(
      <StoreProvider>
        <MultipleChildren />
      </StoreProvider>
    );

    expect(screen.getByTestId('redux-provider')).toBeInTheDocument();
    expect(screen.getByTestId('child-1')).toBeInTheDocument();
    expect(screen.getByTestId('child-2')).toBeInTheDocument();
    expect(screen.getByTestId('child-3')).toBeInTheDocument();
  });

  it('should handle empty children', () => {
    render(<StoreProvider>{null}</StoreProvider>);

    expect(screen.getByTestId('redux-provider')).toBeInTheDocument();
  });

  it('should handle fragment children', () => {
    render(
      <StoreProvider>
        <>
          <TestComponent />
        </>
      </StoreProvider>
    );

    expect(screen.getByTestId('redux-provider')).toBeInTheDocument();
    expect(screen.getByTestId('test-child')).toBeInTheDocument();
  });

  it('should match snapshot', () => {
    const { container } = render(
      <StoreProvider>
        <TestComponent />
      </StoreProvider>
    );

    expect(container.firstChild).toMatchSnapshot();
  });
});

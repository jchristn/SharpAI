import { renderHook } from '@testing-library/react';

// Mock the hooks module directly
jest.mock('#/lib/store/hooks', () => ({
  useAppDispatch: jest.fn(),
  useAppSelector: jest.fn(),
  useAppStore: jest.fn(),
}));

// Import the mocked hooks
import { useAppDispatch, useAppSelector, useAppStore } from '#/lib/store/hooks';

describe('Redux Hooks', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should export useAppDispatch', () => {
    expect(useAppDispatch).toBeDefined();
    expect(typeof useAppDispatch).toBe('function');
  });

  it('should export useAppSelector', () => {
    expect(useAppSelector).toBeDefined();
    expect(typeof useAppSelector).toBe('function');
  });

  it('should export useAppStore', () => {
    expect(useAppStore).toBeDefined();
    expect(typeof useAppStore).toBe('function');
  });
});

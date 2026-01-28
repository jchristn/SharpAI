import React, { renderHook, act } from '@testing-library/react';
import { render } from '@testing-library/react';
import { usePagination, AppContext, useAppContext } from '#/hooks/appHooks';
import { ThemeEnum } from '#/types/types';

describe('App Hooks', () => {
  describe('usePagination', () => {
    it('should initialize with default values', () => {
      const { result } = renderHook(() => usePagination());

      expect(result.current.page).toBe(1);
      expect(result.current.pageSize).toBe(10);
      expect(result.current.skip).toBe(0);
    });

    it('should handle page change correctly', () => {
      const { result } = renderHook(() => usePagination());

      act(() => {
        result.current.handlePageChange(2, 20);
      });

      expect(result.current.page).toBe(2);
      expect(result.current.pageSize).toBe(20);
      expect(result.current.skip).toBe(20); // (2-1) * 20
    });

    it('should calculate skip correctly for different pages', () => {
      const { result } = renderHook(() => usePagination());

      act(() => {
        result.current.handlePageChange(3, 15);
      });

      expect(result.current.skip).toBe(30); // (3-1) * 15
    });

    it('should handle page change to first page', () => {
      const { result } = renderHook(() => usePagination());

      // First change to page 3
      act(() => {
        result.current.handlePageChange(3, 10);
      });

      expect(result.current.page).toBe(3);
      expect(result.current.skip).toBe(20);

      // Then change back to page 1
      act(() => {
        result.current.handlePageChange(1, 10);
      });

      expect(result.current.page).toBe(1);
      expect(result.current.skip).toBe(0);
    });
  });

  describe('AppContext', () => {
    it('should provide default context values', () => {
      const TestComponent = () => {
        const context = useAppContext();
        return (
          <div>
            <span data-testid="theme">{context.theme}</span>
            <span data-testid="set-theme">{typeof context.setTheme}</span>
            <span data-testid="handle-task">{typeof context.handleBackgroundTask}</span>
          </div>
        );
      };

      const { getByTestId } = render(
        <AppContext.Provider
          value={{
            theme: ThemeEnum.LIGHT,
            setTheme: jest.fn(),
            handleBackgroundTask: jest.fn(),
          }}
        >
          <TestComponent />
        </AppContext.Provider>
      );

      expect(getByTestId('theme')).toHaveTextContent(ThemeEnum.LIGHT);
      expect(getByTestId('set-theme')).toHaveTextContent('function');
      expect(getByTestId('handle-task')).toHaveTextContent('function');
    });

    it('should allow theme changes', () => {
      const mockSetTheme = jest.fn();
      const TestComponent = () => {
        const context = useAppContext();
        return (
          <div>
            <span data-testid="theme">{context.theme}</span>
            <button onClick={() => context.setTheme(ThemeEnum.DARK)}>Change Theme</button>
          </div>
        );
      };

      const { getByTestId, getByText } = render(
        <AppContext.Provider
          value={{
            theme: ThemeEnum.LIGHT,
            setTheme: mockSetTheme,
            handleBackgroundTask: jest.fn(),
          }}
        >
          <TestComponent />
        </AppContext.Provider>
      );

      expect(getByTestId('theme')).toHaveTextContent(ThemeEnum.LIGHT);

      act(() => {
        getByText('Change Theme').click();
      });

      expect(mockSetTheme).toHaveBeenCalledWith(ThemeEnum.DARK);
    });

    it('should handle background tasks', async () => {
      const mockTask = jest.fn().mockResolvedValue(undefined);
      const mockHandleBackgroundTask = jest.fn().mockImplementation(async (task) => {
        await task();
      });

      const TestComponent = () => {
        const context = useAppContext();
        return <button onClick={() => context.handleBackgroundTask(mockTask)}>Run Task</button>;
      };

      const { getByText } = render(
        <AppContext.Provider
          value={{
            theme: ThemeEnum.LIGHT,
            setTheme: jest.fn(),
            handleBackgroundTask: mockHandleBackgroundTask,
          }}
        >
          <TestComponent />
        </AppContext.Provider>
      );

      await act(async () => {
        getByText('Run Task').click();
      });

      expect(mockHandleBackgroundTask).toHaveBeenCalledWith(mockTask);
      expect(mockTask).toHaveBeenCalled();
    });
  });

  describe('useAppContext', () => {
    it('should return context values', () => {
      const TestComponent = () => {
        const context = useAppContext();
        return <div data-testid="context">{JSON.stringify(context)}</div>;
      };

      const { getByTestId } = render(
        <AppContext.Provider
          value={{
            theme: ThemeEnum.DARK,
            setTheme: jest.fn(),
            handleBackgroundTask: jest.fn(),
          }}
        >
          <TestComponent />
        </AppContext.Provider>
      );

      const contextElement = getByTestId('context');
      expect(contextElement).toBeInTheDocument();
    });
  });
});

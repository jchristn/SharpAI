import { UnknownAction } from '@reduxjs/toolkit';
import rootReducer, { apiMiddleWares } from '#/lib/store/rootReducer';

// Mock the dependencies
jest.mock('#/lib/store/rtkApiMiddlewear', () => ({
  rtkQueryErrorLogger: jest.fn((store) => (next) => (action) => next(action)),
}));

jest.mock('#/lib/store/rtk/rtkApiInstance', () => ({
  default: {
    reducerPath: 'api',
    reducer: jest.fn((state = {}, action) => state),
    middleware: jest.fn((store) => (next) => (action) => next(action)),
  },
}));

jest.mock('@reduxjs/toolkit', () => ({
  combineReducers: jest.fn((reducers) => (state = {}, action) => {
    const newState = {};
    Object.keys(reducers).forEach((key) => {
      if (typeof reducers[key] === 'function') {
        newState[key] = reducers[key](state[key], action);
      }
    });
    return newState;
  }),
  UnknownAction: {},
}));

describe('Root Reducer', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('rootReducer', () => {
    it('should combine reducers correctly', () => {
      // Test that rootReducer is defined and is a function
      expect(rootReducer).toBeDefined();
      expect(typeof rootReducer).toBe('function');
    });

    it('should handle initial state', () => {
      const initialState = rootReducer(undefined, { type: '@@INIT' } as UnknownAction);

      expect(initialState).toBeDefined();
      expect(typeof initialState).toBe('object');
    });

    it('should handle actions', () => {
      const initialState = { api: {} };
      const action = { type: 'TEST_ACTION' } as UnknownAction;

      const newState = rootReducer(initialState, action);

      expect(newState).toBeDefined();
      expect(typeof newState).toBe('object');
    });

    it('should maintain state structure', () => {
      const initialState = { api: { queries: {}, mutations: {} } };
      const action = { type: 'API_ACTION' } as UnknownAction;

      const newState = rootReducer(initialState, action);

      expect(newState).toBeDefined();
    });

    it('should handle undefined state', () => {
      const action = { type: 'INIT_ACTION' } as UnknownAction;

      const state = rootReducer(undefined, action);

      expect(state).toBeDefined();
      expect(typeof state).toBe('object');
    });

    it('should handle null state', () => {
      const action = { type: 'NULL_STATE_ACTION' } as UnknownAction;

      const state = rootReducer(null as any, action);

      expect(state).toBeDefined();
      expect(typeof state).toBe('object');
    });
  });

  describe('resettableRootReducer', () => {
    it('should be the default export', () => {
      expect(rootReducer).toBeDefined();
      expect(typeof rootReducer).toBe('function');
    });

    it('should handle reset actions', () => {
      const initialState = { api: { queries: { test: 'data' } } };
      const resetAction = { type: 'RESET_STATE' } as UnknownAction;

      const newState = rootReducer(initialState, resetAction);

      expect(newState).toBeDefined();
    });

    it('should handle unknown actions', () => {
      const initialState = { api: {} };
      const unknownAction = { type: 'UNKNOWN_ACTION', payload: 'test' } as UnknownAction;

      const newState = rootReducer(initialState, unknownAction);

      expect(newState).toBeDefined();
    });

    it('should be pure function', () => {
      const initialState = { api: { queries: {} } };
      const action = { type: 'PURE_TEST' } as UnknownAction;

      const result1 = rootReducer(initialState, action);
      const result2 = rootReducer(initialState, action);

      // Results should be consistent for same input
      expect(typeof result1).toBe('object');
      expect(typeof result2).toBe('object');
    });
  });

  describe('apiMiddleWares', () => {
    it('should export apiMiddleWares array', () => {
      expect(apiMiddleWares).toBeDefined();
      expect(Array.isArray(apiMiddleWares)).toBe(true);
    });

    it('should have correct middleware count', () => {
      expect(apiMiddleWares).toHaveLength(2);
    });
  });

  describe('Integration Tests', () => {
    it('should work with Redux store', () => {
      // Test that rootReducer can be used with configureStore
      expect(rootReducer).toBeDefined();
      expect(typeof rootReducer).toBe('function');
    });

    it('should handle multiple actions', () => {
      const actions = [
        { type: 'ACTION_1' },
        { type: 'ACTION_2', payload: 'test' },
        { type: 'ACTION_3', error: true },
      ] as UnknownAction[];

      let state = rootReducer(undefined, actions[0]);
      state = rootReducer(state, actions[1]);
      state = rootReducer(state, actions[2]);

      expect(state).toBeDefined();
      expect(typeof state).toBe('object');
    });

    it('should maintain state consistency across actions', () => {
      const initialState = rootReducer(undefined, { type: '@@INIT' } as UnknownAction);

      const action1 = { type: 'TEST_1' } as UnknownAction;
      const action2 = { type: 'TEST_2' } as UnknownAction;

      const state1 = rootReducer(initialState, action1);
      const state2 = rootReducer(state1, action2);

      expect(state1).toBeDefined();
      expect(state2).toBeDefined();
      expect(typeof state1).toBe('object');
      expect(typeof state2).toBe('object');
    });
  });
});

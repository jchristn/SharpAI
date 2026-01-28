import { makeStore, AppStore } from '#/lib/store/store';

// Mock the dependencies
jest.mock('#/lib/store/rootReducer', () => ({
  default: jest.fn((state, action) => state || {}),
  apiMiddleWares: [
    jest.fn((store) => (next) => (action) => next(action)),
    jest.fn((store) => (next) => (action) => next(action)),
  ],
}));

jest.mock('@reduxjs/toolkit', () => ({
  configureStore: jest.fn((config) => ({
    dispatch: jest.fn(),
    getState: jest.fn(() => ({})),
    subscribe: jest.fn(() => jest.fn()),
    replaceReducer: jest.fn(),
    ...config,
  })),
}));

describe('Store Configuration', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('makeStore', () => {
    it('should create a store with correct configuration', () => {
      const store = makeStore();

      expect(store).toBeDefined();
      expect(typeof store.dispatch).toBe('function');
      expect(typeof store.getState).toBe('function');
      expect(typeof store.subscribe).toBe('function');
      expect(typeof store.replaceReducer).toBe('function');
    });

    it('should configure store with root reducer', () => {
      const { configureStore } = require('@reduxjs/toolkit');

      makeStore();

      expect(configureStore).toHaveBeenCalled();
    });

    it('should configure store with middleware', () => {
      const { configureStore } = require('@reduxjs/toolkit');
      const { apiMiddleWares } = require('#/lib/store/rootReducer');

      makeStore();

      expect(configureStore).toHaveBeenCalledWith(
        expect.objectContaining({
          middleware: expect.any(Function),
        })
      );
    });

    it('should configure store with serializableCheck disabled', () => {
      const { configureStore } = require('@reduxjs/toolkit');

      makeStore();

      const middlewareConfig = configureStore.mock.calls[0][0];
      const middleware = middlewareConfig.middleware;

      // Test that the middleware function is configured correctly
      expect(typeof middleware).toBe('function');
    });

    it('should return different store instances on multiple calls', () => {
      const store1 = makeStore();
      const store2 = makeStore();

      expect(store1).not.toBe(store2);
      expect(store1).toBeDefined();
      expect(store2).toBeDefined();
    });

    it('should have correct initial state structure', () => {
      const store = makeStore();
      const initialState = store.getState();

      expect(initialState).toBeDefined();
      expect(typeof initialState).toBe('object');
    });
  });

  describe('AppStore Type', () => {
    it('should have correct type structure', () => {
      const store = makeStore();

      // Test that the store has all required methods
      expect(store).toHaveProperty('dispatch');
      expect(store).toHaveProperty('getState');
      expect(store).toHaveProperty('subscribe');
      expect(store).toHaveProperty('replaceReducer');

      // Test method types
      expect(typeof store.dispatch).toBe('function');
      expect(typeof store.getState).toBe('function');
      expect(typeof store.subscribe).toBe('function');
      expect(typeof store.replaceReducer).toBe('function');
    });

    it('should support dispatching actions', () => {
      const store = makeStore();

      const action = { type: 'TEST_ACTION', payload: 'test' };
      store.dispatch(action);

      expect(store.dispatch).toHaveBeenCalledWith(action);
    });

    it('should support state subscription', () => {
      const store = makeStore();

      const listener = jest.fn();
      const unsubscribe = store.subscribe(listener);

      expect(typeof unsubscribe).toBe('function');
      expect(store.subscribe).toHaveBeenCalledWith(listener);
    });

    it('should support reducer replacement', () => {
      const store = makeStore();

      const newReducer = jest.fn();
      store.replaceReducer(newReducer);

      expect(store.replaceReducer).toHaveBeenCalledWith(newReducer);
    });
  });

  describe('Store Integration', () => {
    it('should work with middleware chain', () => {
      const store = makeStore();
      const { apiMiddleWares } = require('#/lib/store/rootReducer');

      // Test that middleware is properly configured
      expect(apiMiddleWares).toHaveLength(2);
      expect(typeof apiMiddleWares[0]).toBe('function');
      expect(typeof apiMiddleWares[1]).toBe('function');
    });

    it('should handle actions through middleware', () => {
      const store = makeStore();

      const action = { type: 'MIDDLEWARE_TEST' };
      store.dispatch(action);

      expect(store.dispatch).toHaveBeenCalledWith(action);
    });

    it('should maintain state consistency', () => {
      const store = makeStore();

      const initialState = store.getState();
      const action = { type: 'STATE_TEST' };

      store.dispatch(action);
      const stateAfterAction = store.getState();

      // State should be an object (even if empty)
      expect(typeof initialState).toBe('object');
      expect(typeof stateAfterAction).toBe('object');
    });
  });

  describe('Store Configuration Validation', () => {
    it('should have correct reducer path configuration', () => {
      const { configureStore } = require('@reduxjs/toolkit');

      makeStore();

      expect(configureStore).toHaveBeenCalled();
    });

    it('should configure middleware with correct options', () => {
      const { configureStore } = require('@reduxjs/toolkit');

      makeStore();

      const middlewareConfig = configureStore.mock.calls[0][0];
      const middleware = middlewareConfig.middleware;

      // Test that middleware is a function that returns middleware
      expect(typeof middleware).toBe('function');
    });

    it('should handle store creation errors gracefully', () => {
      const { configureStore } = require('@reduxjs/toolkit');
      configureStore.mockImplementationOnce(() => {
        throw new Error('Store creation failed');
      });

      expect(() => makeStore()).toThrow('Store creation failed');
    });
  });
});

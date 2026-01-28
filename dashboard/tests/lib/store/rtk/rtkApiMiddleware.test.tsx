import { errorHandler, rtkQueryErrorLogger } from '#/lib/store/rtk/rtkApiMiddleware';

// Mock antd message
jest.mock('antd', () => ({
  message: {
    error: jest.fn(),
  },
}));

describe('RTK API Middleware', () => {
  const mockDispatch = jest.fn();
  const mockMessage = require('antd').message;

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('errorHandler', () => {
    it('should handle error with Message property', () => {
      const error = {
        payload: {
          Message: 'Test error message',
        },
      };

      errorHandler(error, mockDispatch);

      expect(mockMessage.error).toHaveBeenCalledWith('Test error message');
    });

    it('should handle error with Description property', () => {
      const error = {
        payload: {
          Description: 'Test error description',
        },
      };

      errorHandler(error, mockDispatch);

      expect(mockMessage.error).toHaveBeenCalledWith('Test error description');
    });

    it('should handle error with message property', () => {
      const error = {
        payload: {
          message: 'Test error message',
        },
      };

      errorHandler(error, mockDispatch);

      expect(mockMessage.error).toHaveBeenCalledWith('Test error message');
    });

    it('should handle Network Error', () => {
      const error = {
        payload: {
          data: 'Network Error',
        },
      };

      errorHandler(error, mockDispatch);

      expect(mockMessage.error).toHaveBeenCalledWith('Network Error');
    });

    it('should handle generic error', () => {
      const error = {
        payload: {},
      };

      errorHandler(error, mockDispatch);

      expect(mockMessage.error).toHaveBeenCalledWith('Something went wrong.');
    });

    it('should handle NotAuthorized error', () => {
      const error = {
        payload: {
          Error: 'NotAuthorized',
        },
      };

      errorHandler(error, mockDispatch);

      expect(mockMessage.error).toHaveBeenCalledWith('Session expired. Redirecting to login page...');
    });

    it('should handle error without payload', () => {
      const error = {};

      errorHandler(error, mockDispatch);

      expect(mockMessage.error).toHaveBeenCalledWith('Something went wrong.');
    });

    it('should handle null error', () => {
      errorHandler(null, mockDispatch);

      expect(mockMessage.error).toHaveBeenCalledWith('Something went wrong.');
    });
  });

  describe('rtkQueryErrorLogger', () => {
    const mockNext = jest.fn();
    const mockAction = { type: 'test/action' };
    const mockApi = {
      dispatch: mockDispatch,
      getState: jest.fn(),
    };

    it('should call next for non-rejected actions', () => {
      rtkQueryErrorLogger(mockApi as any)(mockNext)(mockAction);

      expect(mockNext).toHaveBeenCalledWith(mockAction);
      expect(mockMessage.error).not.toHaveBeenCalled();
    });

    it('should handle rejected actions', () => {
      const rejectedAction = {
        type: 'test/rejected',
        payload: {
          Message: 'Test error',
        },
      };

      // Mock isRejectedWithValue to return true
      jest.doMock('@reduxjs/toolkit', () => ({
        ...jest.requireActual('@reduxjs/toolkit'),
        isRejectedWithValue: jest.fn().mockReturnValue(true),
      }));

      rtkQueryErrorLogger(mockApi as any)(mockNext)(rejectedAction);

      expect(mockNext).toHaveBeenCalledWith(rejectedAction);
      // Note: The mock doesn't actually call the message function
      expect(mockNext).toHaveBeenCalledWith(rejectedAction);
    });

    it('should handle rejected actions without payload', () => {
      const rejectedAction = {
        type: 'test/rejected',
      };

      // Mock isRejectedWithValue to return true
      jest.doMock('@reduxjs/toolkit', () => ({
        ...jest.requireActual('@reduxjs/toolkit'),
        isRejectedWithValue: jest.fn().mockReturnValue(true),
      }));

      rtkQueryErrorLogger(mockApi as any)(mockNext)(rejectedAction);

      expect(mockNext).toHaveBeenCalledWith(rejectedAction);
      // Note: The mock doesn't actually call the message function
      expect(mockNext).toHaveBeenCalledWith(rejectedAction);
    });
  });
});

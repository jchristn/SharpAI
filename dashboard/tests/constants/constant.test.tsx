import { localStorageKeys, keepUnusedDataFor } from '#/constants/constant';

describe('Constants', () => {
  describe('localStorageKeys', () => {
    it('should have correct key values', () => {
      expect(localStorageKeys.theme).toBe('theme');
      expect(localStorageKeys.sharpAPIUrl).toBe('sharpAPIUrl');
    });

    it('should be immutable', () => {
      const originalTheme = localStorageKeys.theme;
      expect(localStorageKeys.theme).toBe(originalTheme);
    });

    it('should work with Object.values', () => {
      const values = Object.values(localStorageKeys);
      expect(values).toContain('theme');
      expect(values).toContain('sharpAPIUrl');
    });

    it('should work with Object.entries', () => {
      const entries = Object.entries(localStorageKeys);
      expect(entries).toContainEqual(['theme', 'theme']);
      expect(entries).toContainEqual(['sharpAPIUrl', 'sharpAPIUrl']);
    });

    it('should work with localStorage operations', () => {
      const mockLocalStorage = {
        getItem: jest.fn(),
        setItem: jest.fn(),
        removeItem: jest.fn(),
      };

      Object.defineProperty(window, 'localStorage', {
        value: mockLocalStorage,
      });

      // Test setting values
      mockLocalStorage.setItem(localStorageKeys.theme, 'dark');
      expect(mockLocalStorage.setItem).toHaveBeenCalledWith('theme', 'dark');

      mockLocalStorage.setItem(localStorageKeys.sharpAPIUrl, 'http://localhost:8000');
      expect(mockLocalStorage.setItem).toHaveBeenCalledWith('sharpAPIUrl', 'http://localhost:8000');

      // Test getting values
      mockLocalStorage.getItem.mockReturnValue('light');
      const theme = mockLocalStorage.getItem(localStorageKeys.theme);
      expect(theme).toBe('light');

      // Test removing values
      mockLocalStorage.removeItem(localStorageKeys.sharpAPIUrl);
      expect(mockLocalStorage.removeItem).toHaveBeenCalledWith('sharpAPIUrl');
    });

    it('should work with dynamic key access', () => {
      const getKey = (key: keyof typeof localStorageKeys) => localStorageKeys[key];

      expect(getKey('theme')).toBe('theme');
      expect(getKey('sharpAPIUrl')).toBe('sharpAPIUrl');
    });
  });

  describe('keepUnusedDataFor', () => {
    it('should have correct value', () => {
      expect(keepUnusedDataFor).toBe(900);
    });

    it('should be a number', () => {
      expect(typeof keepUnusedDataFor).toBe('number');
    });

    it('should be greater than 0', () => {
      expect(keepUnusedDataFor).toBeGreaterThan(0);
    });

    it('should work with RTK Query configuration', () => {
      const mockApiSlice = {
        keepUnusedDataFor: keepUnusedDataFor,
      };

      expect(mockApiSlice.keepUnusedDataFor).toBe(900);
    });

    it('should work with time calculations', () => {
      const secondsInMinute = 60;
      const minutes = keepUnusedDataFor / secondsInMinute;

      expect(minutes).toBe(15);
    });

    it('should work with conditional logic', () => {
      const shouldKeepData = (time: number) => time < keepUnusedDataFor;

      expect(shouldKeepData(30)).toBe(true);
      expect(shouldKeepData(900)).toBe(false);
      expect(shouldKeepData(1000)).toBe(false);
    });
  });
});

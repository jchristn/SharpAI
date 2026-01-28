import {
  formatSize,
  formatSizeInMB,
  formatSizeInKB,
  formatDate,
  formatDateTime,
  formatError,
  parseJSON,
} from "#/utils/utils";

describe("Utils Functions", () => {
  describe("Size formatting functions", () => {
    it("should format size in GB", () => {
      expect(formatSize(1073741824)).toBe("1.00 GB"); // 1 GB in bytes
      expect(formatSize(2147483648)).toBe("2.00 GB"); // 2 GB in bytes
      expect(formatSize(536870912)).toBe("0.50 GB"); // 0.5 GB in bytes
    });

    it("should format size in MB", () => {
      expect(formatSizeInMB(1048576)).toBe("1.00 MB"); // 1 MB in bytes
      expect(formatSizeInMB(2097152)).toBe("2.00 MB"); // 2 MB in bytes
      expect(formatSizeInMB(524288)).toBe("0.50 MB"); // 0.5 MB in bytes
    });

    it("should format size in KB", () => {
      expect(formatSizeInKB(1024)).toBe("1.00 KB"); // 1 KB in bytes
      expect(formatSizeInKB(2048)).toBe("2.00 KB"); // 2 KB in bytes
      expect(formatSizeInKB(512)).toBe("0.50 KB"); // 0.5 KB in bytes
    });

    it("should handle zero size", () => {
      expect(formatSize(0)).toBe("0.00 GB");
      expect(formatSizeInMB(0)).toBe("0.00 MB");
      expect(formatSizeInKB(0)).toBe("0.00 KB");
    });

    it("should handle negative size", () => {
      expect(formatSize(-1024)).toBe("-0.00 GB");
      expect(formatSizeInMB(-1024)).toBe("-0.00 MB");
      expect(formatSizeInKB(-1024)).toBe("-1.00 KB");
    });
  });

  describe("Date formatting functions", () => {
    const testDate = "2024-01-15T10:30:00Z";
    const testDateISO = "2024-01-15T10:30:00.000Z";

    it("should format date", () => {
      const formatted = formatDate(testDate);
      expect(formatted).toBeDefined();
      expect(typeof formatted).toBe("string");
    });

    it("should format date and time", () => {
      const formatted = formatDateTime(testDate);
      expect(formatted).toBeDefined();
      expect(typeof formatted).toBe("string");
    });

    it("should handle invalid date", () => {
      const invalidDate = "invalid-date";
      const formatted = formatDate(invalidDate);
      expect(formatted).toBe("Invalid Date");
    });

    it("should handle invalid date and time", () => {
      const invalidDate = "invalid-date";
      const formatted = formatDateTime(invalidDate);
      expect(formatted).toBe("Invalid Date");
    });
  });

  describe("Error formatting function", () => {
    it("should format error with message", () => {
      const error = { message: "Test error message" };
      expect(formatError(error)).toBe("Test error message");
    });

    it("should format error with data", () => {
      const error = { data: { code: 500, message: "Server error" } };
      expect(formatError(error)).toBe('{"code":500,"message":"Server error"}');
    });

    it("should format error without message or data", () => {
      const error = { status: 404 };
      expect(formatError(error)).toBe('{"status":404}');
    });

    it("should handle null error", () => {
      expect(formatError(null)).toBe("null");
    });

    it("should handle undefined error", () => {
      expect(formatError(undefined)).toBe(undefined);
    });

    it("should handle string error", () => {
      expect(formatError("Simple string error")).toBe('"Simple string error"');
    });
  });

  describe("JSON parsing function", () => {
    // Mock console.log to avoid noise in tests
    const originalConsoleLog = console.log;
    beforeEach(() => {
      console.log = jest.fn();
    });
    afterEach(() => {
      console.log = originalConsoleLog;
    });

    it("should parse valid JSON", () => {
      const validJSON = '{"name": "test", "value": 123}';
      const result = parseJSON<{ name: string; value: number }>(validJSON);

      expect(result).toEqual({ name: "test", value: 123 });
    });

    it("should return null for invalid JSON", () => {
      const invalidJSON = '{"name": "test", "value": 123';
      const result = parseJSON(invalidJSON);

      expect(result).toBeNull();
      expect(console.log).toHaveBeenCalledWith(invalidJSON);
    });

    it("should handle empty string", () => {
      const result = parseJSON("");
      expect(result).toBeNull();
    });

    it("should handle null input", () => {
      const result = parseJSON(null as any);
      expect(result).toBeNull();
    });

    it("should handle undefined input", () => {
      const result = parseJSON(undefined as any);
      expect(result).toBeNull();
    });

    it("should parse array JSON", () => {
      const arrayJSON = '[{"id": 1}, {"id": 2}]';
      const result = parseJSON<Array<{ id: number }>>(arrayJSON);

      expect(result).toEqual([{ id: 1 }, { id: 2 }]);
    });

    it("should parse number JSON", () => {
      const numberJSON = "42";
      const result = parseJSON<number>(numberJSON);

      expect(result).toBe(42);
    });

    it("should parse string JSON", () => {
      const stringJSON = '"hello world"';
      const result = parseJSON<string>(stringJSON);

      expect(result).toBe("hello world");
    });
  });
});

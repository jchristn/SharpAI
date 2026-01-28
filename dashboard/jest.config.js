const nextJest = require("next/jest");

const createJestConfig = nextJest({
  dir: "./",
});

const customJestConfig = {
  coverageProvider: "v8",
  testEnvironment: "./jest.environment.js",
  moduleNameMapper: {
    "^#/(.*)$": "<rootDir>/src/$1",
    "^antd/es/(.*)$": "<rootDir>/node_modules/antd/lib/$1",
  },
  setupFilesAfterEnv: ["<rootDir>/jest.setup.js"],
  testEnvironmentOptions: {
    customExportConditions: [""],
  },
  extensionsToTreatAsEsm: [".ts", ".tsx"],
  globals: {
    "ts-jest": {
      useESM: true,
    },
  },
  transformIgnorePatterns: [
    "<rootDir>/node_modules/(?!react-hot-toast)/",
    "<rootDir>/node_modules/(?!until-async)/"
  ],
  collectCoverageFrom: [
    "src/**/*.{ts,tsx}",
    "!**/*.d.ts",
    "!**/node_modules/**",
    "!**/.next/**",
    "!**/coverage/**",
  ],
  coverageThreshold: {
    global: {
      branches: 80,
      functions: 80,
      lines: 80,
      statements: 80,
    },
  },
  coverageReporters: ["json", "lcov", "text", "clover"],
  coverageDirectory: "coverage",
};

module.exports = createJestConfig(customJestConfig);

/** @type {import('jest').Config} */
module.exports = {
  coverageProvider: "v8",
  testEnvironment: "./jest.environment.js",
  moduleNameMapper: {
    "^#/(.*)$": "<rootDir>/src/$1",
    "^antd/es/(.*)$": "<rootDir>/node_modules/antd/lib/$1",
    "\\.(css|scss|sass)$": "<rootDir>/tests/__mocks__/styleMock.js",
    "\\.(jpg|jpeg|png|gif|svg|ico)$": "<rootDir>/tests/__mocks__/fileMock.js",
  },
  setupFilesAfterEnv: ["<rootDir>/jest.setup.js"],
  transform: {
    "^.+\\.(ts|tsx)$": [
      "ts-jest",
      {
        tsconfig: {
          jsx: "react-jsx",
          esModuleInterop: true,
          module: "commonjs",
          target: "es2020",
          moduleResolution: "node",
          allowJs: true,
          resolveJsonModule: true,
          baseUrl: ".",
          paths: { "#/*": ["./src/*"] },
        },
      },
    ],
    "^.+\\.(js|jsx|mjs)$": "babel-jest",
  },
  testMatch: ["<rootDir>/tests/**/*.(test|spec).(ts|tsx|js|jsx)"],
  transformIgnorePatterns: [
    "/node_modules/(?!(react-hot-toast|until-async)/)",
  ],
  collectCoverageFrom: [
    "src/**/*.{ts,tsx}",
    "!**/*.d.ts",
    "!**/node_modules/**",
    "!**/coverage/**",
  ],
  coverageReporters: ["json", "lcov", "text", "clover"],
  coverageDirectory: "coverage",
};

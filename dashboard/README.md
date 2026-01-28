<img src="https://raw.githubusercontent.com/jchristn/sharpai/main/assets/logo.png" height="48">

# SharpAI Dashboard

A web interface for AI model interactions, built with Next.js and React.

> **Part of the SharpAI monorepo** - See the [main README](../README.md) for an overview of all components.

### SharpAI Github

https://github.com/jchristn/sharpai

## Features

- **Chat Completions**: Interactive AI chat interface
- **Embeddings**: Text embedding generation and management
- **Dashboard**: Comprehensive AI model management interface

## Requirements

- Node.js v18.20.4
- npm

## Quick Start

### Development Setup

#### Install dependencies:

```bash
npm install
```

#### Set the API configuration

Update the API configuration in [`src/constants/apiConfig.ts`](src/constants/apiConfig.ts) to point to your AI service instance.

#### Start the production server (for using Sharp AI locally):

```bash
npm run build
```

```bash
npm run start
```

OR

#### Start the development server (for development, can be used to test web ui locally as well):

```bash
npm run dev
```

The application will be available at `http://localhost:3000`.

### Testing

Run the test suite:

```bash
# Run all tests
npm test

# Run tests with coverage
npm run test:coverage

# Watch mode for development
npm run test:watch
```

## Deployment Process

#### Build the Application

Prepare the app for production:

```bash
npm run build
```

#### Start the Production Server

Start the built application:

```bash
npm run start
```

The app will be available at http://localhost:3000.

### Code Quality

The project uses several tools to maintain code quality:

- ESLint for code linting
- Prettier for code formatting
- TypeScript for type safety

## Development Guidelines

1. **Code Style**

   - Follow the Prettier configuration
   - Use TypeScript for type safety
   - Follow component-based architecture

2. **Testing**
   - Write unit tests for components
   - Test user interactions and API integrations

## Project Structure

- `src/app/` - Next.js app router pages
- `src/components/` - Reusable React components
- `src/lib/` - Redux store and API slice configuration
- `src/page/` - Page-specific components
- `src/theme/` - Theme configuration
- `src/constants/` - Application constants and configurations

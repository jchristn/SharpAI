<img src="https://raw.githubusercontent.com/jchristn/sharpai/main/assets/logo.png" height="48">

# SharpAI Dashboard

A web interface for the SharpAI server, built with Vite, React, and Ant Design.

> **Part of the SharpAI monorepo** — See the [main README](../README.md) for an overview of all components.

### SharpAI Github

https://github.com/jchristn/sharpai

## Features

- **Models** — Pull GGUF models from HuggingFace, delete ones you no longer need, see which models are loaded into memory, and monitor active downloads with a live progress bar and cancel button
- **Embeddings** — Generate vector embeddings for one or more inputs via the Ollama or OpenAI API flavor
- **Completions** — Send raw prompts to completion-capable models with full control over sampling parameters and stop sequences
- **Chat Completion** — Multi-turn conversations with chat-tuned models; the dashboard automatically wraps messages in the correct chat template (ChatML, Llama2/3, Mistral, etc.) based on the model's GGUF architecture
- **Configuration** — Live editor for every field in `sharpai.json`. Saves replace the in-memory server settings and rewrite the file on disk
- **Model-type awareness** — The embeddings page only shows embedding-capable models; completions pages only show completion-capable models. Driven by a `capabilities` object the server returns from `/api/tags`
- **Per-field tooltips** — Every column header, label, and input has an explanatory tooltip
- **Dark / light theme** with a purple accent drawn from the logo
- **Streaming-aware UI** — Animated "thinking" indicator, robust NDJSON parser, and automatic focus return after generation finishes

## Requirements

- Node.js 18+ (tested on 22)
- npm

## Quick Start

### Install dependencies

```bash
npm install
```

### Configure the API endpoint

Update [`src/constants/apiConfig.ts`](src/constants/apiConfig.ts) if your SharpAI server is running somewhere other than `http://localhost:8000`. The dashboard also lets you change this at runtime on the landing page.

### Development server

```bash
npm run dev
```

Vite's dev server starts in roughly 500 ms on the first run. Hot-module replacement is used for all code and SCSS changes — no page reload is required.

Available at `http://localhost:3000`.

### Production build

```bash
npm run build
```

This runs `tsc --noEmit` first (type-check), then `vite build` to produce a static `dist/` directory. Serve it with any static web server, or use `npm start` to preview it locally via `vite preview`:

```bash
npm start
```

### Testing

```bash
# Run all tests
npm test

# Run tests with coverage
npm run test:coverage

# Watch mode for development
npm run test:watch
```

Tests use Jest with `ts-jest` and the jsdom environment. Setup lives in [`jest.setup.js`](jest.setup.js).

## Project Structure

- `index.html` — Vite entry document with the root `<head>` tags and `<script type="module" src="/src/main.tsx">`
- `vite.config.ts` — Vite + React plugin + `vite-tsconfig-paths` + SCSS modern-compiler API
- `src/main.tsx` — ReactDOM root, mounts `<ErrorBoundary>` + `<AppProviders>` + `<App>`
- `src/App.tsx` — `<BrowserRouter>` + `<Routes>` declaring every navigable route with a nested dashboard layout
- `src/components/` — Reusable React components (tables, forms, buttons, tooltips, etc.)
- `src/components/layout/DashboardLayout.tsx` — Top-level dashboard shell with sidebar + header + `<Outlet />`
- `src/components/base/sidebar/` — Left navigation sidebar with Models / Embeddings / Completions / Chat Completion / Configuration links and version display
- `src/components/base/tooltip/` — Shared `SharpTooltip` wrapper and `TooltipHeader` column helper
- `src/page/` — Page-level components: `landing`, `dashboard-home`, `embeddings`, `completion`, `chat-completion`, `configuration`
- `src/hoc/` — `AppProviders`, `ErrorBoundary`, `PullProgressProvider` (owns the live model-pull lifecycle and background download state)
- `src/lib/store/` — Redux Toolkit store and RTK Query `apiSlice` with hooks for every SharpAI endpoint
- `src/lib/reducer/` — API slice definitions and TypeScript types for server responses
- `src/constants/` — App constants, `tooltips.ts` (centralized tooltip text and page descriptions), and `apiConfig.ts`
- `src/theme/` — Ant Design `ConfigProvider` theme tokens for light and dark modes
- `src/utils/` — Helpers: size formatting, error formatting, NDJSON streaming parser, model capability filtering

## Tech Stack

- **Vite 5** for dev server and production bundling
- **React 18** with React Router 6 for routing
- **TypeScript 5** in strict mode
- **Ant Design 5** for the component library (`@ant-design/cssinjs` for styling, path-aliased via `vite-tsconfig-paths`)
- **Redux Toolkit** and **RTK Query** with an `axios`-backed base query for API calls, including streaming progress callbacks and abort signals
- **SCSS modules** via `sass` in modern-compiler mode
- **Jest + ts-jest** for unit and snapshot tests

## Code Quality

- ESLint (flat config in `eslint.config.mjs`) with `@typescript-eslint`, `eslint-plugin-react-hooks`, and `eslint-plugin-react-refresh`
- TypeScript strict mode on the app (`tsconfig.json` `strict: true`; tests live under `tests/` and are excluded from the production type-check because they contain pre-existing strict-null-check violations that ts-jest handles at test time)

## Migrating from the Next.js dashboard

The dashboard was migrated from Next.js 14 App Router to Vite in v4.0.0. There is no SSR, no `src/app/` directory, no `next/link`, no `next/navigation`, and no `AntdRegistry` wrapper. Routes are now declared in `src/App.tsx` via `react-router-dom`. `"use client"` directives have been removed; every component is a client component.

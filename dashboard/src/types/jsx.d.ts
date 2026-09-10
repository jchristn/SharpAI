// React 19's @types/react no longer declares a global `JSX` namespace (it lives under `React.JSX`).
// This shim re-exposes `JSX.Element` globally so existing `: JSX.Element` return annotations keep
// resolving. Intrinsic-element type-checking continues to flow through the react-jsx runtime's React.JSX.

import type { JSX as ReactJSX } from 'react';

declare global {
  namespace JSX {
    type Element = ReactJSX.Element;
  }
}

export {};

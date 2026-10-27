import '@testing-library/jest-dom';
import { expect, vi } from 'vitest';
import { configure } from '@testing-library/react';
import { toHaveNoViolations } from 'jest-axe';

expect.extend(toHaveNoViolations);

// Testing Library's default asyncUtilTimeout is 1000ms, which is a wall-clock budget rather
// than a correctness bound: findBy*/waitFor still resolve the moment their condition holds,
// so a passing test is no slower for this. The default is too tight for suites that render
// lazily-loaded routes behind <Suspense> while the machine is busy — a loaded CI container
// exceeded it and failed tests that pass every time in isolation.
configure({ asyncUtilTimeout: 5000 });

// happy-dom has no layout engine, so window.matchMedia is unreliable. Every test runs at a
// desktop width: `(min-width: Npx)` / `(max-width: Npx)` are answered against 1280px, and any
// other query (prefers-color-scheme, …) does not match. The same fake foundation's setup
// installs, reduced to the queries this app's components ask.
const TEST_VIEWPORT_WIDTH = 1280;
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  configurable: true,
  value: vi.fn((query: string) => {
    const min = /\(min-width:\s*(\d+)px\)/.exec(query);
    const max = /\(max-width:\s*(\d+)px\)/.exec(query);
    const matches =
      (min !== null || max !== null) &&
      (!min || TEST_VIEWPORT_WIDTH >= Number(min[1])) &&
      (!max || TEST_VIEWPORT_WIDTH <= Number(max[1]));
    return {
      matches,
      media: query,
      onchange: null,
      addEventListener: () => {},
      removeEventListener: () => {},
      addListener: () => {},
      removeListener: () => {},
      dispatchEvent: () => false,
    } as unknown as MediaQueryList;
  }),
});

// Polyfill for Radix UI pointer capture (not implemented in happy-dom)
if (!Element.prototype.hasPointerCapture) {
  Element.prototype.hasPointerCapture = function () {
    return false;
  };
}

if (!Element.prototype.setPointerCapture) {
  Element.prototype.setPointerCapture = function () {};
}

if (!Element.prototype.releasePointerCapture) {
  Element.prototype.releasePointerCapture = function () {};
}

// Radix Select scrolls the active item into view on open; happy-dom has no
// layout so it ships no implementation. Stub it as a no-op.
if (!Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = function () {};
}

// Testing Library registers its own afterEach(cleanup) because vitest runs with globals: true.

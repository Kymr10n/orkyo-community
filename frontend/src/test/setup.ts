import '@testing-library/jest-dom';
import { afterEach } from 'vitest';
import { cleanup, configure } from '@testing-library/react';

// Testing Library's default asyncUtilTimeout is 1000ms, which is a wall-clock budget rather
// than a correctness bound: findBy*/waitFor still resolve the moment their condition holds,
// so a passing test is no slower for this. The default is too tight for suites that render
// lazily-loaded routes behind <Suspense> while the machine is busy — a loaded CI container
// exceeded it and failed tests that pass every time in isolation.
configure({ asyncUtilTimeout: 5000 });

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

// Cleanup after each test case
afterEach(() => {
  cleanup();
});

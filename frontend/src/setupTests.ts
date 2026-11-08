import { afterEach } from "vitest";
import { cleanup, configure } from "@testing-library/react";

configure({
  asyncUtilTimeout: 5000,
});

// globals: false means Testing Library cannot register its own cleanup, so rendered trees stayed
// mounted and late React work could run after the jsdom environment was torn down
// ("window is not defined" in CI). Unmount after every test.
afterEach(() => {
  cleanup();
});

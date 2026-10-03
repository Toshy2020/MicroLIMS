/// <reference types="vite/client" />

declare module "vitest" {
  export const describe: (name: string, fn: () => void) => void;
  export const it: (name: string, fn: () => void | Promise<void>) => void;
  export const test: (name: string, fn: () => void | Promise<void>) => void;
  export const expect: any;
  export const vi: any;
  export const beforeEach: any;
  export const afterEach: any;
}

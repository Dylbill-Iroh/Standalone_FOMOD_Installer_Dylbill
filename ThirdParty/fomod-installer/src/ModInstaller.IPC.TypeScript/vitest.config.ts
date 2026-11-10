import { defineConfig } from 'vitest/config';
import path from 'path';

export default defineConfig({
  resolve: {
    alias: {
      // vortex-api is a git dependency with broken exports — alias to our mock
      'vortex-api': path.resolve(__dirname, 'test/__mocks__/vortex-api.ts'),
      // winapi-bindings requires native builds — alias to empty mock for tests
      'winapi-bindings': path.resolve(__dirname, 'test/__mocks__/winapi-bindings.ts'),
    },
  },
  test: {
    include: ['test/**/*.spec.ts'],
    testTimeout: 120_000,
  },
});

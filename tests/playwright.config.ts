import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: '**/*.spec.ts',
  timeout: 30000,
  use: {
    baseURL: 'http://localhost:8080',
  },
  // No browsers needed — API tests only
  projects: [
    {
      name: 'api',
      use: {},
    },
  ],
});

import { defineConfig, devices } from '@playwright/test'

// Playwright runs against the dev stack on localhost:5173. The dev stack
// is started separately by `make dev` (or ./scripts/dev.sh) before running
// the tests — we don't have Playwright start/stop the server because the
// full stack involves docker (postgres), dotnet, and vite, and the
// orchestration is already in scripts/dev.sh.
//
// Before running `npm run e2e`:
// 1. make dev (or ./scripts/dev.sh)
// 2. wait until both backend (8080) and frontend (5173) are ready
// 3. ensure the dev seed user 'dev@fasolt.local' / 'Dev1234!' exists
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})

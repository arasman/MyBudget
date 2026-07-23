import { defineConfig, devices } from '@playwright/test'

/**
 * Playwright E2E configuration for MyBudget.
 * Requires the full Docker Compose stack running locally.
 * Base URL: http://localhost:5173 (Vite dev server or preview).
 *
 * DB isolation:
 *   - API must be started with ASPNETCORE_ENVIRONMENT=E2E before running tests.
 *   - Default API port: 5079. Override with E2E_API_URL env var.
 *   - globalSetup resets mybudget_e2e before the suite; globalTeardown cleans up after.
 */
export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup',
  globalTeardown: './e2e/global-teardown',
  fullyParallel: false, // sequential to avoid test DB collisions
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],

  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:5173',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        launchOptions: {
          args: ['--no-sandbox', '--disable-dev-shm-usage', '--disable-gpu', '--disable-software-rasterizer'],
        },
      },
    },
  ],
})

import { defineConfig, devices } from '@playwright/test'

/**
 * Playwright E2E configuration for MyBudget.
 * Requires the full Docker Compose stack running locally.
 * Base URL: http://localhost:5173 (Vite dev server or preview).
 */
export default defineConfig({
  testDir: './e2e',
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

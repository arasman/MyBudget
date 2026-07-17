import type { Page } from '@playwright/test'
import { expect } from '@playwright/test'

/**
 * Asserts that a toast alert with the given text is visible within 8 seconds.
 * Uses role="alert" filtered by text content, matching the pattern established
 * in budget-structure/helpers.ts.
 */
export async function expectToast(page: Page, text: string): Promise<void> {
  await expect(
    page.getByRole('alert').filter({ hasText: text }).first(),
  ).toBeVisible({ timeout: 8_000 })
}

import type { Page } from '@playwright/test'

export interface LoginTokens {
  accessToken: string
  refreshToken?: string
  activeBudgetId?: string
}

/**
 * Injects authentication tokens into browser localStorage so the Vue app
 * considers the user authenticated without a UI login flow.
 *
 * Sets accessToken, refreshToken (defaults to empty string), and
 * activeBudgetId when provided.
 */
export async function loginWithToken(page: Page, tokens: LoginTokens): Promise<void> {
  await page.goto('/')
  await page.evaluate(
    ({ accessToken, refreshToken, activeBudgetId }) => {
      localStorage.setItem('accessToken', accessToken)
      localStorage.setItem('refreshToken', refreshToken ?? '')
      if (activeBudgetId) {
        localStorage.setItem('activeBudgetId', activeBudgetId)
      }
    },
    {
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      activeBudgetId: tokens.activeBudgetId,
    },
  )
}

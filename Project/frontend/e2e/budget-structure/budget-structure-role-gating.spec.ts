import { test, expect } from '@playwright/test'
import { seedOwnerAndLogin, seedReadOnlyAndLogin } from './helpers'

/**
 * E2E: Role gating — read-only user sees no write actions
 *
 * Strategy: the "read-only" user is a registered user with NO budget memberships
 * (they own their own auto-created budget but NOT the owner's budget).
 * When navigating to the owner's budget routes, their role resolves to undefined
 * → all useRoleGate flags are false → no write buttons shown.
 *
 * Prerequisites: Docker Compose stack running.
 */
test.describe('Budget Structure — Role Gating', () => {
  test('read-only user sees no write buttons in cycle list', async ({ page }) => {
    // Setup: create an owner's budget
    const { budgetId } = await seedOwnerAndLogin(page, 'rg-setup')

    // Now register and login as a second user (no membership in owner's budget)
    await seedReadOnlyAndLogin(page)

    // Navigate to the owner's budget cycles — this user has no membership here
    await page.goto(`/budgets/${budgetId}/cycles`)

    // The page should load (it's a public route in terms of navigation)
    // but no "New Cycle" button should appear in the navbar
    await expect(page.getByRole('navigation').getByRole('button', { name: 'New Cycle' })).not.toBeVisible({
      timeout: 5_000,
    })
  })

  test('read-only user sees no write buttons in category tree', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'rg-cat')
    await seedReadOnlyAndLogin(page)

    await page.goto(`/budgets/${budgetId}/categories`)

    await expect(page.getByRole('navigation').getByRole('button', { name: 'New Group' })).not.toBeVisible({
      timeout: 5_000,
    })
  })

  test('owner user sees write buttons in cycle list', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'rg-owner')

    await page.goto(`/budgets/${budgetId}/cycles`)

    // Owner should see "New Cycle" page action in the navbar
    await expect(page.getByRole('navigation').getByRole('button', { name: 'New Cycle' })).toBeVisible({
      timeout: 5_000,
    })
  })
})

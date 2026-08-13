import { test, expect } from '@playwright/test'
import { seedOwnerAndLogin } from './helpers'
import { inviteMemberWithRole } from '../dashboard/helpers'

/**
 * E2E: Budget Members — open tab, demote a member (WU1 slice, PR2b)
 *
 * Prerequisites: Docker Compose stack running (including Mailpit on port 8025,
 * used by inviteMemberWithRole's real invite → accept round trip).
 *
 * WU2 (soft-delete, remove/restore, show-deleted toggle) is explicitly out of
 * scope here — see budget-member-administration tasks.md PR3 (extends this spec).
 */
test.describe('Budget Structure — Members tab (MEMBERS-UI-1/MEMBERS-ROLE-1, WU1)', () => {
  test('Owner opens the Members tab, sees the member list, and demotes an Admin to Operator', async ({
    page,
    request,
  }) => {
    // Setup: Owner's budget + one Admin member (via real invite → accept flow)
    const { budgetId, accessToken } = await seedOwnerAndLogin(page, 'members')
    const ownerHeaders = { Authorization: `Bearer ${accessToken}` }
    await inviteMemberWithRole(request, ownerHeaders, budgetId, 'admin', 'members-admin')

    // Owner starts on the default Cycles tab
    await page.goto(`/budgets/${budgetId}/cycles`)
    await page.waitForLoadState('networkidle')

    // Open the Members tab via the tab link (proves tab visibility + navigation, REQ-NAV-1)
    await page.getByRole('tab', { name: 'Members' }).click()
    await expect(page).toHaveURL(new RegExp(`/budgets/${budgetId}/members$`))

    // Member list renders — the invited Admin's row has a role select (Owner can act on Admin)
    const roleSelect = page.getByRole('combobox', { name: 'Change role' })
    await expect(roleSelect).toBeVisible({ timeout: 10_000 })
    await expect(roleSelect).toHaveValue('admin')

    // Demote Admin -> Operator
    await roleSelect.selectOption('operator')
    await expect(page.getByText('Member role updated successfully')).toBeVisible({ timeout: 10_000 })

    // Reload and confirm the new role persisted server-side
    await page.reload()
    await page.waitForLoadState('networkidle')
    await expect(page.getByRole('combobox', { name: 'Change role' })).toHaveValue('operator', {
      timeout: 10_000,
    })
  })
})

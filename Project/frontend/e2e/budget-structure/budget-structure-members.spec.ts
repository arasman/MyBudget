import { test, expect } from '@playwright/test'
import { seedOwnerAndLogin } from './helpers'
import { inviteMemberWithRole } from '../dashboard/helpers'

/**
 * E2E: Budget Members — open tab, demote a member (WU1 slice, PR2b).
 * Extended in WU2 (PR3) with revoke → show-deleted → restore, and the
 * cache-eviction contract proven end-to-end in the browser.
 *
 * Prerequisites: Docker Compose stack running (including Mailpit on port 8025,
 * used by inviteMemberWithRole's real invite → accept round trip).
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

/**
 * E2E: Revoke, show-deleted, restore (WU2, PR3). Extends the WU1 spec above with
 * MEMBERS-REMOVE-1 / MEMBERS-RESTORE-1 / MEMBERS-UI-1's WU2 scenarios, plus the
 * security-critical cache-eviction proof (AUTHZ-1) in the actual browser session.
 */
test.describe('Budget Structure — Members tab revoke/restore (MEMBERS-REMOVE-1/MEMBERS-RESTORE-1, WU2)', () => {
  test('Owner revokes a member, the row disappears; show-deleted reveals it dimmed; Restore brings it back', async ({
    page,
    request,
  }) => {
    const { budgetId, accessToken } = await seedOwnerAndLogin(page, 'members-wu2')
    const ownerHeaders = { Authorization: `Bearer ${accessToken}` }
    await inviteMemberWithRole(request, ownerHeaders, budgetId, 'operator', 'members-wu2-op')

    await page.goto(`/budgets/${budgetId}/members`)
    await page.waitForLoadState('networkidle')

    // Member row is visible with a Remove control
    const removeButton = page.getByRole('button', { name: 'Remove' })
    await expect(removeButton).toBeVisible({ timeout: 10_000 })

    // Revoke: click Remove -> confirm dialog -> Confirm
    await removeButton.click()
    await expect(page.getByText('Are you sure you want to remove this member?')).toBeVisible()
    await page.getByRole('button', { name: 'Confirm' }).click()
    await expect(page.getByText('Member removed successfully')).toBeVisible({ timeout: 10_000 })

    // Default view: the removed member's row disappears
    await expect(removeButton).toHaveCount(0)

    // Toggle "show deleted" — the removed member's row reappears, dimmed, with Restore
    await page.getByLabel('Show deleted').check()
    const restoreButton = page.getByRole('button', { name: 'Restore' })
    await expect(restoreButton).toBeVisible({ timeout: 10_000 })

    // Restore brings the member back to the active list
    await restoreButton.click()
    await expect(page.getByText('Member restored successfully')).toBeVisible({ timeout: 10_000 })
    await expect(restoreButton).toHaveCount(0)
  })

  test('A revoked member immediately loses access to a budget:*-gated page — proves synchronous cache eviction', async ({
    page,
    request,
  }) => {
    const { budgetId, accessToken } = await seedOwnerAndLogin(page, 'members-wu2-cache')
    const ownerHeaders = { Authorization: `Bearer ${accessToken}` }
    const member = await inviteMemberWithRole(
      request,
      ownerHeaders,
      budgetId,
      'operator',
      'members-wu2-cache-op',
    )
    const memberHeaders = { Authorization: `Bearer ${member.accessToken}` }

    // Warm the member's auth cache entry moments before revocation (within the 5-min TTL).
    const warmup = await request.get(`/api/budgets/${budgetId}/cycles`, { headers: memberHeaders })
    expect(warmup.status()).toBe(200)

    // Owner revokes the member via the UI.
    await page.goto(`/budgets/${budgetId}/members`)
    await page.waitForLoadState('networkidle')
    await page.getByRole('button', { name: 'Remove' }).click()
    await page.getByRole('button', { name: 'Confirm' }).click()
    await expect(page.getByText('Member removed successfully')).toBeVisible({ timeout: 10_000 })

    // Immediately after — in the SAME browser test run, not after TTL expiry — the removed
    // member's session must lose access to any budget:*-gated endpoint. If the cache entry
    // were not evicted synchronously, this would still return 200.
    const afterRevoke = await request.get(`/api/budgets/${budgetId}/cycles`, { headers: memberHeaders })
    expect(afterRevoke.status()).toBe(403)
  })
})

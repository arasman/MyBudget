import { test, expect } from '@playwright/test'
import { shoot, flushManifest } from './capture'
import {
  seedDashboardBudget,
  upsertCutRecord,
  createExecution,
  loginWithToken,
  goToDashboard,
} from '../dashboard/helpers'

/**
 * Slide screenshots — Dashboard (lifetime trend, BudgetLine comparison,
 * cross-cycle mode, insufficient-history empty state, mobile viewport).
 * Reuses dashboard/helpers.ts (API fixture seeding) rather than duplicating
 * it — it's fixture-heavy and already proven by dashboard-golden-path.spec.ts.
 * Images land in docs/slides/dashboard/.
 */
const FLOW = 'dashboard'

test.describe('Slides — Dashboard', () => {
  test.afterAll(() => flushManifest(FLOW))

  test('lifetime trend + series picker empty state', async ({ page, request }) => {
    const fixture = await seedDashboardBudget(request, 'slide-dash-lifetime', 2)
    await upsertCutRecord(request, fixture, '2026-02-01', 1000)
    await upsertCutRecord(request, fixture, '2026-08-01', 2000)

    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToDashboard(page, fixture.budgetId)

    await expect(page.getByText('Lifetime Trend')).toBeVisible({ timeout: 10_000 })
    const lifetimeSection = page.locator('section', { hasText: 'Lifetime Trend' })
    await expect(lifetimeSection.locator('canvas')).toBeVisible({ timeout: 10_000 })
    await shoot(page, FLOW, 1, 'lifetime-trend', 'Dashboard — lifetime trend', 'The default landing view: net-worth trend across all cut records.')

    await lifetimeSection.getByRole('button', { name: 'Clear all' }).click()
    await expect(lifetimeSection.getByText('No data to display')).toBeVisible({ timeout: 5_000 })
    await shoot(page, FLOW, 2, 'series-picker-empty', 'Dashboard — series picker empty state', 'Clearing the series picker drives the chart to its empty state, proving the picker controls it.')
  })

  test('BudgetLine chart: empty → selected, and cross-cycle mode', async ({ page, request }) => {
    const fixture = await seedDashboardBudget(request, 'slide-dash-line', 2)
    await upsertCutRecord(request, fixture, '2026-02-01', 1000)
    await createExecution(request, fixture, fixture.periodIds[0]!, 150, '2026-01-15')
    await createExecution(request, fixture, fixture.periodIds[1]!, 250, '2026-07-15')

    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToDashboard(page, fixture.budgetId)

    const lineSection = page.locator('section', { hasText: 'Budget Line Behavior' })
    await expect(lineSection).toBeVisible({ timeout: 10_000 })
    await expect(lineSection.getByText('No data to display')).toBeVisible({ timeout: 5_000 })
    await shoot(page, FLOW, 3, 'budget-line-empty', 'Dashboard — BudgetLine chart empty state', 'No line or period selected yet.')

    await lineSection.getByLabel('E2E Dashboard Line').check()
    await lineSection.getByLabel('P1').check()
    await lineSection.getByLabel('P2').check()
    await expect(lineSection.locator('canvas')).toBeVisible({ timeout: 10_000 })
    await shoot(page, FLOW, 4, 'budget-line-selected', 'Dashboard — BudgetLine chart', 'A line and two periods selected: within-cycle period-vs-period comparison.')

    await expect(lineSection.getByText('Cycle', { exact: true })).toBeVisible()
    await lineSection.getByRole('button', { name: 'Cross-cycle' }).click()
    await expect(lineSection.getByText('Cycles', { exact: true })).toBeVisible()
    await shoot(page, FLOW, 5, 'cross-cycle-mode', 'Dashboard — cross-cycle mode', 'Switching modes swaps the within-cycle period picker for a cycle picker.')
  })

  test('insufficient history empty state', async ({ page, request }) => {
    const fixture = await seedDashboardBudget(request, 'slide-dash-insufficient', 2)
    await upsertCutRecord(request, fixture, '2026-02-01', 1000) // 1 cut total — below the 2-cut minimum

    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToDashboard(page, fixture.budgetId)

    const bandSection = page.locator('section', { hasText: 'Average Behavior' })
    await expect(bandSection).toBeVisible({ timeout: 10_000 })
    await expect(bandSection.getByText('Not enough history yet')).toBeVisible({ timeout: 10_000 })
    await shoot(page, FLOW, 6, 'insufficient-history', 'Dashboard — insufficient history', 'Fewer than 2 cut records: an explicit empty state instead of a misleading computed band.')
  })

  test('mobile viewport', async ({ page, request }) => {
    await page.setViewportSize({ width: 375, height: 812 })

    const fixture = await seedDashboardBudget(request, 'slide-dash-mobile', 2)
    await upsertCutRecord(request, fixture, '2026-02-01', 1000)
    await upsertCutRecord(request, fixture, '2026-08-01', 2000)

    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToDashboard(page, fixture.budgetId)

    await expect(page.getByText('Lifetime Trend')).toBeVisible({ timeout: 10_000 })
    await shoot(page, FLOW, 7, 'mobile-viewport', 'Dashboard — mobile viewport', 'The dashboard at a 375px-wide viewport, no horizontal overflow.')
  })
})

import { test, expect } from '@playwright/test'
import {
  seedDashboardBudget,
  upsertCutRecord,
  createExecution,
  loginWithToken,
  goToDashboard,
} from './helpers'

/**
 * E2E: Dashboard golden path — default lifetime view, series-picker driving
 * the chart, BudgetLine selection + comparison-mode switch, and the
 * insufficient-history empty state.
 * Spec: DASH-1, DASH-2, DASH-3, DASH-5, DASH-6, DASH-7.
 */
test.describe('Dashboard — golden path', () => {
  test('default load shows the lifetime trend chart, not KPI tiles (DASH-7)', async ({ page, request }) => {
    const fixture = await seedDashboardBudget(request, 'dash-default', 2)
    await upsertCutRecord(request, fixture, '2026-02-01', 1000)
    await upsertCutRecord(request, fixture, '2026-08-01', 2000)

    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToDashboard(page, fixture.budgetId)

    // Lifetime section renders first (DASH-7 default landing content).
    await expect(page.getByText('Lifetime Trend')).toBeVisible({ timeout: 10_000 })
    const lifetimeSection = page.locator('section', { hasText: 'Lifetime Trend' })
    await expect(lifetimeSection.locator('canvas')).toBeVisible({ timeout: 10_000 })
  })

  test('series picker updates the lifetime chart (DASH-2/DASH-7)', async ({ page, request }) => {
    const fixture = await seedDashboardBudget(request, 'dash-picker', 2)
    await upsertCutRecord(request, fixture, '2026-02-01', 1000)
    await upsertCutRecord(request, fixture, '2026-08-01', 2000)

    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToDashboard(page, fixture.budgetId)

    const lifetimeSection = page.locator('section', { hasText: 'Lifetime Trend' })
    await expect(lifetimeSection.locator('canvas')).toBeVisible({ timeout: 10_000 })

    // Clearing the series selection drives the chart to its empty state —
    // proves the picker is wired to BaseChart, not just decorative.
    await lifetimeSection.getByRole('button', { name: 'Clear all' }).click()
    await expect(lifetimeSection.getByText('No data to display')).toBeVisible({ timeout: 5_000 })
    await expect(lifetimeSection.locator('canvas')).toHaveCount(0)

    // Selecting again brings the chart back.
    await lifetimeSection.getByRole('button', { name: 'Select all' }).click()
    await expect(lifetimeSection.locator('canvas')).toBeVisible({ timeout: 5_000 })
  })

  test('selecting a BudgetLine + 2 periods renders the BudgetLine chart (DASH-4/5)', async ({ page, request }) => {
    const fixture = await seedDashboardBudget(request, 'dash-line', 2)
    await upsertCutRecord(request, fixture, '2026-02-01', 1000)
    await createExecution(request, fixture, fixture.periodIds[0]!, 150, '2026-01-15')
    await createExecution(request, fixture, fixture.periodIds[1]!, 250, '2026-07-15')

    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToDashboard(page, fixture.budgetId)

    const lineSection = page.locator('section', { hasText: 'Budget Line Behavior' })
    await expect(lineSection).toBeVisible({ timeout: 10_000 })

    // No line/periods selected yet — chart is in its empty state.
    await expect(lineSection.getByText('No data to display')).toBeVisible({ timeout: 5_000 })

    await lineSection.getByLabel('E2E Dashboard Line').check()
    await lineSection.getByLabel('P1').check()
    await lineSection.getByLabel('P2').check()

    await expect(lineSection.locator('canvas')).toBeVisible({ timeout: 10_000 })
  })

  test('switching to cross-cycle mode swaps the period picker for a cycle picker (DASH-6)', async ({
    page,
    request,
  }) => {
    const fixture = await seedDashboardBudget(request, 'dash-mode', 2)
    await upsertCutRecord(request, fixture, '2026-02-01', 1000)

    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToDashboard(page, fixture.budgetId)

    const lineSection = page.locator('section', { hasText: 'Budget Line Behavior' })
    await expect(lineSection).toBeVisible({ timeout: 10_000 })

    // Within-cycle mode (default) shows a Cycle select + Periods checkboxes.
    await expect(lineSection.getByText('Cycle', { exact: true })).toBeVisible()

    await lineSection.getByRole('button', { name: 'Cross-cycle' }).click()

    // Cross-cycle mode shows a Cycles checklist instead.
    await expect(lineSection.getByText('Cycles', { exact: true })).toBeVisible()
    await expect(lineSection.getByText('Cycle', { exact: true })).toHaveCount(0)
  })

  test('insufficient history (0-1 cuts) shows the empty state instead of a computed band (DASH-3)', async ({
    page,
    request,
  }) => {
    const fixture = await seedDashboardBudget(request, 'dash-insufficient', 2)
    await upsertCutRecord(request, fixture, '2026-02-01', 1000) // 1 cut total — periodCount < 2

    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToDashboard(page, fixture.budgetId)

    const bandSection = page.locator('section', { hasText: 'Average Behavior' })
    await expect(bandSection).toBeVisible({ timeout: 10_000 })
    await expect(bandSection.getByText('Not enough history yet')).toBeVisible({ timeout: 10_000 })
    await expect(bandSection.locator('canvas')).toHaveCount(0)
  })

  test('renders without horizontal overflow at a mobile viewport (DASH-7 "usable at mobile viewport widths")', async ({
    page,
    request,
  }) => {
    await page.setViewportSize({ width: 375, height: 812 })

    const fixture = await seedDashboardBudget(request, 'dash-mobile', 2)
    await upsertCutRecord(request, fixture, '2026-02-01', 1000)
    await upsertCutRecord(request, fixture, '2026-08-01', 2000)

    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToDashboard(page, fixture.budgetId)

    await expect(page.getByText('Lifetime Trend')).toBeVisible({ timeout: 10_000 })

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)
    expect(overflow).toBe(false)
  })
})

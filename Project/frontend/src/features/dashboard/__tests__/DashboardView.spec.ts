import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/vue'

// DashboardView (DASH-7/DASH-8): page assembly. Lifetime widgets (DASH-1/2)
// must render by default — not last-cut KPI tiles — with the BudgetLine
// comparison widget (DASH-4/5/6) available on the same page. Role gating
// (DASH-8) is enforced server-side per endpoint + the existing router
// `requiresAuth` guard; this view has no extra role logic to test.

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        'dashboard.title': 'Dashboard',
        'dashboard.tabTitle': 'Dashboard',
        'dashboard.lifetime.title': 'Lifetime Trend',
        'dashboard.band.title': 'Average Behavior',
        'dashboard.lineSeries.title': 'Budget Line Behavior',
      }
      return map[key] ?? key
    },
  }),
}))

vi.mock('vue-router', () => ({
  useRoute: () => ({ params: { budgetId: 'budget-42' } }),
}))

vi.mock('@/features/budget-structure/components/BudgetTabs.vue', () => ({
  default: { template: '<div data-testid="stub-budget-tabs" />', props: ['budgetId'] },
}))
vi.mock('../components/LifetimeTotalsChart.vue', () => ({
  default: {
    template: '<div data-testid="stub-lifetime-chart" :data-budget-id="budgetId" />',
    props: ['budgetId'],
  },
}))
vi.mock('../components/TotalsBandChart.vue', () => ({
  default: {
    template: '<div data-testid="stub-band-chart" :data-budget-id="budgetId" />',
    props: ['budgetId'],
  },
}))
vi.mock('../components/BudgetLineSeriesChart.vue', () => ({
  default: {
    template: '<div data-testid="stub-line-series-chart" :data-budget-id="budgetId" />',
    props: ['budgetId'],
  },
}))

import DashboardView from '../views/DashboardView.vue'

describe('DashboardView (DASH-7/DASH-8)', () => {
  it('renders BudgetTabs scoped to the current budgetId', () => {
    render(DashboardView)

    expect(screen.getByTestId('stub-budget-tabs')).not.toBeNull()
  })

  it('renders the lifetime and average-band widgets, passing the route budgetId to each', () => {
    render(DashboardView)

    const lifetime = screen.getByTestId('stub-lifetime-chart')
    const band = screen.getByTestId('stub-band-chart')
    expect(lifetime.getAttribute('data-budget-id')).toBe('budget-42')
    expect(band.getAttribute('data-budget-id')).toBe('budget-42')
  })

  it('renders the BudgetLine comparison widget on the same page, scoped to the current budgetId (DASH-5/6)', () => {
    render(DashboardView)

    const lineSeries = screen.getByTestId('stub-line-series-chart')
    expect(lineSeries.getAttribute('data-budget-id')).toBe('budget-42')
  })

  it('DASH-7: the lifetime trend widget is the default landing content — it renders before the BudgetLine comparison widget in document order', () => {
    const { container } = render(DashboardView)

    const html = container.innerHTML
    const lifetimeIndex = html.indexOf('stub-lifetime-chart')
    const lineSeriesIndex = html.indexOf('stub-line-series-chart')
    expect(lifetimeIndex).toBeGreaterThan(-1)
    expect(lineSeriesIndex).toBeGreaterThan(-1)
    expect(lifetimeIndex).toBeLessThan(lineSeriesIndex)
  })

  it('renders the page title', () => {
    render(DashboardView)

    expect(screen.getByText('Dashboard')).not.toBeNull()
  })
})

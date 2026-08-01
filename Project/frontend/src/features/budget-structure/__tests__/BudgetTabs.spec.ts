import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/vue'

// BudgetTabs (DASH-7 nav integration, PR7 task 7.3): the new Dashboard tab
// must render, link to the Dashboard route scoped to the current budgetId,
// and reflect active/inactive state exactly like the 5 existing tabs.
// No prior test file existed for this component — added test-first for the
// new Dashboard tab addition only.

const routeState: { name: string } = { name: 'CycleList' }

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        'currentSituation.tabTitle': 'Current Situation',
        'dashboard.tabTitle': 'Dashboard',
        'bankAccount.title': 'Bank Accounts',
        'budgetStructure.budgetLines.title': 'Budget Lines',
        'budgetMatrix.title': 'Matrix',
        'nav.budgets': 'Budgets',
      }
      return map[key] ?? key
    },
  }),
}))

vi.mock('vue-router', () => ({
  useRoute: () => routeState,
}))

// vue-router's RouterLink is normally globally registered by `app.use(router)`
// in main.ts. Under @testing-library/vue with no router plugin installed, it
// must be provided explicitly per-render — this stub forwards `to` + $attrs
// (role, aria-selected) exactly like the previous approach, just registered
// via `global.components` instead of a module mock (module mocks can't
// satisfy an unimported, globally-registered template tag).
const RouterLinkStub = {
  props: ['to'],
  inheritAttrs: false,
  template: '<a :data-to-name="to.name" v-bind="$attrs"><slot /></a>',
}

import BudgetTabs from '../components/BudgetTabs.vue'

function renderTabs(): ReturnType<typeof render> {
  return render(BudgetTabs, {
    props: { budgetId: 'budget-1' },
    global: { components: { RouterLink: RouterLinkStub } },
  })
}

describe('BudgetTabs — Dashboard tab (DASH-7)', () => {
  it('renders a Dashboard tab linking to the Dashboard route for the current budget', () => {
    renderTabs()

    const dashboardTab = screen.getByText('Dashboard')
    expect(dashboardTab).not.toBeNull()
    expect(dashboardTab.getAttribute('data-to-name')).toBe('Dashboard')
  })

  it('marks the Dashboard tab selected when the current route is Dashboard', () => {
    routeState.name = 'Dashboard'
    renderTabs()

    const dashboardTab = screen.getByRole('tab', { name: 'Dashboard' })
    expect(dashboardTab.getAttribute('aria-selected')).toBe('true')

    routeState.name = 'CycleList'
  })

  it('does not mark the Dashboard tab selected on an unrelated route', () => {
    routeState.name = 'CurrentSituation'
    renderTabs()

    const dashboardTab = screen.getByRole('tab', { name: 'Dashboard' })
    expect(dashboardTab.getAttribute('aria-selected')).toBe('false')

    routeState.name = 'CycleList'
  })
})

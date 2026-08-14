import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/vue'
import { computed } from 'vue'

// BudgetTabs (DASH-7 nav integration, PR7 task 7.3): the new Dashboard tab
// must render, link to the Dashboard route scoped to the current budgetId,
// and reflect active/inactive state exactly like the 5 existing tabs.
// No prior test file existed for this component — added test-first for the
// new Dashboard tab addition only.
//
// budget-member-administration (REQ-NAV-1, WU1): the Members tab is gated by
// useRoleGate(budgetId).isAdmin — visible to Owner/Admin, entirely absent
// (not disabled) for Operator/ReadOnly, appended as the last tab after
// Dashboard. Dashboard's own position is unchanged — non-admins see zero
// diff in their tab bar.

const routeState: { name: string } = { name: 'CycleList' }

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        'currentSituation.tabTitle': 'Current Situation',
        'dashboard.tabTitle': 'Dashboard',
        'bankAccount.title': 'Bank Accounts',
        'budgetStructure.budgetLines.title': 'Budget Lines',
        'budgetStructure.members.tabTitle': 'Members',
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

const { mockUseRoleGate } = vi.hoisted(() => ({ mockUseRoleGate: vi.fn() }))

vi.mock('../composables/useRoleGate', () => ({
  useRoleGate: mockUseRoleGate,
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

function setIsAdmin(isAdmin: boolean): void {
  mockUseRoleGate.mockReturnValue({
    isAdmin: computed(() => isAdmin),
    isOperator: computed(() => isAdmin),
    canWriteStructure: computed(() => isAdmin),
    canWriteLines: computed(() => isAdmin),
    isOwner: computed(() => isAdmin),
  })
}

function renderTabs(): ReturnType<typeof render> {
  return render(BudgetTabs, {
    props: { budgetId: 'budget-1' },
    global: { components: { RouterLink: RouterLinkStub } },
  })
}

describe('BudgetTabs — Dashboard tab (DASH-7)', () => {
  it('renders a Dashboard tab linking to the Dashboard route for the current budget', () => {
    setIsAdmin(false)
    renderTabs()

    const dashboardTab = screen.getByText('Dashboard')
    expect(dashboardTab).not.toBeNull()
    expect(dashboardTab.getAttribute('data-to-name')).toBe('Dashboard')
  })

  it('marks the Dashboard tab selected when the current route is Dashboard', () => {
    setIsAdmin(false)
    routeState.name = 'Dashboard'
    renderTabs()

    const dashboardTab = screen.getByRole('tab', { name: 'Dashboard' })
    expect(dashboardTab.getAttribute('aria-selected')).toBe('true')

    routeState.name = 'CycleList'
  })

  it('does not mark the Dashboard tab selected on an unrelated route', () => {
    setIsAdmin(false)
    routeState.name = 'CurrentSituation'
    renderTabs()

    const dashboardTab = screen.getByRole('tab', { name: 'Dashboard' })
    expect(dashboardTab.getAttribute('aria-selected')).toBe('false')

    routeState.name = 'CycleList'
  })
})

describe('BudgetTabs — Members tab (REQ-NAV-1, WU1)', () => {
  it('is visible to an Owner, positioned as the last tab, after Dashboard', () => {
    setIsAdmin(true)
    renderTabs()

    const tabs = screen.getAllByRole('tab')
    const labels = tabs.map((tab) => tab.textContent?.trim())
    const dashboardIndex = labels.indexOf('Dashboard')
    const membersIndex = labels.indexOf('Members')

    expect(dashboardIndex).toBeGreaterThanOrEqual(0)
    expect(membersIndex).toBe(labels.length - 1)
    expect(membersIndex).toBe(dashboardIndex + 1)
  })

  it("does not move Dashboard's position for a non-admin (zero visible change)", () => {
    setIsAdmin(false)
    renderTabs()

    const tabs = screen.getAllByRole('tab')
    const labels = tabs.map((tab) => tab.textContent?.trim())

    expect(labels[labels.length - 1]).toBe('Dashboard')
  })

  it('is visible to an Admin', () => {
    setIsAdmin(true)
    renderTabs()

    expect(screen.getByText('Members')).not.toBeNull()
  })

  it('is hidden entirely from the DOM for Operator/ReadOnly (not merely disabled)', () => {
    setIsAdmin(false)
    renderTabs()

    expect(screen.queryByText('Members')).toBeNull()
  })

  it('has the active CSS class when the current route is BudgetMembers', () => {
    setIsAdmin(true)
    routeState.name = 'BudgetMembers'
    renderTabs()

    const membersTab = screen.getByRole('tab', { name: 'Members' })
    expect(membersTab.className).toContain('tab-active')

    routeState.name = 'CycleList'
  })
})

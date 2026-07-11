import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import { computed } from 'vue'

import CycleListView from '../CycleListView.vue'
import type { CycleListItem } from '../../types'

// --- Mocks ---

vi.mock('../../store', () => ({
  useBudgetStructureStore: vi.fn(),
}))

vi.mock('../../composables/useRoleGate', () => ({
  useRoleGate: vi.fn(),
}))

vi.mock('@/stores/layout.store', () => ({
  useLayoutStore: vi.fn(),
}))

// Stub child components that make network calls or have heavy deps
vi.mock('../../components/BudgetTabs.vue', () => ({
  default: { template: '<div data-testid="budget-tabs" />' },
}))

vi.mock('../../components/CycleForm.vue', () => ({
  default: { template: '<div data-testid="cycle-form" />' },
}))

vi.mock('../../components/EmptyState.vue', () => ({
  default: {
    props: ['title', 'description', 'actionLabel', 'action'],
    template: '<div data-testid="empty-state">{{ title }}</div>',
  },
}))

import { useBudgetStructureStore } from '../../store'
import { useRoleGate } from '../../composables/useRoleGate'
import { useLayoutStore } from '@/stores/layout.store'

const BUDGET_ID = 'budget-1'

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/budgets/:budgetId/cycles', name: 'CycleList', component: CycleListView },
      { path: '/budgets/:budgetId/cycles/:cycleId', name: 'CycleDetail', component: { template: '<div/>' } },
    ],
  })
}

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        budgetStructure: {
          cycles: {
            title: 'Cycles',
            create: 'New Cycle',
            edit: 'Edit Cycle',
            delete: 'Delete Cycle',
            setActive: 'Set as Active',
            name: 'Name',
            startDate: 'Start Date',
            endDate: 'End Date',
            active: 'Active',
            periodCount: 'Periods',
            confirmDelete: 'Are you sure?',
            empty: {
              title: 'No cycles yet',
              description: 'Create your first cycle.',
              action: 'New Cycle',
            },
          },
          common: { save: 'Save', cancel: 'Cancel', confirm: 'Confirm', actions: 'Actions', noPermission: 'No permission' },
        },
      },
    },
  })
}

function setupStoreMocks({
  cycles = [] as CycleListItem[],
  loading = false,
  isAdmin = false,
  canWriteStructure = false,
} = {}) {
  const layoutStoreMock = {
    setPageActions: vi.fn(),
    clearPageActions: vi.fn(),
    pageActions: [],
    activeBudgetId: null,
    activeBudgetName: null,
  }

  vi.mocked(useBudgetStructureStore).mockReturnValue({
    cycles,
    loading,
    loadCycles: vi.fn().mockResolvedValue(undefined),
    deleteCycle: vi.fn().mockResolvedValue(undefined),
    setActiveCycle: vi.fn().mockResolvedValue(undefined),
    createCycle: vi.fn().mockResolvedValue(undefined),
    updateCycle: vi.fn().mockResolvedValue(undefined),
  } as unknown as ReturnType<typeof useBudgetStructureStore>)

  vi.mocked(useRoleGate).mockReturnValue({
    isAdmin: computed(() => isAdmin),
    isOperator: computed(() => isAdmin),
    canWriteStructure: computed(() => canWriteStructure),
    canWriteLines: computed(() => isAdmin),
  })

  vi.mocked(useLayoutStore).mockReturnValue(layoutStoreMock as unknown as ReturnType<typeof useLayoutStore>)

  return { layoutStoreMock }
}

describe('CycleListView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  async function renderView(options = {}) {
    const router = makeRouter()
    await router.push(`/budgets/${BUDGET_ID}/cycles`)
    await router.isReady()

    return render(CycleListView, {
      global: {
        plugins: [router, makeI18n()],
      },
      ...options,
    })
  }

  describe('when cycles list is empty', () => {
    it('shows the EmptyState component', async () => {
      setupStoreMocks({ cycles: [], loading: false })
      await renderView()
      expect(screen.getByTestId('empty-state')).toBeTruthy()
    })

    it('does not show the table', async () => {
      setupStoreMocks({ cycles: [], loading: false })
      await renderView()
      expect(screen.queryByRole('table')).toBeNull()
    })
  })

  describe('when cycles are present', () => {
    const cycles: CycleListItem[] = [
      { id: 'c1', name: 'Cycle One', startDate: '2024-01-01' as any, endDate: '2024-12-31' as any, isActive: true, periodCount: 3 },
      { id: 'c2', name: 'Cycle Two', startDate: '2025-01-01' as any, endDate: '2025-12-31' as any, isActive: false, periodCount: 0 },
    ]

    it('renders 2 rows in the table', async () => {
      setupStoreMocks({ cycles, loading: false })
      await renderView()
      expect(screen.getByText('Cycle One')).toBeTruthy()
      expect(screen.getByText('Cycle Two')).toBeTruthy()
    })

    it('does not show EmptyState', async () => {
      setupStoreMocks({ cycles, loading: false })
      await renderView()
      expect(screen.queryByTestId('empty-state')).toBeNull()
    })
  })

  describe('role gating — page actions', () => {
    it('registers "New Cycle" page action when user is admin', async () => {
      const { layoutStoreMock } = setupStoreMocks({
        cycles: [],
        isAdmin: true,
        canWriteStructure: true,
      })
      await renderView()
      expect(layoutStoreMock.setPageActions).toHaveBeenCalled()
      const actions = layoutStoreMock.setPageActions.mock.calls[0]![0] as Array<{ key: string }>
      expect(actions.some((a) => a.key === 'new-cycle')).toBe(true)
    })

    it('does not register page action when user is not admin', async () => {
      const { layoutStoreMock } = setupStoreMocks({
        cycles: [],
        isAdmin: false,
        canWriteStructure: false,
      })
      await renderView()
      expect(layoutStoreMock.setPageActions).not.toHaveBeenCalled()
    })
  })
})

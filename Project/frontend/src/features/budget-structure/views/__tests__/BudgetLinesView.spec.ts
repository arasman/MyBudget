// REQ-BL-1, REQ-BL-STORE-1
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import { computed, nextTick } from 'vue'
import BudgetLinesView from '../BudgetLinesView.vue'
import type { BudgetLineResponse } from '../../types'

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

vi.mock('../../components/BudgetTabs.vue', () => ({
  default: { template: '<div data-testid="budget-tabs" />' },
}))

vi.mock('../../components/BudgetLineRow.vue', () => ({
  default: {
    props: ['line', 'readonly'],
    emits: ['edit', 'delete'],
    template: `
      <tr data-testid="budget-line-row" @dblclick="$emit('edit', line)">
        <td>{{ line.name }}</td>
      </tr>
    `,
  },
}))

vi.mock('../../components/BudgetLineModal.vue', () => ({
  default: {
    props: ['modelValue', 'categoryGroups'],
    emits: ['submit', 'cancel'],
    template: '<div data-testid="budget-line-modal" />',
  },
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

const mockLines: BudgetLineResponse[] = [
  { id: 'l1', name: 'Salary', lineType: 'Expense', startDate: '2025-01-01' as any, endDate: null, budgetedAmount: 1000, currencyId: 'gtq', categoryGroupId: 'g1' },
  { id: 'l2', name: 'Rent', lineType: 'Expense', startDate: '2025-01-01' as any, endDate: null, budgetedAmount: 500, currencyId: 'gtq', categoryGroupId: 'g1' },
  { id: 'l3', name: 'Groceries', lineType: 'Expense', startDate: '2025-01-01' as any, endDate: null, budgetedAmount: 300, currencyId: 'gtq', categoryGroupId: 'g1' },
]

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/budgets/:budgetId/lines',
        name: 'BudgetLines',
        component: BudgetLinesView,
      },
      {
        path: '/budgets/:budgetId/cycles',
        name: 'CycleList',
        component: { template: '<div/>' },
      },
      {
        path: '/budgets/:budgetId/cycles/:cycleId',
        name: 'CycleDetail',
        component: { template: '<div/>' },
      },
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
          cycles: { title: 'Cycles' },
          budgetLines: {
            title: 'Budget Lines',
            create: 'New Line',
            edit: 'Edit Line',
            delete: 'Delete Line',
            confirmDelete: 'Are you sure?',
            name: 'Name',
            lineType: 'Type',
            startDate: 'Start Date',
            endDate: 'End Date',
            budgetedAmount: 'Budgeted Amount',
            currency: 'Currency',
            note: 'Note',
            types: { income: 'Income', expense: 'Expense' },
            empty: { title: 'No budget lines yet', description: 'Add lines.', action: 'New Line' },
          },
          common: { save: 'Save', cancel: 'Cancel', confirm: 'Confirm', actions: 'Actions', noPermission: 'No permission' },
        },
      },
    },
  })
}

function setupMocks({
  lines = [] as BudgetLineResponse[],
  loading = false,
  canWriteLines = false,
  currentCycle = null as { name: string } | null,
} = {}) {
  const layoutStoreMock = {
    setPageActions: vi.fn(),
    clearPageActions: vi.fn(),
    pageActions: [],
    activeBudgetId: null,
    activeBudgetName: null,
  }

  vi.mocked(useBudgetStructureStore).mockReturnValue({
    budgetLines: lines,
    categoryGroups: [],
    loading,
    currentCycle,
    loadLines: vi.fn().mockResolvedValue(undefined),
    loadGroups: vi.fn().mockResolvedValue(undefined),
    createLine: vi.fn().mockResolvedValue(undefined),
    updateLine: vi.fn().mockResolvedValue(undefined),
    deleteLine: vi.fn().mockResolvedValue(undefined),
  } as unknown as ReturnType<typeof useBudgetStructureStore>)

  vi.mocked(useRoleGate).mockReturnValue({
    isAdmin: computed(() => canWriteLines),
    isOperator: computed(() => canWriteLines),
    canWriteStructure: computed(() => canWriteLines),
    canWriteLines: computed(() => canWriteLines),
  })

  vi.mocked(useLayoutStore).mockReturnValue(layoutStoreMock as unknown as ReturnType<typeof useLayoutStore>)

  return { layoutStoreMock }
}

describe('BudgetLinesView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  async function renderView() {
    const router = makeRouter()
    await router.push(`/budgets/${BUDGET_ID}/lines`)
    await router.isReady()

    const result = render(BudgetLinesView, {
      global: {
        plugins: [router, makeI18n()],
      },
    })
    // Flush promises so onMounted async work completes
    await nextTick()
    await nextTick()
    return result
  }

  describe('when lines are present', () => {
    it('renders 3 BudgetLineRow components', async () => {
      setupMocks({ lines: mockLines })
      await renderView()
      const rows = screen.getAllByTestId('budget-line-row')
      expect(rows).toHaveLength(3)
    })

    it('shows line names', async () => {
      setupMocks({ lines: mockLines })
      await renderView()
      expect(screen.getByText('Salary')).toBeTruthy()
      expect(screen.getByText('Rent')).toBeTruthy()
    })
  })

  describe('when lines are empty', () => {
    it('shows EmptyState', async () => {
      setupMocks({ lines: [], loading: false })
      await renderView()
      expect(screen.getByTestId('empty-state')).toBeTruthy()
    })
  })

  describe('role gating — page actions', () => {
    it('registers "New Line" page action when canWriteLines=true', async () => {
      const { layoutStoreMock } = setupMocks({ lines: [], canWriteLines: true })
      await renderView()
      expect(layoutStoreMock.setPageActions).toHaveBeenCalled()
      const actions = layoutStoreMock.setPageActions.mock.calls[0]![0] as Array<{ key: string }>
      expect(actions.some((a) => a.key === 'new-line')).toBe(true)
    })

    it('does not register page action when canWriteLines=false', async () => {
      const { layoutStoreMock } = setupMocks({ lines: [], canWriteLines: false })
      await renderView()
      expect(layoutStoreMock.setPageActions).not.toHaveBeenCalled()
    })
  })

  describe('dblclick on row opens modal', () => {
    it('shows BudgetLineModal after dblclick on a row', async () => {
      setupMocks({ lines: mockLines, canWriteLines: true })
      await renderView()

      // Modal should not be visible initially
      expect(screen.queryByTestId('budget-line-modal')).toBeNull()

      // Dblclick on first row triggers edit
      const firstRow = screen.getAllByTestId('budget-line-row')[0]!
      await fireEvent.dblClick(firstRow)

      expect(screen.getByTestId('budget-line-modal')).toBeTruthy()
    })
  })

  describe('store called without periodId', () => {
    it('calls loadLines with budgetId only (no periodId argument)', async () => {
      const { layoutStoreMock: _ } = setupMocks({ lines: [] })
      await renderView()
      const store = vi.mocked(useBudgetStructureStore)()
      expect(store.loadLines).toHaveBeenCalledWith(BUDGET_ID)
    })
  })
})

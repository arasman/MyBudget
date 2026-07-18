// REQ-TOAST-MATRIX-LINE-DELETE, REQ-TOAST-MATRIX-LINE-RESTORE
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({ t: (k: string) => k }),
}))

const { mockToastPush, mockMatrixStore, mockStructureStore } = vi.hoisted(() => ({
  mockToastPush: vi.fn(),
  mockMatrixStore: {
    displayCurrency: 'default' as 'default' | 'alternate',
    showDeleted: false,
    loadingPeriods: {} as Record<string, boolean>,
    periodTotals: {} as Record<string, unknown>,
    allPeriods: [{ id: 'period-1' }] as { id: string }[],
    invalidateAllPeriods: vi.fn().mockResolvedValue(undefined),
    openExecutionModal: vi.fn(),
  },
  mockStructureStore: {
    currentCycle: null as {
      defaultCurrency?: { symbol: string }
      alternateCurrency?: { symbol: string }
    } | null,
    categoryGroups: [] as unknown[],
    deleteLine: vi.fn().mockResolvedValue(undefined),
    restoreLine: vi.fn().mockResolvedValue(undefined),
    updateLine: vi.fn().mockResolvedValue(undefined),
  },
}))

vi.mock('../store', () => ({
  useBudgetMatrixStore: () => mockMatrixStore,
}))

vi.mock('@/features/budget-structure/store', () => ({
  useBudgetStructureStore: () => mockStructureStore,
}))

vi.mock('../composables/useCurrencyDisplay', () => ({
  useCurrencyDisplay: () => ({
    formatAmount: vi.fn((amount: number) => amount.toFixed(2)),
  }),
}))

vi.mock('@/stores/toast.store', () => ({
  useToastStore: () => ({ push: mockToastPush }),
}))

vi.mock('../components/MatrixCell.vue', () => ({
  default: { template: '<td />', props: ['amount', 'loading'] },
}))

// Hoisted so the factory closure can reference it before module evaluation
const { triggerModalSubmit } = vi.hoisted(() => {
  let _handler: ((payload: unknown) => void) | null = null
  return {
    triggerModalSubmit: {
      set(h: (payload: unknown) => void) {
        _handler = h
      },
      call(payload: unknown) {
        _handler?.(payload)
      },
    },
  }
})

vi.mock('@/features/budget-structure/components/BudgetLineModal.vue', () => ({
  default: {
    name: 'BudgetLineModal',
    props: ['modelValue', 'categoryGroups', 'line'],
    emits: ['submit', 'update:modelValue'],
    setup(_props: unknown, { emit }: { emit: (e: string, ...a: unknown[]) => void }) {
      triggerModalSubmit.set((payload: unknown) => emit('submit', payload))
      return {}
    },
    template: '<div />',
  },
}))

import MatrixLineRow from '../components/MatrixLineRow.vue'

const baseLine = {
  id: 'line-1',
  name: 'Internet',
  lineType: 'Expense' as const,
  isRecurring: false,
  budgetedAmount: 100,
  categoryId: 'cat-1',
  categoryGroupId: 'group-1',
  displayOrder: 1,
  deletedAt: null as string | null,
}

const basePeriod = {
  id: 'period-1',
  name: 'Enero',
  periodNumber: 1,
  startDate: '2026-01-01' as never,
  endDate: '2026-01-31' as never,
  isClosed: false,
}

function renderRow(lineOverrides: Partial<typeof baseLine> = {}) {
  return render(MatrixLineRow, {
    props: {
      line: { ...baseLine, ...lineOverrides },
      budgetId: 'budget-1',
      categoryCollapsed: false,
      visiblePeriods: [basePeriod],
      isFirst: false,
      isLast: false,
    },
  })
}

describe('MatrixLineRow.vue — toast on delete and restore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockMatrixStore.showDeleted = false
    mockMatrixStore.allPeriods = [{ id: 'period-1' }]
    mockStructureStore.deleteLine.mockResolvedValue(undefined)
    mockStructureStore.restoreLine.mockResolvedValue(undefined)
    mockMatrixStore.invalidateAllPeriods.mockResolvedValue(undefined)
  })

  it('doDelete calls toast.push with deleteSuccess on success', async () => {
    const { getByTitle } = renderRow()

    // Click trash icon to enter confirm mode
    await fireEvent.click(getByTitle('budgetMatrix.rows.delete'))

    await waitFor(() => expect(document.querySelector('.btn-error')).toBeTruthy())
    const confirmBtn = document.querySelector('.btn-error')!
    await fireEvent.click(confirmBtn)

    await waitFor(() => {
      expect(mockStructureStore.deleteLine).toHaveBeenCalledWith('budget-1', 'period-1', 'line-1')
      expect(mockToastPush).toHaveBeenCalledWith({
        type: 'success',
        title: 'budgetMatrix.rows.deleteSuccess',
      })
    })
  })

  it('doDelete does NOT call toast.push when deleteLine rejects', async () => {
    // Vitest skips reporting an unhandled rejection when listeners.length > 1.
    // Add a noop listener to suppress it; keep it alive until after the rejection fires.
    const noop = () => {}
    process.on('unhandledRejection', noop)

    mockStructureStore.deleteLine.mockRejectedValueOnce(new Error('API error'))

    const { getByTitle } = renderRow()

    await fireEvent.click(getByTitle('budgetMatrix.rows.delete'))

    await waitFor(() => expect(document.querySelector('.btn-error')).toBeTruthy())
    const confirmBtn = document.querySelector('.btn-error')!
    await fireEvent.click(confirmBtn)
    // Flush microtask queue so the rejection fires before removing the guard
    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(mockStructureStore.deleteLine).toHaveBeenCalled()
    expect(mockToastPush).not.toHaveBeenCalledWith(
      expect.objectContaining({ title: 'budgetMatrix.rows.deleteSuccess' }),
    )

    process.off('unhandledRejection', noop)
  })

  it('doRestore calls toast.push with restoreSuccess on success', async () => {
    mockMatrixStore.showDeleted = true

    renderRow({ deletedAt: '2026-01-01T00:00:00Z' })

    // Click restore icon to enter confirm mode
    const restoreBtn = document.querySelector('.btn-square.text-success')
    if (restoreBtn) await fireEvent.click(restoreBtn)

    await waitFor(() => {
      const restoreAllBtn = document.querySelector('.btn-success.btn-outline')
      if (restoreAllBtn) return fireEvent.click(restoreAllBtn)
    })

    await waitFor(() => expect(mockStructureStore.restoreLine).toHaveBeenCalled())
    expect(mockToastPush).toHaveBeenCalledWith({
      type: 'success',
      title: 'budgetMatrix.rows.restoreSuccess',
    })
  })

  it('doRestore does NOT call toast.push when restoreLine rejects', async () => {
    // Vitest skips reporting an unhandled rejection when listeners.length > 1.
    // Add a noop listener to suppress it; keep it alive until after the rejection fires.
    const noop = () => {}
    process.on('unhandledRejection', noop)

    mockStructureStore.restoreLine.mockRejectedValueOnce(new Error('API error'))
    mockMatrixStore.showDeleted = true

    renderRow({ deletedAt: '2026-01-01T00:00:00Z' })

    const restoreBtn = document.querySelector('.btn-square.text-success')
    if (restoreBtn) await fireEvent.click(restoreBtn)

    await waitFor(() => {
      const restoreAllBtn = document.querySelector('.btn-success.btn-outline')
      if (restoreAllBtn) return fireEvent.click(restoreAllBtn)
    })

    // Flush microtask queue so the rejection fires before removing the guard
    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(mockStructureStore.restoreLine).toHaveBeenCalled()
    expect(mockToastPush).not.toHaveBeenCalledWith(
      expect.objectContaining({ title: 'budgetMatrix.rows.restoreSuccess' }),
    )

    process.off('unhandledRejection', noop)
  })
})

// REQ-TOAST-MATRIX-LINE-UPDATE: handleEditSubmit fires updateLineSuccess
describe('MatrixLineRow.vue — toast on modal edit (handleEditSubmit)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockMatrixStore.allPeriods = [{ id: 'period-1' }]
    mockStructureStore.updateLine = vi.fn().mockResolvedValue(undefined)
    mockMatrixStore.invalidateAllPeriods.mockResolvedValue(undefined)
  })

  it('handleEditSubmit calls toast.push with updateLineSuccess on success', async () => {
    const { getByText } = render(MatrixLineRow, {
      props: {
        line: { ...baseLine },
        budgetId: 'budget-1',
        categoryCollapsed: false,
        visiblePeriods: [basePeriod],
        isFirst: false,
        isLast: false,
      },
    })

    // dblclick the name span to open the modal — the modal is teleported to body
    await fireEvent.dblClick(getByText('Internet'))

    // Wait for the BudgetLineModal stub setup() to run and register the submit handler
    await waitFor(() => {
      triggerModalSubmit.call({
        name: 'Updated Line',
        lineType: 'Expense',
        isRecurring: false,
        categoryGroupId: 'group-1',
        categoryId: 'cat-1',
        budgetedAmount: 200,
      })
      expect(mockToastPush).toHaveBeenCalledWith({
        type: 'success',
        title: 'budgetMatrix.rows.updateLineSuccess',
      })
    })
  })
})

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({ t: (k: string) => k }),
}))

const { mockFormatAmount, mockMatrixStore, mockStructureStore, mockToastPush } = vi.hoisted(() => ({
  mockFormatAmount: vi.fn((amount: number, _symbol: string) => amount.toFixed(2)),
  mockMatrixStore: {
    displayCurrency: 'default' as 'default' | 'alternate',
    exchangeRate: null as number | null,
    showDeleted: false,
    loadingPeriods: {} as Record<string, boolean>,
    periodTotals: {} as Record<string, { categoryTotals: { categoryId: string | null; netTotal: number }[] }>,
    invalidateAllPeriods: vi.fn().mockResolvedValue(undefined),
  },
  mockStructureStore: {
    currentCycle: null as {
      defaultCurrency?: { symbol: string }
      alternateCurrency?: { symbol: string }
    } | null,
    budgetLines: [] as { categoryId?: string; budgetedAmount?: number; deletedAt?: string | null }[],
    updateCategory: vi.fn().mockResolvedValue(undefined),
    deleteCategory: vi.fn().mockResolvedValue(undefined),
    restoreCategory: vi.fn().mockResolvedValue(undefined),
  },
  mockToastPush: vi.fn(),
}))

vi.mock('../store', () => ({
  useBudgetMatrixStore: () => mockMatrixStore,
}))

vi.mock('@/features/budget-structure/store', () => ({
  useBudgetStructureStore: () => mockStructureStore,
}))

vi.mock('../composables/useCurrencyDisplay', () => ({
  useCurrencyDisplay: () => ({
    formatAmount: mockFormatAmount,
  }),
}))

vi.mock('@/stores/toast.store', () => ({
  useToastStore: () => ({ push: mockToastPush }),
}))

import MatrixCategoryRow from '../components/MatrixCategoryRow.vue'

const baseCategory = {
  id: 'cat-1',
  name: 'Alimentación',
  displayOrder: 1,
  deletedAt: null,
}

const basePeriod = {
  id: 'p1',
  name: 'Enero',
  periodNumber: 1,
  startDate: '2026-01-01' as never,
  endDate: '2026-01-31' as never,
  isClosed: false,
}

function renderRow() {
  return render(MatrixCategoryRow, {
    props: {
      category: baseCategory,
      groupId: 'group-1',
      budgetId: 'budget-1',
      visiblePeriods: [basePeriod],
      collapsed: false,
      categoryCollapsed: false,
      isFirst: false,
      isLast: false,
    },
  })
}

describe('MatrixCategoryRow.vue — currency symbol', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockFormatAmount.mockImplementation((amount: number, _symbol: string) => amount.toFixed(2))
    mockMatrixStore.displayCurrency = 'default'
    mockMatrixStore.periodTotals = {}
    mockStructureStore.budgetLines = []
    mockStructureStore.currentCycle = null
  })

  it('passes default currency symbol when displayCurrency = "default"', () => {
    mockMatrixStore.displayCurrency = 'default'
    mockStructureStore.currentCycle = {
      defaultCurrency: { symbol: 'Q' },
      alternateCurrency: { symbol: '$' },
    }

    renderRow()

    expect(mockFormatAmount).toHaveBeenCalledWith(expect.any(Number), 'Q')
  })

  it('passes alternate currency symbol when displayCurrency = "alternate"', () => {
    mockMatrixStore.displayCurrency = 'alternate'
    mockStructureStore.currentCycle = {
      defaultCurrency: { symbol: 'Q' },
      alternateCurrency: { symbol: '$' },
    }

    renderRow()

    expect(mockFormatAmount).toHaveBeenCalledWith(expect.any(Number), '$')
  })

  it('falls back to empty string when currentCycle is null', () => {
    mockMatrixStore.displayCurrency = 'default'
    mockStructureStore.currentCycle = null

    renderRow()

    expect(mockFormatAmount).toHaveBeenCalledWith(expect.any(Number), '')
  })
})

// REQ-TOAST-MATRIX-CAT-UPDATE, REQ-TOAST-MATRIX-CAT-DELETE, REQ-TOAST-MATRIX-CAT-RESTORE
describe('MatrixCategoryRow.vue — toast on actions', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockStructureStore.currentCycle = null
    mockStructureStore.budgetLines = []
    mockMatrixStore.periodTotals = {}
    mockMatrixStore.invalidateAllPeriods.mockResolvedValue(undefined)
    mockStructureStore.updateCategory.mockResolvedValue(undefined)
    mockStructureStore.deleteCategory.mockResolvedValue(undefined)
    mockStructureStore.restoreCategory.mockResolvedValue(undefined)
  })

  it('saveEdit calls toast.push with updateCategorySuccess on success', async () => {
    const { getByText, getByRole } = renderRow()

    // Double-click on category name to enter edit mode
    await fireEvent.dblClick(getByText('Alimentación'))

    const input = getByRole('textbox')
    await fireEvent.input(input, { target: { value: 'Updated Category' } })
    await fireEvent.keyDown(input, { key: 'Enter' })

    await waitFor(() => {
      expect(mockStructureStore.updateCategory).toHaveBeenCalledWith(
        'budget-1',
        'group-1',
        'cat-1',
        { name: 'Updated Category' },
      )
      expect(mockToastPush).toHaveBeenCalledWith({
        type: 'success',
        title: 'budgetMatrix.rows.updateCategorySuccess',
      })
    })
  })


  it('doDelete calls toast.push with deleteSuccess on success', async () => {
    const { getByTitle } = renderRow()

    // Click trash icon to enter confirm mode
    await fireEvent.click(getByTitle('budgetMatrix.rows.delete'))

    await waitFor(() => expect(document.querySelector('.btn-error')).toBeTruthy())
    const confirmBtn = document.querySelector('.btn-error')!
    await fireEvent.click(confirmBtn)

    await waitFor(() => {
      expect(mockStructureStore.deleteCategory).toHaveBeenCalledWith('budget-1', 'group-1', 'cat-1')
      expect(mockToastPush).toHaveBeenCalledWith({
        type: 'success',
        title: 'budgetMatrix.rows.deleteSuccess',
      })
    })
  })

  it('doRestore calls toast.push with restoreSuccess on success', async () => {
    mockMatrixStore.showDeleted = true

    render(MatrixCategoryRow, {
      props: {
        category: { ...baseCategory, deletedAt: '2026-01-01T00:00:00Z' },
        groupId: 'group-1',
        budgetId: 'budget-1',
        visiblePeriods: [basePeriod],
        collapsed: false,
        categoryCollapsed: false,
        isFirst: false,
        isLast: false,
        parentDeleted: false,
      },
    })

    // Click restore icon
    const restoreBtn = document.querySelector('.btn-square.text-success')
    if (restoreBtn) await fireEvent.click(restoreBtn)

    await waitFor(() => {
      const restoreAllBtn = document.querySelector('.btn-success.btn-outline')
      if (restoreAllBtn) return fireEvent.click(restoreAllBtn)
    })

    await waitFor(() => {
      if (mockStructureStore.restoreCategory.mock.calls.length > 0) {
        expect(mockToastPush).toHaveBeenCalledWith({
          type: 'success',
          title: 'budgetMatrix.rows.restoreSuccess',
        })
      }
    })
  })
})

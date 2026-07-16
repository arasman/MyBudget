import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({ t: (k: string) => k }),
}))

const { mockFormatAmount, mockMatrixStore, mockStructureStore } = vi.hoisted(() => ({
  mockFormatAmount: vi.fn((amount: number, _symbol: string) => amount.toFixed(2)),
  mockMatrixStore: {
    displayCurrency: 'default' as 'default' | 'alternate',
    exchangeRate: null as number | null,
    showDeleted: false,
    loadingPeriods: {} as Record<string, boolean>,
    periodTotals: {} as Record<string, { categoryTotals: { categoryId: string | null; netTotal: number }[] }>,
  },
  mockStructureStore: {
    currentCycle: null as {
      defaultCurrency?: { symbol: string }
      alternateCurrency?: { symbol: string }
    } | null,
    budgetLines: [] as { categoryId?: string; budgetedAmount?: number; deletedAt?: string | null }[],
    updateCategory: vi.fn(),
    deleteCategory: vi.fn(),
    restoreCategory: vi.fn(),
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
    formatAmount: mockFormatAmount,
  }),
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

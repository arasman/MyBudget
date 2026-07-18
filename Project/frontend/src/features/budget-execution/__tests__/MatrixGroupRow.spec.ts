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
    periodTotals: {} as Record<string, { categoryTotals: { categoryGroupId: string; netTotal: number }[] }>,
    invalidateAllPeriods: vi.fn().mockResolvedValue(undefined),
  },
  mockStructureStore: {
    currentCycle: null as {
      defaultCurrency?: { symbol: string }
      alternateCurrency?: { symbol: string }
    } | null,
    budgetLines: [] as { categoryId?: string; budgetedAmount?: number; deletedAt?: string | null }[],
    updateGroup: vi.fn().mockResolvedValue(undefined),
    deleteGroup: vi.fn().mockResolvedValue(undefined),
    restoreGroup: vi.fn().mockResolvedValue(undefined),
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

import MatrixGroupRow from '../components/MatrixGroupRow.vue'

const baseGroup = {
  id: 'group-1',
  name: 'Mi hogar',
  displayOrder: 1,
  categories: [{ id: 'cat-1', name: 'Alimentación', displayOrder: 1 }],
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
  return render(MatrixGroupRow, {
    props: {
      group: baseGroup,
      visiblePeriods: [basePeriod],
      collapsed: false,
      isFirst: false,
      isLast: false,
      budgetId: 'budget-1',
    },
  })
}

describe('MatrixGroupRow.vue — currency symbol', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockFormatAmount.mockImplementation((amount: number, _symbol: string) => amount.toFixed(2))
    mockMatrixStore.displayCurrency = 'default'
    mockMatrixStore.periodTotals = {}
    mockStructureStore.budgetLines = []
    mockStructureStore.currentCycle = null
  })

  it('passes default currency symbol to formatAmount when displayCurrency = "default"', () => {
    mockMatrixStore.displayCurrency = 'default'
    mockStructureStore.currentCycle = {
      defaultCurrency: { symbol: 'Q' },
      alternateCurrency: { symbol: '$' },
    }

    renderRow()

    expect(mockFormatAmount).toHaveBeenCalledWith(expect.any(Number), 'Q')
  })

  it('passes alternate currency symbol to formatAmount when displayCurrency = "alternate"', () => {
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

// REQ-TOAST-MATRIX-GROUP-UPDATE, REQ-TOAST-MATRIX-GROUP-DELETE, REQ-TOAST-MATRIX-GROUP-RESTORE
describe('MatrixGroupRow.vue — toast on actions', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockStructureStore.currentCycle = null
    mockStructureStore.budgetLines = []
    mockMatrixStore.periodTotals = {}
    mockMatrixStore.invalidateAllPeriods.mockResolvedValue(undefined)
    mockStructureStore.updateGroup.mockResolvedValue(undefined)
    mockStructureStore.deleteGroup.mockResolvedValue(undefined)
    mockStructureStore.restoreGroup.mockResolvedValue(undefined)
  })

  it('saveEdit calls toast.push with updateGroupSuccess on success', async () => {
    const { getByText, getByRole } = renderRow()

    // Double-click on group name to enter edit mode
    await fireEvent.dblClick(getByText('Mi hogar'))

    const input = getByRole('textbox')
    await fireEvent.input(input, { target: { value: 'Updated Name' } })
    await fireEvent.keyDown(input, { key: 'Enter' })

    await waitFor(() => {
      expect(mockStructureStore.updateGroup).toHaveBeenCalledWith('budget-1', 'group-1', { name: 'Updated Name' })
      expect(mockToastPush).toHaveBeenCalledWith({
        type: 'success',
        title: 'budgetMatrix.rows.updateGroupSuccess',
      })
    })
  })


  it('doDelete calls toast.push with deleteSuccess on success', async () => {
    const { getByTitle } = renderRow()

    // Click trash icon to enter confirm mode
    await fireEvent.click(getByTitle('budgetMatrix.rows.delete'))

    // The confirm delete button is btn-error
    await waitFor(() => expect(document.querySelector('.btn-error')).toBeTruthy())
    const confirmBtn = document.querySelector('.btn-error')!
    await fireEvent.click(confirmBtn)

    await waitFor(() => {
      expect(mockStructureStore.deleteGroup).toHaveBeenCalledWith('budget-1', 'group-1')
      expect(mockToastPush).toHaveBeenCalledWith({
        type: 'success',
        title: 'budgetMatrix.rows.deleteSuccess',
      })
    })
  })

  it('doRestore calls toast.push with restoreSuccess on success', async () => {
    // Render a deleted group with showDeleted = true
    mockMatrixStore.showDeleted = true

    render(MatrixGroupRow, {
      props: {
        group: { ...baseGroup, deletedAt: '2026-01-01T00:00:00Z' },
        visiblePeriods: [basePeriod],
        collapsed: false,
        isFirst: false,
        isLast: false,
        budgetId: 'budget-1',
      },
    })

    // Click the restore icon button
    const restoreBtn = document.querySelector('.btn-square.text-success')
    if (restoreBtn) await fireEvent.click(restoreBtn)

    // Confirm restore with "restore all"
    await waitFor(() => {
      const restoreAllBtn = document.querySelector('.btn-success.btn-outline')
      if (restoreAllBtn) return fireEvent.click(restoreAllBtn)
    })

    await waitFor(() => expect(mockStructureStore.restoreGroup).toHaveBeenCalled())
    expect(mockToastPush).toHaveBeenCalledWith({
      type: 'success',
      title: 'budgetMatrix.rows.restoreSuccess',
    })
  })
})


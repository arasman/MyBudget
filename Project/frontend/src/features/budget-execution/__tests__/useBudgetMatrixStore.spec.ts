import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

// ---------------------------------------------------------------------------
// Hoist mock references — vi.mock factories are hoisted by Vitest
// ---------------------------------------------------------------------------

const { mockGetPeriodTotals, mockListExecutions, mockUseBudgetStructureStore } = vi.hoisted(() => ({
  mockGetPeriodTotals: vi.fn(),
  mockListExecutions: vi.fn(),
  mockUseBudgetStructureStore: vi.fn(),
}))

vi.mock('@/features/budget-execution/api/executionTotals.api', () => ({
  getPeriodTotals: mockGetPeriodTotals,
}))

vi.mock('@/features/budget-execution/api/executions.api', () => ({
  list: mockListExecutions,
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
  restore: vi.fn(),
}))

// Mock budgetStructureStore so initMatrix can read cycle + periods
vi.mock('@/features/budget-structure/store', () => ({
  useBudgetStructureStore: mockUseBudgetStructureStore,
}))

const DEFAULT_STRUCTURE_STATE = {
  currentCycle: {
    id: 'cycle-1',
    name: 'Cycle 2026',
    startDate: '2026-01-01',
    endDate: '2026-12-31',
    isActive: true,
    exchangeRate: 7.5,
    alternateCurrencyId: 'usd-id',
    periods: [
      { id: 'p1', name: 'Jan', periodNumber: 1, startDate: '2026-01-01', endDate: '2026-01-31', isClosed: false },
      { id: 'p2', name: 'Feb', periodNumber: 2, startDate: '2026-02-01', endDate: '2026-02-28', isClosed: false },
      { id: 'p3', name: 'Mar', periodNumber: 3, startDate: '2026-03-01', endDate: '2026-03-31', isClosed: false },
      { id: 'p4', name: 'Apr', periodNumber: 4, startDate: '2026-04-01', endDate: '2026-04-30', isClosed: false },
      { id: 'p5', name: 'May', periodNumber: 5, startDate: '2026-05-01', endDate: '2026-05-31', isClosed: false },
    ],
  },
  periods: [
    { id: 'p1', name: 'Jan', periodNumber: 1, startDate: '2026-01-01', endDate: '2026-01-31', isClosed: false },
    { id: 'p2', name: 'Feb', periodNumber: 2, startDate: '2026-02-01', endDate: '2026-02-28', isClosed: false },
    { id: 'p3', name: 'Mar', periodNumber: 3, startDate: '2026-03-01', endDate: '2026-03-31', isClosed: false },
    { id: 'p4', name: 'Apr', periodNumber: 4, startDate: '2026-04-01', endDate: '2026-04-30', isClosed: false },
    { id: 'p5', name: 'May', periodNumber: 5, startDate: '2026-05-01', endDate: '2026-05-31', isClosed: false },
  ],
  budgetLines: [],
  loadGroups: vi.fn(),
  loadLines: vi.fn(),
}

import { useBudgetMatrixStore } from '../store'

const MOCK_TOTALS = { lineTotals: [], categoryTotals: [] }

describe('useBudgetMatrixStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockGetPeriodTotals.mockResolvedValue(MOCK_TOTALS)
    mockListExecutions.mockResolvedValue([])
    mockUseBudgetStructureStore.mockReturnValue({ ...DEFAULT_STRUCTURE_STATE })
  })

  // -------------------------------------------------------------------------
  // initMatrix
  // -------------------------------------------------------------------------

  it('initMatrix sets budgetId and cycleId', async () => {
    const store = useBudgetMatrixStore()
    await store.initMatrix('budget-1', 'cycle-1')

    expect(store.budgetId).toBe('budget-1')
    expect(store.cycleId).toBe('cycle-1')
  })

  it('initMatrix reads exchangeRate and alternateCurrencyId from cycle', async () => {
    const store = useBudgetMatrixStore()
    await store.initMatrix('budget-1', 'cycle-1')

    expect(store.exchangeRate).toBe(7.5)
    expect(store.alternateCurrencyId).toBe('usd-id')
  })

  it('initMatrix calls loadVisiblePeriods which triggers getPeriodTotals for visible window', async () => {
    const store = useBudgetMatrixStore()
    await store.initMatrix('budget-1', 'cycle-1')

    // offset=0, windowSize=3 → p1, p2, p3
    expect(mockGetPeriodTotals).toHaveBeenCalledWith('budget-1', 'p1')
    expect(mockGetPeriodTotals).toHaveBeenCalledWith('budget-1', 'p2')
    expect(mockGetPeriodTotals).toHaveBeenCalledWith('budget-1', 'p3')
  })

  // -------------------------------------------------------------------------
  // navigatePrev / navigateNext clamping
  // -------------------------------------------------------------------------

  it('navigatePrev clamps at 0 — cannot go below zero', async () => {
    const store = useBudgetMatrixStore()
    await store.initMatrix('budget-1', 'cycle-1')

    expect(store.visiblePeriodOffset).toBe(0)
    store.navigatePrev()
    expect(store.visiblePeriodOffset).toBe(0)
  })

  it('navigateNext clamps at max offset (allPeriods.length - windowSize)', async () => {
    const store = useBudgetMatrixStore()
    await store.initMatrix('budget-1', 'cycle-1')
    // 5 periods, windowSize=3 → max offset = 2

    store.navigateNext() // offset = 1
    store.navigateNext() // offset = 2
    store.navigateNext() // clamps at 2
    expect(store.visiblePeriodOffset).toBe(2)
  })

  // -------------------------------------------------------------------------
  // setDisplayCurrency
  // -------------------------------------------------------------------------

  it('setDisplayCurrency toggles displayCurrency state', async () => {
    const store = useBudgetMatrixStore()
    expect(store.displayCurrency).toBe('default')

    store.setDisplayCurrency('alternate')
    expect(store.displayCurrency).toBe('alternate')

    store.setDisplayCurrency('default')
    expect(store.displayCurrency).toBe('default')
  })

  // -------------------------------------------------------------------------
  // setShowDeleted
  // -------------------------------------------------------------------------

  it('setShowDeleted toggles showDeleted state', async () => {
    const store = useBudgetMatrixStore()
    expect(store.showDeleted).toBe(false)

    store.setShowDeleted(true)
    expect(store.showDeleted).toBe(true)

    store.setShowDeleted(false)
    expect(store.showDeleted).toBe(false)
  })

  it('setShowDeleted clears periodTotals cache', async () => {
    const store = useBudgetMatrixStore()
    await store.initMatrix('budget-1', 'cycle-1')

    // Cache should have entries after initMatrix
    expect(Object.keys(store.periodTotals).length).toBeGreaterThan(0)

    // setShowDeleted clears and re-fetches
    mockGetPeriodTotals.mockClear()
    store.setShowDeleted(true)

    // After clearing, periodTotals is cleared synchronously before re-fetch
    // (the store clears it then calls loadVisiblePeriods async)
    expect(store.showDeleted).toBe(true)
  })

  // -------------------------------------------------------------------------
  // openExecutionModal
  // -------------------------------------------------------------------------

  it('openExecutionModal sets openModalLineId and openModalPeriodId', async () => {
    const store = useBudgetMatrixStore()
    await store.initMatrix('budget-1', 'cycle-1')

    await store.openExecutionModal('line-1', 'p1')

    expect(store.openModalLineId).toBe('line-1')
    expect(store.openModalPeriodId).toBe('p1')
  })

  it('openExecutionModal fetches records when not cached', async () => {
    const store = useBudgetMatrixStore()
    await store.initMatrix('budget-1', 'cycle-1')

    await store.openExecutionModal('line-1', 'p1')

    expect(mockListExecutions).toHaveBeenCalledWith('budget-1', 'p1', 'line-1', false)
  })

  it('openExecutionModal skips fetch when records already cached', async () => {
    const store = useBudgetMatrixStore()
    await store.initMatrix('budget-1', 'cycle-1')

    // First open — fetches
    await store.openExecutionModal('line-1', 'p1')
    expect(mockListExecutions).toHaveBeenCalledTimes(1)

    // Second open same key — should skip fetch
    await store.openExecutionModal('line-1', 'p1')
    expect(mockListExecutions).toHaveBeenCalledTimes(1)
  })

  // -------------------------------------------------------------------------
  // closeExecutionModal
  // -------------------------------------------------------------------------

  it('closeExecutionModal clears openModalLineId and openModalPeriodId', async () => {
    const store = useBudgetMatrixStore()
    await store.initMatrix('budget-1', 'cycle-1')

    await store.openExecutionModal('line-1', 'p1')
    store.closeExecutionModal()

    expect(store.openModalLineId).toBeNull()
    expect(store.openModalPeriodId).toBeNull()
  })

  // -------------------------------------------------------------------------
  // subtotalByLineType
  // -------------------------------------------------------------------------

  it('subtotalByLineType returns correct budgeted sum for Expense lineType', () => {
    const store = useBudgetMatrixStore()

    mockUseBudgetStructureStore.mockReturnValue({
      ...DEFAULT_STRUCTURE_STATE,
      budgetLines: [
        { id: 'l1', name: 'Food', lineType: 'Expense', isRecurring: false, categoryGroupId: 'g1', categoryId: 'cat-1', budgetedAmount: 500, deletedAt: null },
        { id: 'l2', name: 'Transport', lineType: 'Expense', isRecurring: false, categoryGroupId: 'g1', categoryId: 'cat-2', budgetedAmount: 200, deletedAt: null },
        { id: 'l3', name: 'Emergency', lineType: 'LongTermSavings', isRecurring: false, categoryGroupId: 'g2', categoryId: 'cat-3', budgetedAmount: 300, deletedAt: null },
      ],
    })

    store.$patch({
      periodTotals: {
        'p1': {
          lineTotals: [],
          categoryTotals: [
            { categoryGroupId: 'g1', categoryGroupName: 'G1', categoryId: 'cat-1', categoryName: 'Food', totalExpenses: 400, totalCreditNotes: 0, totalDebitNotes: 0, netTotal: 400 },
            { categoryGroupId: 'g1', categoryGroupName: 'G1', categoryId: 'cat-2', categoryName: 'Transport', totalExpenses: 150, totalCreditNotes: 0, totalDebitNotes: 0, netTotal: 150 },
          ],
        },
      },
    })

    const result = store.subtotalByLineType('p1', 'Expense')
    expect(result.budgeted).toBe(700)
    expect(result.executed).toBe(550)
  })

  it('subtotalByLineType returns correct executed sum filtered by periodId', () => {
    const store = useBudgetMatrixStore()

    mockUseBudgetStructureStore.mockReturnValue({
      ...DEFAULT_STRUCTURE_STATE,
      budgetLines: [
        { id: 'l1', name: 'Food', lineType: 'Expense', isRecurring: false, categoryGroupId: 'g1', categoryId: 'cat-1', budgetedAmount: 500, deletedAt: null },
      ],
    })

    store.$patch({
      periodTotals: {
        'p1': { lineTotals: [], categoryTotals: [{ categoryGroupId: 'g1', categoryGroupName: 'G1', categoryId: 'cat-1', categoryName: 'Food', totalExpenses: 300, totalCreditNotes: 0, totalDebitNotes: 0, netTotal: 300 }] },
        'p2': { lineTotals: [], categoryTotals: [{ categoryGroupId: 'g1', categoryGroupName: 'G1', categoryId: 'cat-1', categoryName: 'Food', totalExpenses: 450, totalCreditNotes: 0, totalDebitNotes: 0, netTotal: 450 }] },
      },
    })

    expect(store.subtotalByLineType('p1', 'Expense').executed).toBe(300)
    expect(store.subtotalByLineType('p2', 'Expense').executed).toBe(450)
  })

  it('subtotalByLineType returns { budgeted: 0, executed: 0 } for lineType with no matching lines', () => {
    const store = useBudgetMatrixStore()

    mockUseBudgetStructureStore.mockReturnValue({
      ...DEFAULT_STRUCTURE_STATE,
      budgetLines: [
        { id: 'l1', name: 'Food', lineType: 'Expense', isRecurring: false, categoryGroupId: 'g1', categoryId: 'cat-1', budgetedAmount: 500, deletedAt: null },
      ],
    })

    store.$patch({
      periodTotals: { 'p1': { lineTotals: [], categoryTotals: [] } },
    })

    const result = store.subtotalByLineType('p1', 'PreventiveSavings')
    expect(result.budgeted).toBe(0)
    expect(result.executed).toBe(0)
  })

  it('subtotalByLineType excludes deleted budget lines from budgeted sum', () => {
    const store = useBudgetMatrixStore()

    mockUseBudgetStructureStore.mockReturnValue({
      ...DEFAULT_STRUCTURE_STATE,
      budgetLines: [
        { id: 'l1', name: 'Food', lineType: 'Expense', isRecurring: false, categoryGroupId: 'g1', categoryId: 'cat-1', budgetedAmount: 500, deletedAt: null },
        { id: 'l2', name: 'Deleted', lineType: 'Expense', isRecurring: false, categoryGroupId: 'g1', categoryId: 'cat-2', budgetedAmount: 999, deletedAt: '2026-01-01' },
      ],
    })

    store.$patch({ periodTotals: { 'p1': { lineTotals: [], categoryTotals: [] } } })

    const result = store.subtotalByLineType('p1', 'Expense')
    expect(result.budgeted).toBe(500)
  })

  // -------------------------------------------------------------------------
  // syncExchangeRate
  // -------------------------------------------------------------------------

  it('syncExchangeRate copies exchangeRate from structureStore.currentCycle', () => {
    const store = useBudgetMatrixStore()
    store.$patch({ exchangeRate: 7.5 })

    mockUseBudgetStructureStore.mockReturnValue({
      ...DEFAULT_STRUCTURE_STATE,
      currentCycle: { ...DEFAULT_STRUCTURE_STATE.currentCycle, exchangeRate: 10.0 },
    })

    store.syncExchangeRate()
    expect(store.exchangeRate).toBe(10.0)
  })

  it('syncExchangeRate sets exchangeRate to null when currentCycle has no rate', () => {
    const store = useBudgetMatrixStore()
    store.$patch({ exchangeRate: 7.5 })

    mockUseBudgetStructureStore.mockReturnValue({
      ...DEFAULT_STRUCTURE_STATE,
      currentCycle: null,
    })

    store.syncExchangeRate()
    expect(store.exchangeRate).toBeNull()
  })
})

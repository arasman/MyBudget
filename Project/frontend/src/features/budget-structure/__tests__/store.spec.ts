import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useBudgetStructureStore } from '../store'

// Mock all API modules
vi.mock('../api/cycles.api', () => ({
  list: vi.fn(),
  get: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
  setActive: vi.fn(),
}))

vi.mock('../api/periods.api', () => ({
  list: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  patchStatus: vi.fn(),
  remove: vi.fn(),
}))

vi.mock('../api/categoryGroups.api', () => ({
  list: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
  reorder: vi.fn(),
}))

vi.mock('../api/categories.api', () => ({
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
  reorder: vi.fn(),
}))

vi.mock('../api/budgetLines.api', () => ({
  list: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
  restore: vi.fn(),
  reorder: vi.fn(),
}))

import * as cyclesApi from '../api/cycles.api'
import * as budgetLinesApi from '../api/budgetLines.api'

const BUDGET_ID = 'budget-1'

describe('useBudgetStructureStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  // -------------------------------------------------------------------------
  // Cycles
  // -------------------------------------------------------------------------

  describe('loadCycles', () => {
    it('populates cycles from API', async () => {
      const mockCycles = [
        { id: 'c1', name: 'Cycle 1', startDate: '2024-01-01', endDate: '2024-12-31', isActive: true, periodCount: 3 },
        { id: 'c2', name: 'Cycle 2', startDate: '2025-01-01', endDate: '2025-12-31', isActive: false, periodCount: 0 },
      ]
      vi.mocked(cyclesApi.list).mockResolvedValueOnce(mockCycles as any)

      const store = useBudgetStructureStore()
      await store.loadCycles(BUDGET_ID)

      expect(store.cycles).toHaveLength(2)
      expect(store.cycles[0]!.name).toBe('Cycle 1')
      expect(store.cycles[1]!.name).toBe('Cycle 2')
    })
  })

  describe('createCycle', () => {
    it('reloads the cycle list after creation', async () => {
      vi.mocked(cyclesApi.create).mockResolvedValueOnce({ id: 'new-cycle' } as any)
      vi.mocked(cyclesApi.list).mockResolvedValueOnce([
        { id: 'new-cycle', name: 'New Cycle', startDate: '2024-01-01', endDate: '2024-12-31', isActive: false, periodCount: 0 },
      ] as any)

      const store = useBudgetStructureStore()
      await store.createCycle(BUDGET_ID, {
        name: 'New Cycle',
        startDate: '2024-01-01' as any,
        endDate: '2024-12-31' as any,
        defaultCurrencyId: '11111111-1111-1111-1111-111111111111',
      })

      expect(store.cycles).toHaveLength(1)
      expect(store.cycles[0]!.id).toBe('new-cycle')
      expect(store.cycles[0]!.name).toBe('New Cycle')
      expect(store.cycles[0]!.isActive).toBe(false)
    })
  })

  describe('deleteCycle', () => {
    it('removes the cycle from the list', async () => {
      // Pre-populate
      vi.mocked(cyclesApi.list).mockResolvedValueOnce([
        { id: 'c1', name: 'Cycle 1', startDate: '2024-01-01', endDate: '2024-12-31', isActive: false, periodCount: 0 },
        { id: 'c2', name: 'Cycle 2', startDate: '2025-01-01', endDate: '2025-12-31', isActive: false, periodCount: 0 },
      ] as any)
      vi.mocked(cyclesApi.remove).mockResolvedValueOnce(undefined)

      const store = useBudgetStructureStore()
      await store.loadCycles(BUDGET_ID)
      await store.deleteCycle(BUDGET_ID, 'c1')

      expect(store.cycles).toHaveLength(1)
      expect(store.cycles[0]!.id).toBe('c2')
    })
  })

  describe('setActiveCycle', () => {
    it('marks only the target cycle as active', async () => {
      vi.mocked(cyclesApi.list).mockResolvedValueOnce([
        { id: 'c1', name: 'Cycle 1', startDate: '2024-01-01', endDate: '2024-12-31', isActive: true, periodCount: 0 },
        { id: 'c2', name: 'Cycle 2', startDate: '2025-01-01', endDate: '2025-12-31', isActive: false, periodCount: 0 },
      ] as any)
      vi.mocked(cyclesApi.setActive).mockResolvedValueOnce(undefined)

      const store = useBudgetStructureStore()
      await store.loadCycles(BUDGET_ID)
      await store.setActiveCycle(BUDGET_ID, 'c2')

      const c1 = store.cycles.find((c) => c.id === 'c1')
      const c2 = store.cycles.find((c) => c.id === 'c2')
      expect(c1!.isActive).toBe(false)
      expect(c2!.isActive).toBe(true)
    })
  })

  // -------------------------------------------------------------------------
  // Budget lines
  // -------------------------------------------------------------------------

  describe('loadLines', () => {
    it('populates budgetLines from API', async () => {
      const mockLines = [
        { id: 'l1', name: 'Salary', lineType: 'Expense', startDate: '2025-01-01', endDate: null, budgetedAmount: 1000, currencyId: 'gtq', categoryGroupId: 'g1' },
        { id: 'l2', name: 'Rent', lineType: 'Expense', startDate: '2025-01-01', endDate: null, budgetedAmount: 500, currencyId: 'gtq', categoryGroupId: 'g1' },
        { id: 'l3', name: 'Groceries', lineType: 'Expense', startDate: '2025-01-01', endDate: null, budgetedAmount: 300, currencyId: 'gtq', categoryGroupId: 'g1' },
      ]
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce(mockLines as any)

      const store = useBudgetStructureStore()
      await store.loadLines(BUDGET_ID)

      expect(store.budgetLines).toHaveLength(3)
      expect(store.budgetLines[0]!.name).toBe('Salary')
    })
  })

  describe('createLine', () => {
    it('appends new line to budgetLines', async () => {
      vi.mocked(budgetLinesApi.create).mockResolvedValueOnce({ id: 'new-line' } as any)
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce([
        { id: 'new-line', name: 'Salary', lineType: 'Expense', startDate: '2025-01-01', endDate: null, budgetedAmount: 1000, currencyId: 'gtq', categoryGroupId: 'g1' },
      ] as any)

      const store = useBudgetStructureStore()
      await store.createLine(BUDGET_ID, {
        name: 'Salary',
        lineType: 'Expense',
        startDate: '2025-01-01',
        initialAmount: 1000,
        currencyId: 'gtq',
        categoryGroupId: 'g1',
      })

      expect(store.budgetLines).toHaveLength(1)
      expect(store.budgetLines[0]!.id).toBe('new-line')
      expect(store.budgetLines[0]!.name).toBe('Salary')
    })
  })

  describe('deleteLine', () => {
    it('marks the line as deleted in budgetLines (soft delete)', async () => {
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce([
        { id: 'l1', name: 'Salary', lineType: 'Expense', startDate: '2025-01-01', endDate: null, budgetedAmount: 1000, currencyId: 'gtq', categoryGroupId: 'g1' },
        { id: 'l2', name: 'Rent', lineType: 'Expense', startDate: '2025-01-01', endDate: null, budgetedAmount: 500, currencyId: 'gtq', categoryGroupId: 'g1' },
      ] as any)
      vi.mocked(budgetLinesApi.remove).mockResolvedValueOnce(undefined)

      const store = useBudgetStructureStore()
      await store.loadLines(BUDGET_ID)
      await store.deleteLine(BUDGET_ID, 'l1')

      expect(store.budgetLines).toHaveLength(2)
      expect(store.budgetLines[0]!.deletedAt).toBeTruthy()
      expect(store.budgetLines[1]!.deletedAt).toBeFalsy()
    })
  })

  // -------------------------------------------------------------------------
  // Loading state
  // -------------------------------------------------------------------------

  describe('loading flag', () => {
    it('is false when no action is in flight', () => {
      const store = useBudgetStructureStore()
      expect(store.loading).toBe(false)
    })
  })
})

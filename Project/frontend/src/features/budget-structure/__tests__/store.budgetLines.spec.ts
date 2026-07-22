// REQ-BL-STORE-1: store actions accept budgetId only (no periodId)
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useBudgetStructureStore } from '../store'

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
  restore: vi.fn(),
}))

vi.mock('../api/categoryGroups.api', () => ({
  list: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
  reorder: vi.fn(),
  restore: vi.fn(),
}))

vi.mock('../api/categories.api', () => ({
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
  reorder: vi.fn(),
  restore: vi.fn(),
}))

vi.mock('../api/budgetLines.api', () => ({
  list: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
  restore: vi.fn(),
  reorder: vi.fn(),
  listRevisions: vi.fn(),
  createRevision: vi.fn(),
  deleteRevision: vi.fn(),
  updateRevision: vi.fn(),
}))

import * as budgetLinesApi from '../api/budgetLines.api'
import type { BudgetLineResponse, BudgetLineRevisionResponse } from '../types'

const BUDGET_ID = 'budget-1'

const mockLine: BudgetLineResponse = {
  id: 'l1',
  name: 'Salary',
  lineType: 'Expense',
  startDate: '2025-01-01' as any,
  endDate: null,
  budgetedAmount: 1000,
  currencyId: 'currency-gtq',
  categoryGroupId: 'g1',
}

describe('useBudgetStructureStore — BudgetLine actions (budget-scoped, no periodId)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  describe('loadLines', () => {
    it('calls list(budgetId) without periodId', async () => {
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce([mockLine])
      const store = useBudgetStructureStore()
      await store.loadLines(BUDGET_ID)
      expect(budgetLinesApi.list).toHaveBeenCalledWith(BUDGET_ID, false)
    })

    it('populates budgetLines from API', async () => {
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce([mockLine])
      const store = useBudgetStructureStore()
      await store.loadLines(BUDGET_ID)
      expect(store.budgetLines).toHaveLength(1)
      expect(store.budgetLines[0]!.name).toBe('Salary')
    })

    it('passes includeDeleted when showDeletedBudgetLines is true', async () => {
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce([])
      const store = useBudgetStructureStore()
      store.showDeletedBudgetLines = true
      await store.loadLines(BUDGET_ID)
      expect(budgetLinesApi.list).toHaveBeenCalledWith(BUDGET_ID, true)
    })
  })

  describe('createLine', () => {
    it('calls create(budgetId, payload) without periodId and reloads', async () => {
      vi.mocked(budgetLinesApi.create).mockResolvedValueOnce({ id: 'new-line' })
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce([mockLine])
      const store = useBudgetStructureStore()
      const payload = {
        name: 'Salary',
        lineType: 'Expense' as const,
        startDate: '2025-01-01',
        initialAmount: 1000,
        currencyId: 'currency-gtq',
      }
      await store.createLine(BUDGET_ID, payload)
      expect(budgetLinesApi.create).toHaveBeenCalledWith(BUDGET_ID, payload)
      expect(store.budgetLines).toHaveLength(1)
    })
  })

  describe('updateLine', () => {
    it('calls update(budgetId, lineId, payload) without periodId', async () => {
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce([mockLine])
      vi.mocked(budgetLinesApi.update).mockResolvedValueOnce(undefined)
      const store = useBudgetStructureStore()
      await store.loadLines(BUDGET_ID)
      const payload = { name: 'Updated Name', lineType: 'Expense' as const }
      await store.updateLine(BUDGET_ID, 'l1', payload)
      expect(budgetLinesApi.update).toHaveBeenCalledWith(BUDGET_ID, 'l1', payload)
    })

    it('optimistically updates the line in store', async () => {
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce([mockLine])
      vi.mocked(budgetLinesApi.update).mockResolvedValueOnce(undefined)
      const store = useBudgetStructureStore()
      await store.loadLines(BUDGET_ID)
      await store.updateLine(BUDGET_ID, 'l1', { name: 'Updated', lineType: 'Expense' as const })
      expect(store.budgetLines[0]!.name).toBe('Updated')
    })
  })

  describe('deleteLine', () => {
    it('calls remove(budgetId, lineId) without periodId', async () => {
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce([mockLine])
      vi.mocked(budgetLinesApi.remove).mockResolvedValueOnce(undefined)
      const store = useBudgetStructureStore()
      await store.loadLines(BUDGET_ID)
      await store.deleteLine(BUDGET_ID, 'l1')
      expect(budgetLinesApi.remove).toHaveBeenCalledWith(BUDGET_ID, 'l1')
    })

    it('marks the line as deleted in state', async () => {
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce([mockLine])
      vi.mocked(budgetLinesApi.remove).mockResolvedValueOnce(undefined)
      const store = useBudgetStructureStore()
      await store.loadLines(BUDGET_ID)
      await store.deleteLine(BUDGET_ID, 'l1')
      expect(store.budgetLines[0]!.deletedAt).toBeTruthy()
    })
  })

  describe('restoreLine', () => {
    it('calls restore(budgetId, lineId, includeExecutionRecords) without periodId', async () => {
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce([
        { ...mockLine, deletedAt: new Date().toISOString() },
      ])
      vi.mocked(budgetLinesApi.restore).mockResolvedValueOnce(undefined)
      const store = useBudgetStructureStore()
      await store.loadLines(BUDGET_ID)
      await store.restoreLine(BUDGET_ID, 'l1', false)
      expect(budgetLinesApi.restore).toHaveBeenCalledWith(BUDGET_ID, 'l1', false)
    })

    it('clears deletedAt on the restored line', async () => {
      const deletedLine = { ...mockLine, deletedAt: new Date().toISOString() }
      vi.mocked(budgetLinesApi.list).mockResolvedValueOnce([deletedLine])
      vi.mocked(budgetLinesApi.restore).mockResolvedValueOnce(undefined)
      const store = useBudgetStructureStore()
      await store.loadLines(BUDGET_ID)
      await store.restoreLine(BUDGET_ID, 'l1', false)
      expect(store.budgetLines[0]!.deletedAt).toBeNull()
    })
  })
})

// REQ-BLR-05: store revision actions
describe('useBudgetStructureStore — revision actions (REQ-BLR-01, REQ-BLR-02, REQ-BLR-03)', () => {
  const LINE_ID = 'l1'

  const mockRevision: BudgetLineRevisionResponse = {
    id: 'rev-1',
    budgetedAmount: 1000,
    currencyId: 'currency-gtq',
    validFrom: '2025-01-01' as any,
    validTo: null,
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  describe('fetchRevisions', () => {
    it('calls listRevisions(budgetId, lineId) and populates revisions', async () => {
      vi.mocked(budgetLinesApi.listRevisions).mockResolvedValueOnce([mockRevision])
      const store = useBudgetStructureStore()
      await store.fetchRevisions(BUDGET_ID, LINE_ID)
      expect(budgetLinesApi.listRevisions).toHaveBeenCalledWith(BUDGET_ID, LINE_ID)
      expect(store.revisions).toHaveLength(1)
      expect(store.revisions[0]!.id).toBe('rev-1')
    })

    it('sets error on failure', async () => {
      vi.mocked(budgetLinesApi.listRevisions).mockRejectedValueOnce(new Error('Network error'))
      const store = useBudgetStructureStore()
      await expect(store.fetchRevisions(BUDGET_ID, LINE_ID)).rejects.toThrow()
      expect(store.error).toBeTruthy()
    })
  })

  describe('createRevision', () => {
    it('calls createRevision API and reloads revisions', async () => {
      vi.mocked(budgetLinesApi.createRevision).mockResolvedValueOnce({ id: 'rev-new' })
      vi.mocked(budgetLinesApi.listRevisions).mockResolvedValueOnce([mockRevision])
      const store = useBudgetStructureStore()
      const payload = { validFrom: '2025-06-01', amount: 1500 }
      await store.createRevision(BUDGET_ID, LINE_ID, payload)
      expect(budgetLinesApi.createRevision).toHaveBeenCalledWith(BUDGET_ID, LINE_ID, payload)
      expect(budgetLinesApi.listRevisions).toHaveBeenCalledWith(BUDGET_ID, LINE_ID)
    })
  })

  describe('deleteRevision', () => {
    it('calls deleteRevision API and reloads revisions', async () => {
      vi.mocked(budgetLinesApi.deleteRevision).mockResolvedValueOnce(undefined)
      vi.mocked(budgetLinesApi.listRevisions).mockResolvedValueOnce([])
      const store = useBudgetStructureStore()
      await store.deleteRevision(BUDGET_ID, LINE_ID, 'rev-1')
      expect(budgetLinesApi.deleteRevision).toHaveBeenCalledWith(BUDGET_ID, LINE_ID, 'rev-1')
      expect(budgetLinesApi.listRevisions).toHaveBeenCalledWith(BUDGET_ID, LINE_ID)
      expect(store.revisions).toHaveLength(0)
    })
  })

  describe('updateRevision', () => {
    it('calls updateRevision API with correct args and reloads revisions', async () => {
      const updatedRevision: BudgetLineRevisionResponse = {
        id: 'rev-1',
        budgetedAmount: 1500,
        currencyId: 'currency-gtq',
        validFrom: '2025-01-01' as any,
        validTo: null,
      }
      vi.mocked(budgetLinesApi.updateRevision).mockResolvedValueOnce(undefined)
      vi.mocked(budgetLinesApi.listRevisions).mockResolvedValueOnce([updatedRevision])
      const store = useBudgetStructureStore()
      const payload = { amount: 1500, note: 'Updated note' }
      await store.updateRevision(BUDGET_ID, LINE_ID, 'rev-1', payload)
      expect(budgetLinesApi.updateRevision).toHaveBeenCalledWith(BUDGET_ID, LINE_ID, 'rev-1', payload)
      expect(budgetLinesApi.listRevisions).toHaveBeenCalledWith(BUDGET_ID, LINE_ID)
      expect(store.revisions).toHaveLength(1)
      expect(store.revisions[0]!.budgetedAmount).toBe(1500)
    })

    it('sets error on failure', async () => {
      vi.mocked(budgetLinesApi.updateRevision).mockRejectedValueOnce(new Error('Server error'))
      const store = useBudgetStructureStore()
      await expect(store.updateRevision(BUDGET_ID, LINE_ID, 'rev-1', { amount: 1500 })).rejects.toThrow()
      expect(store.error).toBeTruthy()
    })
  })
})

// REQ-BL-STORE-1, REQ-BL-1: API layer uses budget-scoped routes (no periodId)
import { describe, it, expect, vi, beforeEach } from 'vitest'

const { mockGet, mockPost, mockPut, mockDelete } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPost: vi.fn(),
  mockPut: vi.fn(),
  mockDelete: vi.fn(),
}))

vi.mock('@/api/axios', () => ({
  default: { get: mockGet, post: mockPost, put: mockPut, delete: mockDelete },
}))

import * as budgetLinesApi from '../api/budgetLines.api'

const BUDGET_ID = 'budget-1'
const LINE_ID = 'line-1'

describe('budgetLines.api — budget-scoped routes (no periodId)', () => {
  beforeEach(() => vi.clearAllMocks())

  it('list calls GET /api/budgets/:budgetId/lines', async () => {
    mockGet.mockResolvedValueOnce({ data: [] })
    await budgetLinesApi.list(BUDGET_ID)
    expect(mockGet).toHaveBeenCalledWith(`/api/budgets/${BUDGET_ID}/lines`, expect.anything())
  })

  it('list passes includeDeleted param when true', async () => {
    mockGet.mockResolvedValueOnce({ data: [] })
    await budgetLinesApi.list(BUDGET_ID, true)
    expect(mockGet).toHaveBeenCalledWith(
      `/api/budgets/${BUDGET_ID}/lines`,
      { params: { includeDeleted: true } },
    )
  })

  it('list does NOT include periodId in the URL', async () => {
    mockGet.mockResolvedValueOnce({ data: [] })
    await budgetLinesApi.list(BUDGET_ID)
    const url: string = mockGet.mock.calls[0]![0] as string
    expect(url).not.toContain('period')
  })

  it('create calls POST /api/budgets/:budgetId/lines with new payload shape', async () => {
    mockPost.mockResolvedValueOnce({ data: { id: 'new-line' } })
    const payload = {
      name: 'Test Line',
      lineType: 'Expense' as const,
      startDate: '2025-01-01',
      initialAmount: 1000,
      currencyId: 'currency-gtq',
    }
    await budgetLinesApi.create(BUDGET_ID, payload)
    expect(mockPost).toHaveBeenCalledWith(`/api/budgets/${BUDGET_ID}/lines`, payload)
  })

  it('update calls PUT /api/budgets/:budgetId/lines/:lineId', async () => {
    mockPut.mockResolvedValueOnce({ data: undefined })
    const payload = { name: 'Updated', lineType: 'Expense' as const }
    await budgetLinesApi.update(BUDGET_ID, LINE_ID, payload)
    expect(mockPut).toHaveBeenCalledWith(
      `/api/budgets/${BUDGET_ID}/lines/${LINE_ID}`,
      payload,
    )
  })

  it('remove calls DELETE /api/budgets/:budgetId/lines/:lineId', async () => {
    mockDelete.mockResolvedValueOnce({ data: undefined })
    await budgetLinesApi.remove(BUDGET_ID, LINE_ID)
    expect(mockDelete).toHaveBeenCalledWith(`/api/budgets/${BUDGET_ID}/lines/${LINE_ID}`)
  })

  it('restore calls POST /api/budgets/:budgetId/lines/:lineId/restore', async () => {
    mockPost.mockResolvedValueOnce({ data: undefined })
    await budgetLinesApi.restore(BUDGET_ID, LINE_ID, false)
    expect(mockPost).toHaveBeenCalledWith(
      `/api/budgets/${BUDGET_ID}/lines/${LINE_ID}/restore`,
      null,
      expect.anything(),
    )
  })

  it('reorder calls PUT /api/budgets/:budgetId/lines/order', async () => {
    mockPut.mockResolvedValueOnce({ data: undefined })
    await budgetLinesApi.reorder(BUDGET_ID, ['line-1', 'line-2'])
    expect(mockPut).toHaveBeenCalledWith(
      `/api/budgets/${BUDGET_ID}/lines/order`,
      { orderedIds: ['line-1', 'line-2'] },
    )
  })
})

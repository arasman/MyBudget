import { describe, it, expect, vi, beforeEach } from 'vitest'
import { EntryType } from '../types'

// vi.mock factories are hoisted by Vitest — vi.hoisted() must be used for
// any named references used inside the factory.
const { mockGet, mockPost, mockPut, mockDelete } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPost: vi.fn(),
  mockPut: vi.fn(),
  mockDelete: vi.fn(),
}))

vi.mock('@/api/axios', () => ({
  default: {
    get: mockGet,
    post: mockPost,
    put: mockPut,
    delete: mockDelete,
  },
}))

import * as executionsApi from '../api/executions.api'

const BUDGET_ID = 'budget-1'
const PERIOD_ID = 'period-2'
const LINE_ID = 'line-3'
const EXEC_ID = 'exec-4'

const MOCK_RECORD = {
  id: EXEC_ID,
  entryType: EntryType.Expense,
  amount: 500,
  currencyId: 'GTQ',
  exchangeRate: null,
  exchangeRateTo: null,
  accountId: null,
  paymentMethodId: null,
  note: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: null,
}

describe('executions.api', () => {
  beforeEach(() => vi.clearAllMocks())

  // -------------------------------------------------------------------------
  // list
  // -------------------------------------------------------------------------

  it('list: calls correct URL and returns data', async () => {
    mockGet.mockResolvedValue({ data: [MOCK_RECORD] })

    const result = await executionsApi.list(BUDGET_ID, PERIOD_ID, LINE_ID)

    expect(mockGet).toHaveBeenCalledWith(
      `/api/budgets/${BUDGET_ID}/periods/${PERIOD_ID}/budget-lines/${LINE_ID}/executions`,
      { params: undefined },
    )
    expect(result).toEqual([MOCK_RECORD])
  })

  // -------------------------------------------------------------------------
  // create
  // -------------------------------------------------------------------------

  it('create: posts to correct URL with payload and returns id', async () => {
    mockPost.mockResolvedValue({ data: { id: EXEC_ID } })

    const payload = { entryType: EntryType.Expense, amount: 500, currencyId: 'GTQ' }
    const result = await executionsApi.create(BUDGET_ID, PERIOD_ID, LINE_ID, payload)

    expect(mockPost).toHaveBeenCalledWith(
      `/api/budgets/${BUDGET_ID}/periods/${PERIOD_ID}/budget-lines/${LINE_ID}/executions`,
      payload,
    )
    expect(result).toEqual({ id: EXEC_ID })
  })

  // -------------------------------------------------------------------------
  // update
  // -------------------------------------------------------------------------

  it('update: puts to correct URL with executionId in path', async () => {
    mockPut.mockResolvedValue({ data: { id: EXEC_ID } })

    const payload = { entryType: EntryType.CreditNote, amount: 100, currencyId: 'GTQ', note: 'refund' }
    const result = await executionsApi.update(BUDGET_ID, PERIOD_ID, LINE_ID, EXEC_ID, payload)

    expect(mockPut).toHaveBeenCalledWith(
      `/api/budgets/${BUDGET_ID}/periods/${PERIOD_ID}/budget-lines/${LINE_ID}/executions/${EXEC_ID}`,
      payload,
    )
    expect(result).toEqual({ id: EXEC_ID })
  })

  // -------------------------------------------------------------------------
  // remove
  // -------------------------------------------------------------------------

  it('remove: deletes correct URL with executionId in path', async () => {
    mockDelete.mockResolvedValue({ data: undefined })

    await executionsApi.remove(BUDGET_ID, PERIOD_ID, LINE_ID, EXEC_ID)

    expect(mockDelete).toHaveBeenCalledWith(
      `/api/budgets/${BUDGET_ID}/periods/${PERIOD_ID}/budget-lines/${LINE_ID}/executions/${EXEC_ID}`,
    )
  })

  // -------------------------------------------------------------------------
  // restore
  // -------------------------------------------------------------------------

  it('restore: posts to /restore sub-path and returns id', async () => {
    mockPost.mockResolvedValue({ data: { id: EXEC_ID } })

    const result = await executionsApi.restore(BUDGET_ID, PERIOD_ID, LINE_ID, EXEC_ID)

    expect(mockPost).toHaveBeenCalledWith(
      `/api/budgets/${BUDGET_ID}/periods/${PERIOD_ID}/budget-lines/${LINE_ID}/executions/${EXEC_ID}/restore`,
    )
    expect(result).toEqual({ id: EXEC_ID })
  })

  // -------------------------------------------------------------------------
  // URL construction — budgetId is always in the {id} position
  // -------------------------------------------------------------------------

  it('all functions use budgetId as the budget segment in the URL', async () => {
    mockGet.mockResolvedValue({ data: [] })
    await executionsApi.list('my-budget', 'my-period', 'my-line')
    expect(mockGet).toHaveBeenCalledWith(
      '/api/budgets/my-budget/periods/my-period/budget-lines/my-line/executions',
      { params: undefined },
    )
  })
})

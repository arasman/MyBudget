import { describe, it, expect, vi, beforeEach } from 'vitest'

const { mockGet } = vi.hoisted(() => ({
  mockGet: vi.fn(),
}))

vi.mock('@/api/axios', () => ({
  default: {
    get: mockGet,
  },
}))

import * as executionTotalsApi from '../api/executionTotals.api'

const BUDGET_ID = 'budget-1'
const PERIOD_ID = 'period-2'

const MOCK_TOTALS = {
  lineTotals: [
    {
      budgetLineId: 'line-1',
      budgetedAmount: 1000,
      netExecuted: 750,
      variance: 250,
    },
  ],
  categoryTotals: [
    {
      categoryId: 'cat-1',
      categoryName: 'Alquiler',
      categoryGroupId: 'group-1',
      categoryGroupName: 'Vivienda',
      budgetedAmount: 1000,
      netExecuted: 750,
      variance: 250,
    },
  ],
}

describe('executionTotals.api', () => {
  beforeEach(() => vi.clearAllMocks())

  it('getPeriodTotals: calls correct URL and returns PeriodTotalsDto', async () => {
    mockGet.mockResolvedValue({ data: MOCK_TOTALS })

    const result = await executionTotalsApi.getPeriodTotals(BUDGET_ID, PERIOD_ID)

    expect(mockGet).toHaveBeenCalledWith(
      `/api/budgets/${BUDGET_ID}/periods/${PERIOD_ID}/execution-totals`,
    )
    expect(result).toEqual(MOCK_TOTALS)
    expect(result.lineTotals).toHaveLength(1)
    expect(result.categoryTotals).toHaveLength(1)
  })

  it('getPeriodTotals: rejects when network request fails', async () => {
    mockGet.mockRejectedValue(new Error('Network error'))

    await expect(executionTotalsApi.getPeriodTotals(BUDGET_ID, PERIOD_ID)).rejects.toThrow(
      'Network error',
    )
    expect(mockGet).toHaveBeenCalledOnce()
  })
})

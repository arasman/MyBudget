// budget-member-administration WU1 frontend (PR2b): API module for
// GET /api/budgets/:budgetId/members and PATCH /api/budgets/:budgetId/members/:userId/role
import { describe, it, expect, vi, beforeEach } from 'vitest'

const { mockGet, mockPatch } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPatch: vi.fn(),
}))

vi.mock('@/api/axios', () => ({
  default: { get: mockGet, patch: mockPatch },
}))

import * as budgetMembersApi from '../api/budgetMembers.api'

const BUDGET_ID = 'budget-1'
const USER_ID = 'user-1'

describe('budgetMembers.api', () => {
  beforeEach(() => vi.clearAllMocks())

  it('listMembers calls GET /api/budgets/:budgetId/members and unwraps the members array', async () => {
    const members = [
      {
        userId: USER_ID,
        email: 'a@example.com',
        firstName: 'A',
        lastName: 'B',
        role: 'admin',
        joinedAt: '2026-01-01T00:00:00Z',
      },
    ]
    mockGet.mockResolvedValueOnce({ data: { members } })

    const result = await budgetMembersApi.listMembers(BUDGET_ID)

    expect(mockGet).toHaveBeenCalledWith(`/api/budgets/${BUDGET_ID}/members`)
    expect(result).toEqual(members)
  })

  it('updateMemberRole calls PATCH /api/budgets/:budgetId/members/:userId/role with { role } body', async () => {
    mockPatch.mockResolvedValueOnce({ data: { userId: USER_ID, role: 'operator' } })

    const result = await budgetMembersApi.updateMemberRole(BUDGET_ID, USER_ID, 'operator')

    expect(mockPatch).toHaveBeenCalledWith(
      `/api/budgets/${BUDGET_ID}/members/${USER_ID}/role`,
      { role: 'operator' },
    )
    expect(result).toEqual({ userId: USER_ID, role: 'operator' })
  })

  it.each(['admin', 'operator', 'read-only'] as const)(
    'updateMemberRole round-trips role value %s',
    async (role) => {
      mockPatch.mockResolvedValueOnce({ data: { userId: USER_ID, role } })

      await budgetMembersApi.updateMemberRole(BUDGET_ID, USER_ID, role)

      expect(mockPatch).toHaveBeenCalledWith(
        `/api/budgets/${BUDGET_ID}/members/${USER_ID}/role`,
        { role },
      )
    },
  )
})

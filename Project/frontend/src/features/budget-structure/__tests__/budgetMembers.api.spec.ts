// budget-member-administration WU1 frontend (PR2b): API module for
// GET /api/budgets/:budgetId/members and PATCH /api/budgets/:budgetId/members/:userId/role
// WU2 (PR3) additions: includeDeleted param, removeMember, restoreMember
import { describe, it, expect, vi, beforeEach } from 'vitest'

const { mockGet, mockPatch, mockDelete, mockPost } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPatch: vi.fn(),
  mockDelete: vi.fn(),
  mockPost: vi.fn(),
}))

vi.mock('@/api/axios', () => ({
  default: { get: mockGet, patch: mockPatch, delete: mockDelete, post: mockPost },
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

  // --- WU2 (PR3) ---

  it('listMembers(budgetId, { includeDeleted: true }) calls GET .../members?includeDeleted=true', async () => {
    mockGet.mockResolvedValueOnce({ data: { members: [] } })

    await budgetMembersApi.listMembers(BUDGET_ID, { includeDeleted: true })

    expect(mockGet).toHaveBeenCalledWith(`/api/budgets/${BUDGET_ID}/members?includeDeleted=true`)
  })

  it('listMembers(budgetId) with no options omits the includeDeleted param', async () => {
    mockGet.mockResolvedValueOnce({ data: { members: [] } })

    await budgetMembersApi.listMembers(BUDGET_ID)

    expect(mockGet).toHaveBeenCalledWith(`/api/budgets/${BUDGET_ID}/members`)
  })

  it('removeMember calls DELETE /api/budgets/:budgetId/members/:userId', async () => {
    mockDelete.mockResolvedValueOnce({ data: undefined })

    await budgetMembersApi.removeMember(BUDGET_ID, USER_ID)

    expect(mockDelete).toHaveBeenCalledWith(`/api/budgets/${BUDGET_ID}/members/${USER_ID}`)
  })

  it('restoreMember calls POST /api/budgets/:budgetId/members/:userId/restore', async () => {
    mockPost.mockResolvedValueOnce({ data: { userId: USER_ID, role: 'operator' } })

    const result = await budgetMembersApi.restoreMember(BUDGET_ID, USER_ID)

    expect(mockPost).toHaveBeenCalledWith(`/api/budgets/${BUDGET_ID}/members/${USER_ID}/restore`)
    expect(result).toEqual({ userId: USER_ID, role: 'operator' })
  })
})

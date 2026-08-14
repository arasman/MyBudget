import http from '@/api/axios'
import type { MemberDto, MemberRole } from '../types'

const base = (budgetId: string) => `/api/budgets/${budgetId}/members`

interface ListMembersResponse {
  members: MemberDto[]
}

interface UpdateMemberRoleResponse {
  userId: string
  role: MemberRole
}

interface RestoreMemberResponse {
  userId: string
  role: MemberRole
}

/**
 * GET /api/budgets/:budgetId/members — budget:admin only.
 * WU1: active members only. WU2: `includeDeleted: true` also returns soft-deleted rows.
 */
export async function listMembers(
  budgetId: string,
  options?: { includeDeleted?: boolean },
): Promise<MemberDto[]> {
  const url = options?.includeDeleted ? `${base(budgetId)}?includeDeleted=true` : base(budgetId)
  const { data } = await http.get<ListMembersResponse>(url)
  return data.members
}

/** PATCH /api/budgets/:budgetId/members/:userId/role — budget:admin + MemberActionPolicy */
export async function updateMemberRole(
  budgetId: string,
  userId: string,
  role: MemberRole,
): Promise<UpdateMemberRoleResponse> {
  const { data } = await http.patch<UpdateMemberRoleResponse>(`${base(budgetId)}/${userId}/role`, {
    role,
  })
  return data
}

/** DELETE /api/budgets/:budgetId/members/:userId — budget:admin + MemberActionPolicy (soft-delete) */
export async function removeMember(budgetId: string, userId: string): Promise<void> {
  await http.delete(`${base(budgetId)}/${userId}`)
}

/** POST /api/budgets/:budgetId/members/:userId/restore — budget:admin + MemberActionPolicy */
export async function restoreMember(budgetId: string, userId: string): Promise<RestoreMemberResponse> {
  const { data } = await http.post<RestoreMemberResponse>(`${base(budgetId)}/${userId}/restore`)
  return data
}

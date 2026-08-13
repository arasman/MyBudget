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

/** GET /api/budgets/:budgetId/members — budget:admin only (WU1: active members only) */
export async function listMembers(budgetId: string): Promise<MemberDto[]> {
  const { data } = await http.get<ListMembersResponse>(base(budgetId))
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

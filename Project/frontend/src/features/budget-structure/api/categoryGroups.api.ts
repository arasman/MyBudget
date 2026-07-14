import http from '@/api/axios'
import type { CategoryGroupResponse, CreateGroupPayload, UpdateGroupPayload } from '../types'

const base = (budgetId: string) => `/api/budgets/${budgetId}/category-groups`

/** GET /api/budgets/:budgetId/category-groups */
export async function list(budgetId: string, includeDeleted = false): Promise<CategoryGroupResponse[]> {
  const { data } = await http.get<CategoryGroupResponse[]>(base(budgetId), {
    params: includeDeleted ? { includeDeleted: true } : undefined,
  })
  return data
}

/** POST /api/budgets/:budgetId/category-groups → returns the created group id */
export async function create(
  budgetId: string,
  payload: CreateGroupPayload & { displayOrder: number },
): Promise<{ id: string }> {
  const { data } = await http.post<{ id: string }>(base(budgetId), payload)
  return data
}

/** PUT /api/budgets/:budgetId/category-groups/:groupId */
export async function update(
  budgetId: string,
  groupId: string,
  payload: UpdateGroupPayload & { displayOrder: number },
): Promise<void> {
  await http.put(`${base(budgetId)}/${groupId}`, payload)
}

/** DELETE /api/budgets/:budgetId/category-groups/:groupId */
export async function remove(budgetId: string, groupId: string): Promise<void> {
  await http.delete(`${base(budgetId)}/${groupId}`)
}

/** PUT /api/budgets/:budgetId/category-groups/order  body: { orderedIds } */
export async function reorder(budgetId: string, ids: string[]): Promise<void> {
  await http.put(`${base(budgetId)}/order`, { orderedIds: ids })
}

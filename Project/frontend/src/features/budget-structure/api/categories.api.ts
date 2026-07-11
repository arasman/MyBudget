import http from '@/api/axios'
import type { CreateCategoryPayload, UpdateCategoryPayload } from '../types'

const base = (budgetId: string, groupId: string) =>
  `/api/budgets/${budgetId}/category-groups/${groupId}/categories`

/** POST /api/budgets/:budgetId/category-groups/:groupId/categories → returns created category id */
export async function create(
  budgetId: string,
  groupId: string,
  payload: CreateCategoryPayload & { displayOrder: number },
): Promise<{ id: string }> {
  const { data } = await http.post<{ id: string }>(base(budgetId, groupId), payload)
  return data
}

/** PUT /api/budgets/:budgetId/category-groups/:groupId/categories/:categoryId */
export async function update(
  budgetId: string,
  groupId: string,
  categoryId: string,
  payload: UpdateCategoryPayload & { displayOrder: number },
): Promise<void> {
  await http.put(`${base(budgetId, groupId)}/${categoryId}`, payload)
}

/** DELETE /api/budgets/:budgetId/category-groups/:groupId/categories/:categoryId */
export async function remove(
  budgetId: string,
  groupId: string,
  categoryId: string,
): Promise<void> {
  await http.delete(`${base(budgetId, groupId)}/${categoryId}`)
}

/** PUT /api/budgets/:budgetId/category-groups/:groupId/categories/order  body: { orderedIds } */
export async function reorder(
  budgetId: string,
  groupId: string,
  ids: string[],
): Promise<void> {
  await http.put(`${base(budgetId, groupId)}/order`, { orderedIds: ids })
}

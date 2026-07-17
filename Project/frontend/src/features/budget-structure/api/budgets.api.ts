import http from '@/api/axios'

/** POST /api/budgets — creates a new budget owned by the authenticated user */
export async function createBudget(name: string): Promise<{ id: string; name: string }> {
  const { data } = await http.post<{ budgetId: string; name: string }>('/api/budgets', { name })
  return { id: data.budgetId, name: data.name }
}

/** PUT /api/budgets/:id — renames an existing budget */
export async function renameBudget(
  budgetId: string,
  newName: string,
): Promise<{ id: string; name: string }> {
  const { data } = await http.put<{ id: string; name: string }>(`/api/budgets/${budgetId}`, {
    name: newName,
  })
  return data
}

/** DELETE /api/budgets/:id — soft-deletes a budget (owner only) */
export async function deleteBudget(budgetId: string): Promise<void> {
  await http.delete(`/api/budgets/${budgetId}`)
}

/** POST /api/budgets/:id/restore — restores a soft-deleted budget (owner only) */
export async function restoreBudget(budgetId: string): Promise<{ id: string; name: string }> {
  const { data } = await http.post<{ id: string; name: string }>(
    `/api/budgets/${budgetId}/restore`,
  )
  return data
}

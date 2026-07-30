import http from '@/api/axios'
import type { BankAccount, CreateBankAccountDto, UpdateBankAccountDto } from '../types/bankAccount'

const base = (budgetId: string) => `/api/budgets/${budgetId}/bank-accounts`

/** GET /api/budgets/:budgetId/bank-accounts */
export async function listBankAccounts(
  budgetId: string,
  opts?: { includeDeleted?: boolean },
): Promise<BankAccount[]> {
  const params = opts?.includeDeleted ? { includeDeleted: true } : undefined
  const { data } = await http.get<BankAccount[]>(base(budgetId), { params })
  return data
}

/** POST /api/budgets/:budgetId/bank-accounts → 201 { id } */
export async function createBankAccount(
  budgetId: string,
  payload: CreateBankAccountDto,
): Promise<{ id: string }> {
  const { data } = await http.post<{ id: string }>(base(budgetId), payload)
  return data
}

/** PUT /api/budgets/:budgetId/bank-accounts/:accountId → 200 */
export async function updateBankAccount(
  budgetId: string,
  accountId: string,
  payload: UpdateBankAccountDto,
): Promise<void> {
  await http.put(`${base(budgetId)}/${accountId}`, payload)
}

/** DELETE /api/budgets/:budgetId/bank-accounts/:accountId → 204 */
export async function deleteBankAccount(budgetId: string, accountId: string): Promise<void> {
  await http.delete(`${base(budgetId)}/${accountId}`)
}

/** POST /api/budgets/:budgetId/bank-accounts/:accountId/restore → 204 */
export async function restoreBankAccount(budgetId: string, accountId: string): Promise<void> {
  await http.post(`${base(budgetId)}/${accountId}/restore`)
}

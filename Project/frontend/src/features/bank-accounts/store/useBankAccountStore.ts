import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { BankAccount, CreateBankAccountDto, UpdateBankAccountDto } from '../types/bankAccount'
import * as api from '../api/bankAccountApi'

export const useBankAccountStore = defineStore('bankAccounts', () => {
  // ---------------------------------------------------------------------------
  // State
  // ---------------------------------------------------------------------------
  const accounts = ref<BankAccount[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const showDeletedAccounts = ref(false)

  // ---------------------------------------------------------------------------
  // Actions
  // ---------------------------------------------------------------------------

  async function fetchAccounts(budgetId: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      accounts.value = await api.listBankAccounts(budgetId, {
        includeDeleted: showDeletedAccounts.value,
      })
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load bank accounts'
    } finally {
      loading.value = false
    }
  }

  async function createAccount(budgetId: string, payload: CreateBankAccountDto): Promise<string> {
    const { id } = await api.createBankAccount(budgetId, payload)
    await fetchAccounts(budgetId)
    return id
  }

  async function updateAccount(
    budgetId: string,
    accountId: string,
    payload: UpdateBankAccountDto,
  ): Promise<void> {
    await api.updateBankAccount(budgetId, accountId, payload)
    await fetchAccounts(budgetId)
  }

  async function deleteAccount(budgetId: string, accountId: string): Promise<void> {
    await api.deleteBankAccount(budgetId, accountId)
    accounts.value = accounts.value.filter((a) => a.id !== accountId)
  }

  async function restoreAccount(budgetId: string, accountId: string): Promise<void> {
    await api.restoreBankAccount(budgetId, accountId)
    await fetchAccounts(budgetId)
  }

  // ---------------------------------------------------------------------------
  // Expose
  // ---------------------------------------------------------------------------
  return {
    accounts,
    loading,
    error,
    showDeletedAccounts,
    fetchAccounts,
    createAccount,
    updateAccount,
    deleteAccount,
    restoreAccount,
  }
})

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

// ---------------------------------------------------------------------------
// Hoist mock references
// ---------------------------------------------------------------------------
const {
  mockListBankAccounts,
  mockCreateBankAccount,
  mockUpdateBankAccount,
  mockDeleteBankAccount,
  mockRestoreBankAccount,
} = vi.hoisted(() => ({
  mockListBankAccounts: vi.fn(),
  mockCreateBankAccount: vi.fn(),
  mockUpdateBankAccount: vi.fn(),
  mockDeleteBankAccount: vi.fn(),
  mockRestoreBankAccount: vi.fn(),
}))

vi.mock('@/features/bank-accounts/api/bankAccountApi', () => ({
  listBankAccounts: mockListBankAccounts,
  createBankAccount: mockCreateBankAccount,
  updateBankAccount: mockUpdateBankAccount,
  deleteBankAccount: mockDeleteBankAccount,
  restoreBankAccount: mockRestoreBankAccount,
}))

import { useBankAccountStore } from '../store/useBankAccountStore'
import type { BankAccount } from '../types/bankAccount'

const BUDGET_ID = 'budget-1'

const makeAccount = (overrides: Partial<BankAccount> = {}): BankAccount => ({
  id: 'acc-1',
  budgetId: BUDGET_ID,
  currencyId: 'currency-1',
  alias: 'Caja GTQ',
  isPositive: true,
  displayOrder: 0,
  deletedAt: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  ...overrides,
})

describe('useBankAccountStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.resetAllMocks()
  })

  describe('fetchAccounts', () => {
    it('populates accounts state on success', async () => {
      const accounts = [makeAccount(), makeAccount({ id: 'acc-2', alias: 'Caja USD' })]
      mockListBankAccounts.mockResolvedValue(accounts)

      const store = useBankAccountStore()
      await store.fetchAccounts(BUDGET_ID)

      expect(store.accounts).toHaveLength(2)
      expect(store.accounts[0].alias).toBe('Caja GTQ')
      expect(store.loading).toBe(false)
      expect(store.error).toBeNull()
    })

    it('sets error state on failure', async () => {
      mockListBankAccounts.mockRejectedValue(new Error('Network error'))

      const store = useBankAccountStore()
      await store.fetchAccounts(BUDGET_ID)

      expect(store.accounts).toHaveLength(0)
      expect(store.error).toBe('Network error')
    })

    it('passes includeDeleted=true when showDeletedAccounts is true', async () => {
      mockListBankAccounts.mockResolvedValue([])

      const store = useBankAccountStore()
      store.showDeletedAccounts = true
      await store.fetchAccounts(BUDGET_ID)

      expect(mockListBankAccounts).toHaveBeenCalledWith(BUDGET_ID, { includeDeleted: true })
    })

    it('passes includeDeleted=false when showDeletedAccounts is false', async () => {
      mockListBankAccounts.mockResolvedValue([])

      const store = useBankAccountStore()
      store.showDeletedAccounts = false
      await store.fetchAccounts(BUDGET_ID)

      expect(mockListBankAccounts).toHaveBeenCalledWith(BUDGET_ID, { includeDeleted: false })
    })
  })

  describe('createAccount', () => {
    it('calls api and refreshes accounts list', async () => {
      const newAccount = makeAccount({ id: 'acc-new' })
      mockCreateBankAccount.mockResolvedValue({ id: 'acc-new' })
      mockListBankAccounts.mockResolvedValue([newAccount])

      const store = useBankAccountStore()
      const id = await store.createAccount(BUDGET_ID, {
        alias: 'New Account',
        currencyId: 'currency-1',
        isPositive: true,
        displayOrder: 1,
      })

      expect(id).toBe('acc-new')
      expect(mockCreateBankAccount).toHaveBeenCalledOnce()
      expect(mockListBankAccounts).toHaveBeenCalledOnce()
      expect(store.accounts).toHaveLength(1)
    })
  })

  describe('updateAccount', () => {
    it('calls api and refreshes accounts list', async () => {
      const updated = makeAccount({ alias: 'Updated Alias' })
      mockUpdateBankAccount.mockResolvedValue(undefined)
      mockListBankAccounts.mockResolvedValue([updated])

      const store = useBankAccountStore()
      await store.updateAccount(BUDGET_ID, 'acc-1', {
        alias: 'Updated Alias',
        isPositive: true,
        displayOrder: 0,
      })

      expect(mockUpdateBankAccount).toHaveBeenCalledOnce()
      expect(store.accounts[0].alias).toBe('Updated Alias')
    })
  })

  describe('deleteAccount', () => {
    it('calls api and removes account from local state', async () => {
      const accounts = [makeAccount(), makeAccount({ id: 'acc-2', alias: 'Other' })]
      mockListBankAccounts.mockResolvedValue(accounts)
      await (async () => {
        const store = useBankAccountStore()
        mockDeleteBankAccount.mockResolvedValue(undefined)
        store.accounts = accounts

        await store.deleteAccount(BUDGET_ID, 'acc-1')

        expect(mockDeleteBankAccount).toHaveBeenCalledWith(BUDGET_ID, 'acc-1')
        expect(store.accounts).toHaveLength(1)
        expect(store.accounts[0].id).toBe('acc-2')
      })()
    })
  })

  describe('restoreAccount', () => {
    it('calls restoreBankAccount api and refreshes accounts list', async () => {
      const restoredAccount = makeAccount({ id: 'acc-del', deletedAt: null })
      mockRestoreBankAccount.mockResolvedValue(undefined)
      mockListBankAccounts.mockResolvedValue([restoredAccount])

      const store = useBankAccountStore()
      await store.restoreAccount(BUDGET_ID, 'acc-del')

      expect(mockRestoreBankAccount).toHaveBeenCalledWith(BUDGET_ID, 'acc-del')
      expect(mockListBankAccounts).toHaveBeenCalledOnce()
    })
  })

  describe('showDeletedAccounts', () => {
    it('defaults to false', () => {
      const store = useBankAccountStore()
      expect(store.showDeletedAccounts).toBe(false)
    })
  })
})

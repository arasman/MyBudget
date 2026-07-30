import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import BankAccountListView from '../BankAccountListView.vue'
import type { BankAccount } from '../../types/bankAccount'

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

vi.mock('../../store/useBankAccountStore', () => ({
  useBankAccountStore: vi.fn(),
}))

vi.mock('@/stores/toast.store', () => ({
  useToastStore: vi.fn(),
}))

vi.mock('@/features/budget-structure/api/currencies.api', () => ({
  listCurrencies: vi.fn().mockResolvedValue([]),
}))

vi.mock('@/features/budget-structure/components/BudgetTabs.vue', () => ({
  default: { template: '<div data-testid="budget-tabs" />' },
}))

vi.mock('../../components/BankAccountForm.vue', () => ({
  default: {
    props: ['initialValues', 'currencies', 'isEdit'],
    emits: ['submit', 'cancel'],
    template: '<div data-testid="bank-account-form" />',
  },
}))

import { useBankAccountStore } from '../../store/useBankAccountStore'
import { useToastStore } from '@/stores/toast.store'

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const BUDGET_ID = 'budget-1'

function makeAccount(overrides: Partial<BankAccount> = {}): BankAccount {
  return {
    id: 'acc-1',
    budgetId: BUDGET_ID,
    currencyId: 'cur-1',
    alias: 'Caja GTQ',
    isPositive: true,
    displayOrder: 1,
    deletedAt: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function makeDeletedAccount(overrides: Partial<BankAccount> = {}): BankAccount {
  return makeAccount({
    id: 'acc-del',
    alias: 'Old Account',
    deletedAt: '2026-07-01T00:00:00Z',
    ...overrides,
  })
}

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/budgets/:budgetId/bank-accounts',
        name: 'BankAccounts',
        component: BankAccountListView,
      },
    ],
  })
}

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        bankAccount: {
          title: 'Bank Accounts',
          create: 'New Account',
          createTitle: 'Create Bank Account',
          editTitle: 'Edit Bank Account',
          edit: 'Edit',
          delete: 'Delete',
          deleteTitle: 'Delete Bank Account',
          deleteConfirm: 'Delete {alias}?',
          positive: 'Adds',
          negative: 'Subtracts',
          empty: 'No bank accounts yet.',
          showDeleted: 'Show deleted',
          deleted: 'deleted',
          restore: 'Restore',
          createSuccess: 'Bank account created',
          updateSuccess: 'Bank account updated',
          deleteSuccess: 'Bank account deleted',
          restoreSuccess: 'Bank account restored',
          columns: {
            alias: 'Alias',
            currency: 'Currency',
            type: 'Type',
            order: 'Order',
            actions: 'Actions',
          },
          errors: {
            saveFailed: 'Failed to save',
            deleteFailed: 'Failed to delete',
          },
        },
        common: {
          cancel: 'Cancel',
        },
      },
    },
  })
}

function setupStoreMock({
  accounts = [] as BankAccount[],
  loading = false,
  showDeletedAccounts = false,
} = {}) {
  const fetchAccounts = vi.fn().mockResolvedValue(undefined)
  const restoreAccount = vi.fn().mockResolvedValue(undefined)
  const createAccount = vi.fn().mockResolvedValue('new-id')
  const updateAccount = vi.fn().mockResolvedValue(undefined)
  const deleteAccount = vi.fn().mockResolvedValue(undefined)

  const storeMock = {
    accounts,
    loading,
    error: null,
    showDeletedAccounts,
    fetchAccounts,
    restoreAccount,
    createAccount,
    updateAccount,
    deleteAccount,
  }

  vi.mocked(useBankAccountStore).mockReturnValue(
    storeMock as unknown as ReturnType<typeof useBankAccountStore>,
  )

  return storeMock
}

function setupToastMock() {
  const push = vi.fn()
  vi.mocked(useToastStore).mockReturnValue({ push } as unknown as ReturnType<typeof useToastStore>)
  return { push }
}

async function renderView() {
  const router = makeRouter()
  await router.push(`/budgets/${BUDGET_ID}/bank-accounts`)
  await router.isReady()

  return render(BankAccountListView, {
    global: { plugins: [router, makeI18n()] },
  })
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('BankAccountListView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  describe('Show deleted toggle', () => {
    it('renders the show-deleted checkbox', async () => {
      setupStoreMock()
      setupToastMock()
      await renderView()
      expect(screen.getByLabelText('Show deleted')).toBeTruthy()
    })
  })

  describe('Active account rows', () => {
    it('does not show restore button on active rows', async () => {
      setupStoreMock({ accounts: [makeAccount()] })
      setupToastMock()
      await renderView()
      expect(screen.queryByText('Restore')).toBeNull()
    })
  })

  describe('Deleted account rows', () => {
    it('shows restore button on deleted rows', async () => {
      setupStoreMock({ accounts: [makeDeletedAccount()] })
      setupToastMock()
      await renderView()
      expect(screen.getByText('Restore')).toBeTruthy()
    })

    it('shows deleted badge on deleted rows', async () => {
      setupStoreMock({ accounts: [makeDeletedAccount()] })
      setupToastMock()
      await renderView()
      expect(screen.getByText('deleted')).toBeTruthy()
    })

    it('does not show edit/delete buttons on deleted rows', async () => {
      setupStoreMock({ accounts: [makeDeletedAccount()] })
      setupToastMock()
      await renderView()
      // Edit (Pencil) and Trash2 icon buttons should not be rendered for deleted rows
      // The view renders text-less icon buttons — we check that the Restore button IS there
      // and the delete confirm dialog trigger is NOT reachable via the row buttons
      expect(screen.queryByTitle('Edit')).toBeNull()
      expect(screen.queryByTitle('Delete')).toBeNull()
    })
  })

  describe('Mixed accounts', () => {
    it('shows restore only on deleted rows, not on active rows', async () => {
      setupStoreMock({
        accounts: [makeAccount({ id: 'active' }), makeDeletedAccount({ id: 'deleted' })],
      })
      setupToastMock()
      await renderView()

      // One restore button for the deleted account
      expect(screen.getAllByText('Restore')).toHaveLength(1)
    })
  })

  describe('Restore action', () => {
    it('calls store.restoreAccount when restore button clicked', async () => {
      const store = setupStoreMock({ accounts: [makeDeletedAccount()] })
      setupToastMock()
      await renderView()

      const restoreBtn = screen.getByText('Restore')
      await fireEvent.click(restoreBtn)

      expect(store.restoreAccount).toHaveBeenCalledWith(BUDGET_ID, 'acc-del')
    })

    it('shows success toast after restore', async () => {
      setupStoreMock({ accounts: [makeDeletedAccount()] })
      const { push } = setupToastMock()
      await renderView()

      const restoreBtn = screen.getByText('Restore')
      await fireEvent.click(restoreBtn)

      expect(push).toHaveBeenCalledWith(
        expect.objectContaining({ type: 'success', title: 'Bank account restored' }),
      )
    })
  })
})

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/vue'
import { createI18n } from 'vue-i18n'
import { createRouter, createMemoryHistory } from 'vue-router'
import BudgetSelectionView from '../BudgetSelectionView.vue'

// Hoist mocks for stable references inside vi.mock factories
const { mockFetchMe, mockSetActiveBudget, mockDeleteBudget, mockRestoreBudget } = vi.hoisted(
  () => ({
    mockFetchMe: vi.fn().mockResolvedValue(undefined),
    mockSetActiveBudget: vi.fn(),
    mockDeleteBudget: vi.fn().mockResolvedValue(undefined),
    mockRestoreBudget: vi.fn().mockResolvedValue({ id: 'b-deleted', name: 'Old Budget' }),
  }),
)

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: vi.fn(),
}))

vi.mock('@/stores/layout.store', () => ({
  useLayoutStore: vi.fn(),
}))

vi.mock('../../api/budgets.api', () => ({
  createBudget: vi.fn(),
  deleteBudget: mockDeleteBudget,
  restoreBudget: mockRestoreBudget,
}))

// Stub CreateBudgetModal so it doesn't interfere
vi.mock('../../components/CreateBudgetModal.vue', () => ({
  default: {
    name: 'CreateBudgetModal',
    template: '<div data-testid="create-modal-stub" />',
    expose: ['open'],
    setup() {
      return { open: vi.fn() }
    },
  },
}))

import { useAuthStore } from '@/stores/auth.store'
import { useLayoutStore } from '@/stores/layout.store'

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        common: { cancel: 'Cancel', error: 'An error occurred' },
        budgetStructure: {
          selection: {
            title: 'My Budgets',
            noBudgets: 'You are not a member of any budget yet.',
            createBudget: 'New Budget',
            createBudgetTitle: 'Create Budget',
            budgetNameLabel: 'Budget name',
            budgetNamePlaceholder: 'Enter budget name',
            budgetNameRequired: 'Budget name is required',
            budgetNameTooLong: 'Budget name must be 200 characters or fewer',
            showDeleted: 'Show deleted',
            deletedBadge: 'Deleted',
            restoreBudget: 'Restore',
            deleteBudget: 'Delete',
            confirmDelete: 'Are you sure you want to delete this budget?',
          },
        },
      },
    },
  })
}

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'BudgetSelection', component: { template: '<div />' } },
      {
        path: '/budgets/:budgetId/cycles',
        name: 'CycleList',
        component: { template: '<div />' },
      },
    ],
  })
}

function setupStores(
  memberships: Array<{
    budgetId: string
    budgetName: string
    role: string
    isDeleted: boolean
  }>,
) {
  vi.mocked(useAuthStore).mockReturnValue({
    user: {
      id: 'user-1',
      email: 'test@example.com',
      firstName: 'Test',
      lastName: 'User',
      preferredLocale: 'en',
      memberships,
    },
    fetchMe: mockFetchMe,
  } as unknown as ReturnType<typeof useAuthStore>)

  vi.mocked(useLayoutStore).mockReturnValue({
    activeBudgetId: null,
    activeBudgetName: null,
    setActiveBudget: mockSetActiveBudget,
    clearActiveBudget: vi.fn(),
  } as unknown as ReturnType<typeof useLayoutStore>)
}

function renderView(
  memberships: Array<{
    budgetId: string
    budgetName: string
    role: string
    isDeleted: boolean
  }>,
) {
  setupStores(memberships)
  return render(BudgetSelectionView, {
    global: {
      plugins: [makeI18n(), makeRouter()],
    },
  })
}

describe('BudgetSelectionView — show/hide deleted toggle', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('hides deleted memberships by default', async () => {
    renderView([
      { budgetId: 'b-1', budgetName: 'Active Budget', role: 'owner', isDeleted: false },
      { budgetId: 'b-2', budgetName: 'Deleted Budget', role: 'owner', isDeleted: true },
    ])

    await waitFor(() => {
      expect(screen.getByText('Active Budget')).toBeTruthy()
      expect(screen.queryByText('Deleted Budget')).toBeNull()
    })
  })

  it('reveals deleted memberships when toggle is checked', async () => {
    renderView([
      { budgetId: 'b-1', budgetName: 'Active Budget', role: 'owner', isDeleted: false },
      { budgetId: 'b-2', budgetName: 'Deleted Budget', role: 'owner', isDeleted: true },
    ])

    const toggle = screen.getByLabelText('Show deleted')
    await fireEvent.click(toggle)

    await waitFor(() => {
      expect(screen.getByText('Deleted Budget')).toBeTruthy()
      expect(screen.getByText('Deleted')).toBeTruthy()
    })
  })

  it('shows restore button only on deleted budgets when toggle is on', async () => {
    renderView([
      { budgetId: 'b-1', budgetName: 'Active Budget', role: 'owner', isDeleted: false },
      { budgetId: 'b-2', budgetName: 'Deleted Budget', role: 'owner', isDeleted: true },
    ])

    const toggle = screen.getByLabelText('Show deleted')
    await fireEvent.click(toggle)

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Restore' })).toBeTruthy()
    })

    // Restore button should not appear next to the active budget
    const allRestoreButtons = screen.queryAllByRole('button', { name: 'Restore' })
    expect(allRestoreButtons).toHaveLength(1)
  })

  it('shows delete button only for owner role on active budgets', async () => {
    renderView([
      { budgetId: 'b-1', budgetName: 'My Budget', role: 'owner', isDeleted: false },
      { budgetId: 'b-2', budgetName: 'Shared Budget', role: 'admin', isDeleted: false },
    ])

    await waitFor(() => {
      expect(screen.getByText('My Budget')).toBeTruthy()
    })

    const deleteButtons = screen.queryAllByRole('button', { name: 'Delete' })
    // Only one delete button: the owner row
    expect(deleteButtons).toHaveLength(1)
  })
})

describe('BudgetSelectionView — auto-redirect logic', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('does not auto-redirect when sole membership is deleted', async () => {
    renderView([
      { budgetId: 'b-deleted', budgetName: 'Old Budget', role: 'owner', isDeleted: true },
    ])

    await waitFor(() => {
      // View renders without navigating — no auto-redirect triggered
      expect(mockSetActiveBudget).not.toHaveBeenCalled()
    })
  })

  it('auto-redirects when there is exactly one active membership', async () => {
    renderView([
      { budgetId: 'b-1', budgetName: 'My Budget', role: 'owner', isDeleted: false },
    ])

    await waitFor(() => {
      expect(mockSetActiveBudget).toHaveBeenCalledWith('b-1', 'My Budget')
    })
  })
})

describe('BudgetSelectionView — restore action', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('calls restoreBudget and fetchMe after restore', async () => {
    renderView([
      { budgetId: 'b-deleted', budgetName: 'Old Budget', role: 'owner', isDeleted: true },
    ])

    const toggle = screen.getByLabelText('Show deleted')
    await fireEvent.click(toggle)

    await waitFor(() => expect(screen.getByRole('button', { name: 'Restore' })).toBeTruthy())
    await fireEvent.click(screen.getByRole('button', { name: 'Restore' }))

    await waitFor(() => {
      expect(mockRestoreBudget).toHaveBeenCalledWith('b-deleted')
      expect(mockFetchMe).toHaveBeenCalled()
    })
  })
})

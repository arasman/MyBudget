import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/vue'
import { createI18n } from 'vue-i18n'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createPinia, setActivePinia } from 'pinia'
import BudgetSelectionView from '../BudgetSelectionView.vue'

// Hoist mocks for stable references inside vi.mock factories
const { mockFetchMe, mockSetActiveBudget, mockDeleteBudget, mockRestoreBudget, mockToastPush } =
  vi.hoisted(() => ({
    mockFetchMe: vi.fn().mockResolvedValue(undefined),
    mockSetActiveBudget: vi.fn(),
    mockDeleteBudget: vi.fn().mockResolvedValue(undefined),
    mockRestoreBudget: vi.fn().mockResolvedValue({ id: 'b-deleted', name: 'Old Budget' }),
    mockToastPush: vi.fn(),
  }))

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: vi.fn(),
}))

vi.mock('@/stores/layout.store', () => ({
  useLayoutStore: vi.fn(),
}))

vi.mock('@/stores/toast.store', () => ({
  useToastStore: () => ({ push: mockToastPush }),
}))

const { mockRenameBudget } = vi.hoisted(() => ({
  mockRenameBudget: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('../../api/budgets.api', () => ({
  createBudget: vi.fn(),
  deleteBudget: mockDeleteBudget,
  restoreBudget: mockRestoreBudget,
  renameBudget: mockRenameBudget,
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
            deleteSuccess: 'Budget deleted successfully',
            restoreSuccess: 'Budget restored successfully',
            createSuccess: 'Budget created successfully',
            renameSuccess: 'Budget renamed successfully',
            renameBudget: 'Rename budget',
            viewCycles: 'View cycles',
            confirmDeleteTitle: 'Delete Budget',
          },
          common: {
            save: 'Save',
            cancel: 'Cancel',
            confirm: 'Confirm',
            actions: 'Actions',
            restore: 'Restore',
            deleted: 'Deleted',
            noPermission: 'No permission',
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
  const pinia = createPinia()
  setActivePinia(pinia)
  return render(BudgetSelectionView, {
    global: {
      plugins: [pinia, makeI18n(), makeRouter()],
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

// REQ-TOAST-BUDGET-CREATE, REQ-TOAST-BUDGET-RENAME
describe('BudgetSelectionView — toast on create and rename', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('onBudgetCreated calls toast.push with createSuccess key', async () => {
    const { getByTestId } = renderView([])

    // Simulate the CreateBudgetModal emitting 'created'
    const stub = getByTestId('create-modal-stub')
    await waitFor(() => expect(stub).toBeTruthy())

    // The stub declares no props, so @created="onBudgetCreated" lands in attrs (not props)
    const instance = (stub as unknown as { __vueParentComponent?: { attrs?: Record<string, unknown>; props?: Record<string, unknown> } }).__vueParentComponent
    const onCreated = (instance?.attrs?.['onCreated'] ?? instance?.props?.['onCreated']) as ((arg: unknown) => void) | undefined
    if (onCreated) {
      await onCreated({ id: 'new-budget', name: 'My New Budget' })
    }

    await waitFor(() => {
      expect(mockToastPush).toHaveBeenCalledWith({
        type: 'success',
        title: 'Budget created successfully',
      })
    })
  })

  it('saveInlineEdit calls toast.push with renameSuccess key on success', async () => {
    renderView([
      { budgetId: 'b-1', budgetName: 'My Budget', role: 'owner', isDeleted: false },
    ])

    await waitFor(() => expect(screen.getByText('My Budget')).toBeTruthy())

    // Start inline edit via double-click on the budget name span
    const budgetName = screen.getByText('My Budget')
    await fireEvent.dblClick(budgetName)

    await waitFor(() => {
      const input = screen.getByRole<HTMLInputElement>('textbox')
      expect(input).toBeTruthy()
    })

    const input = screen.getByRole<HTMLInputElement>('textbox')
    await fireEvent.input(input, { target: { value: 'Renamed Budget' } })
    await fireEvent.keyUp(input, { key: 'Enter' })

    await waitFor(() => {
      expect(mockRenameBudget).toHaveBeenCalledWith('b-1', 'Renamed Budget')
      expect(mockToastPush).toHaveBeenCalledWith({
        type: 'success',
        title: 'Budget renamed successfully',
      })
    })
  })

  it('saveInlineEdit does NOT call toast.push when renameBudget rejects', async () => {
    // Vitest skips reporting an unhandled rejection when listeners.length > 1.
    // Add a noop listener to suppress it; keep it alive until after the rejection fires.
    const noop = () => {}
    process.on('unhandledRejection', noop)

    mockRenameBudget.mockRejectedValueOnce(new Error('Network error'))

    renderView([
      { budgetId: 'b-1', budgetName: 'My Budget', role: 'owner', isDeleted: false },
    ])

    await waitFor(() => expect(screen.getByText('My Budget')).toBeTruthy())

    const budgetName = screen.getByText('My Budget')
    await fireEvent.dblClick(budgetName)

    await waitFor(() => {
      const input = screen.getByRole<HTMLInputElement>('textbox')
      expect(input).toBeTruthy()
    })

    const input = screen.getByRole<HTMLInputElement>('textbox')
    await fireEvent.input(input, { target: { value: 'Renamed Budget' } })
    await fireEvent.keyUp(input, { key: 'Enter' })

    await waitFor(() => {
      expect(mockRenameBudget).toHaveBeenCalled()
    })
    // Flush microtask queue so the rejection fires before removing the guard
    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(mockToastPush).not.toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Budget renamed successfully' }),
    )

    process.off('unhandledRejection', noop)
  })
})

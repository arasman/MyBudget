// REQ-TOAST-MATRIX-GROUP-CREATE, REQ-TOAST-MATRIX-CAT-CREATE, REQ-TOAST-MATRIX-LINE-CREATE
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({ t: (k: string) => k }),
}))

vi.mock('vue-router', () => ({
  useRoute: () => ({ params: { budgetId: 'budget-1', cycleId: 'cycle-1' } }),
  useRouter: () => ({ push: vi.fn() }),
  RouterLink: { template: '<a><slot /></a>' },
}))

vi.mock('sortablejs', () => ({
  default: { create: vi.fn(() => ({ destroy: vi.fn() })) },
}))

const { mockToastPush, mockMatrixStore, mockStructureStore } = vi.hoisted(() => ({
  mockToastPush: vi.fn(),
  mockMatrixStore: {
    loading: false,
    error: null,
    showDeleted: false,
    collapsedGroupIds: new Set<string>(),
    collapsedCategoryIds: new Set<string>(),
    allPeriods: [{ id: 'period-1' }],
    loadingPeriods: {} as Record<string, boolean>,
    periodTotals: {} as Record<string, unknown>,
    invalidateAllPeriods: vi.fn().mockResolvedValue(undefined),
    initMatrix: vi.fn().mockResolvedValue(undefined),
    toggleGroupCollapse: vi.fn(),
    toggleCategoryCollapse: vi.fn(),
    openExecutionModal: vi.fn(),
  },
  mockStructureStore: {
    loading: false,
    error: null,
    categoryGroups: [] as unknown[],
    budgetLines: [] as unknown[],
    currentCycle: null,
    loadCycleDetail: vi.fn().mockResolvedValue(undefined),
    loadGroups: vi.fn().mockResolvedValue(undefined),
    loadLines: vi.fn().mockResolvedValue(undefined),
    createGroup: vi.fn().mockResolvedValue(undefined),
    createCategory: vi.fn().mockResolvedValue(undefined),
    createLine: vi.fn().mockResolvedValue(undefined),
    reorderGroups: vi.fn().mockResolvedValue(undefined),
    reorderCategories: vi.fn().mockResolvedValue(undefined),
  },
}))

vi.mock('@/stores/toast.store', () => ({
  useToastStore: () => ({ push: mockToastPush }),
}))

vi.mock('../store', () => ({
  useBudgetMatrixStore: () => mockMatrixStore,
}))

vi.mock('@/features/budget-structure/store', () => ({
  useBudgetStructureStore: () => mockStructureStore,
}))

vi.mock('../composables/useMatrixNavigation', () => ({
  useMatrixNavigation: () => ({
    visiblePeriods: [],
    canGoPrev: false,
    canGoNext: false,
    goPrev: vi.fn(),
    goNext: vi.fn(),
  }),
}))

// Stub child components to isolate BudgetMatrixView logic
vi.mock('@/features/budget-structure/components/BudgetTabs.vue', () => ({
  default: { template: '<div />' },
}))
vi.mock('../components/MatrixControls.vue', () => ({
  default: { template: '<div />' },
}))
vi.mock('../components/MatrixPeriodHeader.vue', () => ({
  default: { template: '<thead />', props: ['periods'] },
}))
vi.mock('../components/MatrixGroupRow.vue', () => ({
  default: { template: '<tr />', props: ['group', 'budgetId', 'visiblePeriods', 'collapsed', 'isFirst', 'isLast'] },
}))
vi.mock('../components/MatrixCategoryRow.vue', () => ({
  default: { template: '<tr />', props: ['category', 'groupId', 'budgetId', 'visiblePeriods', 'collapsed', 'categoryCollapsed', 'isFirst', 'isLast', 'parentDeleted'] },
}))
vi.mock('../components/MatrixLineRow.vue', () => ({
  default: { template: '<tr />', props: ['line', 'budgetId', 'categoryCollapsed', 'visiblePeriods', 'isFirst', 'isLast', 'parentDeleted'] },
}))
vi.mock('../components/MatrixSummaryRow.vue', () => ({
  default: { template: '<tr />', props: ['lineType', 'label', 'visiblePeriods'] },
}))
vi.mock('../components/MatrixTotalRow.vue', () => ({
  default: { template: '<tr />', props: ['label', 'visiblePeriods'] },
}))
vi.mock('../components/ExecutionListModal.vue', () => ({
  default: { template: '<div />', props: ['budgetId'] },
}))
vi.mock('@/features/budget-structure/api/budgetLines.api', () => ({}))

import { render, screen, fireEvent, waitFor } from '@testing-library/vue'
import BudgetMatrixView from '../views/BudgetMatrixView.vue'

function renderView() {
  return render(BudgetMatrixView, {
    global: {
      plugins: [createPinia()],
    },
  })
}

describe('BudgetMatrixView — add group/category/line toasts', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockMatrixStore.loading = false
    mockMatrixStore.error = null
    mockStructureStore.loading = false
    mockStructureStore.error = null
    // Provide one group so the matrix table renders (add-group-btn lives in the table)
    mockStructureStore.categoryGroups = [
      { id: 'group-1', name: 'Group 1', categories: [], deletedAt: null },
    ] as unknown[]
  })

  it('confirmAddGroup fires toast.push with createGroupSuccess on success', async () => {
    renderView()

    // Click "Add group" button
    await waitFor(() => expect(screen.getByTestId('add-group-btn')).toBeTruthy())
    await fireEvent.click(screen.getByTestId('add-group-btn'))

    await waitFor(() => expect(screen.getByTestId('add-group-row')).toBeTruthy())

    const input = screen.getByPlaceholderText('budgetMatrix.rows.newGroupName')
    await fireEvent.input(input, { target: { value: 'New Group' } })
    await fireEvent.keyDown(input, { key: 'Enter' })

    await waitFor(() => {
      expect(mockStructureStore.createGroup).toHaveBeenCalledWith('budget-1', { name: 'New Group' })
      expect(mockToastPush).toHaveBeenCalledWith({
        type: 'success',
        title: 'budgetMatrix.rows.createGroupSuccess',
      })
    })
  })

  it('confirmAddGroup does NOT fire toast.push when createGroup rejects', async () => {
    // Vitest skips reporting an unhandled rejection when listeners.length > 1.
    // Add a noop listener to suppress it; keep it alive until after the rejection fires.
    const noop = () => {}
    process.on('unhandledRejection', noop)

    mockStructureStore.createGroup.mockRejectedValueOnce(new Error('API error'))

    renderView()

    await waitFor(() => expect(screen.getByTestId('add-group-btn')).toBeTruthy())
    await fireEvent.click(screen.getByTestId('add-group-btn'))

    await waitFor(() => expect(screen.getByTestId('add-group-row')).toBeTruthy())

    const input = screen.getByPlaceholderText('budgetMatrix.rows.newGroupName')
    await fireEvent.input(input, { target: { value: 'Bad Group' } })
    await fireEvent.keyDown(input, { key: 'Enter' })

    await waitFor(() => {
      expect(mockStructureStore.createGroup).toHaveBeenCalled()
    })
    // Flush the microtask queue so the unhandled rejection fires before we remove the guard
    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(mockToastPush).not.toHaveBeenCalledWith(
      expect.objectContaining({ title: 'budgetMatrix.rows.createGroupSuccess' }),
    )

    process.off('unhandledRejection', noop)
  })
})

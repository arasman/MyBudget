import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import { computed } from 'vue'
import CategoryTreeView from '../CategoryTreeView.vue'
import type { CategoryGroupResponse } from '../../types'

// --- Mocks ---

vi.mock('../../store', () => ({
  useBudgetStructureStore: vi.fn(),
}))

vi.mock('../../composables/useRoleGate', () => ({
  useRoleGate: vi.fn(),
}))

vi.mock('@/stores/layout.store', () => ({
  useLayoutStore: vi.fn(),
}))

vi.mock('../../components/BudgetTabs.vue', () => ({
  default: { template: '<div data-testid="budget-tabs" />' },
}))

vi.mock('../../components/CategoryGroupForm.vue', () => ({
  default: { template: '<div data-testid="group-form" />' },
}))

vi.mock('../../components/CategoryForm.vue', () => ({
  default: { template: '<div data-testid="category-form" />' },
}))

vi.mock('../../components/EmptyState.vue', () => ({
  default: {
    props: ['title', 'description', 'actionLabel', 'action'],
    template: '<div data-testid="empty-state">{{ title }}</div>',
  },
}))

// Stub VueDraggable — render default slot (component uses v-for inside default slot)
vi.mock('vue-draggable-plus', () => ({
  VueDraggable: {
    props: ['modelValue', 'handle', 'animation'],
    emits: ['update:modelValue', 'end'],
    template: '<div data-testid="vue-draggable"><slot /></div>',
  },
}))

import { useBudgetStructureStore } from '../../store'
import { useRoleGate } from '../../composables/useRoleGate'
import { useLayoutStore } from '@/stores/layout.store'

const BUDGET_ID = 'budget-1'

const mockGroups: CategoryGroupResponse[] = [
  {
    id: 'g1',
    name: 'Group Alpha',
    displayOrder: 1,
    categories: [
      { id: 'cat1', name: 'Category A', displayOrder: 1 },
      { id: 'cat2', name: 'Category B', displayOrder: 2 },
    ],
  },
  {
    id: 'g2',
    name: 'Group Beta',
    displayOrder: 2,
    categories: [
      { id: 'cat3', name: 'Category C', displayOrder: 1 },
      { id: 'cat4', name: 'Category D', displayOrder: 2 },
    ],
  },
]

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/budgets/:budgetId/categories', name: 'CategoryTree', component: CategoryTreeView },
    ],
  })
}

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        budgetStructure: {
          categoryGroups: {
            title: 'Category Groups',
            create: 'New Group',
            edit: 'Edit Group',
            delete: 'Delete Group',
            confirmDelete: 'Are you sure?',
            name: 'Name',
            reorder: 'Drag to reorder',
            empty: { title: 'No groups yet', description: 'Create a group.', action: 'New Group' },
          },
          categories: {
            create: 'New Category',
            edit: 'Edit Category',
            delete: 'Delete Category',
            confirmDelete: 'Are you sure?',
            name: 'Name',
            reorder: 'Drag to reorder',
          },
          common: { save: 'Save', cancel: 'Cancel', confirm: 'Confirm', actions: 'Actions', noPermission: 'No permission' },
        },
      },
    },
  })
}

function setupMocks({
  groups = [] as CategoryGroupResponse[],
  loading = false,
  isAdmin = false,
} = {}) {
  const layoutStoreMock = {
    setPageActions: vi.fn(),
    clearPageActions: vi.fn(),
    pageActions: [],
    activeBudgetId: null,
    activeBudgetName: null,
  }

  vi.mocked(useBudgetStructureStore).mockReturnValue({
    categoryGroups: groups,
    loading,
    loadGroups: vi.fn().mockResolvedValue(undefined),
    createGroup: vi.fn().mockResolvedValue(undefined),
    updateGroup: vi.fn().mockResolvedValue(undefined),
    deleteGroup: vi.fn().mockResolvedValue(undefined),
    reorderGroups: vi.fn().mockResolvedValue(undefined),
    createCategory: vi.fn().mockResolvedValue(undefined),
    updateCategory: vi.fn().mockResolvedValue(undefined),
    deleteCategory: vi.fn().mockResolvedValue(undefined),
    reorderCategories: vi.fn().mockResolvedValue(undefined),
  } as unknown as ReturnType<typeof useBudgetStructureStore>)

  vi.mocked(useRoleGate).mockReturnValue({
    isAdmin: computed(() => isAdmin),
    isOperator: computed(() => isAdmin),
    canWriteStructure: computed(() => isAdmin),
    canWriteLines: computed(() => isAdmin),
  })

  vi.mocked(useLayoutStore).mockReturnValue(layoutStoreMock as unknown as ReturnType<typeof useLayoutStore>)

  return { layoutStoreMock }
}

describe('CategoryTreeView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  async function renderView() {
    const router = makeRouter()
    await router.push(`/budgets/${BUDGET_ID}/categories`)
    await router.isReady()

    return render(CategoryTreeView, {
      global: {
        plugins: [router, makeI18n()],
      },
    })
  }

  describe('when groups are empty', () => {
    it('shows EmptyState', async () => {
      setupMocks({ groups: [], loading: false })
      await renderView()
      expect(screen.getByTestId('empty-state')).toBeTruthy()
    })
  })

  describe('when groups are populated', () => {
    it('renders group names', async () => {
      setupMocks({ groups: mockGroups, isAdmin: true })
      await renderView()
      expect(screen.getByText('Group Alpha')).toBeTruthy()
      expect(screen.getByText('Group Beta')).toBeTruthy()
    })

    it('renders category names', async () => {
      setupMocks({ groups: mockGroups, isAdmin: true })
      await renderView()
      expect(screen.getByText('Category A')).toBeTruthy()
      expect(screen.getByText('Category C')).toBeTruthy()
    })
  })

  describe('role gating — page actions', () => {
    it('registers "New Group" page action when user is admin', async () => {
      const { layoutStoreMock } = setupMocks({ groups: [], isAdmin: true })
      await renderView()
      expect(layoutStoreMock.setPageActions).toHaveBeenCalled()
      const actions = layoutStoreMock.setPageActions.mock.calls[0]![0] as Array<{ key: string }>
      expect(actions.some((a) => a.key === 'new-group')).toBe(true)
    })

    it('does not register page action when user is not admin', async () => {
      const { layoutStoreMock } = setupMocks({ groups: [], isAdmin: false })
      await renderView()
      expect(layoutStoreMock.setPageActions).not.toHaveBeenCalled()
    })
  })
})

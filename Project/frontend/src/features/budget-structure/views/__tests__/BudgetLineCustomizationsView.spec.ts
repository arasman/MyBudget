// REQ-BLR-05: BudgetLineCustomizationsView — route resolution + component behaviour
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/vue'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import { nextTick } from 'vue'
import BudgetLineCustomizationsView from '../BudgetLineCustomizationsView.vue'

vi.mock('../../store', () => ({
  useBudgetStructureStore: vi.fn(),
}))

vi.mock('@/stores/toast.store', () => ({
  useToastStore: vi.fn(),
}))

import { useBudgetStructureStore } from '../../store'
import { useToastStore } from '@/stores/toast.store'
import type { BudgetLineRevisionResponse } from '../../types'

const BUDGET_ID = 'budget-1'
const LINE_ID = 'line-1'

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        budgetStructure: {
          budgetLines: {
            title: 'Budget Lines',
            customizations: {
              title: 'Customizations',
              backToLines: 'Back to Budget Lines',
              revisions: 'Revisions',
              noRevisions: 'No revisions yet.',
              validFrom: 'Valid From',
              validTo: 'Valid To',
              amount: 'Amount',
              currency: 'Currency',
              deleteRevision: 'Delete',
              confirmDeleteRevision: 'Delete this revision?',
            },
          },
          cycles: { title: 'Cycles' },
          common: { actions: 'Actions', cancel: 'Cancel', confirm: 'Confirm' },
        },
      },
    },
  })
}

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/budgets/:budgetId/lines',
        name: 'BudgetLines',
        component: { template: '<div />' },
      },
      {
        path: '/budgets/:budgetId/lines/:lineId/customizations',
        name: 'BudgetLineCustomizations',
        component: BudgetLineCustomizationsView,
      },
    ],
  })
}

function setupMocks(revisions: BudgetLineRevisionResponse[] = []) {
  vi.mocked(useBudgetStructureStore).mockReturnValue({
    revisions,
    budgetLines: [],
    loading: false,
    error: null,
    fetchRevisions: vi.fn().mockResolvedValue(undefined),
    createRevision: vi.fn().mockResolvedValue(undefined),
    deleteRevision: vi.fn().mockResolvedValue(undefined),
  } as unknown as ReturnType<typeof useBudgetStructureStore>)

  vi.mocked(useToastStore).mockReturnValue({
    push: vi.fn(),
  } as unknown as ReturnType<typeof useToastStore>)
}

async function renderView(revisions: BudgetLineRevisionResponse[] = []) {
  setupMocks(revisions)
  const router = makeRouter()
  await router.push(`/budgets/${BUDGET_ID}/lines/${LINE_ID}/customizations`)
  await router.isReady()
  const result = render(BudgetLineCustomizationsView, {
    global: { plugins: [router, makeI18n()] },
  })
  await nextTick()
  await nextTick()
  return result
}

describe('Route: lines/:lineId/customizations (REQ-BLR-05)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('resolves the BudgetLineCustomizations route with lineId param', async () => {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        {
          path: '/budgets/:budgetId/lines/:lineId/customizations',
          name: 'BudgetLineCustomizations',
          component: { template: '<div />' },
        },
      ],
    })
    await router.push(`/budgets/${BUDGET_ID}/lines/${LINE_ID}/customizations`)
    await router.isReady()
    expect(router.currentRoute.value.name).toBe('BudgetLineCustomizations')
    expect(router.currentRoute.value.params.lineId).toBe(LINE_ID)
    expect(router.currentRoute.value.params.budgetId).toBe(BUDGET_ID)
  })
})

describe('BudgetLineCustomizationsView (REQ-BLR-05)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('calls fetchRevisions on mount', async () => {
    setupMocks()
    const router = makeRouter()
    await router.push(`/budgets/${BUDGET_ID}/lines/${LINE_ID}/customizations`)
    await router.isReady()
    render(BudgetLineCustomizationsView, {
      global: { plugins: [router, makeI18n()] },
    })
    await nextTick()
    const store = vi.mocked(useBudgetStructureStore)()
    expect(store.fetchRevisions).toHaveBeenCalledWith(BUDGET_ID, LINE_ID)
  })

  it('shows empty state when no revisions', async () => {
    await renderView([])
    expect(screen.getByText('No revisions yet.')).toBeTruthy()
  })

  it('renders revision rows when revisions exist', async () => {
    const revisions: BudgetLineRevisionResponse[] = [
      {
        id: 'rev-1',
        budgetedAmount: 1000,
        currencyId: 'currency-gtq',
        currencyCode: 'GTQ',
        validFrom: '2025-01-01' as any,
        validTo: null,
      },
    ]
    await renderView(revisions)
    // Should show the valid from date
    expect(screen.getByText('2025-01-01')).toBeTruthy()
  })

  it('shows a back link to BudgetLines', async () => {
    await renderView([])
    // Back navigation is via breadcrumb — displays the budget lines title as a link
    expect(screen.getByText('Budget Lines')).toBeTruthy()
  })

  it('shows the Revisions section heading', async () => {
    await renderView([])
    expect(screen.getByText('Revisions')).toBeTruthy()
  })
})

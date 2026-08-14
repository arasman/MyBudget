// Role gating for the "Save" and "Delete cut record" mutating controls.
// A ReadOnly-role user must not see these buttons even though the current
// record is loaded; an Operator/Admin/Owner user must see them.
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import { computed } from 'vue'
import CurrentSituationView from '../CurrentSituationView.vue'
import type { CutRecordResponse } from '../../types/cutRecord'

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

vi.mock('../../store/useCutRecordStore', () => ({
  useCutRecordStore: vi.fn(),
}))

vi.mock('@/stores/toast.store', () => ({
  useToastStore: vi.fn(),
}))

vi.mock('@/features/budget-structure/composables/useRoleGate', () => ({
  useRoleGate: vi.fn(),
}))

vi.mock('@/features/budget-structure/api/currencies.api', () => ({
  listCurrencies: vi.fn().mockResolvedValue([]),
}))

vi.mock('../../api/cutRecordApi', () => ({
  getCutRecord: vi.fn(),
}))

vi.mock('@/features/budget-structure/components/BudgetTabs.vue', () => ({
  default: { template: '<div data-testid="budget-tabs" />' },
}))

vi.mock('../../components/CutDateNavigator.vue', () => ({
  default: { props: ['currentDate', 'hasPrevious', 'hasNext'], template: '<div data-testid="cut-date-navigator" />' },
}))

vi.mock('../../components/CutRecordForm.vue', () => ({
  default: {
    props: ['accounts', 'exchangeRate', 'isDraft', 'currencies', 'remaining', 'primaryCurrencyId', 'cutDate'],
    emits: ['save', 'update:live-totals', 'update:live-exchange-rate', 'date-change'],
    template: '<div data-testid="cut-record-form" />',
  },
}))

vi.mock('../../components/CutTotalsPanel.vue', () => ({
  default: {
    props: ['totals', 'executionSummary', 'exchangeRate'],
    template: '<div data-testid="cut-totals-panel" />',
  },
}))

vi.mock('../../components/DeleteCutModal.vue', () => ({
  default: {
    props: ['cutDate', 'loading'],
    emits: ['confirm', 'cancel'],
    template: '<div data-testid="delete-cut-modal" />',
  },
}))

vi.mock('../../components/LoadStrategyModal.vue', () => ({
  default: {
    props: ['targetDate', 'cutDates', 'loading'],
    emits: ['select', 'cancel'],
    template: '<div data-testid="load-strategy-modal" />',
  },
}))

import { useCutRecordStore } from '../../store/useCutRecordStore'
import { useToastStore } from '@/stores/toast.store'
import { useRoleGate } from '@/features/budget-structure/composables/useRoleGate'

const BUDGET_ID = 'budget-1'

const mockRecord: CutRecordResponse = {
  isDraft: false,
  cutRecordId: 'cut-1',
  cutDate: '2026-08-01',
  exchangeRate: 1,
  projectionsJson: null,
  primaryCurrencyId: 'currency-gtq',
  executionSummary: { totalBudgeted: 100, totalRegistered: 50, remaining: 50 },
  accounts: [],
  totals: {
    totalPositive: 0,
    totalNegative: 0,
    totalDeudaEnCurso: 0,
    totalPositiveAlt: 0,
    totalNegativeAlt: 0,
    totalDeudaEnCursoAlt: 0,
  },
}

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/budgets/:budgetId/current-situation',
        name: 'CurrentSituation',
        component: CurrentSituationView,
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
        currentSituation: {
          title: 'Current Situation',
          delete: 'Delete cut record',
          noData: 'No data',
          saveSuccess: 'Saved',
          deleteSuccess: 'Deleted',
          errors: { noActivePeriod: 'No active period' },
        },
        common: { save: 'Save' },
      },
    },
  })
}

function setupStoreMock({
  currentRecord = null as CutRecordResponse | null,
  loading = false,
  saveLoading = false,
  currentDate = null as string | null,
} = {}) {
  const storeMock = {
    currentRecord,
    cutDates: [],
    loading,
    error: null,
    saveError: null,
    saveLoading,
    currentDate,
    hasPrevious: false,
    hasNext: false,
    fetchCutDates: vi.fn().mockResolvedValue(undefined),
    fetchCutRecord: vi.fn().mockResolvedValue(undefined),
    upsertCutRecord: vi.fn().mockResolvedValue(undefined),
    deleteCutRecord: vi.fn().mockResolvedValue(undefined),
    navigateToPrevious: vi.fn(),
    navigateToNext: vi.fn(),
  }
  vi.mocked(useCutRecordStore).mockReturnValue(
    storeMock as unknown as ReturnType<typeof useCutRecordStore>,
  )
  return storeMock
}

function setupToastMock() {
  const push = vi.fn()
  vi.mocked(useToastStore).mockReturnValue({ push } as unknown as ReturnType<typeof useToastStore>)
  return { push }
}

function setupRoleGateMock({ isOperator = true } = {}) {
  vi.mocked(useRoleGate).mockReturnValue({
    isAdmin: computed(() => isOperator),
    isOwner: computed(() => false),
    isOperator: computed(() => isOperator),
    canWriteStructure: computed(() => isOperator),
    canWriteLines: computed(() => isOperator),
  })
}

async function renderView() {
  const router = makeRouter()
  await router.push(`/budgets/${BUDGET_ID}/current-situation`)
  await router.isReady()

  return render(CurrentSituationView, {
    global: { plugins: [router, makeI18n()] },
  })
}

describe('CurrentSituationView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  describe('role gating — ReadOnly users', () => {
    it('hides the Save button when isOperator=false', async () => {
      setupStoreMock({ currentRecord: mockRecord })
      setupToastMock()
      setupRoleGateMock({ isOperator: false })
      await renderView()
      expect(screen.queryByText('Save')).toBeNull()
    })

    it('shows the Save button when isOperator=true', async () => {
      setupStoreMock({ currentRecord: mockRecord })
      setupToastMock()
      setupRoleGateMock({ isOperator: true })
      await renderView()
      expect(screen.queryByText('Save')).not.toBeNull()
    })

    it('hides the "Delete cut record" button when isOperator=false', async () => {
      setupStoreMock({ currentRecord: mockRecord, currentDate: '2026-08-01' })
      setupToastMock()
      setupRoleGateMock({ isOperator: false })
      await renderView()
      expect(screen.queryByText('Delete cut record')).toBeNull()
    })

    it('shows the "Delete cut record" button when isOperator=true and a currentDate exists', async () => {
      setupStoreMock({ currentRecord: mockRecord, currentDate: '2026-08-01' })
      setupToastMock()
      setupRoleGateMock({ isOperator: true })
      await renderView()
      expect(screen.queryByText('Delete cut record')).not.toBeNull()
    })
  })
})

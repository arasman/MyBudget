/**
 * ExecutionListModal.vue — Vitest component tests (T-4.9)
 *
 * Strategy: mock both '../store' and '@/features/budget-structure/store' at module level.
 * Control the returned values per-test by mutating the shared state objects.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { computed } from 'vue'
import { EntryType } from '../types'
import type { ExecutionRecordDto } from '../types'

// -----------------------------------------------------------------------
// Shared mutable mock state
// -----------------------------------------------------------------------

// The component accesses store properties directly (Pinia auto-unwraps refs).
// We mimic this by returning plain reactive properties.
const matrixState: {
  openModalLineId: string | null
  openModalPeriodId: string | null
  showDeletedInModal: boolean
  executionRecords: Record<string, ExecutionRecordDto[]>
  loadingExecutions: Record<string, boolean>
  modalError: string | null
  closeExecutionModal: ReturnType<typeof vi.fn>
  toggleShowDeletedInModal: ReturnType<typeof vi.fn>
  createExecution: ReturnType<typeof vi.fn>
  updateExecution: ReturnType<typeof vi.fn>
} = {
  openModalLineId: null,
  openModalPeriodId: null,
  showDeletedInModal: false,
  executionRecords: {},
  loadingExecutions: {},
  modalError: null,
  closeExecutionModal: vi.fn(),
  toggleShowDeletedInModal: vi.fn(),
  createExecution: vi.fn(),
  updateExecution: vi.fn(),
}

const structureState: {
  periods: Array<{ id: string; name: string; periodNumber: number; startDate: string; endDate: string; isClosed: boolean }>
  currentCycle: { defaultCurrency: { id: string; code: string; name: string; symbol: string } } | null
  loading: boolean
  error: string | null
} = {
  periods: [],
  currentCycle: {
    defaultCurrency: { id: 'currency-gtq', code: 'GTQ', name: 'Quetzal', symbol: 'Q' },
  },
  loading: false,
  error: null,
}

vi.mock('../store', () => ({
  useBudgetMatrixStore: () => matrixState,
}))

vi.mock('@/features/budget-structure/store', () => ({
  useBudgetStructureStore: () => structureState,
}))

vi.mock('@/features/budget-structure/composables/useRoleGate', () => ({
  useRoleGate: () => ({
    isOperator: computed(() => true),
  }),
}))

vi.mock('../composables/useCurrencyDisplay', () => ({
  useCurrencyDisplay: () => ({
    formatAmount: (amount: number) => amount.toFixed(2),
  }),
}))

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (k: string) => {
      const map: Record<string, string> = {
        'budgetExecution.modal.title': 'Executions',
        'budgetExecution.modal.noEntries': 'No entries yet',
        'budgetExecution.modal.addEntry': 'Add entry',
        'budgetExecution.form.entryType': 'Entry type',
        'budgetExecution.form.entryTypes.expense': 'Expense',
        'budgetExecution.form.entryTypes.creditNote': 'Credit Note',
        'budgetExecution.form.entryTypes.debitNote': 'Debit Note',
        'budgetExecution.form.amount': 'Amount',
        'budgetExecution.form.note': 'Note',
        'budgetExecution.form.save': 'Save',
        'budgetExecution.form.cancel': 'Cancel',
        'budgetExecution.form.error': 'An error occurred',
        'budgetExecution.form.validation.amountRequired': 'Amount must be greater than 0',
        'budgetExecution.form.validation.noteRequired': 'Note required',
        'budgetExecution.row.edit': 'Edit',
        'budgetExecution.row.delete': 'Delete',
        'budgetExecution.row.restore': 'Restore',
      }
      return map[k] ?? k
    },
  }),
}))

// Import component AFTER mocks are declared (static import hoisting is fine here
// because vi.mock is hoisted too — both happen before component import).
import ExecutionListModal from '../components/ExecutionListModal.vue'

const sampleRecord: ExecutionRecordDto = {
  id: 'rec-1',
  entryType: EntryType.Expense,
  amount: 100,
  currencyId: 'currency-gtq',
  exchangeRate: null,
  exchangeRateTo: null,
  accountId: null,
  paymentMethodId: null,
  note: 'Test note',
  createdAt: '2026-01-10T10:00:00Z',
  updatedAt: null,
  deletedAt: null,
  operationDate: null,
}

const openPeriod = {
  id: 'period-open',
  name: 'Period 1',
  periodNumber: 1,
  startDate: '2026-01-01',
  endDate: '2026-01-31',
  isClosed: false,
}

const closedPeriod = { ...openPeriod, isClosed: true }

describe('ExecutionListModal.vue', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    // Reset state to clean defaults
    matrixState.openModalLineId = null
    matrixState.openModalPeriodId = null
    matrixState.showDeletedInModal = false
    matrixState.executionRecords = {}
    matrixState.loadingExecutions = {}
    matrixState.modalError = null
    structureState.periods = [openPeriod]
  })

  it('does not render dialog when openModalLineId is null', () => {
    matrixState.openModalLineId = null

    const { container } = render(ExecutionListModal, {
      props: { budgetId: 'budget-1' },
    })

    expect(container.querySelector('dialog')).toBeNull()
  })

  it('hides the form when period is closed', () => {
    matrixState.openModalLineId = 'line-1'
    matrixState.openModalPeriodId = 'period-open'
    matrixState.executionRecords = { 'line-1:period-open:false': [] }
    structureState.periods = [closedPeriod]

    render(ExecutionListModal, { props: { budgetId: 'budget-1' } })

    expect(screen.queryByText('Add entry')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Save' })).toBeNull()
  })

  it('shows the form when period is open', () => {
    matrixState.openModalLineId = 'line-1'
    matrixState.openModalPeriodId = 'period-open'
    matrixState.executionRecords = { 'line-1:period-open:false': [] }
    structureState.periods = [openPeriod]

    const { container } = render(ExecutionListModal, { props: { budgetId: 'budget-1' } })

    expect(screen.queryByText('Add entry')).not.toBeNull()
    // The form renders — check for the submit button via its text directly
    const submitButtons = container.querySelectorAll('button[type="submit"]')
    expect(submitButtons.length).toBeGreaterThan(0)
  })

  it('renders records from store state', () => {
    matrixState.openModalLineId = 'line-1'
    matrixState.openModalPeriodId = 'period-open'
    matrixState.executionRecords = {
      'line-1:period-open:false': [
        { ...sampleRecord, id: 'rec-2', createdAt: '2026-01-12T10:00:00Z', note: 'Second note' },
        { ...sampleRecord, id: 'rec-1', createdAt: '2026-01-10T10:00:00Z', note: 'First note' },
      ],
    }
    structureState.periods = [openPeriod]

    render(ExecutionListModal, { props: { budgetId: 'budget-1' } })

    expect(screen.queryByText('First note')).not.toBeNull()
    expect(screen.queryByText('Second note')).not.toBeNull()
  })

  it('shows empty state message when no records', () => {
    matrixState.openModalLineId = 'line-1'
    matrixState.openModalPeriodId = 'period-open'
    matrixState.executionRecords = { 'line-1:period-open:false': [] }
    structureState.periods = [openPeriod]

    render(ExecutionListModal, { props: { budgetId: 'budget-1' } })

    expect(screen.queryByText('No entries yet')).not.toBeNull()
  })
})

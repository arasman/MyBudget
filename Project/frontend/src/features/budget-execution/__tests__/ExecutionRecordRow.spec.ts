/**
 * ExecutionRecordRow.vue — Vitest component tests
 *
 * Covers:
 *  - Two-step delete confirmation (REQ-EXEC-CONFIRM-1)
 *  - Delete success toast (REQ-EXEC-TOAST-1)
 *  - Restore success toast (REQ-EXEC-TOAST-1)
 *  - Row-local isolation of confirm state
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { computed } from 'vue'
import { EntryType } from '../types'
import type { ExecutionRecordDto } from '../types'

// -----------------------------------------------------------------------
// Shared mutable mock state
// -----------------------------------------------------------------------

const matrixState: {
  deleteExecution: ReturnType<typeof vi.fn>
  restoreExecution: ReturnType<typeof vi.fn>
  showDeletedInModal: boolean
} = {
  deleteExecution: vi.fn().mockResolvedValue(undefined),
  restoreExecution: vi.fn().mockResolvedValue(undefined),
  showDeletedInModal: false,
}

const structureState: {
  currentCycle: { defaultCurrency: { id: string; code: string } } | null
} = {
  currentCycle: {
    defaultCurrency: { id: 'currency-gtq', code: 'GTQ' },
  },
}

const toastState: {
  push: ReturnType<typeof vi.fn>
} = {
  push: vi.fn(),
}

vi.mock('../store', () => ({
  useBudgetMatrixStore: () => matrixState,
}))

vi.mock('@/features/budget-structure/store', () => ({
  useBudgetStructureStore: () => structureState,
}))

vi.mock('@/stores/toast.store', () => ({
  useToastStore: () => toastState,
}))

vi.mock('@/features/budget-structure/composables/useRoleGate', () => ({
  useRoleGate: () => ({
    isOperator: computed(() => true),
  }),
}))

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (k: string) => {
      const map: Record<string, string> = {
        'budgetExecution.row.edit': 'Edit',
        'budgetExecution.row.delete': 'Delete',
        'budgetExecution.row.restore': 'Restore',
        'budgetExecution.record.confirmDelete': 'Delete this entry?',
        'budgetExecution.record.deleteSuccess': 'Entry deleted successfully',
        'budgetExecution.record.restoreSuccess': 'Entry restored successfully',
        'budgetExecution.form.entryTypes.expense': 'Expense',
        'budgetExecution.form.entryTypes.creditNote': 'Credit Note',
        'budgetExecution.form.entryTypes.debitNote': 'Debit Note',
        'common.cancel': 'Cancel',
      }
      return map[k] ?? k
    },
  }),
}))

import ExecutionRecordRow from '../components/ExecutionRecordRow.vue'

// -----------------------------------------------------------------------
// Test fixtures
// -----------------------------------------------------------------------

const activeRecord: ExecutionRecordDto = {
  id: 'rec-1',
  entryType: EntryType.Expense,
  amount: 150,
  currencyId: 'currency-gtq',
  exchangeRate: null,
  exchangeRateTo: null,
  accountId: null,
  paymentMethodId: null,
  note: 'Lunch',
  createdAt: '2026-01-10T10:00:00Z',
  updatedAt: null,
  deletedAt: null,
  operationDate: null,
}

const deletedRecord: ExecutionRecordDto = {
  ...activeRecord,
  id: 'rec-2',
  deletedAt: '2026-01-11T10:00:00Z',
}

const defaultProps = {
  record: activeRecord,
  periodClosed: false,
  budgetId: 'budget-1',
  periodId: 'period-1',
  lineId: 'line-1',
}

// -----------------------------------------------------------------------
// Tests
// -----------------------------------------------------------------------

describe('ExecutionRecordRow.vue', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    matrixState.deleteExecution = vi.fn().mockResolvedValue(undefined)
    matrixState.restoreExecution = vi.fn().mockResolvedValue(undefined)
  })

  // --- Two-step delete confirmation ---

  it('shows delete button in normal state for active record', () => {
    render(ExecutionRecordRow, { props: defaultProps })

    expect(screen.getByTestId('delete-record-btn')).toBeTruthy()
    expect(screen.queryByTestId('delete-record-confirm-btn')).toBeNull()
  })

  it('first click on delete enters confirm state without calling deleteExecution', async () => {
    render(ExecutionRecordRow, { props: defaultProps })

    await fireEvent.click(screen.getByTestId('delete-record-btn'))

    expect(screen.queryByTestId('delete-record-btn')).toBeNull()
    expect(screen.getByTestId('delete-record-confirm-btn')).toBeTruthy()
    expect(screen.getByTestId('delete-record-cancel-btn')).toBeTruthy()
    expect(screen.getByText('Delete this entry?')).toBeTruthy()
    expect(matrixState.deleteExecution).not.toHaveBeenCalled()
  })

  it('second click on confirm button calls deleteExecution', async () => {
    render(ExecutionRecordRow, { props: defaultProps })

    await fireEvent.click(screen.getByTestId('delete-record-btn'))
    await fireEvent.click(screen.getByTestId('delete-record-confirm-btn'))

    expect(matrixState.deleteExecution).toHaveBeenCalledWith(
      'budget-1',
      'period-1',
      'line-1',
      'rec-1',
    )
  })

  it('cancel button resets to normal delete state', async () => {
    render(ExecutionRecordRow, { props: defaultProps })

    await fireEvent.click(screen.getByTestId('delete-record-btn'))
    expect(screen.getByTestId('delete-record-confirm-btn')).toBeTruthy()

    await fireEvent.click(screen.getByTestId('delete-record-cancel-btn'))

    expect(screen.getByTestId('delete-record-btn')).toBeTruthy()
    expect(screen.queryByTestId('delete-record-confirm-btn')).toBeNull()
    expect(matrixState.deleteExecution).not.toHaveBeenCalled()
  })

  it('confirm state is row-local: one row entering confirm does not affect others', async () => {
    const { container } = render({
      template: `
        <div>
          <ExecutionRecordRow v-bind="props1" />
          <ExecutionRecordRow v-bind="props2" />
        </div>
      `,
      components: { ExecutionRecordRow },
      setup() {
        return {
          props1: { ...defaultProps, record: { ...activeRecord, id: 'rec-a' } },
          props2: { ...defaultProps, record: { ...activeRecord, id: 'rec-b' } },
        }
      },
    })

    const [btn1] = container.querySelectorAll('[data-testid="delete-record-btn"]')
    await fireEvent.click(btn1)

    const confirmBtns = container.querySelectorAll('[data-testid="delete-record-confirm-btn"]')
    const normalBtns = container.querySelectorAll('[data-testid="delete-record-btn"]')

    expect(confirmBtns).toHaveLength(1)
    expect(normalBtns).toHaveLength(1)
  })

  // --- Toast wiring ---

  it('pushes deleteSuccess toast after successful delete', async () => {
    render(ExecutionRecordRow, { props: defaultProps })

    await fireEvent.click(screen.getByTestId('delete-record-btn'))
    await fireEvent.click(screen.getByTestId('delete-record-confirm-btn'))

    // Wait for async handleDelete to settle
    await vi.waitFor(() => {
      expect(toastState.push).toHaveBeenCalledWith({ type: 'success', title: 'Entry deleted successfully' })
    })
  })

  it('does not push toast when delete fails', async () => {
    matrixState.deleteExecution = vi.fn().mockRejectedValue(new Error('server error'))

    render(ExecutionRecordRow, { props: defaultProps })

    await fireEvent.click(screen.getByTestId('delete-record-btn'))
    await fireEvent.click(screen.getByTestId('delete-record-confirm-btn'))

    await vi.waitFor(() => {
      expect(matrixState.deleteExecution).toHaveBeenCalled()
    })

    expect(toastState.push).not.toHaveBeenCalled()
  })

  it('pushes restoreSuccess toast after successful restore', async () => {
    render(ExecutionRecordRow, {
      props: { ...defaultProps, record: deletedRecord },
    })

    const restoreBtn = screen.getByTitle('Restore')
    await fireEvent.click(restoreBtn)

    await vi.waitFor(() => {
      expect(toastState.push).toHaveBeenCalledWith({ type: 'success', title: 'Entry restored successfully' })
    })
  })

  // --- Deleted record rendering ---

  it('shows restore button and no delete button for deleted record', () => {
    render(ExecutionRecordRow, {
      props: { ...defaultProps, record: deletedRecord },
    })

    expect(screen.queryByTestId('delete-record-btn')).toBeNull()
    expect(screen.getByTitle('Restore')).toBeTruthy()
  })

  it('hides actions when period is closed and record is active', () => {
    render(ExecutionRecordRow, {
      props: { ...defaultProps, periodClosed: true },
    })

    expect(screen.queryByTestId('delete-record-btn')).toBeNull()
    expect(screen.queryByText('Edit')).toBeNull()
  })

  it('still shows restore button when period is closed and record is deleted', () => {
    render(ExecutionRecordRow, {
      props: { ...defaultProps, periodClosed: true, record: deletedRecord },
    })

    expect(screen.getByTitle('Restore')).toBeTruthy()
  })
})

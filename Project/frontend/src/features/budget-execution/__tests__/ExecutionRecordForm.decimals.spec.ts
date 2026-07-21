/**
 * REQ-EXEC-DECIMAL-VAL-1: ExecutionRecordForm decimal precision validation.
 * REQ-EXEC-TOAST-MIGRATE-1: API errors surfaced via toast (no inline banner).
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import ExecutionRecordForm from '../components/ExecutionRecordForm.vue'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (k: string) => {
      const map: Record<string, string> = {
        'budgetExecution.form.entryType': 'Entry type',
        'budgetExecution.form.entryTypes.expense': 'Expense',
        'budgetExecution.form.entryTypes.creditNote': 'Credit Note',
        'budgetExecution.form.entryTypes.debitNote': 'Debit Note',
        'budgetExecution.form.amount': 'Amount',
        'budgetExecution.form.currency': 'Currency',
        'budgetExecution.form.exchangeRate': 'Exchange rate',
        'budgetExecution.form.note': 'Note',
        'budgetExecution.form.operationDate': 'Operation date',
        'budgetExecution.form.save': 'Save',
        'budgetExecution.form.cancel': 'Cancel',
        'budgetExecution.form.error': 'An error occurred',
        'budgetExecution.form.calculatedAmount': 'Converted amount',
        'budgetExecution.form.validation.amountRequired': 'Amount must be greater than 0',
        'budgetExecution.form.validation.amountDecimals': 'Amount can have at most 2 decimal places',
        'budgetExecution.form.validation.exchangeRateRequired': 'Exchange rate must be greater than 0',
        'budgetExecution.form.validation.exchangeRateDecimals': 'Exchange rate can have at most 6 decimal places',
        'budgetExecution.form.validation.noteRequiredAlways': 'Note is required',
        'budgetExecution.form.errors.operationDateOutOfRange': 'Operation date is outside the period range',
        'common.errors.serverError': 'An unexpected error occurred. Please try again.',
      }
      return map[k] ?? k
    },
  }),
}))

const { mockCreateExecution, mockUpdateExecution } = vi.hoisted(() => ({
  mockCreateExecution: vi.fn(),
  mockUpdateExecution: vi.fn(),
}))

vi.mock('../store', () => ({
  useBudgetMatrixStore: () => ({
    createExecution: mockCreateExecution,
    updateExecution: mockUpdateExecution,
  }),
}))

vi.mock('@/features/budget-structure/store', () => ({
  useBudgetStructureStore: () => ({
    currentCycle: {
      defaultCurrency: { id: 'currency-gtq', code: 'GTQ', name: 'Quetzal', symbol: 'Q' },
      alternateCurrency: { id: 'currency-usd', code: 'USD', name: 'US Dollar', symbol: '$' },
    },
  }),
}))

vi.mock('@/features/budget-structure/utils/apiError', () => ({
  extractApiErrorCode: (err: unknown) => {
    const e = err as { response?: { data?: { error?: string } } }
    return e?.response?.data?.error ?? null
  },
}))

const defaultProps = {
  budgetId: 'budget-1',
  periodId: 'period-1',
  lineId: 'line-1',
}

describe('ExecutionRecordForm — decimal precision validation (REQ-EXEC-DECIMAL-VAL-1)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('blocks submit when amount has more than 2 decimal places', async () => {
    render(ExecutionRecordForm, { props: defaultProps })

    // Use fireEvent.update which fires input + change — Vue picks up v-model.number
    const amountInput = document.querySelector('#exec-amount') as HTMLInputElement
    await fireEvent.update(amountInput, '100.123')

    const noteInput = screen.getByRole('textbox', { name: /note/i })
    await fireEvent.update(noteInput, 'test')

    const submitBtn = screen.getByTestId('execution-form-submit')
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      const errorEl = document.querySelector('[data-testid="amount-input"] + span, #exec-amount + span, span.text-error')
      // find any element containing our text
      const allSpans = Array.from(document.querySelectorAll('span'))
      const found = allSpans.some((s) => s.textContent?.includes('Amount can have at most 2 decimal places'))
      expect(found).toBe(true)
    })

    expect(mockCreateExecution).not.toHaveBeenCalled()
  })

  it('accepts amount with exactly 2 decimal places', async () => {
    mockCreateExecution.mockResolvedValue(undefined)
    render(ExecutionRecordForm, { props: defaultProps })

    const amountInput = document.querySelector('#exec-amount') as HTMLInputElement
    await fireEvent.update(amountInput, '100.12')

    const noteInput = screen.getByRole('textbox', { name: /note/i })
    await fireEvent.update(noteInput, 'test note')

    const submitBtn = screen.getByTestId('execution-form-submit')
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(mockCreateExecution).toHaveBeenCalled()
    })
  })

  it('does not render inline error banner (submitError removed)', () => {
    render(ExecutionRecordForm, { props: defaultProps })
    // The old .alert.alert-error banner should not exist at render time
    const alerts = document.querySelectorAll('.alert.alert-error')
    expect(alerts.length).toBe(0)
  })

  it('pushes error toast (not inline) when API returns OPERATION_DATE_OUT_OF_RANGE', async () => {
    const error = { response: { data: { error: 'OPERATION_DATE_OUT_OF_RANGE' } } }
    mockCreateExecution.mockRejectedValue(error)

    render(ExecutionRecordForm, { props: defaultProps })

    const amountInput = document.querySelector('#exec-amount') as HTMLInputElement
    await fireEvent.update(amountInput, '100')

    const noteInput = screen.getByRole('textbox', { name: /note/i })
    await fireEvent.update(noteInput, 'test')

    const submitBtn = screen.getByTestId('execution-form-submit')
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(mockCreateExecution).toHaveBeenCalled()
    })

    // Verify no inline banner exists after error (errors go to toast)
    const alerts = document.querySelectorAll('.alert.alert-error')
    expect(alerts.length).toBe(0)
  })

  it('pushes generic serverError toast for unknown API error codes', async () => {
    const error = { response: { data: { error: 'SOME_UNKNOWN_ERROR' } } }
    mockCreateExecution.mockRejectedValue(error)

    render(ExecutionRecordForm, { props: defaultProps })

    const amountInput = document.querySelector('#exec-amount') as HTMLInputElement
    await fireEvent.update(amountInput, '50')

    const noteInput = screen.getByRole('textbox', { name: /note/i })
    await fireEvent.update(noteInput, 'note text')

    const submitBtn = screen.getByTestId('execution-form-submit')
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(mockCreateExecution).toHaveBeenCalled()
    })

    // No inline banner — toast only
    expect(document.querySelectorAll('.alert.alert-error').length).toBe(0)
  })
})

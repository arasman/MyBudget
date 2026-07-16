import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import ExecutionRecordForm from '../components/ExecutionRecordForm.vue'
import { EntryType } from '../types'

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
        'budgetExecution.form.save': 'Save',
        'budgetExecution.form.cancel': 'Cancel',
        'budgetExecution.form.error': 'An error occurred',
        'budgetExecution.form.validation.amountRequired': 'Amount must be greater than 0',
        'budgetExecution.form.validation.exchangeRateRequired': 'Exchange rate must be greater than 0',
        'budgetExecution.form.validation.noteRequiredAlways': 'Note is required',
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

const defaultProps = {
  budgetId: 'budget-1',
  periodId: 'period-1',
  lineId: 'line-1',
}

describe('ExecutionRecordForm.vue', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('shows note validation error when note is empty (any entry type)', async () => {
    render(ExecutionRecordForm, { props: defaultProps })

    // Set a valid amount but leave note empty
    const amountInput = screen.getByRole('spinbutton')
    await fireEvent.input(amountInput, { target: { value: '100' } })

    const submitBtn = screen.getByRole('button', { name: 'Save' })
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(screen.queryByText('Note is required')).not.toBeNull()
    })

    expect(mockCreateExecution).not.toHaveBeenCalled()
  })

  it('blocks submit when amount is empty', async () => {
    render(ExecutionRecordForm, { props: defaultProps })

    // Do not fill in amount
    const submitBtn = screen.getByRole('button', { name: 'Save' })
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(screen.queryByText('Amount must be greater than 0')).not.toBeNull()
    })

    expect(mockCreateExecution).not.toHaveBeenCalled()
  })

  it('calls createExecution on happy path submit for Expense', async () => {
    mockCreateExecution.mockResolvedValue(undefined)
    const { emitted } = render(ExecutionRecordForm, { props: defaultProps })

    const amountInput = screen.getByRole('spinbutton')
    await fireEvent.input(amountInput, { target: { value: '250' } })

    const noteInput = screen.getByRole('textbox', { name: /note/i })
    await fireEvent.input(noteInput, { target: { value: 'Test note' } })

    const submitBtn = screen.getByRole('button', { name: 'Save' })
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(mockCreateExecution).toHaveBeenCalledWith(
        'budget-1',
        'period-1',
        'line-1',
        expect.objectContaining({
          entryType: EntryType.Expense,
          currencyId: 'currency-gtq',
          note: 'Test note',
        }),
      )
    })

    expect(emitted()['saved']).toBeTruthy()
  })

  it('blocks submit when alternate currency is selected and exchange rate is empty', async () => {
    render(ExecutionRecordForm, { props: defaultProps })

    // Select alternate currency — shows exchange rate field
    const currencySelect = screen.getByRole('combobox', { name: /currency/i })
    await fireEvent.change(currencySelect, { target: { value: 'currency-usd' } })

    // Fill valid amount and note; leave exchange rate at null (default)
    const amountInput = screen.getByRole('spinbutton', { name: /amount/i })
    await fireEvent.input(amountInput, { target: { value: '100' } })

    const noteInput = screen.getByRole('textbox', { name: /note/i })
    await fireEvent.input(noteInput, { target: { value: 'Test' } })

    await fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => {
      expect(screen.queryByText('Exchange rate must be greater than 0')).not.toBeNull()
    })

    expect(mockCreateExecution).not.toHaveBeenCalled()
  })

  it('calls updateExecution when editRecord prop is provided', async () => {
    mockUpdateExecution.mockResolvedValue(undefined)
    const editRecord = {
      id: 'rec-1',
      entryType: EntryType.Expense,
      amount: 100,
      currencyId: 'currency-gtq',
      exchangeRate: null,
      exchangeRateTo: null,
      accountId: null,
      paymentMethodId: null,
      note: 'existing note',
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: null,
      deletedAt: null,
      operationDate: null,
    }

    const { emitted } = render(ExecutionRecordForm, {
      props: { ...defaultProps, editRecord },
    })

    const submitBtn = screen.getByRole('button', { name: 'Save' })
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(mockUpdateExecution).toHaveBeenCalledWith(
        'budget-1',
        'period-1',
        'line-1',
        'rec-1',
        expect.objectContaining({ amount: 100 }),
      )
    })

    expect(emitted()['saved']).toBeTruthy()
  })
})

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
        'budgetExecution.form.note': 'Note',
        'budgetExecution.form.save': 'Save',
        'budgetExecution.form.cancel': 'Cancel',
        'budgetExecution.form.error': 'An error occurred',
        'budgetExecution.form.validation.amountRequired': 'Amount must be greater than 0',
        'budgetExecution.form.validation.noteRequired':
          'Note is required for Credit Note and Debit Note',
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
      defaultCurrency: { id: 'currency-gtq', code: 'GTQ' },
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

  it('shows note validation error when entryType is CreditNote and note is empty', async () => {
    render(ExecutionRecordForm, { props: defaultProps })

    // Change entry type to CreditNote
    const select = screen.getByRole('combobox')
    await fireEvent.change(select, { target: { value: String(EntryType.CreditNote) } })

    // Set a valid amount
    const amountInput = screen.getByRole('spinbutton')
    await fireEvent.input(amountInput, { target: { value: '100' } })

    // Submit without note
    const submitBtn = screen.getByRole('button', { name: 'Save' })
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(
        screen.queryByText('Note is required for Credit Note and Debit Note'),
      ).not.toBeNull()
    })

    expect(mockCreateExecution).not.toHaveBeenCalled()
  })

  it('does NOT show note validation error when entryType is Expense and note is empty', async () => {
    mockCreateExecution.mockResolvedValue(undefined)
    render(ExecutionRecordForm, { props: defaultProps })

    // Entry type defaults to Expense
    const amountInput = screen.getByRole('spinbutton')
    await fireEvent.input(amountInput, { target: { value: '50' } })

    const submitBtn = screen.getByRole('button', { name: 'Save' })
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(mockCreateExecution).toHaveBeenCalled()
    })

    expect(
      screen.queryByText('Note is required for Credit Note and Debit Note'),
    ).toBeNull()
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
        }),
      )
    })

    expect(emitted()['saved']).toBeTruthy()
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

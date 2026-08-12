import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import BudgetLineModal from '../BudgetLineModal.vue'
import type { DateString } from '../../types'

vi.mock('@/features/budget-structure/store', () => ({
  useBudgetStructureStore: () => ({
    currentCycle: {
      defaultCurrency: { id: 'currency-gtq', code: 'GTQ', name: 'Quetzal', symbol: 'Q' },
      alternateCurrency: null,
    },
  }),
}))

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        budgetStructure: {
          budgetLines: {
            create: 'New Line',
            edit: 'Edit Line',
            name: 'Name',
            lineType: 'Type',
            startDate: 'Start Date',
            endDate: 'End Date',
            budgetedAmount: 'Budgeted Amount',
            initialAmount: 'Initial Amount',
            currency: 'Currency',
            description: 'Description',
            types: {
              expense: 'Expense',
              longTermSavings: 'Long-term Savings',
              preventiveSavings: 'Preventive Savings',
            },
            validation: {
              nameRequired: 'Name is required',
              nameTooLong: 'Name must be 200 characters or fewer',
              amountRequired: 'Amount is required',
              amountPositive: 'Amount must be greater than 0',
              startDateRequired: 'Start date is required',
              endDateAfterStartDate: 'End date must be after start date',
              validFromRequired: 'Valid from date is required',
              validFromNotInPast: 'Valid from date cannot be in the past',
              validFromOutOfRange: 'Valid from date is out of range',
            },
          },
          categoryGroups: { title: 'Category Groups' },
          categories: { edit: 'Edit Category' },
          common: { save: 'Save', cancel: 'Cancel' },
        },
      },
    },
  })
}

function renderModal(modelValue = null) {
  setActivePinia(createPinia())
  return render(BudgetLineModal, {
    props: { modelValue, categoryGroups: [] },
    global: { plugins: [makeI18n()] },
  })
}

function getErrorText(): string {
  const errors = document.querySelectorAll('.text-error')
  return Array.from(errors)
    .map((el) => el.textContent ?? '')
    .join(' ')
}

describe('BudgetLineModal — validation (REQ-BL-2, REQ-BL-3)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('shows nameRequired error when name is empty on submit', async () => {
    renderModal()
    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(getErrorText()).toContain('Name is required')
    })
  })

  it('shows nameTooLong error when name exceeds 200 chars', async () => {
    renderModal()
    const nameInput = document.querySelector('#line-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'a'.repeat(201))
    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(getErrorText()).toContain('Name must be 200 characters or fewer')
    })
  })

  it('shows startDateRequired error when startDate is empty (create mode)', async () => {
    renderModal()
    const nameInput = document.querySelector('#line-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'Test Line')
    // leave startDate empty
    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(getErrorText()).toContain('Start date is required')
    })
  })

  it('does NOT show isRecurring checkbox in create mode', () => {
    renderModal()
    const recurringCheckbox = document.querySelector('#line-recurring')
    expect(recurringCheckbox).toBeNull()
  })

  it('shows startDate input in create mode', () => {
    renderModal()
    const startDateInput = document.querySelector('#line-startDate')
    expect(startDateInput).toBeTruthy()
  })

  it('shows endDate input in create mode', () => {
    renderModal()
    const endDateInput = document.querySelector('#line-endDate')
    expect(endDateInput).toBeTruthy()
  })

  it('emits submit with startDate and initialAmount when name and startDate are valid', async () => {
    const { emitted } = renderModal()
    const nameInput = document.querySelector('#line-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'My Line')

    const startDateInput = document.querySelector('#line-startDate') as HTMLInputElement
    await fireEvent.update(startDateInput, '2025-01-01')

    const amountInput = document.querySelector('#line-initialAmount') as HTMLInputElement
    await fireEvent.update(amountInput, '1000')

    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      const submitted = emitted()['submit']
      expect(submitted).toBeTruthy()
      const payload = (submitted as unknown[][])[0]![0] as Record<string, unknown>
      expect(payload).toHaveProperty('startDate', '2025-01-01')
      expect(payload).toHaveProperty('initialAmount', 1000)
      expect(payload).not.toHaveProperty('isRecurring')
      expect(payload).not.toHaveProperty('periodId')
    })
  })

  it('does not block submit when endDate is not set', async () => {
    const { emitted } = renderModal()
    const nameInput = document.querySelector('#line-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'My Line')
    const startDateInput = document.querySelector('#line-startDate') as HTMLInputElement
    await fireEvent.update(startDateInput, '2025-01-01')
    const amountInput = document.querySelector('#line-initialAmount') as HTMLInputElement
    await fireEvent.update(amountInput, '500')
    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(emitted()['submit']).toBeTruthy()
    })
  })

  it('shows amountPositive error when initialAmount is 0', async () => {
    renderModal()
    const nameInput = document.querySelector('#line-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'Test Line')
    const startDateInput = document.querySelector('#line-startDate') as HTMLInputElement
    await fireEvent.update(startDateInput, '2025-01-01')

    const amountInput = document.querySelector('#line-initialAmount') as HTMLInputElement
    await fireEvent.update(amountInput, '0')

    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(getErrorText()).toContain('Amount must be greater than 0')
    })
  })
})

// REQ-BLR-05: edit mode must NOT show Amount Revision section
describe('BudgetLineModal — edit mode strips Amount Revision section (REQ-BLR-05)', () => {
  function renderEditModal() {
    setActivePinia(createPinia())
    const existingLine = {
      id: 'l1',
      name: 'Salary',
      lineType: 'Expense' as const,
      startDate: '2025-01-01' as DateString,
      endDate: null,
      budgetedAmount: 1000,
      currencyId: 'currency-gtq',
      categoryGroupId: 'g1',
    }
    return render(BudgetLineModal, {
      props: { modelValue: existingLine, categoryGroups: [] },
      global: { plugins: [makeI18n()] },
    })
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('does not render #line-validFrom in edit mode', () => {
    renderEditModal()
    expect(document.querySelector('#line-validFrom')).toBeNull()
  })

  it('does not render #line-validTo in edit mode', () => {
    renderEditModal()
    expect(document.querySelector('#line-validTo')).toBeNull()
  })

  it('does not render #line-newAmount in edit mode', () => {
    renderEditModal()
    expect(document.querySelector('#line-newAmount')).toBeNull()
  })
})

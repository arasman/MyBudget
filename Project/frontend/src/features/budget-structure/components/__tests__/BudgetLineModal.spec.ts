import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import BudgetLineModal from '../BudgetLineModal.vue'

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
            isRecurring: 'Recurring',
            budgetedAmount: 'Budgeted Amount',
            currency: 'Currency',
            note: 'Note',
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

// jsdom marks <dialog> content as inaccessible; query via document.querySelector
describe('BudgetLineModal — validation (REQ-FORM-INLINE-VAL-1)', () => {
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

  it('shows amountPositive error when budgetedAmount is 0', async () => {
    renderModal()
    const nameInput = document.querySelector('#line-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'Test Line')

    const amountInput = document.querySelector('#line-amount') as HTMLInputElement
    await fireEvent.update(amountInput, '0')

    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(getErrorText()).toContain('Amount must be greater than 0')
    })
  })

  it('shows amountPositive error when budgetedAmount is negative', async () => {
    renderModal()
    const nameInput = document.querySelector('#line-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'Test Line')

    const amountInput = document.querySelector('#line-amount') as HTMLInputElement
    await fireEvent.update(amountInput, '-5')

    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(getErrorText()).toContain('Amount must be greater than 0')
    })
  })

  it('emits submit when name is valid and amount is positive', async () => {
    const { emitted } = renderModal()
    const nameInput = document.querySelector('#line-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'My Line')

    const amountInput = document.querySelector('#line-amount') as HTMLInputElement
    await fireEvent.update(amountInput, '100')

    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(emitted()['submit']).toBeTruthy()
    })
  })

  it('does not block submit when budgetedAmount is not set', async () => {
    const { emitted } = renderModal()
    const nameInput = document.querySelector('#line-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'My Line')
    // leave amount empty — should not block
    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(emitted()['submit']).toBeTruthy()
    })
  })
})

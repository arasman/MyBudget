import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/vue'
import { fireEvent } from '@testing-library/vue'
import { createI18n } from 'vue-i18n'
import CycleForm from '../CycleForm.vue'
import type { CycleListItem } from '../../types'

// Mock the currencies API so no network calls happen
vi.mock('../../api/currencies.api', () => ({
  listCurrencies: vi.fn().mockResolvedValue([
    { id: '11111111-1111-1111-1111-111111111111', code: 'GTQ', name: 'Quetzal', symbol: 'Q' },
    { id: '22222222-2222-2222-2222-222222222222', code: 'USD', name: 'US Dollar', symbol: '$' },
    { id: '33333333-3333-3333-3333-333333333333', code: 'EUR', name: 'Euro', symbol: '€' },
  ]),
}))

const USD_ID = '22222222-2222-2222-2222-222222222222'

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        budgetStructure: {
          cycles: {
            create: 'New Cycle',
            edit: 'Edit Cycle',
            name: 'Name',
            startDate: 'Start Date',
            endDate: 'End Date',
            defaultCurrency: 'Default Currency',
            alternateCurrency: 'Alternate Currency',
            exchangeRate: 'Exchange Rate',
            exchangeRateLabel: '{defaultCurrency} per 1 {alternateCurrency}',
            pairValidationError: 'Both alternate currency and exchange rate are required, or leave both empty',
            noneSelected: '— None —',
          },
          common: { save: 'Save', cancel: 'Cancel' },
        },
      },
    },
  })
}

function renderForm(props: { modelValue?: CycleListItem | null; budgetId?: string } = {}) {
  return render(CycleForm, {
    props: {
      modelValue: props.modelValue ?? null,
      budgetId: props.budgetId ?? 'budget-1',
    },
    global: {
      plugins: [makeI18n()],
    },
  })
}

/** Simulate selecting a value in a native <select> element for Vue v-model. */
async function selectValue(selectEl: HTMLSelectElement, value: string) {
  // Manually set the option selected before dispatching change
  const options = Array.from(selectEl.options)
  for (const opt of options) {
    opt.selected = opt.value === value
  }
  await fireEvent.change(selectEl)
}

describe('CycleForm — REQ-CYC-FORM-1: exchange rate input visibility', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('does not show exchange rate input when no alternate currency is selected', async () => {
    renderForm()
    await waitFor(() => {
      expect(screen.getByLabelText('Alternate Currency')).toBeTruthy()
    })
    expect(screen.queryByRole('spinbutton')).toBeNull()
  })

  it('shows exchange rate input when alternate currency is selected', async () => {
    renderForm()

    await waitFor(() => {
      expect(screen.getByLabelText('Alternate Currency')).toBeTruthy()
    })

    const altSelect = screen.getByLabelText('Alternate Currency') as HTMLSelectElement
    await selectValue(altSelect, USD_ID)

    await waitFor(() => {
      // After selecting alternate currency, the exchange rate block is rendered.
      // Look for the number input by its container label text (the dynamic interpolated label).
      expect(screen.getByText('GTQ per 1 USD')).toBeTruthy()
      // Also verify the input exists by querying the number input directly
      const numberInput = document.querySelector('input[type="number"]')
      expect(numberInput).not.toBeNull()
    })
  })

  it('exchange rate label contains default and alternate currency codes when both selected', async () => {
    renderForm()

    await waitFor(() => {
      expect(screen.getByLabelText('Alternate Currency')).toBeTruthy()
    })

    const altSelect = screen.getByLabelText('Alternate Currency') as HTMLSelectElement
    await selectValue(altSelect, USD_ID)

    await waitFor(() => {
      expect(screen.getByText('GTQ per 1 USD')).toBeTruthy()
    })
  })
})

describe('CycleForm — REQ-CYC-FORM-2: pair validation', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  async function fillRequiredFields() {
    await fireEvent.update(screen.getByLabelText('Name'), 'Test Cycle')
    await fireEvent.update(screen.getByLabelText('Start Date'), '2025-01-01')
    await fireEvent.update(screen.getByLabelText('End Date'), '2025-12-31')
  }

  function clickSave() {
    const buttons = document.querySelectorAll('button[type="submit"]')
    const submitBtn = buttons[0]
    return submitBtn ? fireEvent.click(submitBtn) : Promise.resolve()
  }

  it('shows pair validation error when only alternate currency is filled (no rate)', async () => {
    renderForm()

    await waitFor(() => {
      expect(screen.getByLabelText('Alternate Currency')).toBeTruthy()
    })

    await fillRequiredFields()

    const altSelect = screen.getByLabelText('Alternate Currency') as HTMLSelectElement
    await selectValue(altSelect, USD_ID)
    // exchange rate left empty (null)

    await clickSave()

    await waitFor(() => {
      expect(screen.getByText(/both alternate currency and exchange rate are required/i)).toBeTruthy()
    })
  })

  it('does not show pair validation error when both are filled', async () => {
    renderForm()

    await waitFor(() => {
      expect(screen.getByLabelText('Alternate Currency')).toBeTruthy()
    })

    await fillRequiredFields()

    const altSelect = screen.getByLabelText('Alternate Currency') as HTMLSelectElement
    await selectValue(altSelect, USD_ID)

    // Wait for the exchange rate input to appear, then fill it
    await waitFor(() => {
      const rateInput = document.querySelector('input[type="number"]')
      expect(rateInput).not.toBeNull()
    })

    const rateInput = document.querySelector('input[type="number"]') as HTMLInputElement
    await fireEvent.update(rateInput, '7.5')

    await clickSave()

    await waitFor(() => {
      expect(screen.queryByText(/both alternate currency and exchange rate are required/i)).toBeNull()
    })
  })

  it('does not show pair validation error when both are empty', async () => {
    renderForm()

    await waitFor(() => {
      expect(screen.getByLabelText('Name')).toBeTruthy()
    })

    await fillRequiredFields()
    // alternate currency = empty string, exchange rate = null

    await clickSave()

    await waitFor(() => {
      expect(screen.queryByText(/both alternate currency and exchange rate are required/i)).toBeNull()
    })
  })
})

import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import BankAccountForm from '../components/BankAccountForm.vue'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (k: string) => {
      const map: Record<string, string> = {
        'bankAccount.form.alias': 'Alias',
        'bankAccount.form.aliasPlaceholder': 'Account alias',
        'bankAccount.form.currency': 'Currency',
        'bankAccount.form.selectCurrency': '-- Select --',
        'bankAccount.form.isPositive': 'Adds to total',
        'bankAccount.form.isPositiveHint': 'Adds to balance',
        'bankAccount.form.isNegativeHint': 'Subtracts from balance',
        'bankAccount.form.displayOrder': 'Display Order',
        'bankAccount.validation.aliasRequired': 'Alias is required',
        'bankAccount.validation.aliasTooLong': 'Alias must be 100 characters or fewer',
        'bankAccount.validation.currencyRequired': 'Currency is required',
        'bankAccount.validation.displayOrderMin': 'Display order must be 0 or greater',
        'common.save': 'Save',
        'common.cancel': 'Cancel',
      }
      return map[k] ?? k
    },
  }),
}))

const CURRENCIES = [
  { id: 'gtq-id', code: 'GTQ', symbol: 'Q' },
  { id: 'usd-id', code: 'USD', symbol: '$' },
]

function renderForm(props: Record<string, unknown> = {}) {
  return render(BankAccountForm, {
    props: {
      currencies: CURRENCIES,
      ...props,
    },
  })
}

describe('BankAccountForm', () => {
  it('renders alias, currency, isPositive, and displayOrder fields', () => {
    renderForm()
    expect(screen.getByRole('textbox')).not.toBeNull()
    expect(screen.getByRole('combobox')).not.toBeNull()
    expect(screen.getByRole('checkbox')).not.toBeNull()
    expect(screen.getByRole('spinbutton')).not.toBeNull()
  })

  it('shows error when alias is empty on submit', async () => {
    renderForm()
    await fireEvent.submit(screen.getByRole('button', { name: 'Save' }).closest('form')!)
    expect(screen.queryByText('Alias is required')).not.toBeNull()
  })

  it('emits submit with correct payload when form is valid', async () => {
    const { emitted } = renderForm()

    await fireEvent.update(screen.getByRole('textbox'), 'Caja GTQ')
    await fireEvent.change(screen.getByRole('combobox'), { target: { value: 'gtq-id' } })
    await fireEvent.submit(screen.getByRole('button', { name: 'Save' }).closest('form')!)

    expect(emitted()['submit']).toBeTruthy()
    const payload = (emitted()['submit']![0] as [Record<string, unknown>])[0]
    expect(payload['alias']).toBe('Caja GTQ')
    expect(payload['currencyId']).toBe('gtq-id')
  })

  it('emits cancel when cancel button clicked', async () => {
    const { emitted } = renderForm()
    await fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(emitted()['cancel']).toBeTruthy()
  })

  it('disables currency select in edit mode', () => {
    renderForm({
      isEdit: true,
      initialValues: { alias: 'Test', currencyId: 'gtq-id', isPositive: true, displayOrder: 0 },
    })
    const select = screen.getByRole('combobox') as HTMLSelectElement
    expect(select.disabled).toBe(true)
  })
})

import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/vue'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        'dashboard.currencyMismatch.title': 'Currency mismatch',
        'dashboard.currencyMismatch.description':
          'These lines belong to Cycles with different currencies — they cannot be compared on one chart.',
      }
      return map[key] ?? key
    },
  }),
}))

import CurrencyMismatchWarning from '../components/CurrencyMismatchWarning.vue'

describe('CurrencyMismatchWarning (DASH-12)', () => {
  it('renders the mismatch title and explanation copy with an alert role', () => {
    render(CurrencyMismatchWarning)

    const alert = screen.getByRole('alert')
    expect(alert.textContent).toContain('Currency mismatch')
    expect(alert.textContent).toContain('These lines belong to Cycles with different currencies')
  })
})

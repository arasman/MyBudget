import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/vue'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        'dashboard.band.insufficientData.title': 'Not enough history yet',
        'dashboard.band.insufficientData.description': 'At least 2 periods are needed.',
      }
      return map[key] ?? key
    },
  }),
}))

import InsufficientDataState from '../components/InsufficientDataState.vue'

describe('InsufficientDataState (DASH-3)', () => {
  it('renders the not-enough-history title and description copy', () => {
    render(InsufficientDataState)

    expect(screen.queryByText('Not enough history yet')).not.toBeNull()
    expect(screen.queryByText('At least 2 periods are needed.')).not.toBeNull()
  })
})

// Decision 7: shared decorative backdrop layer used by PublicLayout + LandingView
import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/vue'
import PublicBackdrop from '../PublicBackdrop.vue'

describe('PublicBackdrop', () => {
  it('renders its default slot content', () => {
    render(PublicBackdrop, {
      slots: { default: '<p>Slot content</p>' },
    })
    expect(screen.getByText('Slot content')).toBeTruthy()
  })
})

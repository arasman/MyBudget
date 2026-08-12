// LAYOUT-4: global footer, plain text, no links
import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/vue'
import { createI18n } from 'vue-i18n'
import AppFooter from '../AppFooter.vue'

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: { footer: { poweredBy: 'Powered by ARAS Systems' } },
    },
  })
}

describe('AppFooter', () => {
  it('renders "© {currentYear} · Powered by ARAS Systems"', () => {
    render(AppFooter, { global: { plugins: [makeI18n()] } })
    const currentYear = new Date().getFullYear()
    expect(screen.getByText(`© ${currentYear} · Powered by ARAS Systems`)).toBeTruthy()
  })

  it('renders no anchor/link elements', () => {
    const { container } = render(AppFooter, { global: { plugins: [makeI18n()] } })
    expect(container.querySelectorAll('a')).toHaveLength(0)
  })
})

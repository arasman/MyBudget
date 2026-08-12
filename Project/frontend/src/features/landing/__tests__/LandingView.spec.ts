// LAYOUT-4 (footer visible on public view, including "/") + Decision 7/13:
// LandingView is anonymous-facing, so it wraps its content in the shared
// PublicBackdrop and mounts AppFooter directly (RootGate renders LandingView
// without going through PublicLayout — see design.md Decision 1).
// PR 4 (task 4.9) replaces only the inner stub content; this shell wiring must
// keep working unchanged.
import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/vue'
import { createI18n } from 'vue-i18n'
import LandingView from '../views/LandingView.vue'

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        common: { appName: 'MyBudget' },
        footer: { poweredBy: 'Powered by ARAS Systems' },
      },
    },
  })
}

describe('LandingView', () => {
  it('renders AppFooter for anonymous visitors at "/"', () => {
    render(LandingView, { global: { plugins: [makeI18n()] } })

    const currentYear = new Date().getFullYear()
    expect(screen.getByText(`© ${currentYear} · Powered by ARAS Systems`)).toBeTruthy()
  })

  it('keeps the stub content visible alongside the footer', () => {
    render(LandingView, { global: { plugins: [makeI18n()] } })

    expect(screen.getByTestId('landing-view')).toBeTruthy()
    expect(screen.getByText('MyBudget')).toBeTruthy()
  })
})

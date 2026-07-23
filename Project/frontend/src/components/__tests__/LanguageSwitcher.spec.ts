// REQ-LSYNC-2: PATCH called on authenticated switch
// REQ-LSYNC-5: LanguageSwitcher must have a non-empty aria-label attribute
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import { createI18n } from 'vue-i18n'
import { createPinia, setActivePinia } from 'pinia'

const { mockPatch, mockSetLocale, mockIsAuthenticated } = vi.hoisted(() => ({
  mockPatch: vi.fn().mockResolvedValue({ status: 204 }),
  mockSetLocale: vi.fn(),
  mockIsAuthenticated: vi.fn().mockReturnValue(false),
}))

vi.mock('@/api/axios', () => ({
  default: {
    defaults: { headers: { common: {} } },
    patch: mockPatch,
  },
}))

vi.mock('@/stores/locale.store', () => ({
  useLocaleStore: vi.fn(() => ({
    locale: 'en',
    setLocale: mockSetLocale,
  })),
}))

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: vi.fn(() => ({
    get isAuthenticated() {
      return mockIsAuthenticated()
    },
  })),
}))

import LanguageSwitcher from '../LanguageSwitcher.vue'

function makeI18n(locale: 'en' | 'es' = 'en') {
  return createI18n({
    legacy: false,
    locale,
    messages: {
      en: { common: { switchLanguage: 'Switch language' } },
      es: { common: { switchLanguage: 'Cambiar idioma' } },
    },
  })
}

describe('LanguageSwitcher', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    setActivePinia(createPinia())
  })

  it('has a non-empty aria-label attribute on the wrapper', () => {
    const { container } = render(LanguageSwitcher, {
      global: { plugins: [makeI18n('en')] },
    })
    const wrapper = container.firstElementChild as HTMLElement
    const ariaLabel = wrapper?.getAttribute('aria-label')
    expect(ariaLabel).toBeTruthy()
    expect(ariaLabel!.length).toBeGreaterThan(0)
  })

  it('aria-label reflects the active locale translation', () => {
    const i18n = makeI18n('es')
    i18n.global.locale.value = 'es'
    const { container } = render(LanguageSwitcher, {
      global: { plugins: [i18n] },
    })
    const wrapper = container.firstElementChild as HTMLElement
    expect(wrapper?.getAttribute('aria-label')).toBe('Cambiar idioma')
  })

  it('calls PATCH when authenticated and user switches locale', async () => {
    mockIsAuthenticated.mockReturnValue(true)
    render(LanguageSwitcher, {
      global: { plugins: [makeI18n()] },
    })
    await fireEvent.click(screen.getByText('ES'))
    expect(mockSetLocale).toHaveBeenCalledWith('es')
    expect(mockPatch).toHaveBeenCalledWith('/api/auth/me/locale', { locale: 'es' })
  })

  it('does NOT call PATCH when unauthenticated', async () => {
    mockIsAuthenticated.mockReturnValue(false)
    render(LanguageSwitcher, {
      global: { plugins: [makeI18n()] },
    })
    await fireEvent.click(screen.getByText('ES'))
    expect(mockSetLocale).toHaveBeenCalledWith('es')
    expect(mockPatch).not.toHaveBeenCalled()
  })
})

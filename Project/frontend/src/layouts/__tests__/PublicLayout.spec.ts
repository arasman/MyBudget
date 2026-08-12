// LAYOUT-2: PublicLayout is the parent shell for /login, /register,
// /forgot-password, /reset-password, /invitations/accept. It must render a
// centered card container (no authenticated navbar), a shared PublicBackdrop
// behind its content, a header bar with LanguageSwitcher, and the global
// AppFooter (LAYOUT-4).
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import PublicLayout from '../PublicLayout.vue'

const { mockSetLocale, mockIsAuthenticated } = vi.hoisted(() => ({
  mockSetLocale: vi.fn(),
  mockIsAuthenticated: vi.fn().mockReturnValue(false),
}))

vi.mock('@/api/axios', () => ({
  default: {
    defaults: { headers: { common: {} } },
    patch: vi.fn().mockResolvedValue({ status: 204 }),
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

function makeRouter() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'Login', component: { template: '<div data-testid="login-stub">Login form</div>' } },
    ],
  })
  return router
}

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        common: { switchLanguage: 'Switch language', appName: 'MyBudget' },
        footer: { poweredBy: 'Powered by ARAS Systems' },
      },
    },
  })
}

describe('PublicLayout', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockIsAuthenticated.mockReturnValue(false)
  })

  async function renderLayout() {
    const router = makeRouter()
    await router.push('/')
    await router.isReady()

    return render(PublicLayout, {
      global: {
        plugins: [router, makeI18n()],
      },
    })
  }

  it('renders a centered card container without an authenticated navbar', async () => {
    const { container } = await renderLayout()

    const card = container.querySelector('.card')
    expect(card).toBeTruthy()
    expect(card?.className).toContain('max-w-md')
    expect(container.querySelector('nav')).toBeNull()
  })

  it('renders the routed content inside the card body', async () => {
    await renderLayout()
    expect(screen.getByTestId('login-stub')).toBeTruthy()
  })

  it('renders LanguageSwitcher in the header bar', async () => {
    await renderLayout()
    expect(screen.getByText('EN')).toBeTruthy()
    expect(screen.getByText('ES')).toBeTruthy()
  })

  it('renders a MyBudget brand link in the header bar pointing to the landing page', async () => {
    await renderLayout()
    const brandLink = screen.getByRole('link', { name: 'MyBudget' })
    expect(brandLink).toBeTruthy()
    expect(brandLink.getAttribute('href')).toBe('/')
  })

  it('renders PublicBackdrop behind the content', async () => {
    const { container } = await renderLayout()
    expect(container.querySelector('[aria-hidden="true"]')).toBeTruthy()
  })

  it('renders AppFooter', async () => {
    await renderLayout()
    const currentYear = new Date().getFullYear()
    expect(screen.getByText(`© ${currentYear} · Powered by ARAS Systems`)).toBeTruthy()
  })
})

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import ForgotPasswordView from '@/views/ForgotPasswordView.vue'

const mockRequestPasswordReset = vi.fn()

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: () => ({
    requestPasswordReset: mockRequestPasswordReset,
    isAuthenticated: false,
    accessToken: null,
    user: null,
  }),
}))

const messages = {
  en: {
    'auth.password.forgotTitle': 'Forgot your password?',
    'auth.password.forgotDescription': "Enter your email and we'll send you a reset link.",
    'auth.password.forceChangeNotice': 'Your password has expired and must be changed.',
    'auth.password.emailLabel': 'Email address',
    'auth.password.sendLink': 'Send reset link',
    'auth.password.linkSent': 'If your email is registered, a reset link has been sent. Check your inbox.',
    'auth.login.submit': 'Sign In',
  },
}

async function buildGlobals(path = '/forgot-password') {
  const pinia = createPinia()
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/forgot-password', component: ForgotPasswordView },
      { path: '/login', component: { template: '<div>Login</div>' } },
    ],
  })
  await router.push(path)
  await router.isReady()
  const i18n = createI18n({ legacy: false, locale: 'en', messages })
  return { global: { plugins: [pinia, router, i18n] } }
}

describe('ForgotPasswordView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('renders the email form', async () => {
    const globals = await buildGlobals()
    render(ForgotPasswordView, globals)
    expect(screen.getByText('Forgot your password?')).toBeTruthy()
    expect(screen.getByText('Send reset link')).toBeTruthy()
    expect(document.querySelector('input[type="email"]')).toBeTruthy()
  })

  it('shows success state after submit (regardless of outcome)', async () => {
    mockRequestPasswordReset.mockResolvedValue(undefined)
    const globals = await buildGlobals()
    render(ForgotPasswordView, globals)

    const emailInput = document.querySelector('input[type="email"]') as HTMLInputElement
    await fireEvent.update(emailInput, 'user@example.com')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('If your email is registered, a reset link has been sent. Check your inbox.')).toBeTruthy()
    })
  })

  it('shows success state even when request throws', async () => {
    mockRequestPasswordReset.mockRejectedValue(new Error('Network error'))
    const globals = await buildGlobals()
    render(ForgotPasswordView, globals)

    const emailInput = document.querySelector('input[type="email"]') as HTMLInputElement
    await fireEvent.update(emailInput, 'unknown@example.com')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('If your email is registered, a reset link has been sent. Check your inbox.')).toBeTruthy()
    })
  })

  it('shows force-change banner when ?reason=force is present', async () => {
    const globals = await buildGlobals('/forgot-password?reason=force')
    render(ForgotPasswordView, globals)
    await waitFor(() => {
      expect(screen.getByText('Your password has expired and must be changed.')).toBeTruthy()
    })
  })
})

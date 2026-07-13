import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import ResetPasswordView from '@/views/ResetPasswordView.vue'

const mockResetPassword = vi.fn()

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: () => ({
    resetPassword: mockResetPassword,
    isAuthenticated: false,
    accessToken: null,
    user: null,
  }),
}))

const messages = {
  en: {
    'auth.password.resetTitle': 'Reset your password',
    'auth.password.newPasswordLabel': 'New password',
    'auth.password.confirmPasswordLabel': 'Confirm new password',
    'auth.password.resetSubmit': 'Reset password',
    'auth.password.resetSuccess': 'Password reset successfully. You can now log in.',
    'auth.password.tokenInvalid': 'This reset link is invalid or has expired.',
    'auth.password.passwordMismatch': 'Passwords do not match.',
    'auth.password.passwordTooShort': 'Password must be at least 8 characters.',
    'auth.password.sendLink': 'Send reset link',
    'auth.login.submit': 'Sign In',
  },
}

function buildGlobals(path = '/reset-password?token=abc123') {
  const pinia = createPinia()
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/reset-password', component: ResetPasswordView },
      { path: '/forgot-password', component: { template: '<div>Forgot</div>' } },
      { path: '/login', component: { template: '<div>Login</div>' } },
    ],
  })
  router.push(path)
  const i18n = createI18n({ legacy: false, locale: 'en', messages })
  return { global: { plugins: [pinia, router, i18n] } }
}

describe('ResetPasswordView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('renders the reset form with token from query param', async () => {
    render(ResetPasswordView, buildGlobals())
    await waitFor(() => {
      expect(screen.getByText('Reset your password')).toBeTruthy()
      expect(screen.getByText('Reset password')).toBeTruthy()
    })
    const passwordInputs = document.querySelectorAll('input[type="password"]')
    expect(passwordInputs.length).toBe(2)
  })

  it('shows password mismatch error when passwords do not match', async () => {
    render(ResetPasswordView, buildGlobals())
    const [newPwdInput, confirmPwdInput] = document.querySelectorAll('input[type="password"]')

    await fireEvent.update(newPwdInput as HTMLInputElement, 'Password1!')
    await fireEvent.update(confirmPwdInput as HTMLInputElement, 'DifferentPass1!')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('Passwords do not match.')).toBeTruthy()
    })
    expect(mockResetPassword).not.toHaveBeenCalled()
  })

  it('shows password too short error', async () => {
    render(ResetPasswordView, buildGlobals())
    const [newPwdInput, confirmPwdInput] = document.querySelectorAll('input[type="password"]')

    await fireEvent.update(newPwdInput as HTMLInputElement, 'short')
    await fireEvent.update(confirmPwdInput as HTMLInputElement, 'short')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('Password must be at least 8 characters.')).toBeTruthy()
    })
  })

  it('shows success message on valid submit', async () => {
    mockResetPassword.mockResolvedValue(undefined)
    render(ResetPasswordView, buildGlobals())
    const [newPwdInput, confirmPwdInput] = document.querySelectorAll('input[type="password"]')

    await fireEvent.update(newPwdInput as HTMLInputElement, 'ValidPass1!')
    await fireEvent.update(confirmPwdInput as HTMLInputElement, 'ValidPass1!')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('Password reset successfully. You can now log in.')).toBeTruthy()
    })
  })

  it('shows token-invalid error on PWD_TOKEN_INVALID response', async () => {
    mockResetPassword.mockRejectedValue({
      response: { data: { detail: 'PWD_TOKEN_INVALID' } },
    })
    render(ResetPasswordView, buildGlobals())
    const [newPwdInput, confirmPwdInput] = document.querySelectorAll('input[type="password"]')

    await fireEvent.update(newPwdInput as HTMLInputElement, 'ValidPass1!')
    await fireEvent.update(confirmPwdInput as HTMLInputElement, 'ValidPass1!')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('This reset link is invalid or has expired.')).toBeTruthy()
    })
  })

  it('shows token-invalid error on PWD_TOKEN_EXPIRED response', async () => {
    mockResetPassword.mockRejectedValue({
      response: { data: { detail: 'PWD_TOKEN_EXPIRED' } },
    })
    render(ResetPasswordView, buildGlobals())
    const [newPwdInput, confirmPwdInput] = document.querySelectorAll('input[type="password"]')

    await fireEvent.update(newPwdInput as HTMLInputElement, 'ValidPass1!')
    await fireEvent.update(confirmPwdInput as HTMLInputElement, 'ValidPass1!')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('This reset link is invalid or has expired.')).toBeTruthy()
    })
  })
})

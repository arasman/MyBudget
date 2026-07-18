import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import ChangePasswordModal from '@/components/auth/ChangePasswordModal.vue'

const mockChangePassword = vi.fn()
const mockPush = vi.fn()

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: () => ({
    changePassword: mockChangePassword,
    isAuthenticated: true,
    accessToken: 'token',
    user: { id: '1', email: 'user@example.com', firstName: 'User', lastName: 'Test', preferredLocale: 'en', memberships: [] },
  }),
}))

vi.mock('@/stores/toast.store', () => ({
  useToastStore: () => ({
    push: mockPush,
  }),
}))

const i18nMessages = {
  en: {
    'auth.password.changeTitle':          'Change your password',
    'auth.password.currentPasswordLabel': 'Current password',
    'auth.password.newPasswordLabel':     'New password',
    'auth.password.confirmPasswordLabel': 'Confirm new password',
    'auth.password.changeSubmit':         'Change password',
    'auth.password.changeSuccess':        'Password changed successfully.',
    'auth.password.currentIncorrect':     'Current password is incorrect.',
    'auth.password.passwordMismatch':     'Passwords do not match.',
    'auth.password.passwordTooShort':     'Password must be at least 8 characters.',
    'common.cancel':                      'Cancel',
    'common.error':                       'An error occurred',
  },
}

function buildGlobals() {
  const pinia = createPinia()
  const i18n  = createI18n({ legacy: false, locale: 'en', messages: i18nMessages })
  return { global: { plugins: [pinia, i18n] } }
}

function renderModal() {
  // jsdom does not implement HTMLDialogElement — patch to make it visible
  if (!HTMLDialogElement.prototype.showModal) {
    HTMLDialogElement.prototype.showModal = function () {
      this.setAttribute('open', '')
    }
  }
  if (!HTMLDialogElement.prototype.close) {
    HTMLDialogElement.prototype.close = function () {
      this.removeAttribute('open')
    }
  }

  const wrapper = render(ChangePasswordModal, buildGlobals())
  // Simulate opening the modal
  const dialog = document.querySelector('dialog')!
  dialog.setAttribute('open', '')
  return wrapper
}

describe('ChangePasswordModal', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('renders all password fields and submit button', () => {
    renderModal()
    expect(screen.getByText('Change your password')).toBeTruthy()
    const passwordInputs = document.querySelectorAll('input[type="password"]')
    expect(passwordInputs.length).toBe(3)
    expect(screen.getByText('Change password')).toBeTruthy()
  })

  it('shows currentIncorrect error when PWD_CURRENT_INCORRECT is returned', async () => {
    mockChangePassword.mockRejectedValue({
      response: { data: { detail: 'PWD_CURRENT_INCORRECT' } },
    })

    renderModal()
    const [currentPwdInput, newPwdInput, confirmPwdInput] = document.querySelectorAll('input[type="password"]')

    await fireEvent.update(currentPwdInput as HTMLInputElement, 'WrongCurrent1!')
    await fireEvent.update(newPwdInput as HTMLInputElement, 'NewPassword1!')
    await fireEvent.update(confirmPwdInput as HTMLInputElement, 'NewPassword1!')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('Current password is incorrect.')).toBeTruthy()
    })
  })

  it('shows password mismatch error when new passwords do not match', async () => {
    renderModal()
    const [currentPwdInput, newPwdInput, confirmPwdInput] = document.querySelectorAll('input[type="password"]')

    await fireEvent.update(currentPwdInput as HTMLInputElement, 'Current1!')
    await fireEvent.update(newPwdInput as HTMLInputElement, 'NewPassword1!')
    await fireEvent.update(confirmPwdInput as HTMLInputElement, 'DifferentPass1!')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('Passwords do not match.')).toBeTruthy()
    })
    expect(mockChangePassword).not.toHaveBeenCalled()
  })

  it('calls changePassword and pushes notification on success', async () => {
    mockChangePassword.mockResolvedValue(undefined)

    renderModal()
    const [currentPwdInput, newPwdInput, confirmPwdInput] = document.querySelectorAll('input[type="password"]')

    await fireEvent.update(currentPwdInput as HTMLInputElement, 'Current1!')
    await fireEvent.update(newPwdInput as HTMLInputElement, 'NewPassword1!')
    await fireEvent.update(confirmPwdInput as HTMLInputElement, 'NewPassword1!')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(mockChangePassword).toHaveBeenCalledWith('Current1!', 'NewPassword1!')
      expect(mockPush).toHaveBeenCalledWith(expect.objectContaining({
        type: 'success',
        title: 'Password changed successfully.',
      }))
    })
  })
})

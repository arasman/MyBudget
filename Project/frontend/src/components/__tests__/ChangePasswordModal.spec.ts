// REQ-TOAST-NOTIFICATION-MIGRATION
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({ t: (k: string) => k }),
}))

const { mockToastPush, mockChangePassword } = vi.hoisted(() => ({
  mockToastPush: vi.fn(),
  mockChangePassword: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: () => ({ changePassword: mockChangePassword }),
}))

vi.mock('@/stores/toast.store', () => ({
  useToastStore: () => ({ push: mockToastPush }),
}))

// Ensure notification store is NOT used — if it were imported the mock would catch it
vi.mock('@/stores/notification.store', () => ({
  useNotificationStore: vi.fn(() => {
    throw new Error('useNotificationStore must not be called by ChangePasswordModal')
  }),
}))

import ChangePasswordModal from '../auth/ChangePasswordModal.vue'

function renderModal() {
  const result = render(ChangePasswordModal, {
    global: { plugins: [createPinia()] },
  })
  // Expose the modal dialog
  result.baseElement.querySelector('dialog')?.setAttribute('open', '')
  return result
}

describe('ChangePasswordModal — uses toastStore, not notificationStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockChangePassword.mockResolvedValue(undefined)
  })

  it('calls toast.push with changeSuccess on successful password change', async () => {
    const { getByRole } = renderModal()

    // Fill in the form fields
    const inputs = document.querySelectorAll('input[type="password"]')
    await fireEvent.input(inputs[0]!, { target: { value: 'OldPass123' } })
    await fireEvent.input(inputs[1]!, { target: { value: 'NewPass456!' } })
    await fireEvent.input(inputs[2]!, { target: { value: 'NewPass456!' } })

    const submitBtn = getByRole('button', { name: 'auth.password.changeSubmit' })
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(mockChangePassword).toHaveBeenCalledWith('OldPass123', 'NewPass456!')
      expect(mockToastPush).toHaveBeenCalledWith({
        type: 'success',
        title: 'auth.password.changeSuccess',
      })
    })
  })

  it('does NOT call toast.push when changePassword rejects', async () => {
    mockChangePassword.mockRejectedValueOnce({
      response: { data: { detail: 'PWD_CURRENT_INCORRECT' } },
    })

    renderModal()

    const inputs = document.querySelectorAll('input[type="password"]')
    await fireEvent.input(inputs[0]!, { target: { value: 'WrongPass' } })
    await fireEvent.input(inputs[1]!, { target: { value: 'NewPass456!' } })
    await fireEvent.input(inputs[2]!, { target: { value: 'NewPass456!' } })

    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(mockChangePassword).toHaveBeenCalled()
    })

    expect(mockToastPush).not.toHaveBeenCalled()
  })

  it('toast.push is called with no message property (toastStore pattern)', async () => {
    renderModal()

    const inputs = document.querySelectorAll('input[type="password"]')
    await fireEvent.input(inputs[0]!, { target: { value: 'OldPass123' } })
    await fireEvent.input(inputs[1]!, { target: { value: 'NewPass456!' } })
    await fireEvent.input(inputs[2]!, { target: { value: 'NewPass456!' } })

    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(mockToastPush).toHaveBeenCalled()
    })

    const call = mockToastPush.mock.calls[0][0]
    expect(call).not.toHaveProperty('message')
  })
})

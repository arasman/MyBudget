import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import InviteUserModal from '@/components/budget/InviteUserModal.vue'

const { mockPost } = vi.hoisted(() => ({ mockPost: vi.fn() }))

vi.mock('@/api/axios', () => ({
  default: { post: mockPost },
}))

const i18nMessages = {
  en: {
    'invitation.modal.title':               'Invite a User',
    'invitation.modal.emailLabel':          'Email address',
    'invitation.modal.roleLabel':           'Role',
    'invitation.modal.submit':              'Send Invitation',
    'invitation.modal.successMessage':      'Invitation sent successfully.',
    'invitation.modal.error.alreadyMember': 'This user is already a member of this budget.',
    'common.cancel':                        'Cancel',
    'common.error':                         'An error occurred',
  },
}

function buildGlobals() {
  const pinia = createPinia()
  const i18n  = createI18n({ legacy: false, locale: 'en', messages: i18nMessages })
  return { global: { plugins: [pinia, i18n] } }
}

/** Render the modal with dialog patched open so Testing Library can find elements. */
function renderModal(budgetId = 'budget-id-123') {
  // jsdom does not implement HTMLDialogElement — patch to make it a no-op
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

  return render(InviteUserModal, {
    props: { budgetId },
    ...buildGlobals(),
  })
}

describe('InviteUserModal', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('renders the invite form with email and role fields', () => {
    renderModal()
    // Title and labels are always in the DOM (dialog is always rendered by Vue)
    expect(screen.getByText('Invite a User')).toBeTruthy()
    expect(screen.getByText('Email address')).toBeTruthy()
    expect(screen.getByText('Role')).toBeTruthy()
    expect(screen.getByText('Send Invitation')).toBeTruthy()
  })

  it('shows Zod validation error for invalid email without hitting API', async () => {
    renderModal()

    const emailInput = document.querySelector('input[type="email"]') as HTMLInputElement
    await fireEvent.update(emailInput, 'not-an-email')

    const form = document.querySelector('form:not([method])')!
    await fireEvent.submit(form)

    await waitFor(() => {
      const errors = document.querySelectorAll('.label-text-alt.text-error')
      expect(errors.length).toBeGreaterThan(0)
    })
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('calls POST /api/budgets/{id}/invitations with correct payload', async () => {
    mockPost.mockResolvedValue({ data: {} })
    renderModal('my-budget-id')

    const emailInput = document.querySelector('input[type="email"]') as HTMLInputElement
    await fireEvent.update(emailInput, 'invitee@example.com')

    const form = document.querySelector('form:not([method])')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith(
        '/api/budgets/my-budget-id/invitations',
        expect.objectContaining({ email: 'invitee@example.com', role: 'operator' }),
      )
    })
  })

  it('shows success message after successful invite', async () => {
    mockPost.mockResolvedValue({ data: {} })
    renderModal()

    const emailInput = document.querySelector('input[type="email"]') as HTMLInputElement
    await fireEvent.update(emailInput, 'success@example.com')

    const form = document.querySelector('form:not([method])')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('Invitation sent successfully.')).toBeTruthy()
    })
  })

  it('emits "invited" event on successful invite', async () => {
    mockPost.mockResolvedValue({ data: {} })
    const wrapper = renderModal()

    const emailInput = document.querySelector('input[type="email"]') as HTMLInputElement
    await fireEvent.update(emailInput, 'emit@example.com')

    const form = document.querySelector('form:not([method])')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(wrapper.emitted('invited')).toBeTruthy()
    })
  })

  it('shows already-member error when server returns AUTH_ALREADY_MEMBER', async () => {
    mockPost.mockRejectedValue({
      response: { status: 409, data: { error: 'AUTH_ALREADY_MEMBER' } },
    })
    renderModal()

    const emailInput = document.querySelector('input[type="email"]') as HTMLInputElement
    await fireEvent.update(emailInput, 'member@example.com')

    const form = document.querySelector('form:not([method])')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('This user is already a member of this budget.')).toBeTruthy()
    })
  })

  it('shows generic error on 500', async () => {
    mockPost.mockRejectedValue({ response: { status: 500 } })
    renderModal()

    const emailInput = document.querySelector('input[type="email"]') as HTMLInputElement
    await fireEvent.update(emailInput, 'error@example.com')

    const form = document.querySelector('form:not([method])')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('An error occurred')).toBeTruthy()
    })
  })
})

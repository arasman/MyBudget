import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createWebHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import AcceptInvitationView from '@/views/AcceptInvitationView.vue'

// Hoist mutable state so vi.mock factories can reference it safely
const { mockPost, mockAuth, mockFetchMe } = vi.hoisted(() => {
  const mockPost = vi.fn()
  const mockAuth = { isAuthenticated: true }
  const mockFetchMe = vi.fn().mockResolvedValue(undefined)
  return { mockPost, mockAuth, mockFetchMe }
})

vi.mock('@/api/axios', () => ({
  default: { post: mockPost },
}))

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: () => ({
    get isAuthenticated() { return mockAuth.isAuthenticated },
    fetchMe: mockFetchMe,
  }),
}))

const i18nMessages = {
  en: {
    'invitation.accept.title':             'Accept Budget Invitation',
    'invitation.accept.loading':           'Processing your invitation...',
    'invitation.accept.successMessage':    'You have successfully joined the budget.',
    'invitation.accept.error.expired':     'This invitation has expired. Please request a new one.',
    'invitation.accept.error.alreadyUsed': 'This invitation has already been used.',
    'invitation.accept.error.mismatch':    'This invitation was not sent to your email address.',
    'invitation.accept.error.alreadyMember': 'You are already a member of this budget.',
    'common.error':                        'An error occurred',
  },
}

/**
 * Build router + i18n, navigate to the given URL, and wait until navigation completes.
 * Returns { router, globals } so tests can also inspect router state.
 */
async function setup(url: string) {
  const pinia  = createPinia()
  const router = createRouter({
    history: createWebHistory(),
    routes: [
      { path: '/',                   component: { template: '<div>Home</div>' } },
      { path: '/login',              component: { template: '<div>Login</div>' } },
      { path: '/invitations/accept', component: AcceptInvitationView },
    ],
  })
  await router.push(url)
  await router.isReady()

  const i18n = createI18n({ legacy: false, locale: 'en', messages: i18nMessages })

  return {
    router,
    globals: { global: { plugins: [pinia, router, i18n] } },
  }
}

describe('AcceptInvitationView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    mockAuth.isAuthenticated = true
    vi.clearAllMocks()
  })

  it('shows success message when token is valid and user is authenticated', async () => {
    mockPost.mockResolvedValue({ data: { budgetId: 'budget-uuid', role: 'operator' } })

    const { globals } = await setup('/invitations/accept?token=valid-token')
    render(AcceptInvitationView, globals)

    await waitFor(() => {
      expect(screen.getByText('You have successfully joined the budget.')).toBeTruthy()
    })
    expect(mockPost).toHaveBeenCalledWith(
      '/api/auth/invitations/accept',
      { token: 'valid-token' },
    )
  })

  it('refreshes the auth store profile after a successful accept, so the new membership/role is available immediately', async () => {
    mockPost.mockResolvedValue({ data: { budgetId: 'budget-uuid', role: 'admin' } })

    const { globals } = await setup('/invitations/accept?token=valid-token')
    render(AcceptInvitationView, globals)

    await waitFor(() => {
      expect(screen.getByText('You have successfully joined the budget.')).toBeTruthy()
    })
    expect(mockFetchMe).toHaveBeenCalled()
  })

  it('shows expired error when server returns AUTH_INVITATION_EXPIRED', async () => {
    mockPost.mockRejectedValue({
      response: { status: 410, data: { detail: 'AUTH_INVITATION_EXPIRED' } },
    })

    const { globals } = await setup('/invitations/accept?token=expired-token')
    render(AcceptInvitationView, globals)

    await waitFor(() => {
      expect(
        screen.getByText('This invitation has expired. Please request a new one.'),
      ).toBeTruthy()
    })
  })

  it('shows already-used error when server returns AUTH_INVITATION_ALREADY_USED', async () => {
    mockPost.mockRejectedValue({
      response: { status: 409, data: { detail: 'AUTH_INVITATION_ALREADY_USED' } },
    })

    const { globals } = await setup('/invitations/accept?token=used-token')
    render(AcceptInvitationView, globals)

    await waitFor(() => {
      expect(screen.getByText('This invitation has already been used.')).toBeTruthy()
    })
  })

  it('shows mismatch error when server returns AUTH_INVITATION_EMAIL_MISMATCH', async () => {
    mockPost.mockRejectedValue({
      response: { status: 422, data: { detail: 'AUTH_INVITATION_EMAIL_MISMATCH' } },
    })

    const { globals } = await setup('/invitations/accept?token=wrong-email-token')
    render(AcceptInvitationView, globals)

    await waitFor(() => {
      expect(
        screen.getByText('This invitation was not sent to your email address.'),
      ).toBeTruthy()
    })
  })

  it('shows already-member error when server returns AUTH_ALREADY_MEMBER', async () => {
    mockPost.mockRejectedValue({
      response: { status: 409, data: { detail: 'AUTH_ALREADY_MEMBER' } },
    })

    const { globals } = await setup('/invitations/accept?token=already-member-token')
    render(AcceptInvitationView, globals)

    await waitFor(() => {
      expect(
        screen.getByText('You are already a member of this budget.'),
      ).toBeTruthy()
    })
  })

  it('shows generic error on unexpected server failure', async () => {
    mockPost.mockRejectedValue({ response: { status: 500 } })

    const { globals } = await setup('/invitations/accept?token=any-token')
    render(AcceptInvitationView, globals)

    await waitFor(() => {
      expect(screen.getByText('An error occurred')).toBeTruthy()
    })
  })

  it('shows generic error when no token is present in query', async () => {
    const { globals } = await setup('/invitations/accept')
    render(AcceptInvitationView, globals)

    await waitFor(() => {
      expect(screen.getByText('An error occurred')).toBeTruthy()
    })
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('redirects unauthenticated user to /login with redirect param', async () => {
    mockAuth.isAuthenticated = false

    const { router, globals } = await setup('/invitations/accept?token=secret-token')
    render(AcceptInvitationView, globals)

    await waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/login')
    })
    expect(router.currentRoute.value.query['redirect']).toContain('secret-token')
    expect(mockPost).not.toHaveBeenCalled()
  })
})

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { RouterView } from 'vue-router'
import { i18n } from '@/i18n'

// ── Hoisted mocks ────────────────────────────────────────────────────────────
// Hoisted so vi.mock factories below can reference them (Vitest hoists vi.mock calls).
const { mockFetchMe, mockClearSession, mockLogout, mockHttpGet, mockHttpPost, mockHttpPut, mockHttpPatch, mockHttpDelete } =
  vi.hoisted(() => ({
    mockFetchMe: vi.fn(),
    mockClearSession: vi.fn(),
    mockLogout: vi.fn().mockResolvedValue(undefined),
    mockHttpGet: vi.fn(),
    mockHttpPost: vi.fn(),
    mockHttpPut: vi.fn(),
    mockHttpPatch: vi.fn(),
    mockHttpDelete: vi.fn(),
  }))

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: vi.fn(),
}))

vi.mock('@/stores/layout.store', () => ({
  useLayoutStore: vi.fn(),
}))

vi.mock('@/stores/notification.store', () => ({
  useNotificationStore: vi.fn(),
}))

vi.mock('@/stores/toast.store', () => ({
  useToastStore: vi.fn(),
}))

vi.mock('@/stores/locale.store', () => ({
  useLocaleStore: vi.fn(() => ({ locale: 'en', setLocale: vi.fn() })),
}))

// Blocks every real /api/* call. Any test that expects "zero API calls" asserts
// these were never invoked — this is the network-log proxy for that assertion.
vi.mock('@/api/axios', () => ({
  default: {
    get: mockHttpGet,
    post: mockHttpPost,
    put: mockHttpPut,
    patch: mockHttpPatch,
    delete: mockHttpDelete,
    interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
  },
}))

import { useAuthStore } from '@/stores/auth.store'
import { useLayoutStore } from '@/stores/layout.store'
import { useNotificationStore } from '@/stores/notification.store'
import { useToastStore } from '@/stores/toast.store'
import { router } from '../index'
import RootGate from '@/layouts/RootGate.vue'

interface MembershipInput {
  budgetId: string
  budgetName: string
  role: string
  isDeleted: boolean
}

interface AuthUserInput {
  id: string
  email: string
  firstName: string
  lastName: string
  preferredLocale: string
  memberships: MembershipInput[]
}

interface AuthMockOptions {
  isAuthenticated?: boolean
  user?: AuthUserInput | null
  forcePasswordChange?: boolean
  fetchMeImpl?: () => Promise<void>
}

function setupAuthMock(options: AuthMockOptions = {}) {
  const { isAuthenticated = false, user = null, forcePasswordChange = false, fetchMeImpl } = options

  mockFetchMe.mockReset()
  if (fetchMeImpl) {
    mockFetchMe.mockImplementation(fetchMeImpl)
  } else {
    mockFetchMe.mockResolvedValue(undefined)
  }

  vi.mocked(useAuthStore).mockReturnValue({
    isAuthenticated,
    user,
    forcePasswordChange,
    fetchMe: mockFetchMe,
    clearSession: mockClearSession,
    logout: mockLogout,
  } as unknown as ReturnType<typeof useAuthStore>)
}

function setupLayoutMock() {
  vi.mocked(useLayoutStore).mockReturnValue({
    activeBudgetId: null,
    activeBudgetName: null,
    pageActions: [],
    setPageActions: vi.fn(),
    clearPageActions: vi.fn(),
    setActiveBudget: vi.fn(),
    clearActiveBudget: vi.fn(),
  } as unknown as ReturnType<typeof useLayoutStore>)
}

function setupNotificationMock() {
  vi.mocked(useNotificationStore).mockReturnValue({
    notifications: [],
    unreadCount: 0,
    push: vi.fn(),
    markRead: vi.fn(),
    markAllRead: vi.fn(),
    remove: vi.fn(),
  } as unknown as ReturnType<typeof useNotificationStore>)
}

function setupToastMock() {
  vi.mocked(useToastStore).mockReturnValue({
    toasts: [],
    push: vi.fn(),
    dismiss: vi.fn(),
  } as unknown as ReturnType<typeof useToastStore>)
}

describe('router — root gate and auth boundary (PR 1, behavioral-risk slice)', () => {
  beforeEach(async () => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    // Vue Router 4 short-circuits push() to an already-current route as a
    // NavigationDuplicated no-op and never re-runs beforeEach guards. The router
    // is a module-level singleton shared across tests in this file, so reset to a
    // neutral route first — otherwise a test that leaves currentRoute at "/" would
    // silently swallow the next test's `router.push('/')` and its guard never runs.
    await router.push('/login')
  })

  describe('anonymous visitor', () => {
    it('at / resolves with no redirect and zero /api/* calls (LANDING-1, LAYOUT-3)', async () => {
      setupAuthMock({ isAuthenticated: false })

      await router.push('/')
      await router.isReady()

      expect(router.currentRoute.value.fullPath).toBe('/')
      expect(router.currentRoute.value.name).toBe('BudgetSelection')
      expect(router.currentRoute.value.matched[0]?.components?.['default']).toBe(RootGate)

      // Network-log proxy: neither the guard nor the landing branch issued a single call.
      expect(mockFetchMe).not.toHaveBeenCalled()
      expect(mockHttpGet).not.toHaveBeenCalled()
      expect(mockHttpPost).not.toHaveBeenCalled()
      expect(mockHttpPut).not.toHaveBeenCalled()
      expect(mockHttpPatch).not.toHaveBeenCalled()
      expect(mockHttpDelete).not.toHaveBeenCalled()
    })

    it.each([
      ['/budgets/b-1/cycles'],
      ['/budgets/b-1/cycles/c-1'],
      ['/budgets/b-1/categories'],
      ['/budgets/b-1/lines'],
      ['/budgets/b-1/lines/l-1/customizations'],
      ['/budgets/b-1/cycles/c-1/matrix'],
      ['/budgets/b-1/bank-accounts'],
      ['/budgets/b-1/current-situation'],
      ['/budgets/b-1/dashboard'],
    ])('redirects %s to /login (threat matrix: routing anonymous surface)', async (path) => {
      setupAuthMock({ isAuthenticated: false })

      await router.push(path)
      await router.isReady()

      expect(router.currentRoute.value.fullPath).toBe('/login')
    })
  })

  describe('authenticated visitor', () => {
    it('single-membership user at / auto-redirects to their budget (BUDSEL-1 regression)', async () => {
      setupAuthMock({
        isAuthenticated: true,
        user: {
          id: 'u-1',
          email: 'solo@example.com',
          firstName: 'Solo',
          lastName: 'User',
          preferredLocale: 'en',
          memberships: [{ budgetId: 'b-solo', budgetName: 'Solo Budget', role: 'owner', isDeleted: false }],
        },
      })
      setupLayoutMock()
      setupNotificationMock()
      setupToastMock()

      await router.push('/')
      await router.isReady()
      expect(router.currentRoute.value.fullPath).toBe('/')

      // Intercept the component's own redirect so we don't lazy-load CycleListView
      // (an unrelated, unmocked view) inside what is meant to be a router-boundary test.
      const pushSpy = vi.spyOn(router, 'push').mockResolvedValue(undefined)

      render(RouterView, {
        global: { plugins: [i18n, router] },
      })

      expect(pushSpy).toHaveBeenCalledWith({ name: 'CycleList', params: { budgetId: 'b-solo' } })

      pushSpy.mockRestore()
    })

    it('multi-membership user at / sees the selection list, no redirect (BUDSEL-2 regression)', async () => {
      setupAuthMock({
        isAuthenticated: true,
        user: {
          id: 'u-2',
          email: 'multi@example.com',
          firstName: 'Multi',
          lastName: 'User',
          preferredLocale: 'en',
          memberships: [
            { budgetId: 'b-1', budgetName: 'Budget One', role: 'owner', isDeleted: false },
            { budgetId: 'b-2', budgetName: 'Budget Two', role: 'admin', isDeleted: false },
            { budgetId: 'b-3', budgetName: 'Budget Three', role: 'operator', isDeleted: false },
          ],
        },
      })
      setupLayoutMock()
      setupNotificationMock()
      setupToastMock()

      await router.push('/')
      await router.isReady()

      render(RouterView, {
        global: { plugins: [i18n, router] },
      })

      expect(router.currentRoute.value.fullPath).toBe('/')
      expect(screen.getByText('Budget One')).toBeTruthy()
      expect(screen.getByText('Budget Two')).toBeTruthy()
      expect(screen.getByText('Budget Three')).toBeTruthy()
    })

    it('forcePasswordChange redirects to /forgot-password ahead of the / gate', async () => {
      setupAuthMock({
        isAuthenticated: true,
        forcePasswordChange: true,
        user: {
          id: 'u-3',
          email: 'force@example.com',
          firstName: 'Force',
          lastName: 'User',
          preferredLocale: 'en',
          memberships: [],
        },
      })

      await router.push('/')
      await router.isReady()

      expect(router.currentRoute.value.path).toBe('/forgot-password')
      expect(router.currentRoute.value.query['reason']).toBe('force')
    })

    it('deleted-budget redirect still lands on / (router/index.ts guard regression)', async () => {
      setupAuthMock({
        isAuthenticated: true,
        user: {
          id: 'u-4',
          email: 'del@example.com',
          firstName: 'Del',
          lastName: 'User',
          preferredLocale: 'en',
          memberships: [{ budgetId: 'b-del', budgetName: 'Deleted Budget', role: 'owner', isDeleted: true }],
        },
      })

      await router.push('/budgets/b-del/cycles')
      await router.isReady()

      expect(router.currentRoute.value.fullPath).toBe('/')
      expect(router.currentRoute.value.name).toBe('BudgetSelection')
    })
  })

  describe('dead/stale token (fetchMe failure) — Decision 5', () => {
    it('at / clears the session and stays on / instead of redirecting to /login', async () => {
      setupAuthMock({
        isAuthenticated: true,
        user: null,
        fetchMeImpl: () => Promise.reject(new Error('401')),
      })

      await router.push('/')
      await router.isReady()

      expect(mockClearSession).toHaveBeenCalledTimes(1)
      expect(router.currentRoute.value.fullPath).toBe('/')
    })

    it('at another requiresAuth route still redirects to /login (no regression outside /)', async () => {
      setupAuthMock({
        isAuthenticated: true,
        user: null,
        fetchMeImpl: () => Promise.reject(new Error('401')),
      })

      await router.push('/budgets/b-1/cycles')
      await router.isReady()

      expect(router.currentRoute.value.fullPath).toBe('/login')
      expect(mockClearSession).not.toHaveBeenCalled()
    })

    it('does not loop between / and /login once the dead session is cleared', async () => {
      const state = {
        isAuthenticated: true,
        user: null as AuthUserInput | null,
        forcePasswordChange: false,
      }
      mockFetchMe.mockReset().mockImplementation(() => Promise.reject(new Error('401')))
      vi.mocked(useAuthStore).mockImplementation(
        () =>
          ({
            ...state,
            fetchMe: mockFetchMe,
            clearSession: mockClearSession.mockImplementation(() => {
              state.isAuthenticated = false
              state.user = null
            }),
            logout: mockLogout,
          }) as unknown as ReturnType<typeof useAuthStore>,
      )

      // First visit — dead token, cleared, lands on / (not /login)
      await router.push('/')
      await router.isReady()
      expect(router.currentRoute.value.fullPath).toBe('/')
      expect(state.isAuthenticated).toBe(false)

      // Simulate a fresh second visit (navigate away, then back to /) so the guard
      // genuinely re-runs against the now-cleared, anonymous state rather than being
      // skipped as a same-route no-op by vue-router's own duplicate-navigation guard.
      await router.push('/register')
      await router.isReady()

      await router.push('/')
      await router.isReady()
      expect(router.currentRoute.value.fullPath).toBe('/')
      expect(mockFetchMe).toHaveBeenCalledTimes(1) // only from the first, authenticated visit
    })
  })
})

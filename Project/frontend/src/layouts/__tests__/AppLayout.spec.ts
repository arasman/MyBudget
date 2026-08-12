import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import { ref } from 'vue'
import AppLayout from '../AppLayout.vue'

// --- Mocks ---

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: vi.fn(),
}))

vi.mock('@/stores/layout.store', () => ({
  useLayoutStore: vi.fn(),
}))

vi.mock('@/stores/notification.store', () => ({
  useNotificationStore: vi.fn(),
}))

import { useAuthStore } from '@/stores/auth.store'
import { useLayoutStore } from '@/stores/layout.store'
import { useNotificationStore } from '@/stores/notification.store'

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'Home', component: { template: '<div>Home</div>' } },
      { path: '/login', name: 'Login', component: { template: '<div>Login</div>' } },
    ],
  })
}

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        common: { appName: 'MyBudget' },
        nav: { backToHome: 'Back to Home' },
        auth: { logoutLabel: 'Logout' },
        footer: { poweredBy: 'Powered by ARAS Systems' },
      },
    },
  })
}

function setupMocks({
  firstName = 'John',
  lastName = 'Doe',
  memberships = [] as Array<{ budgetId: string; budgetName: string; role: string }>,
  unreadCount = 0,
  pageActions = [] as Array<{ key: string; label: string; action: () => void; variant?: string }>,
  activeBudgetId = null as string | null,
  activeBudgetName = null as string | null,
} = {}) {
  vi.mocked(useAuthStore).mockReturnValue({
    user: {
      id: 'user-1',
      email: 'john@example.com',
      firstName,
      lastName,
      preferredLocale: 'en',
      memberships,
    },
    isAuthenticated: ref(true),
    logout: vi.fn().mockResolvedValue(undefined),
  } as unknown as ReturnType<typeof useAuthStore>)

  vi.mocked(useLayoutStore).mockReturnValue({
    activeBudgetId,
    activeBudgetName,
    pageActions,
    setPageActions: vi.fn(),
    clearPageActions: vi.fn(),
    setActiveBudget: vi.fn(),
    clearActiveBudget: vi.fn(),
  } as unknown as ReturnType<typeof useLayoutStore>)

  vi.mocked(useNotificationStore).mockReturnValue({
    notifications: [],
    unreadCount,
    push: vi.fn(),
    markRead: vi.fn(),
    markAllRead: vi.fn(),
    remove: vi.fn(),
  } as unknown as ReturnType<typeof useNotificationStore>)
}

describe('AppLayout', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  async function renderLayout() {
    const router = makeRouter()
    await router.push('/')
    await router.isReady()

    return render(AppLayout, {
      global: {
        plugins: [router, makeI18n()],
      },
    })
  }

  describe('user initials', () => {
    it('derives "JD" from firstName=John lastName=Doe', async () => {
      setupMocks({ firstName: 'John', lastName: 'Doe' })
      await renderLayout()
      expect(screen.getByText('JD')).toBeTruthy()
    })

    it('derives "AM" from firstName=Ana lastName=Martinez', async () => {
      setupMocks({ firstName: 'Ana', lastName: 'Martinez' })
      await renderLayout()
      expect(screen.getByText('AM')).toBeTruthy()
    })
  })

  describe('notification badge', () => {
    it('does not show badge when unreadCount is 0', async () => {
      setupMocks({ unreadCount: 0 })
      await renderLayout()
      // Badge element should not be rendered
      expect(screen.queryByText('3')).toBeNull()
    })

    it('shows badge with count when unreadCount > 0', async () => {
      setupMocks({ unreadCount: 3 })
      await renderLayout()
      expect(screen.getByText('3')).toBeTruthy()
    })
  })

  describe('page actions', () => {
    it('renders page action buttons when present', async () => {
      setupMocks({
        pageActions: [
          { key: 'action-1', label: 'New Item', action: vi.fn() },
          { key: 'action-2', label: 'Export', action: vi.fn() },
        ],
      })
      await renderLayout()
      // Both buttons should appear (rendered twice: desktop + mobile dropdown)
      expect(screen.getAllByText('New Item').length).toBeGreaterThan(0)
      expect(screen.getAllByText('Export').length).toBeGreaterThan(0)
    })

    it('shows no action buttons when pageActions is empty', async () => {
      setupMocks({ pageActions: [] })
      await renderLayout()
      // The mobile dropdown trigger (⋮) should not be rendered when no actions
      expect(screen.queryByText('⋮')).toBeNull()
    })
  })

  describe('when user is null', () => {
    it('shows "?" as initials', async () => {
      vi.mocked(useAuthStore).mockReturnValue({
        user: null,
        isAuthenticated: ref(false),
        logout: vi.fn().mockResolvedValue(undefined),
      } as unknown as ReturnType<typeof useAuthStore>)

      vi.mocked(useLayoutStore).mockReturnValue({
        activeBudgetId: null,
        activeBudgetName: null,
        pageActions: [],
        setPageActions: vi.fn(),
        clearPageActions: vi.fn(),
        setActiveBudget: vi.fn(),
        clearActiveBudget: vi.fn(),
      } as unknown as ReturnType<typeof useLayoutStore>)

      vi.mocked(useNotificationStore).mockReturnValue({
        notifications: [],
        unreadCount: 0,
        push: vi.fn(),
        markRead: vi.fn(),
        markAllRead: vi.fn(),
        remove: vi.fn(),
      } as unknown as ReturnType<typeof useNotificationStore>)

      await renderLayout()
      expect(screen.getByText('?')).toBeTruthy()
    })
  })

  describe('default slot fallback (LAYOUT-3 regression)', () => {
    it('renders <RouterView /> when no slot content is passed', async () => {
      setupMocks()
      await renderLayout()
      // makeRouter()'s '/' route renders "Home" — proves the default slot fell back
      // to <RouterView /> rather than rendering nothing.
      expect(screen.getByText('Home')).toBeTruthy()
    })
  })
})

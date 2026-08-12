import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import RootGate from '../RootGate.vue'

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

import { useAuthStore } from '@/stores/auth.store'
import { useLayoutStore } from '@/stores/layout.store'
import { useNotificationStore } from '@/stores/notification.store'
import { useToastStore } from '@/stores/toast.store'

function makeRouter() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'BudgetSelection', component: { template: '<div />' } },
      { path: '/login', name: 'Login', component: { template: '<div />' } },
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
        common: { appName: 'MyBudget' },
        nav: { backToHome: 'Back to Home' },
        auth: { logoutLabel: 'Logout' },
        budgetStructure: {
          selection: {
            title: 'My Budgets',
            createBudget: 'New Budget',
            showDeleted: 'Show deleted',
            noBudgets: 'You are not a member of any budget yet.',
          },
        },
      },
    },
  })
}

describe('RootGate', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()

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

    vi.mocked(useToastStore).mockReturnValue({
      toasts: [],
      push: vi.fn(),
      dismiss: vi.fn(),
    } as unknown as ReturnType<typeof useToastStore>)
  })

  async function renderRootGate() {
    const router = makeRouter()
    await router.push('/')
    await router.isReady()

    return render(RootGate, {
      global: {
        plugins: [router, makeI18n()],
      },
    })
  }

  it('renders the landing branch for an anonymous visitor', async () => {
    vi.mocked(useAuthStore).mockReturnValue({
      isAuthenticated: false,
      user: null,
      logout: vi.fn(),
    } as unknown as ReturnType<typeof useAuthStore>)

    await renderRootGate()

    expect(screen.getByTestId('landing-view')).toBeTruthy()
  })

  it('renders AppLayout + BudgetSelectionView for an authenticated visitor', async () => {
    vi.mocked(useAuthStore).mockReturnValue({
      isAuthenticated: true,
      user: {
        id: 'u-1',
        email: 'a@example.com',
        firstName: 'A',
        lastName: 'B',
        preferredLocale: 'en',
        memberships: [],
      },
      logout: vi.fn().mockResolvedValue(undefined),
    } as unknown as ReturnType<typeof useAuthStore>)

    await renderRootGate()

    expect(screen.queryByTestId('landing-view')).toBeNull()
    expect(screen.getByText('My Budgets')).toBeTruthy()
  })
})

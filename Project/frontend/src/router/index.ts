import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth.store'
import AppLayout from '@/layouts/AppLayout.vue'
import PublicLayout from '@/layouts/PublicLayout.vue'
import LoginView from '@/views/LoginView.vue'
import BudgetSelectionView from '@/features/budget-structure/views/BudgetSelectionView.vue'

const routes: RouteRecordRaw[] = [
  // Authenticated routes — wrapped by AppLayout
  {
    path: '/',
    component: AppLayout,
    meta: { requiresAuth: true },
    children: [
      // Budget selection — root route for authenticated users
      {
        path: '',
        name: 'BudgetSelection',
        component: BudgetSelectionView,
      },
      // Budget-scoped routes
      {
        path: 'budgets/:budgetId',
        children: [
          {
            path: '',
            redirect: { name: 'CycleList' },
          },
          {
            path: 'cycles',
            name: 'CycleList',
            component: () =>
              import('@/features/budget-structure/views/CycleListView.vue'),
          },
          {
            path: 'cycles/:cycleId',
            name: 'CycleDetail',
            component: () =>
              import('@/features/budget-structure/views/CycleDetailView.vue'),
          },
          {
            path: 'categories',
            name: 'CategoryTree',
            component: () =>
              import('@/features/budget-structure/views/CategoryTreeView.vue'),
          },
          {
            path: 'lines',
            name: 'BudgetLines',
            component: () =>
              import('@/features/budget-structure/views/BudgetLinesView.vue'),
          },
          {
            path: 'lines/:lineId/customizations',
            name: 'BudgetLineCustomizations',
            component: () =>
              import(
                '@/features/budget-structure/views/BudgetLineCustomizationsView.vue'
              ),
          },
          {
            path: 'cycles/:cycleId/matrix',
            name: 'BudgetMatrix',
            component: () =>
              import('@/features/budget-execution/views/BudgetMatrixView.vue'),
            meta: { requiresAuth: true },
          },
          {
            path: 'bank-accounts',
            name: 'BankAccounts',
            component: () =>
              import('@/features/bank-accounts/views/BankAccountListView.vue'),
          },
          {
            path: 'current-situation',
            name: 'CurrentSituation',
            component: () =>
              import('@/features/current-situation/views/CurrentSituationView.vue'),
          },
          {
            path: 'dashboard',
            name: 'Dashboard',
            component: () =>
              import('@/features/dashboard/views/DashboardView.vue'),
          },
        ],
      },
    ],
  },

  // Public routes — wrapped by PublicLayout
  {
    path: '/login',
    component: PublicLayout,
    children: [
      {
        path: '',
        name: 'Login',
        component: LoginView,
        meta: { public: true },
      },
    ],
  },
  {
    path: '/register',
    component: PublicLayout,
    children: [
      {
        path: '',
        name: 'Register',
        component: () => import('@/views/RegisterView.vue'),
        meta: { public: true },
      },
    ],
  },
  {
    path: '/invitations/accept',
    component: PublicLayout,
    children: [
      {
        path: '',
        name: 'AcceptInvitation',
        component: () => import('@/views/AcceptInvitationView.vue'),
        meta: { public: true },
      },
    ],
  },
  {
    path: '/forgot-password',
    component: PublicLayout,
    children: [
      {
        path: '',
        name: 'ForgotPassword',
        component: () => import('@/views/ForgotPasswordView.vue'),
        meta: { requiresAuth: false },
      },
    ],
  },
  {
    path: '/reset-password',
    component: PublicLayout,
    children: [
      {
        path: '',
        name: 'ResetPassword',
        component: () => import('@/views/ResetPasswordView.vue'),
        meta: { requiresAuth: false },
      },
    ],
  },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

// Navigation guard — redirects unauthenticated users to /login
// If authenticated but user profile not yet loaded (e.g. page reload), fetchMe()
// so the 401 interceptor can silently refresh an expired access token.
// When forcePasswordChange is true, all requiresAuth routes redirect to /forgot-password.
router.beforeEach(async (to) => {
  if (to.meta.requiresAuth) {
    const authStore = useAuthStore()
    if (!authStore.isAuthenticated) {
      return '/login'
    }
    if (authStore.forcePasswordChange) {
      return '/forgot-password?reason=force'
    }
    if (!authStore.user) {
      try {
        await authStore.fetchMe()
      } catch {
        return '/login'
      }
    }

    // Guard: redirect to / if navigating to a deleted budget
    const budgetId = to.params['budgetId']
    if (typeof budgetId === 'string' && authStore.user) {
      const membership = authStore.user.memberships.find((m) => m.budgetId === budgetId)
      if (membership?.isDeleted) {
        return '/'
      }
    }
  }
})

export default router

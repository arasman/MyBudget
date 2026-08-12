import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth.store'
import AppLayout from '@/layouts/AppLayout.vue'
import PublicLayout from '@/layouts/PublicLayout.vue'
import RootGate from '@/layouts/RootGate.vue'
import LoginView from '@/views/LoginView.vue'

const routes: RouteRecordRaw[] = [
  // Root route — public. RootGate renders the landing page for anonymous visitors,
  // and AppLayout + BudgetSelectionView (today's behavior) for authenticated ones.
  // The name is kept as 'BudgetSelection' so BudgetTabs.vue, BudgetMatrixView.vue,
  // and AppLayout.vue's route.name check need zero changes (design.md Decision #2).
  {
    path: '/',
    name: 'BudgetSelection',
    component: RootGate,
    meta: { public: true },
  },

  // Budget-scoped routes — promoted to a top-level record wrapped by AppLayout.
  // This makes the landing structurally unreachable from any budget URL
  // (design.md Decision #1): a guard regression here can never render RootGate
  // under a budget path, since this subtree no longer lives beneath it.
  {
    path: '/budgets/:budgetId',
    component: AppLayout,
    meta: { requiresAuth: true },
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

// Navigation guard — redirects unauthenticated users to /login.
// If authenticated but user profile not yet loaded (e.g. page reload), fetchMe()
// so the 401 interceptor can silently refresh an expired access token.
// When forcePasswordChange is true, all requiresAuth routes redirect to /forgot-password.
//
// "/" is the one route that is auth-boundary AND public: it needs the full
// authenticated pipeline (forcePasswordChange, fetchMe, deleted-budget guard) when the
// visitor IS authenticated, but must stay reachable with zero checks when anonymous.
// needsAuth captures exactly that (design.md Decision #3).
router.beforeEach(async (to) => {
  const authStore = useAuthStore()
  const needsAuth =
    to.meta.requiresAuth === true || (to.name === 'BudgetSelection' && authStore.isAuthenticated)

  if (!needsAuth) return

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
      // Dead/stale token: on "/" only, clear the session and let the landing render
      // instead of bouncing to /login (design.md Decision #5). clearSession() is a
      // thin wrapper with no network call — logout() would POST with the dead token
      // and loop through the refresh interceptor. Every other requiresAuth route keeps
      // today's /login redirect.
      if (to.name === 'BudgetSelection') {
        authStore.clearSession()
        return true
      }
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
})

export default router

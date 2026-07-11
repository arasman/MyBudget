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
            path: 'cycles/:cycleId/periods/:periodId/lines',
            name: 'BudgetLines',
            component: () =>
              import('@/features/budget-structure/views/BudgetLinesView.vue'),
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
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

// Navigation guard — redirects unauthenticated users to /login
// If authenticated but user profile not yet loaded (e.g. page reload), fetchMe()
// so the 401 interceptor can silently refresh an expired access token.
router.beforeEach(async (to) => {
  if (to.meta.requiresAuth) {
    const authStore = useAuthStore()
    if (!authStore.isAuthenticated) {
      return '/login'
    }
    if (!authStore.user) {
      try {
        await authStore.fetchMe()
      } catch {
        return '/login'
      }
    }
  }
})

export default router

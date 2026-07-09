import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth.store'
import LoginView from '@/views/LoginView.vue'
import HomeView from '@/views/HomeView.vue'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'Login',
    component: LoginView,
    meta: { public: true },
  },
  {
    path: '/register',
    name: 'Register',
    component: () => import('@/views/RegisterView.vue'),
    meta: { public: true },
  },
  {
    path: '/invitations/accept',
    name: 'AcceptInvitation',
    component: () => import('@/views/AcceptInvitationView.vue'),
    meta: { public: true },
  },
  {
    path: '/',
    name: 'Home',
    component: HomeView,
    meta: { requiresAuth: true },
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

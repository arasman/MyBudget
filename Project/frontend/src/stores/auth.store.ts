import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import http from '@/api/axios'
import { useLocaleStore } from '@/stores/locale.store'

export interface BudgetMembershipDto {
  budgetId: string
  budgetName: string
  role: string
  isDeleted: boolean
}

export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  preferredLocale: string
  memberships: BudgetMembershipDto[]
}

interface LoginResponse {
  accessToken: string
  refreshToken: string
  expiresIn: number
  user: {
    id: string
    email: string
    firstName: string
    lastName: string
    preferredLocale: string
  }
}

interface RegisterPayload {
  email: string
  password: string
  firstName: string
  lastName: string
  preferredLocale?: string
}

export const useAuthStore = defineStore('auth', () => {
  // State
  const accessToken = ref<string | null>(localStorage.getItem('accessToken'))
  const user = ref<User | null>(null)
  const forcePasswordChange = ref(false)

  // Derived
  const isAuthenticated = computed(() => !!accessToken.value)

  // Restore user from localStorage on store init if we have a token
  // (user profile is loaded lazily via fetchMe)

  async function login(email: string, password: string): Promise<void> {
    try {
      const { data } = await http.post<LoginResponse>('/api/auth/login', { email, password })
      _storeTokens(data.accessToken, data.refreshToken)
      await fetchMe()
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { detail?: string } } }
      const detail = axiosErr.response?.data?.detail ?? ''
      if (detail.includes('AUTH_FORCE_PASSWORD_CHANGE')) {
        forcePasswordChange.value = true
        const router = useRouter()
        router.push('/forgot-password?reason=force')
        return
      }
      throw err
    }
  }

  async function register(payload: RegisterPayload): Promise<void> {
    const { data } = await http.post<LoginResponse>('/api/auth/register', payload)
    _storeTokens(data.accessToken, data.refreshToken)
    await fetchMe()
  }

  async function logout(): Promise<void> {
    const refreshToken = localStorage.getItem('refreshToken')
    if (refreshToken && accessToken.value) {
      try {
        await http.post('/api/auth/logout', { refreshToken })
      } catch {
        // best-effort — clear local state regardless
      }
    }
    _clearTokens()
  }

  async function refresh(): Promise<void> {
    const storedRefreshToken = localStorage.getItem('refreshToken')
    const storedUserId = user.value?.id ?? _tryDecodeUserId(accessToken.value)
    if (!storedRefreshToken || !storedUserId) throw new Error('No refresh token available')

    const { data } = await http.post<LoginResponse>('/api/auth/refresh', {
      refreshToken: storedRefreshToken,
      userId: storedUserId,
    })
    // Only update access token — do not clear user
    accessToken.value = data.accessToken
    localStorage.setItem('accessToken', data.accessToken)
    if (data.refreshToken) {
      localStorage.setItem('refreshToken', data.refreshToken)
    }
  }

  async function fetchMe(): Promise<void> {
    const { data } = await http.get<User>('/api/auth/me')
    user.value = data
    // Seed locale from server only when localStorage has no explicit locale preference.
    if (!localStorage.getItem('locale') && data.preferredLocale) {
      const localeStore = useLocaleStore()
      localeStore.setLocale(data.preferredLocale as 'en' | 'es')
    }
  }

  // Private helpers
  function _storeTokens(access: string, refresh: string): void {
    accessToken.value = access
    localStorage.setItem('accessToken', access)
    localStorage.setItem('refreshToken', refresh)
  }

  function _clearTokens(): void {
    accessToken.value = null
    user.value = null
    localStorage.removeItem('accessToken')
    localStorage.removeItem('refreshToken')
  }

  /** Best-effort JWT sub claim extraction for refresh calls when user not yet loaded. */
  function _tryDecodeUserId(token: string | null): string | null {
    if (!token) return null
    try {
      const payload = JSON.parse(atob(token.split('.')[1]!))
      return payload.sub ?? null
    } catch {
      return null
    }
  }

  async function requestPasswordReset(email: string): Promise<void> {
    await http.post('/api/auth/forgot-password', { email })
  }

  async function resetPassword(token: string, email: string, newPassword: string): Promise<void> {
    await http.post('/api/auth/reset-password', { email, token, newPassword })
  }

  async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
    const currentRefreshToken = localStorage.getItem('refreshToken')
    await http.post('/api/auth/change-password', { currentPassword, newPassword, currentRefreshToken })
    forcePasswordChange.value = false
  }

  return {
    accessToken,
    user,
    isAuthenticated,
    forcePasswordChange,
    login,
    register,
    logout,
    refresh,
    fetchMe,
    requestPasswordReset,
    resetPassword,
    changePassword,
  }
})

import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import http from '@/api/axios'

export interface BudgetMembershipDto {
  budgetId: string
  budgetName: string
  role: string
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

  // Derived
  const isAuthenticated = computed(() => !!accessToken.value)

  // Restore user from localStorage on store init if we have a token
  // (user profile is loaded lazily via fetchMe)

  async function login(email: string, password: string): Promise<void> {
    const { data } = await http.post<LoginResponse>('/api/auth/login', { email, password })
    _storeTokens(data.accessToken, data.refreshToken)
    await fetchMe()
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

  return {
    accessToken,
    user,
    isAuthenticated,
    login,
    register,
    logout,
    refresh,
    fetchMe,
  }
})

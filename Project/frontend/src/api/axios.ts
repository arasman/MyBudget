import axios, { type AxiosRequestConfig } from 'axios'
import { useAuthStore } from '@/stores/auth.store'

// Track in-flight retry to prevent infinite loops
let _isRefreshing = false

export const http = axios.create({
  // Empty string → relative URLs → Vite proxy forwards /api/* to backend.
  // Override with VITE_API_BASE_URL env var for Docker Compose or prod.
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '',
})

// Request interceptor: attaches correlation ID, locale, and auth token
http.interceptors.request.use((config) => {
  // 1. Unique correlation ID per request
  config.headers['X-Correlation-Id'] = crypto.randomUUID()

  // 2. Accept-Language from localStorage or default to English
  config.headers['Accept-Language'] = localStorage.getItem('locale') ?? 'en'

  // 3. Authorization Bearer — read accessToken from store
  try {
    const authStore = useAuthStore()
    if (authStore.accessToken) {
      config.headers['Authorization'] = `Bearer ${authStore.accessToken}`
    }
  } catch {
    // Pinia not yet initialized — skip auth header (only possible in unit tests)
  }

  return config
})

// Response interceptor: silent token refresh on 401
http.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean }
    const status = error.response?.status

    // Only intercept 401 and only retry once
    // Skip if no refresh token — avoids intercepting auth endpoints (login, register)
    const hasRefreshToken = !!localStorage.getItem('refreshToken')
    if (status === 401 && !originalRequest._retry && hasRefreshToken) {
      if (_isRefreshing) {
        // Already refreshing — bail out to avoid infinite loop
        try {
          const authStore = useAuthStore()
          authStore.logout()
          window.location.href = '/login'
        } catch {
          // best-effort
        }
        return Promise.reject(error)
      }

      originalRequest._retry = true
      _isRefreshing = true

      try {
        const authStore = useAuthStore()
        await authStore.refresh()

        // Retry original request with new token
        if (originalRequest.headers) {
          originalRequest.headers['Authorization'] = `Bearer ${authStore.accessToken}`
        }
        return http(originalRequest)
      } catch {
        // Refresh failed — logout and redirect
        try {
          const authStore = useAuthStore()
          await authStore.logout()
        } catch {
          // best-effort
        }
        window.location.href = '/login'
        return Promise.reject(error)
      } finally {
        _isRefreshing = false
      }
    }

    return Promise.reject(error)
  },
)

export default http

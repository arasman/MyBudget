import axios from 'axios'

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5050',
})

// Request interceptor: attaches correlation ID, locale, and auth token
http.interceptors.request.use((config) => {
  // 1. Unique correlation ID per request
  config.headers['X-Correlation-Id'] = crypto.randomUUID()

  // 2. Accept-Language from localStorage or default to English
  config.headers['Accept-Language'] = localStorage.getItem('locale') ?? 'en'

  // 3. Authorization Bearer — lazy import to avoid circular dep with auth store
  // Imported lazily at intercept time, not at module load time
  import('@/stores/auth.store').then(({ useAuthStore }) => {
    // Note: pinia must be initialized before this runs — guaranteed by main.ts ordering
    try {
      const authStore = useAuthStore()
      if (authStore.token) {
        config.headers['Authorization'] = `Bearer ${authStore.token}`
      }
    } catch {
      // Auth store not yet initialized — skip auth header
    }
  })

  return config
})

export default http

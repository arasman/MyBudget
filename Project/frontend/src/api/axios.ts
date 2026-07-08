import axios from 'axios'
import { useAuthStore } from '@/stores/auth.store'

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5050',
})

// Request interceptor: attaches correlation ID, locale, and auth token
http.interceptors.request.use((config) => {
  // 1. Unique correlation ID per request
  config.headers['X-Correlation-Id'] = crypto.randomUUID()

  // 2. Accept-Language from localStorage or default to English
  config.headers['Accept-Language'] = localStorage.getItem('locale') ?? 'en'

  // 3. Authorization Bearer — synchronous, Pinia is initialized before any request fires
  try {
    const authStore = useAuthStore()
    if (authStore.token) {
      config.headers['Authorization'] = `Bearer ${authStore.token}`
    }
  } catch {
    // Pinia not yet initialized — skip auth header (only possible in unit tests)
  }

  return config
})

export default http

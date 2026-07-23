import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '@/stores/auth.store'

// Mock the axios module
vi.mock('@/api/axios', () => ({
  default: {
    post: vi.fn(),
    get: vi.fn(),
    defaults: { headers: { common: {} } },
  },
}))

vi.mock('@/stores/locale.store', () => ({
  useLocaleStore: vi.fn(() => ({ setLocale: vi.fn(), locale: 'en' })),
}))

vi.mock('@/i18n', () => ({
  i18n: { global: { locale: { value: 'en' } } },
}))

import http from '@/api/axios'

const mockUser = {
  id: '11111111-1111-1111-1111-111111111111',
  email: 'test@example.com',
  firstName: 'Test',
  lastName: 'User',
  preferredLocale: 'en',
  memberships: [],
}

const mockLoginResponse = {
  data: {
    accessToken:  'mock-access-token',
    refreshToken: 'mock-refresh-token',
    expiresIn:    900,
    user:         { ...mockUser, memberships: undefined },
  },
}

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.clearAllMocks()
  })

  afterEach(() => {
    localStorage.clear()
  })

  it('initializes with null accessToken when localStorage is empty', () => {
    const store = useAuthStore()
    expect(store.accessToken).toBeNull()
    expect(store.isAuthenticated).toBe(false)
  })

  it('restores accessToken from localStorage on init', () => {
    localStorage.setItem('accessToken', 'restored-token')
    setActivePinia(createPinia()) // reinitialize
    const store = useAuthStore()
    expect(store.accessToken).toBe('restored-token')
    expect(store.isAuthenticated).toBe(true)
  })

  describe('login', () => {
    it('sets accessToken in state and localStorage', async () => {
      vi.mocked(http.post).mockResolvedValueOnce(mockLoginResponse)
      vi.mocked(http.get).mockResolvedValueOnce({ data: mockUser })

      const store = useAuthStore()
      await store.login('test@example.com', 'Password1')

      expect(store.accessToken).toBe('mock-access-token')
      expect(localStorage.getItem('accessToken')).toBe('mock-access-token')
      expect(store.isAuthenticated).toBe(true)
    })

    it('stores refreshToken in localStorage', async () => {
      vi.mocked(http.post).mockResolvedValueOnce(mockLoginResponse)
      vi.mocked(http.get).mockResolvedValueOnce({ data: mockUser })

      const store = useAuthStore()
      await store.login('test@example.com', 'Password1')

      expect(localStorage.getItem('refreshToken')).toBe('mock-refresh-token')
    })

    it('calls fetchMe after successful login', async () => {
      vi.mocked(http.post).mockResolvedValueOnce(mockLoginResponse)
      vi.mocked(http.get).mockResolvedValueOnce({ data: mockUser })

      const store = useAuthStore()
      await store.login('test@example.com', 'Password1')

      expect(http.get).toHaveBeenCalledWith('/api/auth/me')
      expect(store.user).toEqual(mockUser)
    })
  })

  describe('logout', () => {
    it('clears state and localStorage', async () => {
      // Setup logged-in state
      vi.mocked(http.post)
        .mockResolvedValueOnce(mockLoginResponse)
        .mockResolvedValueOnce({ data: {} }) // logout call
      vi.mocked(http.get).mockResolvedValueOnce({ data: mockUser })

      const store = useAuthStore()
      await store.login('test@example.com', 'Password1')

      await store.logout()

      expect(store.accessToken).toBeNull()
      expect(store.user).toBeNull()
      expect(store.isAuthenticated).toBe(false)
      expect(localStorage.getItem('accessToken')).toBeNull()
      expect(localStorage.getItem('refreshToken')).toBeNull()
    })
  })

  describe('refresh', () => {
    it('updates accessToken without clearing user', async () => {
      vi.mocked(http.post)
        .mockResolvedValueOnce(mockLoginResponse)
        .mockResolvedValueOnce({
          data: { accessToken: 'new-access-token', refreshToken: 'new-refresh', expiresIn: 900, user: mockUser },
        })
      vi.mocked(http.get).mockResolvedValueOnce({ data: mockUser })

      localStorage.setItem('refreshToken', 'old-refresh')
      const store = useAuthStore()
      await store.login('test@example.com', 'Password1')

      await store.refresh()

      expect(store.accessToken).toBe('new-access-token')
      expect(store.user).toEqual(mockUser) // user not cleared
    })
  })

  describe('register', () => {
    it('calls POST /api/auth/register with correct payload', async () => {
      vi.mocked(http.post).mockResolvedValueOnce(mockLoginResponse)
      vi.mocked(http.get).mockResolvedValueOnce({ data: mockUser })

      const store = useAuthStore()
      await store.register({
        email: 'new@example.com',
        password: 'Password1',
        firstName: 'New',
        lastName: 'User',
      })

      expect(http.post).toHaveBeenCalledWith('/api/auth/register', expect.objectContaining({
        email: 'new@example.com',
        firstName: 'New',
      }))
    })
  })

  describe('fetchMe', () => {
    it('populates user from response', async () => {
      localStorage.setItem('accessToken', 'some-token')
      vi.mocked(http.get).mockResolvedValueOnce({ data: mockUser })

      const store = useAuthStore()
      await store.fetchMe()

      expect(store.user).toEqual(mockUser)
    })
  })
})

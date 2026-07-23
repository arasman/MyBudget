// REQ-LSYNC-1: fetchMe seeds locale only when localStorage('locale') is absent
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

// ── Hoisted mocks ──────────────────────────────────────────────────────────────
const { mockSetLocale, mockHttpGet } = vi.hoisted(() => ({
  mockSetLocale: vi.fn(),
  mockHttpGet: vi.fn(),
}))

vi.mock('@/api/axios', () => ({
  default: {
    get: mockHttpGet,
    post: vi.fn().mockResolvedValue({ data: {} }),
    defaults: { headers: { common: {} } },
  },
}))

vi.mock('@/stores/locale.store', () => ({
  useLocaleStore: vi.fn(() => ({
    setLocale: mockSetLocale,
    locale: 'en',
  })),
}))

const mockUserData = {
  id: 'user-1',
  email: 'test@example.com',
  firstName: 'Test',
  lastName: 'User',
  preferredLocale: 'es',
  memberships: [],
}

import { useAuthStore } from '@/stores/auth.store'

describe('useAuthStore — fetchMe locale seeding', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    localStorage.clear()
    mockHttpGet.mockResolvedValue({ data: mockUserData })
  })

  afterEach(() => {
    localStorage.clear()
  })

  it('seeds locale from server when localStorage has no locale key', async () => {
    // localStorage has no 'locale' key
    localStorage.removeItem('locale')
    const store = useAuthStore()
    await store.fetchMe()

    expect(mockSetLocale).toHaveBeenCalledWith('es')
  })

  it('does NOT override locale when localStorage already has a locale key', async () => {
    localStorage.setItem('locale', 'en')
    const store = useAuthStore()
    await store.fetchMe()

    expect(mockSetLocale).not.toHaveBeenCalled()
  })

  it('does NOT call setLocale when preferredLocale is absent', async () => {
    localStorage.removeItem('locale')
    mockHttpGet.mockResolvedValue({
      data: { ...mockUserData, preferredLocale: undefined },
    })
    const store = useAuthStore()
    await store.fetchMe()

    expect(mockSetLocale).not.toHaveBeenCalled()
  })
})

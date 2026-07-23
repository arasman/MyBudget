// REQ-LSYNC-2: setLocale updates locale state, localStorage, and Accept-Language header
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

const { mockAxiosHeaders } = vi.hoisted(() => ({
  mockAxiosHeaders: {} as Record<string, string>,
}))

vi.mock('@/api/axios', () => ({
  default: {
    defaults: { headers: { common: mockAxiosHeaders } },
    patch: vi.fn(),
  },
}))

vi.mock('@/i18n', () => ({
  i18n: {
    global: {
      locale: { value: 'en' },
    },
  },
}))

import { useLocaleStore } from '@/stores/locale.store'

describe('useLocaleStore — setLocale', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    localStorage.clear()
  })

  it('updates locale ref', () => {
    const store = useLocaleStore()
    store.setLocale('es')
    expect(store.locale).toBe('es')
  })

  it('writes to localStorage', () => {
    const store = useLocaleStore()
    store.setLocale('es')
    expect(localStorage.getItem('locale')).toBe('es')
  })

  it('sets Accept-Language header on axios', () => {
    const store = useLocaleStore()
    store.setLocale('es')
    expect(mockAxiosHeaders['Accept-Language']).toBe('es')
  })
})

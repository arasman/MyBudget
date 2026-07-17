import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useToastStore } from '@/stores/toast.store'
import { useNotificationStore } from '@/stores/notification.store'

describe('useToastStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('starts with an empty toasts list', () => {
    const store = useToastStore()
    expect(store.toasts).toHaveLength(0)
  })

  it('push adds a toast to the list', () => {
    const store = useToastStore()
    store.push({ type: 'success', title: 'Saved!' })
    expect(store.toasts).toHaveLength(1)
    expect(store.toasts[0].type).toBe('success')
    expect(store.toasts[0].title).toBe('Saved!')
  })

  it('push returns the toast id', () => {
    const store = useToastStore()
    const id = store.push({ type: 'info', title: 'Hello' })
    expect(typeof id).toBe('string')
    expect(id).toBeTruthy()
  })

  it('push sets default autoDismiss of 3000ms', () => {
    const store = useToastStore()
    store.push({ type: 'success', title: 'Hi' })
    expect(store.toasts[0].autoDismiss).toBe(3000)
  })

  it('push respects custom autoDismiss', () => {
    const store = useToastStore()
    store.push({ type: 'warning', title: 'Watch out', autoDismiss: 5000 })
    expect(store.toasts[0].autoDismiss).toBe(5000)
  })

  it('auto-dismisses after the autoDismiss timeout', () => {
    const store = useToastStore()
    store.push({ type: 'success', title: 'Auto gone', autoDismiss: 1000 })
    expect(store.toasts).toHaveLength(1)

    vi.advanceTimersByTime(1000)
    expect(store.toasts).toHaveLength(0)
  })

  it('does not dismiss before the timeout elapses', () => {
    const store = useToastStore()
    store.push({ type: 'success', title: 'Still here', autoDismiss: 3000 })

    vi.advanceTimersByTime(2999)
    expect(store.toasts).toHaveLength(1)
  })

  it('manual dismiss removes the toast immediately', () => {
    const store = useToastStore()
    const id = store.push({ type: 'error', title: 'Oops' })
    expect(store.toasts).toHaveLength(1)

    store.dismiss(id)
    expect(store.toasts).toHaveLength(0)
  })

  it('dismiss on unknown id is a no-op', () => {
    const store = useToastStore()
    store.push({ type: 'info', title: 'Keep me' })
    store.dismiss('non-existent-id')
    expect(store.toasts).toHaveLength(1)
  })

  it('supports stacking multiple toasts', () => {
    const store = useToastStore()
    store.push({ type: 'success', title: 'First' })
    store.push({ type: 'error', title: 'Second' })
    store.push({ type: 'info', title: 'Third' })
    expect(store.toasts).toHaveLength(3)
  })

  // REQ-TOAST-3: Bell exclusion — toast push MUST NOT write to useNotificationStore
  it('does not affect the notification store when push is called', () => {
    const toastStore = useToastStore()
    const notificationStore = useNotificationStore()

    const initialCount = notificationStore.notifications.length
    toastStore.push({ type: 'success', title: 'Saved' })

    expect(notificationStore.notifications.length).toBe(initialCount)
    expect(notificationStore.unreadCount).toBe(initialCount)
  })

  it('auto-dismiss of each toast fires independently', () => {
    const store = useToastStore()
    store.push({ type: 'success', title: 'Short', autoDismiss: 1000 })
    store.push({ type: 'info', title: 'Long', autoDismiss: 5000 })

    vi.advanceTimersByTime(1000)
    expect(store.toasts).toHaveLength(1)
    expect(store.toasts[0].title).toBe('Long')

    vi.advanceTimersByTime(4000)
    expect(store.toasts).toHaveLength(0)
  })
})

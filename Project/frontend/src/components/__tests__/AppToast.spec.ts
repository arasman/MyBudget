import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import AppToast from '@/components/AppToast.vue'
import { useToastStore } from '@/stores/toast.store'

describe('AppToast', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('renders nothing when there are no toasts', () => {
    render(AppToast)
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('renders a toast when pushed to the store', async () => {
    const toastStore = useToastStore()
    render(AppToast)

    toastStore.push({ type: 'success', title: 'Saved successfully!' })

    await vi.waitFor(() => {
      expect(screen.getByRole('alert')).toBeTruthy()
    })
    expect(screen.getByText('Saved successfully!')).toBeTruthy()
  })

  it('renders the optional message when provided', async () => {
    const toastStore = useToastStore()
    render(AppToast)

    toastStore.push({ type: 'info', title: 'Heads up', message: 'Some detail here' })

    await vi.waitFor(() => {
      expect(screen.getByText('Some detail here')).toBeTruthy()
    })
  })

  it('renders stacked toasts for multiple pushes', async () => {
    const toastStore = useToastStore()
    render(AppToast)

    toastStore.push({ type: 'success', title: 'First toast' })
    toastStore.push({ type: 'error', title: 'Second toast' })

    await vi.waitFor(() => {
      expect(screen.getAllByRole('alert')).toHaveLength(2)
    })
  })

  it('close button calls dismiss on the correct toast', async () => {
    const toastStore = useToastStore()
    render(AppToast)

    toastStore.push({ type: 'warning', title: 'Dismiss me' })

    await vi.waitFor(() => {
      expect(screen.getByRole('alert')).toBeTruthy()
    })

    const closeButton = screen.getByLabelText('Close')
    await fireEvent.click(closeButton)

    await vi.waitFor(() => {
      expect(screen.queryByRole('alert')).toBeNull()
    })
  })

  it('toast container uses z-index above modals (z-[1000])', () => {
    render(AppToast)
    const container = document.querySelector('.toast')
    expect(container?.className).toContain('z-[1000]')
  })
})

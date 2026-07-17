import { defineStore } from 'pinia'
import { ref } from 'vue'

export type ToastType = 'success' | 'error' | 'info' | 'warning'

export interface Toast {
  id: string
  type: ToastType
  title: string
  message?: string
  autoDismiss: number
}

export interface PushToastOptions {
  type: ToastType
  title: string
  message?: string
  autoDismiss?: number
}

const DEFAULT_AUTO_DISMISS_MS = 3000

export const useToastStore = defineStore('toast', () => {
  const toasts = ref<Toast[]>([])
  const timers = new Map<string, ReturnType<typeof setTimeout>>()

  function push(options: PushToastOptions): string {
    const id = crypto.randomUUID()
    const autoDismiss = options.autoDismiss ?? DEFAULT_AUTO_DISMISS_MS

    toasts.value.push({
      id,
      type: options.type,
      title: options.title,
      message: options.message,
      autoDismiss,
    })

    timers.set(
      id,
      setTimeout(() => {
        dismiss(id)
      }, autoDismiss),
    )

    return id
  }

  function dismiss(id: string): void {
    const timer = timers.get(id)
    if (timer !== undefined) {
      clearTimeout(timer)
      timers.delete(id)
    }
    const index = toasts.value.findIndex((t) => t.id === id)
    if (index !== -1) {
      toasts.value.splice(index, 1)
    }
  }

  return {
    toasts,
    push,
    dismiss,
  }
})

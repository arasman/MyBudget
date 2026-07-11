import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

export interface Notification {
  id: string
  type: 'info' | 'success' | 'warning' | 'error'
  title: string
  message: string
  read: boolean
  createdAt: string
}

export const useNotificationStore = defineStore('notification', () => {
  // State
  const notifications = ref<Notification[]>([])

  // Computed
  const unreadCount = computed(() => notifications.value.filter((n) => !n.read).length)

  function push(notification: Omit<Notification, 'id' | 'read' | 'createdAt'>): void {
    notifications.value.unshift({
      ...notification,
      id: crypto.randomUUID(),
      read: false,
      createdAt: new Date().toISOString(),
    })
  }

  function markRead(id: string): void {
    const notification = notifications.value.find((n) => n.id === id)
    if (notification) {
      notification.read = true
    }
  }

  function markAllRead(): void {
    notifications.value.forEach((n) => {
      n.read = true
    })
  }

  function remove(id: string): void {
    const index = notifications.value.findIndex((n) => n.id === id)
    if (index !== -1) {
      notifications.value.splice(index, 1)
    }
  }

  return {
    notifications,
    unreadCount,
    push,
    markRead,
    markAllRead,
    remove,
  }
})

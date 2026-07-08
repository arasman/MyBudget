import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useAuthStore = defineStore('auth', () => {
  const isAuthenticated = ref(false)
  const user = ref<null>(null)
  const token = ref<string | null>(null)

  return {
    isAuthenticated,
    user,
    token,
  }
})

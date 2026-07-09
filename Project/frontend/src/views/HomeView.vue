<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth.store'

const router = useRouter()
const authStore = useAuthStore()

async function onLogout() {
  await authStore.logout()
  router.push('/login')
}
</script>

<template>
  <div class="min-h-screen bg-base-200">
    <nav class="navbar bg-base-100 shadow px-4">
      <div class="flex-1">
        <span class="font-bold text-lg">{{ $t('common.appName') }}</span>
      </div>
      <div class="flex-none">
        <button class="btn btn-ghost" @click="onLogout">
          {{ $t('auth.logoutLabel') }}
        </button>
      </div>
    </nav>
    <div class="container mx-auto px-4 py-8">
      <h1 class="text-3xl font-bold text-base-content">
        {{ $t('common.appName') }}
      </h1>
      <p class="mt-4 text-base-content/70">
        Welcome to MyBudget. Your personal finance management app.
      </p>
    </div>
  </div>
</template>

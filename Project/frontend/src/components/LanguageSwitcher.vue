<template>
  <div class="flex gap-2" role="group" :aria-label="$t('common.switchLanguage')">
    <button
      class="btn btn-sm"
      :class="localeStore.locale === 'en' ? 'btn-primary' : 'btn-ghost'"
      @click="switchLocale('en')"
    >
      EN
    </button>
    <button
      class="btn btn-sm"
      :class="localeStore.locale === 'es' ? 'btn-primary' : 'btn-ghost'"
      @click="switchLocale('es')"
    >
      ES
    </button>
  </div>
</template>

<script setup lang="ts">
import { useLocaleStore, type SupportedLocale } from '@/stores/locale.store'
import { useAuthStore } from '@/stores/auth.store'
import http from '@/api/axios'

const localeStore = useLocaleStore()
const authStore = useAuthStore()

function switchLocale(lang: SupportedLocale): void {
  localeStore.setLocale(lang)
  if (authStore.isAuthenticated) {
    http.patch('/api/auth/me/locale', { locale: lang }).catch(() => {
      // Ignore errors — locale is already applied locally
    })
  }
}
</script>

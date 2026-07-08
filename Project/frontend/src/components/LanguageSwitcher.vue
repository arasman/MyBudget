<template>
  <div class="flex gap-2">
    <button
      class="btn btn-sm"
      :class="localeStore.locale === 'en' ? 'btn-primary' : 'btn-ghost'"
      @click="localeStore.setLocale('en')"
    >
      EN
    </button>
    <button
      class="btn btn-sm"
      :class="localeStore.locale === 'es' ? 'btn-primary' : 'btn-ghost'"
      @click="localeStore.setLocale('es')"
    >
      ES
    </button>
  </div>
</template>

<script setup lang="ts">
import { useLocaleStore } from '@/stores/locale.store'

const localeStore = useLocaleStore()
</script>

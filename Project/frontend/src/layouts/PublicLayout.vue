<template>
  <PublicBackdrop>
    <div class="min-h-screen flex flex-col">
      <header class="flex justify-between items-center px-4 py-4">
        <RouterLink
          to="/"
          class="btn btn-ghost text-lg font-bold"
        >
          {{ $t('common.appName') }}
        </RouterLink>
        <LanguageSwitcher />
      </header>
      <div class="flex-1 flex items-center justify-center px-4">
        <div class="card w-full max-w-md bg-base-100 shadow-xl">
          <div class="card-body">
            <RouterView />
          </div>
        </div>
      </div>
      <AppFooter />
    </div>
  </PublicBackdrop>
</template>

<script setup lang="ts">
import LanguageSwitcher from '@/components/LanguageSwitcher.vue'
import PublicBackdrop from '@/components/PublicBackdrop.vue'
import AppFooter from '@/components/AppFooter.vue'
</script>

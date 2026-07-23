<template>
  <div class="min-h-screen flex items-center justify-center bg-base-200 px-4 relative">
    <div class="absolute top-4 right-4">
      <LanguageSwitcher />
    </div>
    <div class="card w-full max-w-md bg-base-100 shadow-xl">
      <div class="card-body">
        <RouterView />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import LanguageSwitcher from '@/components/LanguageSwitcher.vue'
</script>

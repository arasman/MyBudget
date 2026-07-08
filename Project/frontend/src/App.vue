<template>
  <RouterView />
</template>

<script setup lang="ts">
import { onMounted } from 'vue'

// Set initial theme on html element
onMounted(() => {
  const savedTheme = localStorage.getItem('theme') ?? 'light'
  document.documentElement.setAttribute('data-theme', savedTheme)
})
</script>

<template>
  <div class="flex items-center gap-3">
    <button
      class="btn btn-ghost btn-sm"
      :disabled="!hasPrevious"
      :aria-label="t('currentSituation.navigation.previous')"
      @click="emit('navigate', 'previous')"
    >
      ←
    </button>

    <span class="font-mono text-base font-semibold min-w-[120px] text-center">
      {{ currentDate ?? '—' }}
    </span>

    <button
      class="btn btn-ghost btn-sm"
      :disabled="!hasNext"
      :aria-label="t('currentSituation.navigation.next')"
      @click="emit('navigate', 'next')"
    >
      →
    </button>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{
  currentDate: string | null
  hasPrevious: boolean
  hasNext: boolean
}>()

const emit = defineEmits<{
  navigate: [direction: 'previous' | 'next']
}>()

const { t } = useI18n()
</script>

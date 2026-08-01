<template>
  <div
    class="card bg-base-200 p-4 h-[18rem] md:h-[24rem] flex flex-col items-center justify-center text-center gap-2"
  >
    <TrendingUp
      class="w-8 h-8 text-base-content/30"
      aria-hidden="true"
    />
    <p class="font-semibold text-base-content/70">
      {{ t('dashboard.band.insufficientData.title') }}
    </p>
    <p class="text-sm text-base-content/50 max-w-xs">
      {{ t('dashboard.band.insufficientData.description') }}
    </p>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { TrendingUp } from 'lucide-vue-next'

const { t } = useI18n()
</script>

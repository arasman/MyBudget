<template>
  <div
    role="alert"
    class="alert alert-warning h-[18rem] md:h-[24rem] flex flex-col items-center justify-center text-center gap-2"
  >
    <TriangleAlert
      class="w-8 h-8"
      aria-hidden="true"
    />
    <p class="font-semibold">
      {{ t('dashboard.currencyMismatch.title') }}
    </p>
    <p class="text-sm max-w-xs">
      {{ t('dashboard.currencyMismatch.description') }}
    </p>
  </div>
</template>

<script setup lang="ts">
// DASH-12: rendered instead of BaseChart whenever the selected periods span
// Cycles with different defaultCurrencyId — the guard blocks the chart
// entirely rather than plotting mismatched currencies on one shared axis
// (design.md "Cross-cycle currency risk", mirrors DASH-9's "MUST NOT blend"
// precedent). Same fixed footprint as BaseChart/InsufficientDataState so it
// swaps in-place without layout shift.
import { useI18n } from 'vue-i18n'
import { TriangleAlert } from 'lucide-vue-next'

const { t } = useI18n()
</script>

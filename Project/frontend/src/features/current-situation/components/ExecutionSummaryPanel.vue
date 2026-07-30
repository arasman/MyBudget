<template>
  <div class="card bg-base-200 p-4">
    <h3 class="text-sm font-semibold mb-3 text-base-content/70 uppercase tracking-wide">
      {{ t('currentSituation.executionSummary.title') }}
    </h3>
    <div class="grid grid-cols-3 gap-4">
      <div class="text-center">
        <div class="text-xs text-base-content/50 mb-1">
          {{ t('currentSituation.executionSummary.totalBudgeted') }}
        </div>
        <div class="font-mono font-semibold">{{ formatAmount(summary.totalBudgeted) }}</div>
      </div>
      <div class="text-center">
        <div class="text-xs text-base-content/50 mb-1">
          {{ t('currentSituation.executionSummary.totalRegistered') }}
        </div>
        <div class="font-mono font-semibold">{{ formatAmount(summary.totalRegistered) }}</div>
      </div>
      <div class="text-center">
        <div class="text-xs text-base-content/50 mb-1">
          {{ t('currentSituation.executionSummary.remaining') }}
        </div>
        <div
          class="font-mono font-semibold"
          :class="summary.remaining >= 0 ? 'text-success' : 'text-error'"
        >
          {{ formatAmount(summary.remaining) }}
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { BudgetExecutionSummaryDto } from '../types/cutRecord'

defineProps<{
  summary: BudgetExecutionSummaryDto
}>()

const { t } = useI18n()

function formatAmount(value: number): string {
  return value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
</script>

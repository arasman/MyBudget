<template>
  <div class="card bg-base-200 p-4">
    <h3 class="text-sm font-semibold mb-3 text-base-content/70 uppercase tracking-wide">
      {{ t('currentSituation.totals.title') }}
    </h3>
    <div class="grid grid-cols-1 gap-2">
      <!-- Assets -->
      <div class="flex justify-between items-center">
        <span class="text-sm text-success">{{ t('currentSituation.totals.positive') }}</span>
        <div class="text-right">
          <span class="font-mono font-semibold">{{ formatAmount(totals.totalPositive) }}</span>
          <span v-if="totals.totalPositiveAlt !== 0" class="text-xs text-base-content/50 ml-2">
            / {{ formatAmount(totals.totalPositiveAlt) }}
          </span>
        </div>
      </div>

      <!-- Liabilities -->
      <div class="flex justify-between items-center">
        <span class="text-sm text-error">{{ t('currentSituation.totals.negative') }}</span>
        <div class="text-right">
          <span class="font-mono font-semibold">{{ formatAmount(totals.totalNegative) }}</span>
          <span v-if="totals.totalNegativeAlt !== 0" class="text-xs text-base-content/50 ml-2">
            / {{ formatAmount(totals.totalNegativeAlt) }}
          </span>
        </div>
      </div>

      <div class="divider my-1"></div>

      <!-- Budget execution summary -->
      <div class="flex justify-between items-center">
        <span class="text-sm text-base-content/70">{{ t('currentSituation.executionSummary.totalBudgeted') }}</span>
        <span class="font-mono text-sm">{{ formatAmount(executionSummary.totalBudgeted) }}</span>
      </div>
      <div class="flex justify-between items-center">
        <span class="text-sm text-base-content/70">{{ t('currentSituation.executionSummary.totalRegistered') }}</span>
        <span class="font-mono text-sm">{{ formatAmount(executionSummary.totalRegistered) }}</span>
      </div>
      <div class="flex justify-between items-center">
        <span class="text-sm text-base-content/70">{{ t('currentSituation.executionSummary.remaining') }}</span>
        <span
          class="font-mono text-sm"
          :class="executionSummary.remaining >= 0 ? 'text-success' : 'text-error'"
        >
          {{ formatAmount(executionSummary.remaining) }}
        </span>
      </div>

      <div class="divider my-1"></div>

      <!-- Net Position -->
      <div class="flex justify-between items-center font-bold">
        <span class="text-sm">{{ t('currentSituation.totals.deudaEnCurso') }}</span>
        <div class="text-right">
          <span
            class="font-mono"
            :class="totals.totalDeudaEnCurso >= 0 ? 'text-success' : 'text-error'"
          >
            {{ formatAmount(totals.totalDeudaEnCurso) }}
          </span>
          <span
            v-if="totals.totalDeudaEnCursoAlt !== 0"
            class="text-xs text-base-content/50 ml-2"
          >
            / {{ formatAmount(totals.totalDeudaEnCursoAlt) }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { CutTotalsDto, BudgetExecutionSummaryDto } from '../types/cutRecord'

defineProps<{
  totals: CutTotalsDto
  executionSummary: BudgetExecutionSummaryDto
}>()

const { t } = useI18n()

function formatAmount(value: number): string {
  return value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
</script>

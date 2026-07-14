<template>
  <tr v-show="!categoryCollapsed" class="text-xs text-base-content/60">
    <!-- Sticky label cell -->
    <td class="sticky left-0 z-10 bg-base-100 px-3 py-1 border-b border-base-300">
      <div class="pl-12 flex gap-2">
        <span>{{ t('budgetMatrix.rows.estimatedVariance') }}</span>
        <span class="text-base-content/40">|</span>
        <span>{{ t('budgetMatrix.rows.executedVariance') }}</span>
      </div>
    </td>

    <!-- Variance cells per visible period -->
    <template v-for="period in visiblePeriods" :key="period.id">
      <!-- Est. - Real -->
      <td class="text-right px-3 py-1 border-b border-base-300">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-3 w-12 ml-auto" />
        <span v-else :class="estimatedVarianceClass(period.id)">
          {{ formatVariance(estimatedVariance(period.id)) }}
        </span>
      </td>
      <!-- Real - Ejecutado -->
      <td class="text-right px-3 py-1 border-b border-base-300">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-3 w-12 ml-auto" />
        <span v-else :class="executedVarianceClass(period.id)">
          {{ formatVariance(executedVariance(period.id)) }}
        </span>
      </td>
    </template>
  </tr>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useBudgetMatrixStore } from '../store'
import { useCurrencyDisplay } from '../composables/useCurrencyDisplay'
import type { BudgetLineResponse, PeriodSummary } from '@/features/budget-structure/types'
import type { LineTotalDto } from '../types'

const props = defineProps<{
  line: BudgetLineResponse
  categoryCollapsed: boolean
  visiblePeriods: PeriodSummary[]
}>()

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()
const { formatAmount } = useCurrencyDisplay(matrixStore)

function getLineTotal(periodId: string): LineTotalDto | undefined {
  const totals = matrixStore.periodTotals[periodId]
  return totals?.lineTotals.find((lt) => lt.budgetLineId === props.line.id)
}

/**
 * Est. - Real: estimatedAmount (fallback to budgetedAmount) minus budgetedAmount.
 * Since LineTotalDto has no estimatedAmount, this always returns 0 for the skeleton.
 */
function estimatedVariance(periodId: string): number {
  const lt = getLineTotal(periodId)
  if (!lt) return 0
  // LineTotalDto does not have estimatedAmount in PR1 — returns 0 (documented deviation)
  return 0
}

/**
 * Real - Ejecutado: budgetedAmount minus netExecuted.
 */
function executedVariance(periodId: string): number {
  const lt = getLineTotal(periodId)
  if (!lt) return 0
  return lt.budgetedAmount - lt.netExecuted
}

function formatVariance(value: number): string {
  return formatAmount(value, '')
}

function estimatedVarianceClass(periodId: string): string {
  const v = estimatedVariance(periodId)
  if (v > 0) return 'text-success'
  if (v < 0) return 'text-error'
  return ''
}

function executedVarianceClass(periodId: string): string {
  const v = executedVariance(periodId)
  if (v > 0) return 'text-success'
  if (v < 0) return 'text-error'
  return ''
}
</script>

<template>
  <tr class="bg-base-300 text-base-content font-bold border-t-2 border-base-content/20">
    <!-- Sticky label cell -->
    <th class="sticky left-0 z-10 px-3 py-2 text-left text-xs whitespace-nowrap bg-base-300">
      {{ label }}
    </th>

    <!-- Per-period totals -->
    <template v-for="period in visiblePeriods" :key="period.id">
      <td class="px-2 py-2 text-right text-xs">
        {{ formatAmount(totalBudgeted(period.id)) }}
      </td>
      <td class="px-2 py-2 text-right text-xs">
        {{ formatAmount(totalExecuted(period.id)) }}
      </td>
      <td class="px-2 py-2 text-right text-xs" :class="differenceClass(period.id)">
        {{ formatAmount(totalBudgeted(period.id) - totalExecuted(period.id)) }}
      </td>
    </template>
  </tr>
</template>

<script setup lang="ts">
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import { useCurrencyDisplay } from '../composables/useCurrencyDisplay'
import type { PeriodSummary } from '@/features/budget-structure/types'

const props = defineProps<{
  /** i18n-resolved label for the total row */
  label: string
  visiblePeriods: PeriodSummary[]
}>()

const matrixStore = useBudgetMatrixStore()
const structureStore = useBudgetStructureStore()
const { convert } = useCurrencyDisplay(matrixStore)

/** Sum budgeted amounts across ALL non-deleted budget lines. */
function totalBudgeted(_periodId: string): number {
  return structureStore.budgetLines
    .filter((l) => !l.deletedAt)
    .reduce((sum, l) => sum + (l.budgetedAmount ?? 0), 0)
}

/** Sum net-executed amounts across ALL categories for a given period. */
function totalExecuted(periodId: string): number {
  const totals = matrixStore.periodTotals[periodId]
  if (!totals) return 0
  return totals.categoryTotals.reduce((sum, ct) => sum + ct.netTotal, 0)
}

function formatAmount(amount: number): string {
  const converted = convert(amount)
  return converted.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}

function differenceClass(periodId: string): string {
  const diff = totalBudgeted(periodId) - totalExecuted(periodId)
  if (diff > 0) return 'text-success'
  if (diff < 0) return 'text-error'
  return ''
}
</script>

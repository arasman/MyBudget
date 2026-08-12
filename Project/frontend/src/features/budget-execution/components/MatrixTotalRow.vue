<template>
  <tr class="bg-base-300 text-base-content font-bold border-t-2 border-base-content/20">
    <!-- Sticky label cell -->
    <th class="sticky left-0 z-10 px-3 py-2 text-left text-xs whitespace-nowrap bg-base-300">
      {{ label }}
    </th>

    <!-- Per-period totals -->
    <template
      v-for="period in visiblePeriods"
      :key="period.id"
    >
      <td class="px-2 py-2 text-right text-xs">
        {{ formatAmount(totalBudgeted(period.id), currencySymbol) }}
      </td>
      <td class="px-2 py-2 text-right text-xs">
        {{ formatAmount(totalExecuted(period.id), currencySymbol) }}
      </td>
      <td
        class="px-2 py-2 text-right text-xs"
        :class="differenceClass(period.id)"
      >
        {{ formatAmount(totalBudgeted(period.id) - totalExecuted(period.id), currencySymbol) }}
      </td>
    </template>
  </tr>
</template>

<script setup lang="ts">
import { computed } from 'vue'
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
const { formatAmount } = useCurrencyDisplay(matrixStore)

/** Currency symbol derived from cycle based on the active display currency */
const currencySymbol = computed<string>(() =>
  matrixStore.displayCurrency === 'alternate'
    ? structureStore.currentCycle?.alternateCurrency?.symbol ?? ''
    : structureStore.currentCycle?.defaultCurrency?.symbol ?? '',
)

/** Sum budgeted across all 3 lineTypes for a given period. */
function totalBudgeted(periodId: string): number {
  const expense = matrixStore.subtotalByLineType(periodId, 'Expense')
  const preventive = matrixStore.subtotalByLineType(periodId, 'PreventiveSavings')
  const longTerm = matrixStore.subtotalByLineType(periodId, 'LongTermSavings')
  return expense.budgeted + preventive.budgeted + longTerm.budgeted
}

/** Sum executed across all 3 lineTypes for a given period. */
function totalExecuted(periodId: string): number {
  const expense = matrixStore.subtotalByLineType(periodId, 'Expense')
  const preventive = matrixStore.subtotalByLineType(periodId, 'PreventiveSavings')
  const longTerm = matrixStore.subtotalByLineType(periodId, 'LongTermSavings')
  return expense.executed + preventive.executed + longTerm.executed
}

function differenceClass(periodId: string): string {
  const diff = totalBudgeted(periodId) - totalExecuted(periodId)
  if (diff > 0) return 'text-success'
  if (diff < 0) return 'text-error'
  return ''
}
</script>

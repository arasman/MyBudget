<template>
  <tr :class="rowClass">
    <!-- Sticky label cell -->
    <th
      class="sticky left-0 z-10 px-3 py-2 text-left text-xs whitespace-nowrap"
      :class="rowClass"
    >
      {{ label }}
    </th>

    <!-- Per-period totals (Budgeted + Executed columns per period) -->
    <template v-for="period in visiblePeriods" :key="period.id">
      <!-- Budgeted total -->
      <td class="px-2 py-2 text-right text-xs" :class="rowClass">
        {{ formatAmount(budgetedForPeriod(period.id)) }}
      </td>
      <!-- Executed total -->
      <td class="px-2 py-2 text-right text-xs" :class="rowClass">
        {{ formatAmount(executedForPeriod(period.id)) }}
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

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

const props = defineProps<{
  /** 1 = Expense, 2 = LongTermSavings, 3 = PreventiveSavings */
  lineType: number
  /** i18n-resolved label for this summary row */
  label: string
  visiblePeriods: PeriodSummary[]
}>()

// ---------------------------------------------------------------------------
// Stores + composables
// ---------------------------------------------------------------------------

const matrixStore = useBudgetMatrixStore()
const structureStore = useBudgetStructureStore()
const { convert, formatAmount: _fmt } = useCurrencyDisplay(matrixStore)

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Map lineType number → string used in BudgetLineResponse */
const lineTypeString = computed((): string => {
  switch (props.lineType) {
    case 1:
      return 'Expense'
    case 2:
      return 'LongTermSavings'
    case 3:
      return 'PreventiveSavings'
    default:
      return ''
  }
})

/**
 * Collect categoryIds whose lines match this lineType.
 * A category is included if ANY of its budget lines match the lineType.
 */
const matchingCategoryIds = computed((): Set<string> => {
  const ids = new Set<string>()
  for (const line of structureStore.budgetLines) {
    if (line.lineType === lineTypeString.value && line.categoryId) {
      ids.add(line.categoryId)
    }
  }
  return ids
})

/** Sum budgeted amounts for matching categories in a given period */
function budgetedForPeriod(periodId: string): number {
  const totals = matrixStore.periodTotals[periodId]
  if (!totals) return 0
  return totals.categoryTotals
    .filter((ct) => matchingCategoryIds.value.has(ct.categoryId))
    .reduce((sum, ct) => sum + ct.budgetedAmount, 0)
}

/** Sum net-executed amounts for matching categories in a given period */
function executedForPeriod(periodId: string): number {
  const totals = matrixStore.periodTotals[periodId]
  if (!totals) return 0
  return totals.categoryTotals
    .filter((ct) => matchingCategoryIds.value.has(ct.categoryId))
    .reduce((sum, ct) => sum + ct.netExecuted, 0)
}

/** Format an amount with currency conversion applied */
function formatAmount(amount: number): string {
  const converted = convert(amount)
  return converted.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}

// ---------------------------------------------------------------------------
// Color class per lineType
// ---------------------------------------------------------------------------

const rowClass = computed((): string => {
  switch (props.lineType) {
    case 1:
      return 'bg-error/10 text-error font-semibold'
    case 2:
      return 'bg-success/10 text-success font-semibold'
    case 3:
      return 'bg-warning/10 text-warning font-semibold'
    default:
      return ''
  }
})
</script>

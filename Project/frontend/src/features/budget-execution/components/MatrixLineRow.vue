<template>
  <tr v-show="!categoryCollapsed" class="hover:bg-base-100/50">
    <!-- Sticky label cell -->
    <td class="sticky left-0 z-10 bg-base-100 px-3 py-2 border-b border-base-300">
      <div class="flex items-center gap-2 pl-12">
        <span class="flex-1 text-sm">{{ line.name }}</span>

        <!-- Reorder buttons -->
        <button
          type="button"
          class="btn btn-xs btn-ghost btn-square"
          :disabled="isFirst"
          :title="t('budgetMatrix.rows.moveUp')"
          @click="$emit('move-up')"
        >
          <ArrowUp :size="12" />
        </button>
        <button
          type="button"
          class="btn btn-xs btn-ghost btn-square"
          :disabled="isLast"
          :title="t('budgetMatrix.rows.moveDown')"
          @click="$emit('move-down')"
        >
          <ArrowDown :size="12" />
        </button>
      </div>
    </td>

    <!-- Real + Ejecutado cells per visible period -->
    <template v-for="period in visiblePeriods" :key="period.id">
      <!-- Real (budgetedAmount) — read-only display cell -->
      <td class="text-right px-3 py-2 border-b border-base-300 text-sm">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else>{{ formatLineAmount(period.id) }}</span>
      </td>

      <!-- Ejecutado (netTotal) — interactive MatrixCell -->
      <MatrixCell
        :amount="getLineTotal(period.id)?.netTotal ?? 0"
        :loading="matrixStore.loadingPeriods[period.id] ?? false"
        @dblclick="matrixStore.openExecutionModal(line.id, period.id)"
      />
    </template>
  </tr>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ArrowUp, ArrowDown } from 'lucide-vue-next'
import { useBudgetMatrixStore } from '../store'
import { useCurrencyDisplay } from '../composables/useCurrencyDisplay'
import MatrixCell from './MatrixCell.vue'
import type { BudgetLineResponse, PeriodSummary } from '@/features/budget-structure/types'
import type { LineTotalDto } from '../types'

const props = defineProps<{
  line: BudgetLineResponse
  categoryCollapsed: boolean
  visiblePeriods: PeriodSummary[]
  isFirst: boolean
  isLast: boolean
}>()

defineEmits<{
  'move-up': []
  'move-down': []
}>()

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()
const { formatAmount } = useCurrencyDisplay(matrixStore)

function getLineTotal(periodId: string): LineTotalDto | undefined {
  const totals = matrixStore.periodTotals[periodId]
  return totals?.lineTotals.find((lt) => lt.budgetLineId === props.line.id)
}

function formatLineAmount(_periodId: string): string {
  return formatAmount(props.line.budgetedAmount ?? 0, '')
}
</script>

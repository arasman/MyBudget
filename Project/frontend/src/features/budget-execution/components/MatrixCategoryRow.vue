<template>
  <tr v-show="!collapsed" data-testid="matrix-category-row" class="hover:bg-base-100">
    <!-- Sticky label cell -->
    <td class="sticky left-0 z-10 bg-base-100 px-3 py-2 border-b border-base-300">
      <div class="flex items-center gap-2 pl-6">
        <!-- Collapse/expand toggle -->
        <button
          type="button"
          class="btn btn-xs btn-ghost btn-square"
          :title="categoryCollapsed ? t('budgetMatrix.rows.expandCategory') : t('budgetMatrix.rows.collapseCategory')"
          @click="$emit('toggle-category-collapse')"
        >
          <ChevronDown v-if="!categoryCollapsed" :size="14" />
          <ChevronRight v-else :size="14" />
        </button>

        <span class="flex-1 text-sm">{{ category.name }}</span>

        <!-- Reorder buttons -->
        <button
          type="button"
          class="btn btn-xs btn-ghost btn-square"
          :disabled="isFirst"
          :title="t('budgetMatrix.rows.moveUp')"
          @click="$emit('move-up')"
        >
          <ArrowUp :size="14" />
        </button>
        <button
          type="button"
          class="btn btn-xs btn-ghost btn-square"
          :disabled="isLast"
          :title="t('budgetMatrix.rows.moveDown')"
          @click="$emit('move-down')"
        >
          <ArrowDown :size="14" />
        </button>
      </div>
    </td>

    <!-- Aggregated category totals per visible period -->
    <template v-for="period in visiblePeriods" :key="period.id">
      <!-- Real (budgetedAmount) -->
      <td class="text-right px-3 py-2 border-b border-base-300 text-sm">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else>{{ formatCategoryTotal(period.id, 'budgeted') }}</span>
      </td>
      <!-- Ejecutado (netExecuted) -->
      <td class="text-right px-3 py-2 border-b border-base-300 text-sm">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else>{{ formatCategoryTotal(period.id, 'executed') }}</span>
      </td>
    </template>
  </tr>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ChevronDown, ChevronRight, ArrowUp, ArrowDown } from 'lucide-vue-next'
import { useBudgetMatrixStore } from '../store'
import { useCurrencyDisplay } from '../composables/useCurrencyDisplay'
import type { CategoryItem, PeriodSummary } from '@/features/budget-structure/types'

const props = defineProps<{
  category: CategoryItem
  groupId: string
  visiblePeriods: PeriodSummary[]
  collapsed: boolean
  categoryCollapsed: boolean
  isFirst: boolean
  isLast: boolean
}>()

defineEmits<{
  'toggle-category-collapse': []
  'move-up': []
  'move-down': []
  'insert-line': [{ categoryId: string }]
  'reorder-line': [{ lineId: string; direction: 'up' | 'down' }]
}>()

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()
const { formatAmount } = useCurrencyDisplay(matrixStore)

function formatCategoryTotal(periodId: string, type: 'budgeted' | 'executed'): string {
  const totals = matrixStore.periodTotals[periodId]
  if (!totals) return formatAmount(0, '')

  const catTotal = totals.categoryTotals.find((ct) => ct.categoryId === props.category.id)
  if (!catTotal) return formatAmount(0, '')

  return formatAmount(type === 'budgeted' ? 0 : catTotal.netTotal, '')
}
</script>

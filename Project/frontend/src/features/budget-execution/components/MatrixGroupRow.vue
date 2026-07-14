<template>
  <tr
    data-testid="matrix-group-row"
    class="bg-base-200 font-semibold"
    :class="{ 'opacity-50 text-base-content/50': group.deletedAt }"
  >
    <!-- Sticky label cell -->
    <td class="sticky left-0 z-10 bg-base-200 px-3 py-2 border-b border-base-300">
      <div class="flex items-center gap-2">
        <!-- Collapse/expand toggle -->
        <button
          data-testid="group-collapse-btn"
          type="button"
          class="btn btn-xs btn-ghost btn-square"
          :title="collapsed ? t('budgetMatrix.rows.expandGroup') : t('budgetMatrix.rows.collapseGroup')"
          @click="$emit('toggle-collapse')"
        >
          <ChevronDown v-if="!collapsed" :size="14" />
          <ChevronRight v-else :size="14" />
        </button>

        <span class="flex-1">{{ group.name }}</span>

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

    <!-- Aggregated totals per visible period -->
    <template v-for="period in visiblePeriods" :key="period.id">
      <!-- Real (budgetedAmount sum) -->
      <td class="text-right px-3 py-2 border-b border-base-300">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else>{{ formatGroupTotal(period.id, 'budgeted') }}</span>
      </td>
      <!-- Ejecutado (netExecuted sum) -->
      <td class="text-right px-3 py-2 border-b border-base-300">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else>{{ formatGroupTotal(period.id, 'executed') }}</span>
      </td>
    </template>
  </tr>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ChevronDown, ChevronRight, ArrowUp, ArrowDown } from 'lucide-vue-next'
import { useBudgetMatrixStore } from '../store'
import { useCurrencyDisplay } from '../composables/useCurrencyDisplay'
import type { CategoryGroupResponse, PeriodSummary } from '@/features/budget-structure/types'

const props = defineProps<{
  group: CategoryGroupResponse
  visiblePeriods: PeriodSummary[]
  collapsed: boolean
  isFirst: boolean
  isLast: boolean
}>()

defineEmits<{
  'toggle-collapse': []
  'move-up': []
  'move-down': []
}>()

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()
const { formatAmount } = useCurrencyDisplay(matrixStore)

/**
 * Aggregate group totals from categoryTotals.
 * categoryTotals carries categoryGroupId which we match against the group's id.
 */
function formatGroupTotal(periodId: string, type: 'budgeted' | 'executed'): string {
  const totals = matrixStore.periodTotals[periodId]
  if (!totals) return formatAmount(0, '')

  const total = totals.categoryTotals
    .filter((ct) => ct.categoryGroupId === props.group.id)
    .reduce((sum, ct) => sum + (type === 'budgeted' ? 0 : ct.netTotal), 0)

  return formatAmount(total, '')
}
</script>

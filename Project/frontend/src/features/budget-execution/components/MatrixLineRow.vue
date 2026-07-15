<template>
  <tr
    v-show="!categoryCollapsed"
    data-testid="matrix-line-row"
    class="hover:bg-base-100/50"
    :class="{ 'opacity-50 text-base-content/50': line.deletedAt }"
  >
    <!-- Sticky label cell -->
    <td class="sticky left-0 z-10 bg-base-100 px-3 py-2 border-b border-base-300">
      <div class="flex items-center gap-1 pl-12">
        <!-- Delete confirmation mode -->
        <template v-if="confirmingDelete">
          <span class="text-xs text-error flex-1">{{ t('budgetMatrix.rows.confirmDelete') }}</span>
          <button type="button" class="btn btn-xs btn-error" :disabled="acting" @click="doDelete">
            <span v-if="acting" class="loading loading-spinner loading-xs" />
            <span v-else>{{ t('budgetMatrix.rows.delete') }}</span>
          </button>
          <button type="button" class="btn btn-xs btn-ghost" @click="confirmingDelete = false">{{ t('common.cancel') }}</button>
        </template>

        <!-- Restore confirmation mode -->
        <template v-else-if="confirmingRestore">
          <span class="text-xs flex-1">{{ t('budgetMatrix.rows.confirmRestore') }}</span>
          <button type="button" class="btn btn-xs btn-success btn-outline" :disabled="acting" @click="doRestore(true)">
            <span v-if="acting" class="loading loading-spinner loading-xs" />
            <span v-else>{{ t('budgetMatrix.rows.restoreWithExecutions') }}</span>
          </button>
          <button type="button" class="btn btn-xs btn-ghost" :disabled="acting" @click="doRestore(false)">{{ t('budgetMatrix.rows.restoreStructureOnly') }}</button>
          <button type="button" class="btn btn-xs btn-ghost" @click="confirmingRestore = false">{{ t('common.cancel') }}</button>
        </template>

        <!-- Normal display mode -->
        <template v-else>
          <span
            class="flex-1 text-sm cursor-pointer"
            :class="{ 'line-through': line.deletedAt }"
            @dblclick="!line.deletedAt && openEditModal()"
          >{{ line.name }}</span>

          <!-- Reorder + delete (only non-deleted) -->
          <template v-if="!line.deletedAt">
            <button type="button" class="btn btn-xs btn-ghost btn-square" :disabled="isFirst" :title="t('budgetMatrix.rows.moveUp')" @click="$emit('move-up')">
              <ArrowUp :size="12" />
            </button>
            <button type="button" class="btn btn-xs btn-ghost btn-square" :disabled="isLast" :title="t('budgetMatrix.rows.moveDown')" @click="$emit('move-down')">
              <ArrowDown :size="12" />
            </button>
            <button type="button" class="btn btn-xs btn-ghost btn-square text-error" :title="t('budgetMatrix.rows.delete')" @click="confirmingDelete = true">
              <Trash2 :size="12" />
            </button>
          </template>

          <!-- Restore button (only deleted + showDeleted mode + parent not deleted) -->
          <button
            v-if="line.deletedAt && matrixStore.showDeleted && !parentDeleted"
            type="button"
            class="btn btn-xs btn-ghost btn-square text-success"
            :title="t('budgetMatrix.rows.restore')"
            @click="confirmingRestore = true"
          >
            <RotateCcw :size="12" />
          </button>
        </template>
      </div>
    </td>

    <!-- Budgeted + Executed + Difference cells per visible period -->
    <template v-for="period in visiblePeriods" :key="period.id">
      <!-- Budgeted — read-only display cell -->
      <td class="text-right px-3 py-2 border-b border-base-300 text-sm">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else>{{ formatLineAmount() }}</span>
      </td>

      <!-- Ejecutado — interactive cell (disabled for deleted lines) -->
      <MatrixCell
        :amount="getLineTotal(period.id)?.netTotal ?? 0"
        :loading="matrixStore.loadingPeriods[period.id] ?? false"
        @dblclick="!line.deletedAt && matrixStore.openExecutionModal(line.id, period.id)"
      />

      <!-- Difference = Budgeted - Executed -->
      <td class="text-right px-3 py-2 border-b border-base-300 text-sm">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else :class="differenceClass(period.id)">{{ formatDifference(period.id) }}</span>
      </td>
    </template>
  </tr>

  <!-- Edit modal (teleported to avoid table nesting issues) -->
  <Teleport to="body">
    <BudgetLineModal
      v-if="showEditModal"
      :model-value="line"
      :category-groups="structureStore.categoryGroups"
      @submit="handleEditSubmit"
      @cancel="showEditModal = false"
    />
  </Teleport>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { ArrowUp, ArrowDown, Trash2, RotateCcw } from 'lucide-vue-next'
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import { useCurrencyDisplay } from '../composables/useCurrencyDisplay'
import MatrixCell from './MatrixCell.vue'
import BudgetLineModal from '@/features/budget-structure/components/BudgetLineModal.vue'
import type { BudgetLineResponse, PeriodSummary, CreateBudgetLinePayload } from '@/features/budget-structure/types'
import type { LineTotalDto } from '../types'

const props = defineProps<{
  line: BudgetLineResponse
  budgetId: string
  categoryCollapsed: boolean
  visiblePeriods: PeriodSummary[]
  isFirst: boolean
  isLast: boolean
  parentDeleted?: boolean
}>()

defineEmits<{
  'move-up': []
  'move-down': []
}>()

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()
const structureStore = useBudgetStructureStore()
const { formatAmount } = useCurrencyDisplay(matrixStore)

// Edit modal state
const showEditModal = ref(false)

// Action confirmation state
const confirmingDelete = ref(false)
const confirmingRestore = ref(false)
const acting = ref(false)

// Derive periodId from loaded period (lines are loaded from first period)
const loadedPeriodId = computed(() => matrixStore.allPeriods[0]?.id ?? '')

function openEditModal(): void {
  showEditModal.value = true
}

async function handleEditSubmit(payload: CreateBudgetLinePayload): Promise<void> {
  showEditModal.value = false
  const periodId = loadedPeriodId.value
  if (!periodId) return
  await structureStore.updateLine(props.budgetId, periodId, props.line.id, {
    name: payload.name,
    lineType: payload.lineType ?? props.line.lineType,
    isRecurring: payload.isRecurring ?? props.line.isRecurring,
    categoryGroupId: payload.categoryGroupId ?? props.line.categoryGroupId,
    categoryId: payload.categoryId ?? props.line.categoryId,
    budgetedAmount: payload.budgetedAmount,
  })
  await matrixStore.invalidateAllPeriods()
}

async function doDelete(): Promise<void> {
  acting.value = true
  try {
    await structureStore.deleteLine(props.budgetId, loadedPeriodId.value, props.line.id)
    confirmingDelete.value = false
    await matrixStore.invalidateAllPeriods()
  } finally {
    acting.value = false
  }
}

async function doRestore(includeExecutionRecords: boolean): Promise<void> {
  acting.value = true
  try {
    await structureStore.restoreLine(props.budgetId, loadedPeriodId.value, props.line.id, includeExecutionRecords)
    confirmingRestore.value = false
    await matrixStore.invalidateAllPeriods()
  } finally {
    acting.value = false
  }
}

function getLineTotal(periodId: string): LineTotalDto | undefined {
  const totals = matrixStore.periodTotals[periodId]
  return totals?.lineTotals.find((lt) => lt.budgetLineId === props.line.id)
}

function formatLineAmount(): string {
  return formatAmount(props.line.budgetedAmount ?? 0, '')
}

function formatDifference(periodId: string): string {
  const budgeted = props.line.budgetedAmount ?? 0
  const executed = getLineTotal(periodId)?.netTotal ?? 0
  return formatAmount(budgeted - executed, '')
}

function differenceClass(periodId: string): string {
  const diff = (props.line.budgetedAmount ?? 0) - (getLineTotal(periodId)?.netTotal ?? 0)
  if (diff > 0) return 'text-success'
  if (diff < 0) return 'text-error'
  return ''
}
</script>

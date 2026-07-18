<template>
  <tr
    v-show="!collapsed"
    data-testid="matrix-category-row"
    class="hover:bg-base-100"
    :class="{ 'opacity-50 text-base-content/50': category.deletedAt }"
  >
    <!-- Sticky label cell -->
    <td class="sticky left-0 z-10 bg-base-100 px-3 py-2 border-b border-base-300">
      <div class="flex items-center gap-1 pl-6">
        <!-- Collapse/expand toggle -->
        <button
          type="button"
          class="btn btn-xs btn-ghost btn-square shrink-0"
          :title="categoryCollapsed ? t('budgetMatrix.rows.expandCategory') : t('budgetMatrix.rows.collapseCategory')"
          @click="$emit('toggle-category-collapse')"
        >
          <ChevronDown v-if="!categoryCollapsed" :size="14" />
          <ChevronRight v-else :size="14" />
        </button>

        <!-- Inline edit mode -->
        <template v-if="editing">
          <input
            ref="editInput"
            v-model="editName"
            type="text"
            class="input input-xs input-bordered flex-1 min-w-0"
            @keydown.enter="saveEdit"
            @keydown.escape="cancelEdit"
          />
          <button type="button" class="btn btn-xs btn-ghost btn-square text-success" @click="saveEdit">
            <Check :size="12" />
          </button>
          <button type="button" class="btn btn-xs btn-ghost btn-square" @click="cancelEdit">
            <X :size="12" />
          </button>
        </template>

        <!-- Delete confirmation mode -->
        <template v-else-if="confirmingDelete">
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
            :class="{ 'line-through': category.deletedAt }"
            @dblclick="startEdit"
          >{{ category.name }}</span>

          <!-- Reorder + add-line + delete (only non-deleted) -->
          <template v-if="!category.deletedAt">
            <button type="button" class="btn btn-xs btn-ghost btn-square" :disabled="isFirst" :title="t('budgetMatrix.rows.moveUp')" @click="$emit('move-up')">
              <ArrowUp :size="12" />
            </button>
            <button type="button" class="btn btn-xs btn-ghost btn-square" :disabled="isLast" :title="t('budgetMatrix.rows.moveDown')" @click="$emit('move-down')">
              <ArrowDown :size="12" />
            </button>
            <button type="button" class="btn btn-xs btn-ghost btn-square" :title="t('budgetMatrix.rows.addLine')" @click="$emit('add-line')">
              <Plus :size="12" />
            </button>
            <button type="button" class="btn btn-xs btn-ghost btn-square text-error" :title="t('budgetMatrix.rows.delete')" @click="confirmingDelete = true">
              <Trash2 :size="12" />
            </button>
          </template>

          <!-- Restore button (only deleted + showDeleted mode + parent not deleted) -->
          <button
            v-if="category.deletedAt && matrixStore.showDeleted && !parentDeleted"
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

    <!-- Aggregated category totals per visible period -->
    <template v-for="period in visiblePeriods" :key="period.id">
      <td class="text-right px-3 py-2 border-b border-base-300 text-sm">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else>{{ formatCategoryTotal(period.id, 'budgeted') }}</span>
      </td>
      <td class="text-right px-3 py-2 border-b border-base-300 text-sm">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else>{{ formatCategoryTotal(period.id, 'executed') }}</span>
      </td>
      <td class="text-right px-3 py-2 border-b border-base-300 text-sm">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else :class="differenceClass(period.id)">{{ formatCategoryDifference(period.id) }}</span>
      </td>
    </template>
  </tr>
</template>

<script setup lang="ts">
import { ref, computed, nextTick } from 'vue'
import { useI18n } from 'vue-i18n'
import { ChevronDown, ChevronRight, ArrowUp, ArrowDown, Plus, Trash2, RotateCcw, Check, X } from 'lucide-vue-next'
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import { useToastStore } from '@/stores/toast.store'
import { useCurrencyDisplay } from '../composables/useCurrencyDisplay'
import type { CategoryItem, PeriodSummary } from '@/features/budget-structure/types'

const props = defineProps<{
  category: CategoryItem
  groupId: string
  budgetId: string
  visiblePeriods: PeriodSummary[]
  collapsed: boolean
  categoryCollapsed: boolean
  isFirst: boolean
  isLast: boolean
  parentDeleted?: boolean
}>()

defineEmits<{
  'toggle-category-collapse': []
  'move-up': []
  'move-down': []
  'add-line': []
}>()

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()
const structureStore = useBudgetStructureStore()
const toast = useToastStore()
const { formatAmount } = useCurrencyDisplay(matrixStore)

const currencySymbol = computed<string>(() =>
  matrixStore.displayCurrency === 'alternate'
    ? structureStore.currentCycle?.alternateCurrency?.symbol ?? ''
    : structureStore.currentCycle?.defaultCurrency?.symbol ?? '',
)

// Inline edit state
const editing = ref(false)
const editName = ref('')
const editInput = ref<HTMLInputElement | null>(null)

// Action confirmation state
const confirmingDelete = ref(false)
const confirmingRestore = ref(false)
const acting = ref(false)

function startEdit(): void {
  if (props.category.deletedAt) return
  window.getSelection()?.removeAllRanges()
  editName.value = props.category.name
  editing.value = true
  nextTick(() => editInput.value?.focus())
}

function cancelEdit(): void {
  editing.value = false
}

async function saveEdit(): Promise<void> {
  const name = editName.value.trim()
  if (!name) return cancelEdit()
  editing.value = false
  await structureStore.updateCategory(props.budgetId, props.groupId, props.category.id, { name })
  toast.push({ type: 'success', title: t('budgetMatrix.rows.updateCategorySuccess') })
}

async function doDelete(): Promise<void> {
  acting.value = true
  try {
    await structureStore.deleteCategory(props.budgetId, props.groupId, props.category.id)
    confirmingDelete.value = false
    await matrixStore.invalidateAllPeriods()
    toast.push({ type: 'success', title: t('budgetMatrix.rows.deleteSuccess') })
  } finally {
    acting.value = false
  }
}

async function doRestore(includeExecutionRecords: boolean): Promise<void> {
  acting.value = true
  try {
    await structureStore.restoreCategory(props.budgetId, props.groupId, props.category.id, includeExecutionRecords)
    confirmingRestore.value = false
    await matrixStore.invalidateAllPeriods()
    toast.push({ type: 'success', title: t('budgetMatrix.rows.restoreSuccess') })
  } finally {
    acting.value = false
  }
}

function categoryBudgeted(): number {
  return structureStore.budgetLines
    .filter((l) => l.categoryId === props.category.id && !l.deletedAt)
    .reduce((sum, l) => sum + (l.budgetedAmount ?? 0), 0)
}

function categoryExecuted(periodId: string): number {
  const totals = matrixStore.periodTotals[periodId]
  if (!totals) return 0
  const catTotal = totals.categoryTotals.find((ct) => ct.categoryId === props.category.id)
  return catTotal?.netTotal ?? 0
}

function formatCategoryTotal(periodId: string, type: 'budgeted' | 'executed'): string {
  return formatAmount(type === 'budgeted' ? categoryBudgeted() : categoryExecuted(periodId), currencySymbol.value)
}

function formatCategoryDifference(periodId: string): string {
  return formatAmount(categoryBudgeted() - categoryExecuted(periodId), currencySymbol.value)
}

function differenceClass(periodId: string): string {
  const diff = categoryBudgeted() - categoryExecuted(periodId)
  if (diff > 0) return 'text-success'
  if (diff < 0) return 'text-error'
  return ''
}
</script>

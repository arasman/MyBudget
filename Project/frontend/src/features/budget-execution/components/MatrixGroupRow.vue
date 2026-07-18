<template>
  <tr
    data-testid="matrix-group-row"
    class="bg-base-200 font-semibold"
    :class="{ 'opacity-50 text-base-content/50': group.deletedAt }"
  >
    <!-- Sticky label cell -->
    <td class="sticky left-0 z-10 bg-base-200 px-3 py-2 border-b border-base-300">
      <div class="flex items-center gap-1">
        <!-- Drag handle (only non-deleted) -->
        <span
          v-if="!group.deletedAt"
          class="group-drag-handle cursor-grab text-base-content/30 hover:text-base-content shrink-0 select-none"
          title="Drag to reorder"
        >&#8597;</span>

        <!-- Collapse/expand toggle -->
        <button
          data-testid="group-collapse-btn"
          type="button"
          class="btn btn-xs btn-ghost btn-square shrink-0"
          :title="collapsed ? t('budgetMatrix.rows.expandGroup') : t('budgetMatrix.rows.collapseGroup')"
          @click="$emit('toggle-collapse')"
        >
          <ChevronDown v-if="!collapsed" :size="14" />
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
            class="flex-1 cursor-pointer"
            :class="{ 'line-through': group.deletedAt }"
            @dblclick="startEdit"
          >{{ group.name }}</span>

          <!-- Reorder buttons (only non-deleted) -->
          <template v-if="!group.deletedAt">
            <button type="button" class="btn btn-xs btn-ghost btn-square" :disabled="isFirst" :title="t('budgetMatrix.rows.moveUp')" @click="$emit('move-up')">
              <ArrowUp :size="12" />
            </button>
            <button type="button" class="btn btn-xs btn-ghost btn-square" :disabled="isLast" :title="t('budgetMatrix.rows.moveDown')" @click="$emit('move-down')">
              <ArrowDown :size="12" />
            </button>
            <!-- Add category button -->
            <button type="button" class="btn btn-xs btn-ghost btn-square" :title="t('budgetMatrix.rows.addCategory')" @click="$emit('add-category')">
              <Plus :size="12" />
            </button>
            <!-- Delete button -->
            <button type="button" class="btn btn-xs btn-ghost btn-square text-error" :title="t('budgetMatrix.rows.delete')" @click="confirmingDelete = true">
              <Trash2 :size="12" />
            </button>
          </template>

          <!-- Restore button (only deleted + showDeleted mode) -->
          <button
            v-if="group.deletedAt && matrixStore.showDeleted"
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

    <!-- Aggregated totals per visible period -->
    <template v-for="period in visiblePeriods" :key="period.id">
      <td class="text-right px-3 py-2 border-b border-base-300">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else>{{ formatGroupTotal(period.id, 'budgeted') }}</span>
      </td>
      <td class="text-right px-3 py-2 border-b border-base-300">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else>{{ formatGroupTotal(period.id, 'executed') }}</span>
      </td>
      <td class="text-right px-3 py-2 border-b border-base-300">
        <div v-if="matrixStore.loadingPeriods[period.id]" class="skeleton h-4 w-16 ml-auto" />
        <span v-else :class="differenceClass(period.id)">{{ formatGroupDifference(period.id) }}</span>
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
import type { CategoryGroupResponse, PeriodSummary } from '@/features/budget-structure/types'

const props = defineProps<{
  group: CategoryGroupResponse
  visiblePeriods: PeriodSummary[]
  collapsed: boolean
  isFirst: boolean
  isLast: boolean
  budgetId: string
}>()

defineEmits<{
  'toggle-collapse': []
  'move-up': []
  'move-down': []
  'add-category': []
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
  if (props.group.deletedAt) return
  window.getSelection()?.removeAllRanges()
  editName.value = props.group.name
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
  await structureStore.updateGroup(props.budgetId, props.group.id, { name })
  toast.push({ type: 'success', title: t('budgetMatrix.rows.updateGroupSuccess') })
}

async function doDelete(): Promise<void> {
  acting.value = true
  try {
    await structureStore.deleteGroup(props.budgetId, props.group.id)
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
    await structureStore.restoreGroup(props.budgetId, props.group.id, includeExecutionRecords)
    confirmingRestore.value = false
    await matrixStore.invalidateAllPeriods()
    toast.push({ type: 'success', title: t('budgetMatrix.rows.restoreSuccess') })
  } finally {
    acting.value = false
  }
}

function groupBudgeted(): number {
  const groupCategoryIds = props.group.categories.map((c) => c.id)
  return structureStore.budgetLines
    .filter((l) => l.categoryId !== undefined && groupCategoryIds.includes(l.categoryId) && !l.deletedAt)
    .reduce((sum, l) => sum + (l.budgetedAmount ?? 0), 0)
}

function groupExecuted(periodId: string): number {
  const totals = matrixStore.periodTotals[periodId]
  if (!totals) return 0
  return totals.categoryTotals
    .filter((ct) => ct.categoryGroupId === props.group.id)
    .reduce((sum, ct) => sum + ct.netTotal, 0)
}

function formatGroupTotal(periodId: string, type: 'budgeted' | 'executed'): string {
  return formatAmount(type === 'budgeted' ? groupBudgeted() : groupExecuted(periodId), currencySymbol.value)
}

function formatGroupDifference(periodId: string): string {
  return formatAmount(groupBudgeted() - groupExecuted(periodId), currencySymbol.value)
}

function differenceClass(periodId: string): string {
  const diff = groupBudgeted() - groupExecuted(periodId)
  if (diff > 0) return 'text-success'
  if (diff < 0) return 'text-error'
  return ''
}
</script>

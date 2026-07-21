<template>
  <div class="container mx-auto px-4 py-6">
    <!-- Breadcrumb -->
    <div class="breadcrumbs text-sm mb-4">
      <ul>
        <li>
          <RouterLink :to="{ name: 'CycleList', params: { budgetId } }">
            {{ t('budgetStructure.cycles.title') }}
          </RouterLink>
        </li>
        <li>
          <RouterLink :to="{ name: 'CycleDetail', params: { budgetId, cycleId } }">
            {{ store.currentCycle?.name ?? '...' }}
          </RouterLink>
        </li>
        <li>{{ t('budgetStructure.budgetLines.title') }}</li>
      </ul>
    </div>

    <BudgetTabs :budget-id="budgetId" class="mb-6" />

    <!-- Show-deleted toggle -->
    <div class="flex items-center gap-2 mb-4">
      <input
        id="show-deleted-lines"
        v-model="store.showDeletedBudgetLines"
        type="checkbox"
        class="checkbox checkbox-sm"
      />
      <label for="show-deleted-lines" class="label-text cursor-pointer">
        {{ t('budgetStructure.budgetLines.showDeleted') }}
      </label>
    </div>

    <!-- Loading indicator -->
    <div v-if="store.loading" class="flex justify-center py-8">
      <span class="loading loading-spinner loading-md" />
    </div>

    <!-- Empty state -->
    <EmptyState
      v-else-if="store.budgetLines.length === 0"
      :title="t('budgetStructure.budgetLines.empty.title')"
      :description="t('budgetStructure.budgetLines.empty.description')"
      :action-label="canWriteLines ? t('budgetStructure.budgetLines.empty.action') : undefined"
      :action="canWriteLines ? openCreateModal : undefined"
    />

    <!-- Lines table -->
    <div v-else class="overflow-x-auto">
      <table class="table table-zebra w-full">
        <thead>
          <tr>
            <th class="cursor-pointer select-none" @click="toggleSort('group')">
              {{ t('budgetStructure.categoryGroups.column') }}
              <span v-if="sortColumn === 'group'">{{ sortDir === 'asc' ? '↑' : '↓' }}</span>
            </th>
            <th class="cursor-pointer select-none" @click="toggleSort('category')">
              {{ t('budgetStructure.categories.title') }}
              <span v-if="sortColumn === 'category'">{{ sortDir === 'asc' ? '↑' : '↓' }}</span>
            </th>
            <th class="cursor-pointer select-none" @click="toggleSort('type')">
              {{ t('budgetStructure.budgetLines.lineType') }}
              <span v-if="sortColumn === 'type'">{{ sortDir === 'asc' ? '↑' : '↓' }}</span>
            </th>
            <th class="cursor-pointer select-none" @click="toggleSort('name')">
              {{ t('budgetStructure.budgetLines.name') }}
              <span v-if="sortColumn === 'name'">{{ sortDir === 'asc' ? '↑' : '↓' }}</span>
            </th>
            <th class="cursor-pointer select-none" @click="toggleSort('currency')">
              {{ t('budgetStructure.budgetLines.currency') }}
              <span v-if="sortColumn === 'currency'">{{ sortDir === 'asc' ? '↑' : '↓' }}</span>
            </th>
            <th class="cursor-pointer select-none" @click="toggleSort('budgetedAmount')">
              {{ t('budgetStructure.budgetLines.budgetedAmount') }}
              <span v-if="sortColumn === 'budgetedAmount'">{{ sortDir === 'asc' ? '↑' : '↓' }}</span>
            </th>
            <th class="cursor-pointer select-none" @click="toggleSort('isRecurring')">
              {{ t('budgetStructure.budgetLines.isRecurring') }}
              <span v-if="sortColumn === 'isRecurring'">{{ sortDir === 'asc' ? '↑' : '↓' }}</span>
            </th>
            <th>{{ t('budgetStructure.budgetLines.note') }}</th>
            <th>{{ t('budgetStructure.common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <BudgetLineRow
            v-for="line in sortedLines"
            :key="line.id"
            :line="line"
            :readonly="!canWriteLines"
            :editing="line.id === inlineEditingLineId"
            :category-groups="store.categoryGroups"
            @edit="openEditModal"
            @delete="confirmDelete"
            @restore="handleRestore"
            @start-edit="handleStartEdit"
            @inline-save="handleInlineSave"
            @inline-cancel="handleInlineCancel"
          />

          <!-- Inline add row -->
          <tr v-if="showInlineAdd" class="bg-base-200">
            <td>
              <select
                v-model="inlineAddForm.categoryGroupId"
                class="select select-xs select-bordered w-full"
                @change="inlineAddForm.categoryId = undefined"
              >
                <option value="" disabled>—</option>
                <option v-for="g in store.categoryGroups" :key="g.id" :value="g.id">
                  {{ g.name }}
                </option>
              </select>
            </td>
            <td>
              <select
                v-model="inlineAddForm.categoryId"
                class="select select-xs select-bordered w-full"
              >
                <option :value="undefined">—</option>
                <option
                  v-for="cat in (store.categoryGroups.find(g => g.id === inlineAddForm.categoryGroupId)?.categories ?? []).filter(c => !c.deletedAt)"
                  :key="cat.id"
                  :value="cat.id"
                >
                  {{ cat.name }}
                </option>
              </select>
            </td>
            <td>
              <select v-model="inlineAddForm.lineType" class="select select-xs select-bordered w-full">
                <option value="Expense">{{ t('budgetStructure.budgetLines.types.expense') }}</option>
                <option value="LongTermSavings">{{ t('budgetStructure.budgetLines.types.longTermSavings') }}</option>
                <option value="PreventiveSavings">{{ t('budgetStructure.budgetLines.types.preventiveSavings') }}</option>
              </select>
            </td>
            <td>
              <input
                v-model="inlineAddForm.name"
                type="text"
                class="input input-xs input-bordered w-full"
                :placeholder="t('budgetStructure.budgetLines.name')"
              />
            </td>
            <td>
              <select v-model="inlineAddForm.currencyId" class="select select-xs select-bordered">
                <option :value="undefined">—</option>
                <option
                  v-for="currency in store.currentCycle?.defaultCurrency ? [store.currentCycle.defaultCurrency, ...(store.currentCycle.alternateCurrency ? [store.currentCycle.alternateCurrency] : [])] : []"
                  :key="currency.id"
                  :value="currency.id"
                >
                  {{ currency.code }}
                </option>
              </select>
            </td>
            <td>
              <input
                v-model.number="inlineAddForm.budgetedAmount"
                type="number"
                step="0.01"
                class="input input-xs input-bordered w-24"
              />
            </td>
            <td>
              <input v-model="inlineAddForm.isRecurring" type="checkbox" class="checkbox checkbox-xs" />
            </td>
            <td>
              <input
                v-model="inlineAddForm.note"
                type="text"
                class="input input-xs input-bordered w-full"
                :placeholder="t('budgetStructure.budgetLines.note')"
              />
            </td>
            <td>
              <div class="flex gap-1">
                <button
                  type="button"
                  class="btn btn-xs btn-ghost btn-square text-success"
                  :title="t('budgetStructure.common.save')"
                  @click="handleInlineAddSave"
                >
                  <Check :size="14" />
                </button>
                <button
                  type="button"
                  class="btn btn-xs btn-ghost btn-square"
                  :title="t('budgetStructure.common.cancel')"
                  @click="showInlineAdd = false"
                >
                  <X :size="14" />
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>

      <!-- Inline add row trigger -->
      <div v-if="canWriteLines" class="mt-3">
        <button type="button" class="btn btn-sm btn-ghost" @click="openInlineAdd">
          + {{ t('budgetStructure.budgetLines.create') }}
        </button>
      </div>
    </div>

    <!-- BudgetLineModal — create / edit -->
    <BudgetLineModal
      v-if="showModal"
      :model-value="editingLine"
      :category-groups="store.categoryGroups"
      @submit="handleModalSubmit"
      @cancel="closeModal"
    />

    <!-- Delete confirmation dialog -->
    <dialog v-if="showDeleteConfirm" class="modal modal-open">
      <div class="modal-box">
        <h3 class="font-bold text-lg mb-4">{{ t('budgetStructure.budgetLines.delete') }}</h3>
        <p>{{ t('budgetStructure.budgetLines.confirmDelete') }}</p>
        <div class="modal-action">
          <button type="button" class="btn btn-ghost" @click="showDeleteConfirm = false">
            {{ t('budgetStructure.common.cancel') }}
          </button>
          <button type="button" class="btn btn-error" @click="handleDelete">
            {{ t('budgetStructure.common.confirm') }}
          </button>
        </div>
      </div>
      <div class="modal-backdrop" @click="showDeleteConfirm = false" />
    </dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Check, X } from 'lucide-vue-next'
import { useBudgetStructureStore } from '../store'
import { useLayoutStore } from '@/stores/layout.store'
import { useToastStore } from '@/stores/toast.store'
import { useRoleGate } from '../composables/useRoleGate'
import { extractApiErrorCode } from '../utils/apiError'
import BudgetTabs from '../components/BudgetTabs.vue'
import BudgetLineRow from '../components/BudgetLineRow.vue'
import BudgetLineModal from '../components/BudgetLineModal.vue'
import EmptyState from '../components/EmptyState.vue'
import type { BudgetLineResponse, CreateBudgetLinePayload, LineType, UpdateBudgetLinePayload } from '../types'

const route = useRoute()
const { t } = useI18n()

const budgetId = route.params.budgetId as string
const cycleId = route.params.cycleId as string
const periodId = route.params.periodId as string

const store = useBudgetStructureStore()
const layoutStore = useLayoutStore()
const toastStore = useToastStore()
const { canWriteLines } = useRoleGate(budgetId)

// Modal state
const showModal = ref(false)
const editingLine = ref<BudgetLineResponse | null>(null)

// Delete confirmation state
const showDeleteConfirm = ref(false)
const deletingLineId = ref<string | null>(null)

// Inline edit state
const inlineEditingLineId = ref<string | null>(null)

// Sort state
type SortColumn = 'group' | 'category' | 'type' | 'name' | 'currency' | 'budgetedAmount' | 'isRecurring'
type SortDir = 'asc' | 'desc'

const sortColumn = ref<SortColumn>('group')
const sortDir = ref<SortDir>('asc')

function toggleSort(col: SortColumn): void {
  if (sortColumn.value === col) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortColumn.value = col
    sortDir.value = 'asc'
  }
}

const sortedLines = computed(() => {
  const lines = [...store.budgetLines]
  const dir = sortDir.value === 'asc' ? 1 : -1

  const groupName = (line: BudgetLineResponse): string =>
    store.categoryGroups.find((g) => g.id === line.categoryGroupId)?.name ?? ''
  const categoryName = (line: BudgetLineResponse): string => {
    const g = store.categoryGroups.find((g) => g.id === line.categoryGroupId)
    return g?.categories.find((c) => c.id === line.categoryId)?.name ?? ''
  }

  const defaultSort = (a: BudgetLineResponse, b: BudgetLineResponse): number => {
    return (
      groupName(a).localeCompare(groupName(b)) ||
      categoryName(a).localeCompare(categoryName(b)) ||
      a.lineType.localeCompare(b.lineType) ||
      a.name.localeCompare(b.name)
    )
  }

  return lines.sort((a, b) => {
    let primary = 0
    switch (sortColumn.value) {
      case 'group':
        return dir * defaultSort(a, b)
      case 'category':
        primary = categoryName(a).localeCompare(categoryName(b))
        break
      case 'type':
        primary = a.lineType.localeCompare(b.lineType)
        break
      case 'name':
        primary = a.name.localeCompare(b.name)
        break
      case 'currency':
        primary = (a.currencyCode ?? '').localeCompare(b.currencyCode ?? '')
        break
      case 'budgetedAmount':
        primary = (a.budgetedAmount ?? 0) - (b.budgetedAmount ?? 0)
        break
      case 'isRecurring':
        primary = Number(a.isRecurring) - Number(b.isRecurring)
        break
    }
    return primary !== 0 ? dir * primary : defaultSort(a, b)
  })
})

// Inline add state
const showInlineAdd = ref(false)
const inlineAddForm = reactive({
  name: '',
  lineType: 'Expense' as LineType,
  isRecurring: false,
  budgetedAmount: null as number | null,
  currencyId: undefined as string | undefined,
  note: '',
  categoryGroupId: '',
  categoryId: undefined as string | undefined,
})

function openCreateModal(): void {
  editingLine.value = null
  showModal.value = true
}

function openEditModal(line: BudgetLineResponse): void {
  editingLine.value = line
  showModal.value = true
}

function closeModal(): void {
  showModal.value = false
  editingLine.value = null
}

function confirmDelete(lineId: string): void {
  deletingLineId.value = lineId
  showDeleteConfirm.value = true
}

async function handleDelete(): Promise<void> {
  if (!deletingLineId.value) return
  await store.deleteLine(budgetId, periodId, deletingLineId.value)
  showDeleteConfirm.value = false
  deletingLineId.value = null
  toastStore.push({ type: 'success', title: t('budgetStructure.budgetLines.deleteSuccess') })
}

async function handleRestore(lineId: string): Promise<void> {
  await store.restoreLine(budgetId, periodId, lineId, false)
  toastStore.push({ type: 'success', title: t('budgetStructure.budgetLines.restoreSuccess') })
}

function _lineErrorToast(err: unknown): void {
  const code = extractApiErrorCode(err)
  if (code === 'BUDGET_LINE_NAME_DUPLICATE') {
    toastStore.push({ type: 'error', title: t('budgetStructure.budgetLines.errors.nameDuplicate') })
  } else {
    toastStore.push({ type: 'error', title: t('common.errors.serverError') })
  }
}

async function handleModalSubmit(payload: CreateBudgetLinePayload): Promise<void> {
  try {
    if (editingLine.value) {
      await store.updateLine(budgetId, periodId, editingLine.value.id, payload)
      toastStore.push({ type: 'success', title: t('budgetStructure.budgetLines.updateSuccess') })
    } else {
      await store.createLine(budgetId, periodId, payload)
      toastStore.push({ type: 'success', title: t('budgetStructure.budgetLines.createSuccess') })
    }
    closeModal()
  } catch (err) {
    _lineErrorToast(err)
  }
}

// Inline edit handlers

function handleStartEdit(line: BudgetLineResponse): void {
  showInlineAdd.value = false
  inlineEditingLineId.value = line.id
}

async function handleInlineSave(lineId: string, payload: UpdateBudgetLinePayload): Promise<void> {
  try {
    await store.updateLine(budgetId, periodId, lineId, payload)
    inlineEditingLineId.value = null
    toastStore.push({ type: 'success', title: t('budgetStructure.budgetLines.updateSuccess') })
  } catch (err) {
    _lineErrorToast(err)
  }
}

function handleInlineCancel(_lineId: string): void {
  inlineEditingLineId.value = null
}

// Inline add handlers

function openInlineAdd(): void {
  inlineEditingLineId.value = null
  inlineAddForm.name = ''
  inlineAddForm.lineType = 'Expense'
  inlineAddForm.isRecurring = false
  inlineAddForm.budgetedAmount = null
  inlineAddForm.currencyId = undefined
  inlineAddForm.note = ''
  inlineAddForm.categoryGroupId = store.categoryGroups[0]?.id ?? ''
  inlineAddForm.categoryId = undefined
  showInlineAdd.value = true
}

async function handleInlineAddSave(): Promise<void> {
  if (!inlineAddForm.name.trim()) return
  try {
    await store.createLine(budgetId, periodId, {
      name: inlineAddForm.name,
      lineType: inlineAddForm.lineType,
      isRecurring: inlineAddForm.isRecurring,
      budgetedAmount: inlineAddForm.budgetedAmount ?? undefined,
      currencyId: inlineAddForm.currencyId || undefined,
      note: inlineAddForm.note || undefined,
      categoryGroupId: inlineAddForm.categoryGroupId || undefined,
      categoryId: inlineAddForm.categoryId || undefined,
    })
    toastStore.push({ type: 'success', title: t('budgetStructure.budgetLines.createSuccess') })
    showInlineAdd.value = false
  } catch (err) {
    _lineErrorToast(err)
  }
}

watch(() => store.showDeletedBudgetLines, async () => {
  await store.loadLines(budgetId, periodId)
})

onMounted(async () => {
  await Promise.all([
    store.loadLines(budgetId, periodId),
    store.loadGroups(budgetId),
  ])

  if (canWriteLines.value) {
    layoutStore.setPageActions([
      {
        key: 'new-line',
        label: t('budgetStructure.budgetLines.create'),
        action: openCreateModal,
        variant: 'primary',
      },
    ])
  }
})

onUnmounted(() => {
  layoutStore.clearPageActions()
})
</script>

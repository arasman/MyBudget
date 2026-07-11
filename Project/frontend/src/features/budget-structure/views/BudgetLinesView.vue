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
            <th>{{ t('budgetStructure.budgetLines.name') }}</th>
            <th>{{ t('budgetStructure.budgetLines.lineType') }}</th>
            <th>{{ t('budgetStructure.budgetLines.isRecurring') }}</th>
            <th>{{ t('budgetStructure.budgetLines.budgetedAmount') }}</th>
            <th>{{ t('budgetStructure.budgetLines.currency') }}</th>
            <th>{{ t('budgetStructure.budgetLines.note') }}</th>
            <th>{{ t('budgetStructure.common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <BudgetLineRow
            v-for="line in store.budgetLines"
            :key="line.id"
            :line="line"
            :readonly="!canWriteLines"
            :editing="line.id === inlineEditingLineId"
            :category-groups="store.categoryGroups"
            @edit="openEditModal"
            @delete="confirmDelete"
            @start-edit="handleStartEdit"
            @inline-save="handleInlineSave"
            @inline-cancel="handleInlineCancel"
          />

          <!-- Inline add row -->
          <tr v-if="showInlineAdd" class="bg-base-200">
            <td>
              <input
                v-model="inlineAddForm.name"
                type="text"
                class="input input-xs input-bordered w-full mb-1"
                :placeholder="t('budgetStructure.budgetLines.name')"
              />
              <select
                v-model="inlineAddForm.categoryGroupId"
                class="select select-xs select-bordered w-full"
              >
                <option v-for="g in store.categoryGroups" :key="g.id" :value="g.id">
                  {{ g.name }}
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
              <input v-model="inlineAddForm.isRecurring" type="checkbox" class="checkbox checkbox-xs" />
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
              <select v-model="inlineAddForm.currency" class="select select-xs select-bordered">
                <option value="GTQ">GTQ</option>
                <option value="USD">USD</option>
              </select>
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
import { ref, reactive, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Check, X } from 'lucide-vue-next'
import { useBudgetStructureStore } from '../store'
import { useLayoutStore } from '@/stores/layout.store'
import { useRoleGate } from '../composables/useRoleGate'
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
const { canWriteLines } = useRoleGate(budgetId)

// Modal state
const showModal = ref(false)
const editingLine = ref<BudgetLineResponse | null>(null)

// Delete confirmation state
const showDeleteConfirm = ref(false)
const deletingLineId = ref<string | null>(null)

// Inline edit state
const inlineEditingLineId = ref<string | null>(null)

// Inline add state
const showInlineAdd = ref(false)
const inlineAddForm = reactive({
  name: '',
  lineType: 'Expense' as LineType,
  isRecurring: false,
  budgetedAmount: null as number | null,
  currency: 'GTQ',
  note: '',
  categoryGroupId: '',
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
}

async function handleModalSubmit(payload: CreateBudgetLinePayload): Promise<void> {
  if (editingLine.value) {
    await store.updateLine(budgetId, periodId, editingLine.value.id, payload)
  } else {
    await store.createLine(budgetId, periodId, payload)
  }
  closeModal()
}

// Inline edit handlers

function handleStartEdit(line: BudgetLineResponse): void {
  showInlineAdd.value = false
  inlineEditingLineId.value = line.id
}

async function handleInlineSave(lineId: string, payload: UpdateBudgetLinePayload): Promise<void> {
  await store.updateLine(budgetId, periodId, lineId, payload)
  inlineEditingLineId.value = null
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
  inlineAddForm.currency = 'GTQ'
  inlineAddForm.note = ''
  inlineAddForm.categoryGroupId = store.categoryGroups[0]?.id ?? ''
  showInlineAdd.value = true
}

async function handleInlineAddSave(): Promise<void> {
  if (!inlineAddForm.name.trim()) return
  await store.createLine(budgetId, periodId, {
    name: inlineAddForm.name,
    lineType: inlineAddForm.lineType,
    isRecurring: inlineAddForm.isRecurring,
    budgetedAmount: inlineAddForm.budgetedAmount ?? undefined,
    currency: inlineAddForm.currency,
    note: inlineAddForm.note || undefined,
    categoryGroupId: inlineAddForm.categoryGroupId || undefined,
  })
  showInlineAdd.value = false
}

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

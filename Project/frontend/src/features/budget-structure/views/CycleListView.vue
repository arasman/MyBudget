<template>
  <div class="container mx-auto px-4 py-6">
    <BudgetTabs :budget-id="budgetId" class="mb-6" />

    <!-- Empty state -->
    <EmptyState
      v-if="!store.loading && store.cycles.length === 0"
      :title="t('budgetStructure.cycles.empty.title')"
      :description="t('budgetStructure.cycles.empty.description')"
      :action-label="canWriteStructure ? t('budgetStructure.cycles.empty.action') : undefined"
      :action="canWriteStructure ? openCreateModal : undefined"
    />

    <!-- Cycles table -->
    <div v-else class="overflow-x-auto">
      <table class="table table-zebra w-full">
        <thead>
          <tr>
            <th>{{ t('budgetStructure.cycles.name') }}</th>
            <th>{{ t('budgetStructure.cycles.startDate') }}</th>
            <th>{{ t('budgetStructure.cycles.endDate') }}</th>
            <th>{{ t('budgetStructure.cycles.periodCount') }}</th>
            <th>{{ t('budgetStructure.cycles.active') }}</th>
            <th>{{ t('budgetStructure.cycles.alternateCurrency') }}</th>
            <th>{{ t('budgetStructure.common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="cycle in store.cycles"
            :key="cycle.id"
            class="hover select-none"
            :class="{ 'cursor-pointer': canWriteStructure && inlineEditingCycleId !== cycle.id }"
            @dblclick="canWriteStructure ? handleStartEdit(cycle) : undefined"
          >
            <!-- Name -->
            <td class="font-medium">
              <template v-if="inlineEditingCycleId === cycle.id">
                <input v-model="inlineEditForm.name" type="text" class="input input-xs input-bordered w-full" />
              </template>
              <template v-else>{{ cycle.name }}</template>
            </td>

            <!-- startDate -->
            <td>
              <template v-if="inlineEditingCycleId === cycle.id">
                <input v-model="inlineEditForm.startDate" type="date" class="input input-xs input-bordered" />
              </template>
              <template v-else>{{ cycle.startDate }}</template>
            </td>

            <!-- endDate -->
            <td>
              <template v-if="inlineEditingCycleId === cycle.id">
                <input v-model="inlineEditForm.endDate" type="date" class="input input-xs input-bordered" />
              </template>
              <template v-else>{{ cycle.endDate }}</template>
            </td>

            <!-- periodCount — no inline edit -->
            <td>{{ cycle.periodCount }}</td>

            <!-- isActive badge — no inline edit -->
            <td>
              <span v-if="cycle.isActive" class="badge badge-success badge-sm">
                {{ t('budgetStructure.cycles.active') }}
              </span>
            </td>

            <!-- Alternate currency — show symbol/code when present -->
            <td>
              <span v-if="cycle.alternateCurrency" class="text-sm text-base-content/70">
                {{ cycle.alternateCurrency.symbol }} {{ cycle.alternateCurrency.code }}
              </span>
            </td>

            <!-- Actions -->
            <td>
              <div class="flex gap-2">
                <template v-if="inlineEditingCycleId === cycle.id">
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost btn-square text-success"
                    :title="t('budgetStructure.common.save')"
                    @click.stop="handleInlineSave(cycle.id)"
                  >
                    <Check :size="14" />
                  </button>
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost btn-square"
                    :title="t('budgetStructure.common.cancel')"
                    @click.stop="inlineEditingCycleId = null"
                  >
                    <X :size="14" />
                  </button>
                </template>
                <template v-else>
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost btn-square"
                    :title="t('budgetStructure.cycles.viewPeriods')"
                    @click="goToDetail(cycle.id)"
                  >
                    <List :size="14" />
                  </button>
                  <button
                    v-if="canWriteStructure"
                    type="button"
                    class="btn btn-xs btn-ghost btn-square"
                    :title="t('budgetStructure.cycles.edit')"
                    @click="openEditModal(cycle)"
                  >
                    <Pencil :size="14" />
                  </button>
                  <button
                    v-if="canWriteStructure"
                    type="button"
                    class="btn btn-xs btn-ghost btn-square"
                    :class="cycle.isActive ? 'text-warning' : ''"
                    :disabled="cycle.isActive"
                    :title="t('budgetStructure.cycles.setActive')"
                    @click="handleSetActive(cycle.id)"
                  >
                    <Star :size="14" :fill="cycle.isActive ? 'currentColor' : 'none'" />
                  </button>
                  <button
                    v-if="canWriteStructure"
                    type="button"
                    class="btn btn-xs btn-ghost btn-square text-error"
                    :title="t('budgetStructure.cycles.delete')"
                    @click="confirmDelete(cycle.id)"
                  >
                    <Trash2 :size="14" />
                  </button>
                </template>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Loading indicator -->
    <div v-if="store.loading" class="flex justify-center py-8">
      <span class="loading loading-spinner loading-md" />
    </div>

    <!-- CycleForm modal -->
    <CycleForm
      v-if="showForm"
      :model-value="editingCycle"
      :budget-id="budgetId"
      @submit="handleFormSubmit"
      @cancel="closeModal"
    />

    <!-- Delete confirmation dialog -->
    <dialog v-if="showDeleteConfirm" class="modal modal-open">
      <div class="modal-box">
        <h3 class="font-bold text-lg mb-4">{{ t('budgetStructure.cycles.delete') }}</h3>
        <p>{{ t('budgetStructure.cycles.confirmDelete') }}</p>
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
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useBudgetStructureStore } from '../store'
import { useLayoutStore } from '@/stores/layout.store'
import { useRoleGate } from '../composables/useRoleGate'
import { Check, List, Pencil, Star, Trash2, X } from 'lucide-vue-next'
import BudgetTabs from '../components/BudgetTabs.vue'
import CycleForm from '../components/CycleForm.vue'
import EmptyState from '../components/EmptyState.vue'
import type { CycleListItem, DateString } from '../types'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const budgetId = route.params.budgetId as string

const store = useBudgetStructureStore()
const layoutStore = useLayoutStore()
const { canWriteStructure } = useRoleGate(budgetId)

// Modal state
const showForm = ref(false)
const editingCycle = ref<CycleListItem | null>(null)
const showDeleteConfirm = ref(false)
const deletingCycleId = ref<string | null>(null)

// Inline edit state
const inlineEditingCycleId = ref<string | null>(null)
const inlineEditForm = reactive({
  name: '',
  startDate: '' as DateString,
  endDate: '' as DateString,
})

function handleStartEdit(cycle: CycleListItem): void {
  inlineEditingCycleId.value = cycle.id
  inlineEditForm.name = cycle.name
  inlineEditForm.startDate = cycle.startDate
  inlineEditForm.endDate = cycle.endDate
}

async function handleInlineSave(cycleId: string): Promise<void> {
  const existing = store.cycles.find((c) => c.id === cycleId)
  await store.updateCycle(budgetId, cycleId, {
    name: inlineEditForm.name,
    startDate: inlineEditForm.startDate,
    endDate: inlineEditForm.endDate,
    defaultCurrencyId: existing?.defaultCurrency?.id ?? '11111111-1111-1111-1111-111111111111',
  })
  inlineEditingCycleId.value = null
}

function openCreateModal(): void {
  editingCycle.value = null
  showForm.value = true
}

function openEditModal(cycle: CycleListItem): void {
  editingCycle.value = cycle
  showForm.value = true
}

function closeModal(): void {
  showForm.value = false
  editingCycle.value = null
}

function confirmDelete(cycleId: string): void {
  deletingCycleId.value = cycleId
  showDeleteConfirm.value = true
}

async function handleDelete(): Promise<void> {
  if (!deletingCycleId.value) return
  await store.deleteCycle(budgetId, deletingCycleId.value)
  showDeleteConfirm.value = false
  deletingCycleId.value = null
}

async function handleSetActive(cycleId: string): Promise<void> {
  await store.setActiveCycle(budgetId, cycleId)
}

async function handleFormSubmit(payload: {
  name: string
  startDate: DateString
  endDate: DateString
  defaultCurrencyId: string
  alternateCurrencyId?: string
  exchangeRate?: number
}): Promise<void> {
  if (editingCycle.value) {
    await store.updateCycle(budgetId, editingCycle.value.id, payload)
  } else {
    await store.createCycle(budgetId, payload)
  }
  closeModal()
}

function goToDetail(cycleId: string): void {
  router.push({ name: 'CycleDetail', params: { budgetId, cycleId } })
}

onMounted(async () => {
  await store.loadCycles(budgetId)

  if (canWriteStructure.value) {
    layoutStore.setPageActions([
      {
        key: 'new-cycle',
        label: t('budgetStructure.cycles.create'),
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

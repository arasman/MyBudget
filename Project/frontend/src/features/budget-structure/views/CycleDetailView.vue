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
        <li>{{ store.currentCycle?.name ?? '...' }}</li>
      </ul>
    </div>

    <BudgetTabs :budget-id="budgetId" class="mb-6" />

    <!-- Cycle currency info — only shown when alternate currency is set -->
    <div
      v-if="store.currentCycle?.alternateCurrency"
      class="alert alert-info mb-4 text-sm"
    >
      <span>
        {{ t('budgetStructure.cycles.exchangeRate') }}:
        {{ t('budgetStructure.cycles.exchangeRateDisplay', {
          rate: store.currentCycle.exchangeRate,
          defaultCurrency: store.currentCycle.defaultCurrency?.code,
          alternateCurrency: store.currentCycle.alternateCurrency.code,
        }) }}
      </span>
    </div>

    <!-- Empty state -->
    <div v-if="!store.loading && store.periods.length === 0" class="text-center py-16">
      <p class="text-base-content/60 text-lg mb-2">{{ t('budgetStructure.periods.empty.title') }}</p>
      <p class="text-base-content/40 text-sm mb-6">{{ t('budgetStructure.periods.empty.description') }}</p>
      <button
        v-if="canWriteStructure"
        type="button"
        class="btn btn-primary"
        @click="openCreateModal"
      >
        {{ t('budgetStructure.periods.empty.action') }}
      </button>
    </div>

    <!-- Periods table -->
    <div v-else class="overflow-x-auto">
      <table class="table table-zebra w-full">
        <thead>
          <tr>
            <th>{{ t('budgetStructure.periods.name') }}</th>
            <th>{{ t('budgetStructure.periods.startDate') }}</th>
            <th>{{ t('budgetStructure.periods.endDate') }}</th>
            <th>{{ t('budgetStructure.periods.status') }}</th>
            <th>{{ t('budgetStructure.common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="period in store.periods"
            :key="period.id"
            class="hover select-none"
            :class="{ 'cursor-pointer': canWriteStructure && inlineEditingPeriodId !== period.id }"
            @dblclick="canWriteStructure ? handleStartEdit(period) : undefined"
          >
            <!-- Name -->
            <td class="font-medium">
              <template v-if="inlineEditingPeriodId === period.id">
                <input v-model="inlineEditForm.name" type="text" class="input input-xs input-bordered w-full" />
              </template>
              <template v-else>{{ period.name }}</template>
            </td>

            <!-- startDate -->
            <td>
              <template v-if="inlineEditingPeriodId === period.id">
                <input v-model="inlineEditForm.startDate" type="date" class="input input-xs input-bordered" />
              </template>
              <template v-else>{{ period.startDate }}</template>
            </td>

            <!-- endDate -->
            <td>
              <template v-if="inlineEditingPeriodId === period.id">
                <input v-model="inlineEditForm.endDate" type="date" class="input input-xs input-bordered" />
              </template>
              <template v-else>{{ period.endDate }}</template>
            </td>

            <!-- status badge — no inline edit -->
            <td>
              <span class="badge badge-sm" :class="statusBadgeClass(period.status)">
                {{ period.status }}
              </span>
            </td>

            <!-- Actions -->
            <td>
              <div class="flex gap-2">
                <template v-if="inlineEditingPeriodId === period.id">
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost btn-square text-success"
                    :title="t('budgetStructure.common.save')"
                    @click.stop="handleInlineSave(period.id)"
                  >
                    <Check :size="14" />
                  </button>
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost btn-square"
                    :title="t('budgetStructure.common.cancel')"
                    @click.stop="inlineEditingPeriodId = null"
                  >
                    <X :size="14" />
                  </button>
                </template>
                <template v-else>
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost btn-square"
                    :title="t('budgetStructure.periods.viewLines')"
                    @click="goToLines(period.id)"
                  >
                    <List :size="14" />
                  </button>
                  <button
                    v-if="canWriteStructure"
                    type="button"
                    class="btn btn-xs btn-ghost btn-square"
                    :title="t('budgetStructure.periods.edit')"
                    @click="openEditModal(period)"
                  >
                    <Pencil :size="14" />
                  </button>
                  <button
                    v-if="canWriteStructure"
                    type="button"
                    class="btn btn-xs btn-ghost btn-square"
                    :title="t('budgetStructure.periods.changeStatus')"
                    @click="openStatusModal(period)"
                  >
                    <RefreshCw :size="14" />
                  </button>
                  <button
                    v-if="canWriteStructure"
                    type="button"
                    class="btn btn-xs btn-ghost btn-square text-error"
                    :title="t('budgetStructure.periods.delete')"
                    @click="confirmDelete(period.id)"
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

    <!-- PeriodForm modal (create / edit) -->
    <PeriodForm
      v-if="showForm"
      :model-value="editingPeriod"
      @submit="handleFormSubmit"
      @cancel="closeModal"
    />

    <!-- Change status dialog -->
    <dialog v-if="showStatusDialog" class="modal modal-open">
      <div class="modal-box">
        <h3 class="font-bold text-lg mb-4">{{ t('budgetStructure.periods.changeStatus') }}</h3>
        <div class="form-control">
          <label class="label" for="status-select">
            <span class="label-text">{{ t('budgetStructure.periods.status') }}</span>
          </label>
          <select id="status-select" v-model="newStatus" class="select select-bordered w-full">
            <option value="Open">Open</option>
            <option value="Closed">Closed</option>
            <option value="Locked">Locked</option>
          </select>
        </div>
        <div class="modal-action">
          <button type="button" class="btn btn-ghost" @click="showStatusDialog = false">
            {{ t('budgetStructure.common.cancel') }}
          </button>
          <button type="button" class="btn btn-primary" @click="handleStatusChange">
            {{ t('budgetStructure.common.save') }}
          </button>
        </div>
      </div>
      <div class="modal-backdrop" @click="showStatusDialog = false" />
    </dialog>

    <!-- Delete confirmation dialog -->
    <dialog v-if="showDeleteConfirm" class="modal modal-open">
      <div class="modal-box">
        <h3 class="font-bold text-lg mb-4">{{ t('budgetStructure.periods.delete') }}</h3>
        <p>{{ t('budgetStructure.periods.confirmDelete') }}</p>
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
import { Check, List, Pencil, RefreshCw, Trash2, X } from 'lucide-vue-next'
import BudgetTabs from '../components/BudgetTabs.vue'
import PeriodForm from '../components/PeriodForm.vue'
import type { PeriodSummary, DateString } from '../types'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const budgetId = route.params.budgetId as string
const cycleId = route.params.cycleId as string

const store = useBudgetStructureStore()
const layoutStore = useLayoutStore()
const { canWriteStructure } = useRoleGate(budgetId)

// Modal state
const showForm = ref(false)
const editingPeriod = ref<PeriodSummary | null>(null)
const showDeleteConfirm = ref(false)
const deletingPeriodId = ref<string | null>(null)
const showStatusDialog = ref(false)
const statusTargetId = ref<string | null>(null)
const newStatus = ref('Open')

// Inline edit state
const inlineEditingPeriodId = ref<string | null>(null)
const inlineEditForm = reactive({
  name: '',
  startDate: '' as DateString,
  endDate: '' as DateString,
})

function handleStartEdit(period: PeriodSummary): void {
  inlineEditingPeriodId.value = period.id
  inlineEditForm.name = period.name
  inlineEditForm.startDate = period.startDate
  inlineEditForm.endDate = period.endDate
}

async function handleInlineSave(periodId: string): Promise<void> {
  await store.updatePeriod(budgetId, cycleId, periodId, {
    name: inlineEditForm.name,
    startDate: inlineEditForm.startDate,
    endDate: inlineEditForm.endDate,
  })
  inlineEditingPeriodId.value = null
}

function statusBadgeClass(status: string): string {
  if (status === 'Open') return 'badge-success'
  if (status === 'Locked') return 'badge-error'
  return 'badge-neutral'
}

function openCreateModal(): void {
  editingPeriod.value = null
  showForm.value = true
}

function openEditModal(period: PeriodSummary): void {
  editingPeriod.value = period
  showForm.value = true
}

function openStatusModal(period: PeriodSummary): void {
  statusTargetId.value = period.id
  newStatus.value = period.status
  showStatusDialog.value = true
}

function closeModal(): void {
  showForm.value = false
  editingPeriod.value = null
}

function confirmDelete(periodId: string): void {
  deletingPeriodId.value = periodId
  showDeleteConfirm.value = true
}

async function handleDelete(): Promise<void> {
  if (!deletingPeriodId.value) return
  await store.deletePeriod(budgetId, cycleId, deletingPeriodId.value)
  showDeleteConfirm.value = false
  deletingPeriodId.value = null
}

async function handleStatusChange(): Promise<void> {
  if (!statusTargetId.value) return
  await store.patchPeriodStatus(budgetId, cycleId, statusTargetId.value, {
    status: newStatus.value,
  })
  showStatusDialog.value = false
  statusTargetId.value = null
}

async function handleFormSubmit(payload: {
  name: string
  startDate: DateString
  endDate: DateString
  status?: string
}): Promise<void> {
  if (editingPeriod.value) {
    await store.updatePeriod(budgetId, cycleId, editingPeriod.value.id, {
      name: payload.name,
      startDate: payload.startDate,
      endDate: payload.endDate,
    })
  } else {
    await store.createPeriod(budgetId, cycleId, {
      name: payload.name,
      startDate: payload.startDate,
      endDate: payload.endDate,
    })
  }
  closeModal()
}

function goToLines(periodId: string): void {
  router.push({ name: 'BudgetLines', params: { budgetId, cycleId, periodId } })
}

onMounted(async () => {
  await store.loadPeriods(budgetId, cycleId)

  if (canWriteStructure.value) {
    layoutStore.setPageActions([
      {
        key: 'new-period',
        label: t('budgetStructure.periods.create'),
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

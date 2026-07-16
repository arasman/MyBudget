<template>
  <dialog
    v-if="matrixStore.openModalLineId !== null"
    data-testid="execution-list-modal"
    class="modal modal-open"
    @keydown.escape="matrixStore.closeExecutionModal()"
  >
    <div
      class="modal-box flex flex-col"
      :class="isFullscreen
        ? 'w-screen h-screen max-w-none max-h-none rounded-none'
        : 'w-11/12 max-w-2xl max-h-[85vh]'"
    >
      <!-- Header (always visible) -->
      <div class="flex items-center justify-between mb-4">
        <h3 class="font-bold text-lg">
          {{ mode === 'edit' ? t('budgetExecution.modal.editEntry') : t('budgetExecution.modal.title') }}
        </h3>
        <div class="flex items-center gap-2">
          <!-- Include deleted toggle (only in list mode) -->
          <label v-if="mode === 'list'" class="flex items-center gap-1.5 text-sm cursor-pointer select-none">
            <input
              type="checkbox"
              data-testid="modal-include-deleted-toggle"
              class="checkbox checkbox-xs"
              :checked="matrixStore.showDeletedInModal"
              @change="matrixStore.toggleShowDeletedInModal()"
            />
            {{ t('budgetExecution.modal.includeDeleted') }}
          </label>
          <!-- Fullscreen toggle -->
          <button
            type="button"
            class="btn btn-sm btn-ghost btn-square"
            :title="isFullscreen ? t('budgetExecution.modal.exitFullscreen') : t('budgetExecution.modal.fullscreen')"
            @click="isFullscreen = !isFullscreen"
          >
            <Maximize2 v-if="!isFullscreen" :size="16" />
            <Minimize2 v-else :size="16" />
          </button>
          <!-- Close -->
          <button
            type="button"
            data-testid="modal-close-btn"
            class="btn btn-sm btn-ghost btn-square"
            @click="matrixStore.closeExecutionModal()"
          >
            ✕
          </button>
        </div>
      </div>

      <!-- EDIT MODE: only the edit form -->
      <template v-if="mode === 'edit' && editingRecord">
        <ExecutionRecordForm
          :budget-id="budgetId"
          :period-id="periodId"
          :line-id="lineId"
          :edit-record="editingRecord"
          @saved="finishEdit"
          @cancelled="finishEdit"
        />
      </template>

      <!-- LIST MODE -->
      <template v-else>
        <!-- Records list (scrollable) -->
        <div class="flex-1 overflow-y-auto min-h-0">
          <!-- Loading state -->
          <template v-if="loadingKey">
            <div v-for="i in 3" :key="i" class="skeleton h-12 w-full mb-2" />
          </template>

          <!-- Modal-scoped fetch error -->
          <div
            v-else-if="matrixStore.modalError"
            class="alert alert-error text-sm"
          >
            {{ matrixStore.modalError }}
          </div>

          <!-- Empty state -->
          <div
            v-else-if="records.length === 0"
            class="text-center py-8 text-base-content/50 text-sm"
          >
            {{ t('budgetExecution.modal.noEntries') }}
          </div>

          <!-- Records (paginated) -->
          <template v-else>
            <ExecutionRecordRow
              v-for="record in paginatedRecords"
              :key="record.id"
              :record="record"
              :period-closed="periodClosed"
              :budget-id="budgetId"
              :period-id="periodId"
              :line-id="lineId"
              @edit="startEdit"
            />
          </template>
        </div>

        <!-- Pagination (only if totalPages > 1) -->
        <div v-if="totalPages > 1" class="flex items-center justify-between py-2 text-sm">
          <button
            :disabled="currentPage === 1"
            class="btn btn-xs btn-ghost"
            @click="currentPage--"
          >
            {{ t('budgetExecution.modal.previous') }}
          </button>
          <span class="text-base-content/60">{{ currentPage }} / {{ totalPages }}</span>
          <button
            :disabled="currentPage === totalPages"
            class="btn btn-xs btn-ghost"
            @click="currentPage++"
          >
            {{ t('budgetExecution.modal.next') }}
          </button>
        </div>

        <!-- Closed-period notice -->
        <div v-if="periodClosed" data-testid="closed-period-banner" class="alert alert-info text-sm mt-4">
          {{ t('budgetExecution.modal.periodClosed') }}
        </div>

        <!-- Add form (collapsible, hidden when period closed) -->
        <div v-if="!periodClosed" class="border-t border-base-300 mt-2">
          <button
            type="button"
            class="w-full flex items-center justify-between py-2 text-sm font-semibold"
            @click="addFormOpen = !addFormOpen"
          >
            <span>{{ t('budgetExecution.modal.addEntry') }}</span>
            <ChevronUp v-if="addFormOpen" :size="16" />
            <ChevronDown v-else :size="16" />
          </button>
          <div v-if="addFormOpen">
            <ExecutionRecordForm
              :budget-id="budgetId"
              :period-id="periodId"
              :line-id="lineId"
              @saved="onFormSaved"
            />
          </div>
        </div>
      </template>
    </div>

    <!-- Backdrop -->
    <div class="modal-backdrop" @click="matrixStore.closeExecutionModal()" />
  </dialog>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { Maximize2, Minimize2, ChevronDown, ChevronUp } from 'lucide-vue-next'
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import type { ExecutionRecordDto } from '../types'
import ExecutionRecordRow from './ExecutionRecordRow.vue'
import ExecutionRecordForm from './ExecutionRecordForm.vue'

const props = defineProps<{
  budgetId: string
}>()

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()
const structureStore = useBudgetStructureStore()

const lineId = computed(() => matrixStore.openModalLineId ?? '')
const periodId = computed(() => matrixStore.openModalPeriodId ?? '')

const cacheKey = computed(() => `${lineId.value}:${periodId.value}:${matrixStore.showDeletedInModal}`)
const loadingKey = computed(() => matrixStore.loadingExecutions[cacheKey.value] ?? false)

const records = computed(() => matrixStore.executionRecords[cacheKey.value] ?? [])

// Sort by createdAt ascending
const sortedRecords = computed(() =>
  [...records.value].sort(
    (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime(),
  ),
)

const period = computed(() =>
  structureStore.periods.find((p) => p.id === periodId.value),
)

const periodClosed = computed(() => period.value?.isClosed === true)

// Mode system
const mode = ref<'list' | 'edit'>('list')
const editingRecord = ref<ExecutionRecordDto | null>(null)
const isFullscreen = ref(false)
const addFormOpen = ref(true)

// Pagination
const PAGE_SIZE = 10
const currentPage = ref(1)

const totalPages = computed(() => Math.ceil(sortedRecords.value.length / PAGE_SIZE))
const paginatedRecords = computed(() => {
  const start = (currentPage.value - 1) * PAGE_SIZE
  return sortedRecords.value.slice(start, start + PAGE_SIZE)
})

// Reset page when records change
watch(sortedRecords, () => {
  currentPage.value = 1
})

function startEdit(record: ExecutionRecordDto): void {
  editingRecord.value = record
  mode.value = 'edit'
  addFormOpen.value = false
}

function finishEdit(): void {
  editingRecord.value = null
  mode.value = 'list'
  addFormOpen.value = true
}

function onFormSaved(): void {
  // Records refresh is handled by the store's _invalidateAndRefresh
  // Nothing extra needed here
}
</script>

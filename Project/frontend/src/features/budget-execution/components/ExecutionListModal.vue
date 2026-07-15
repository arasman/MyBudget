<template>
  <dialog
    v-if="matrixStore.openModalLineId !== null"
    data-testid="execution-list-modal"
    class="modal modal-open"
    @keydown.escape="matrixStore.closeExecutionModal()"
  >
    <div class="modal-box w-11/12 max-w-2xl flex flex-col max-h-[80vh]">
      <!-- Header -->
      <div class="flex items-center justify-between mb-4">
        <h3 class="font-bold text-lg">
          {{ t('budgetExecution.modal.title') }}
        </h3>
        <div class="flex items-center gap-3">
          <label class="flex items-center gap-1.5 text-sm cursor-pointer select-none">
            <input
              type="checkbox"
              data-testid="modal-include-deleted-toggle"
              class="checkbox checkbox-xs"
              :checked="matrixStore.showDeletedInModal"
              @change="matrixStore.toggleShowDeletedInModal()"
            />
            {{ t('budgetExecution.modal.includeDeleted') }}
          </label>
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

        <!-- Records -->
        <template v-else>
          <ExecutionRecordRow
            v-for="record in sortedRecords"
            :key="record.id"
            :record="record"
            :period-closed="periodClosed"
            :budget-id="budgetId"
            :period-id="periodId"
            :line-id="lineId"
          />
        </template>
      </div>

      <!-- Closed-period notice (T-5.3) -->
      <div v-if="periodClosed" data-testid="closed-period-banner" class="alert alert-info text-sm mt-4">
        {{ t('budgetExecution.modal.periodClosed') }}
      </div>

      <!-- Footer: form (hidden when period closed) -->
      <div v-if="!periodClosed" class="border-t border-base-300 pt-4 mt-4">
        <p class="text-sm font-semibold mb-2">{{ t('budgetExecution.modal.addEntry') }}</p>
        <ExecutionRecordForm
          :budget-id="budgetId"
          :period-id="periodId"
          :line-id="lineId"
          @saved="onFormSaved"
        />
      </div>
    </div>

    <!-- Backdrop -->
    <div class="modal-backdrop" @click="matrixStore.closeExecutionModal()" />
  </dialog>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
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

function onFormSaved(): void {
  // Records refresh is handled by the store's _invalidateAndRefresh
  // Nothing extra needed here
}
</script>

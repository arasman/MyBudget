<template>
  <div class="p-4">
    <!-- Page header with navigation -->
    <div class="flex items-center justify-between mb-4">
      <h2 class="text-xl font-semibold">{{ t('currentSituation.title') }}</h2>
      <div class="flex items-center gap-3">
        <CutDateNavigator
          :current-date="store.currentDate"
          :has-previous="store.hasPrevious"
          :has-next="store.hasNext"
          @navigate="handleNavigate"
        />
        <button
          v-if="store.currentDate"
          class="btn btn-ghost btn-sm text-error"
          @click="openDeleteModal"
        >
          {{ t('currentSituation.delete') }}
        </button>
      </div>
    </div>

    <!-- Loading state -->
    <div v-if="store.loading" class="text-center py-12">
      <span class="loading loading-spinner loading-lg"></span>
    </div>

    <!-- Error state -->
    <div v-else-if="store.error" class="alert alert-error">
      {{ store.error }}
    </div>

    <!-- Empty state -->
    <div v-else-if="!store.currentRecord" class="text-center py-12 text-base-content/50">
      {{ t('currentSituation.noData') }}
    </div>

    <!-- Main content -->
    <div v-else class="flex flex-col gap-4">
      <!-- Execution summary (read-only) -->
      <ExecutionSummaryPanel :summary="store.currentRecord.executionSummary" />

      <!-- Cut form: exchange rate + balances -->
      <CutRecordForm
        :accounts="store.currentRecord.accounts"
        :exchange-rate="store.currentRecord.exchangeRate"
        :is-draft="store.currentRecord.isDraft"
        :currencies="currencies"
        :save-loading="store.saveLoading"
        :save-error="store.saveError"
        @save="handleSave"
      />

      <!-- Totals -->
      <CutTotalsPanel :totals="store.currentRecord.totals" />
    </div>

    <!-- Delete modal -->
    <DeleteCutModal
      v-if="showDeleteModal && store.currentDate"
      :cut-date="store.currentDate"
      :loading="deleteLoading"
      @confirm="handleDelete"
      @cancel="showDeleteModal = false"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useCutRecordStore } from '../store/useCutRecordStore'
import CutDateNavigator from '../components/CutDateNavigator.vue'
import CutRecordForm from '../components/CutRecordForm.vue'
import ExecutionSummaryPanel from '../components/ExecutionSummaryPanel.vue'
import CutTotalsPanel from '../components/CutTotalsPanel.vue'
import DeleteCutModal from '../components/DeleteCutModal.vue'
import { listCurrencies } from '@/features/budget-structure/api/currencies.api'
import type { CurrencyItem } from '@/features/budget-structure/types'

const route = useRoute()
const { t } = useI18n()
const store = useCutRecordStore()

const budgetId = computed(() => route.params['budgetId'] as string)

const currencies = ref<CurrencyItem[]>([])
const showDeleteModal = ref(false)
const deleteLoading = ref(false)

function openDeleteModal(): void {
  showDeleteModal.value = true
}

function handleNavigate(direction: 'previous' | 'next'): void {
  if (direction === 'previous') {
    store.navigateToPrevious(budgetId.value)
  } else {
    store.navigateToNext(budgetId.value)
  }
}

async function handleSave(payload: {
  exchangeRate: number
  accounts: { bankAccountId: string; balance: number }[]
}): Promise<void> {
  if (!store.currentDate) return
  try {
    await store.upsertCutRecord(budgetId.value, store.currentDate, payload)
  } catch {
    // saveError is set inside the store
  }
}

async function handleDelete(): Promise<void> {
  if (!store.currentDate) return
  deleteLoading.value = true
  try {
    const dateToDelete = store.currentDate
    await store.deleteCutRecord(budgetId.value, dateToDelete)
    showDeleteModal.value = false
    // Load most recent cut if available
    if (store.cutDates.length > 0) {
      const latestDate = store.cutDates[store.cutDates.length - 1]
      await store.fetchCutRecord(budgetId.value, latestDate)
    }
  } finally {
    deleteLoading.value = false
  }
}

onMounted(async () => {
  await store.fetchCutDates(budgetId.value)

  // Load the most recent cut date or show empty state
  if (store.cutDates.length > 0) {
    const latestDate = store.cutDates[store.cutDates.length - 1]
    await store.fetchCutRecord(budgetId.value, latestDate)
  } else {
    // Show draft for today
    const today = new Date().toISOString().slice(0, 10)
    await store.fetchCutRecord(budgetId.value, today)
  }

  try {
    currencies.value = await listCurrencies(budgetId.value)
  } catch {
    // non-fatal
  }
})
</script>

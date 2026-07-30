<template>
  <div class="container mx-auto px-4 py-6">
    <BudgetTabs :budget-id="budgetId" class="mb-6" />
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
    <div v-else class="flex flex-col gap-4 select-none">
      <!-- Cut form: date + exchange rate + accounts -->
      <CutRecordForm
        ref="formRef"
        :accounts="store.currentRecord.accounts"
        :exchange-rate="store.currentRecord.exchangeRate"
        :is-draft="store.currentRecord.isDraft"
        :currencies="currencies"
        :remaining="store.currentRecord.executionSummary.remaining"
        :primary-currency-id="store.currentRecord.primaryCurrencyId"
        :cut-date="selectedDate"
        @save="handleSave"
        @update:live-totals="liveTotals = $event"
        @update:live-exchange-rate="liveExchangeRate = $event"
        @date-change="handleDateChange"
      />

      <!-- Combined totals + execution summary panel -->
      <CutTotalsPanel
        :totals="liveTotals ?? store.currentRecord.totals"
        :execution-summary="store.currentRecord.executionSummary"
        :exchange-rate="liveExchangeRate"
      />

      <!-- Save error + action -->
      <div v-if="store.saveError" class="alert alert-error text-sm">
        {{
          store.saveError === 'noActivePeriod'
            ? t('currentSituation.errors.noActivePeriod')
            : store.saveError
        }}
      </div>
      <div class="flex justify-end">
        <button
          class="btn btn-primary w-full sm:w-auto sm:btn-sm"
          :disabled="store.saveLoading"
          @click="formRef?.triggerSave()"
        >
          <span v-if="store.saveLoading" class="loading loading-spinner loading-xs"></span>
          {{ t('common.save') }}
        </button>
      </div>
    </div>

    <!-- Load strategy modal -->
    <LoadStrategyModal
      v-if="showStrategyModal && pendingDate"
      :target-date="pendingDate"
      :cut-dates="store.cutDates"
      :loading="strategyLoading"
      @select="handleStrategySelect"
      @cancel="handleStrategyCancel"
    />

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
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useCutRecordStore } from '../store/useCutRecordStore'
import { useToastStore } from '@/stores/toast.store'
import type { CutTotalsDto } from '../types/cutRecord'
import BudgetTabs from '@/features/budget-structure/components/BudgetTabs.vue'
import CutDateNavigator from '../components/CutDateNavigator.vue'
import CutRecordForm from '../components/CutRecordForm.vue'
import CutTotalsPanel from '../components/CutTotalsPanel.vue'
import DeleteCutModal from '../components/DeleteCutModal.vue'
import LoadStrategyModal from '../components/LoadStrategyModal.vue'
import { listCurrencies } from '@/features/budget-structure/api/currencies.api'
import type { CurrencyItem } from '@/features/budget-structure/types'
import { getCutRecord } from '../api/cutRecordApi'

const route = useRoute()
const { t } = useI18n()
const store = useCutRecordStore()
const toastStore = useToastStore()

const budgetId = computed(() => route.params['budgetId'] as string)

const formRef = ref<InstanceType<typeof CutRecordForm> | null>(null)
const currencies = ref<CurrencyItem[]>([])
const showDeleteModal = ref(false)
const deleteLoading = ref(false)
const liveTotals = ref<CutTotalsDto | null>(null)
const liveExchangeRate = ref(1)

const selectedDate = ref('')
const showStrategyModal = ref(false)
const pendingDate = ref<string | null>(null)
const strategyLoading = ref(false)

// Reset live totals whenever a new record loads (form will re-emit immediately)
watch(() => store.currentRecord, (record) => {
  liveTotals.value = null
  liveExchangeRate.value = record?.exchangeRate ?? 1
  if (record) selectedDate.value = record.cutDate
})

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
  if (!selectedDate.value) return
  try {
    await store.upsertCutRecord(budgetId.value, selectedDate.value, payload)
    toastStore.push({ type: 'success', title: t('currentSituation.saveSuccess') })
  } catch {
    // saveError is set inside the store
  }
}

async function handleDateChange(date: string): Promise<void> {
  selectedDate.value = date
  if (store.cutDates.includes(date)) {
    await store.fetchCutRecord(budgetId.value, date)
  } else {
    pendingDate.value = date
    showStrategyModal.value = true
  }
}

function handleStrategyCancel(): void {
  showStrategyModal.value = false
  pendingDate.value = null
  if (store.currentRecord) selectedDate.value = store.currentRecord.cutDate
}

async function handleStrategySelect(
  strategy: 'blank' | 'clone' | 'from-date',
  sourceDate?: string,
): Promise<void> {
  if (!pendingDate.value) return
  const targetDate = pendingDate.value
  strategyLoading.value = true
  try {
    if (strategy === 'blank') {
      await store.fetchCutRecord(budgetId.value, targetDate)
      if (store.currentRecord) {
        store.currentRecord = {
          ...store.currentRecord,
          accounts: store.currentRecord.accounts.map((a) => ({
            ...a,
            balance: 0,
            balanceInPrimary: 0,
          })),
        }
      }
    } else if (strategy === 'clone') {
      await store.fetchCutRecord(budgetId.value, targetDate)
    } else if (strategy === 'from-date' && sourceDate) {
      const sourceRecord = await getCutRecord(budgetId.value, sourceDate)
      const sourceBalances: Record<string, number> = {}
      for (const acc of sourceRecord.accounts) {
        sourceBalances[acc.bankAccountId] = acc.balance
      }
      await store.fetchCutRecord(budgetId.value, targetDate)
      if (store.currentRecord) {
        store.currentRecord = {
          ...store.currentRecord,
          accounts: store.currentRecord.accounts.map((a) => ({
            ...a,
            balance: sourceBalances[a.bankAccountId] ?? 0,
            balanceInPrimary: sourceBalances[a.bankAccountId] ?? 0,
          })),
        }
      }
    }
    showStrategyModal.value = false
    pendingDate.value = null
  } finally {
    strategyLoading.value = false
  }
}

async function handleDelete(): Promise<void> {
  if (!store.currentDate) return
  deleteLoading.value = true
  try {
    const dateToDelete = store.currentDate
    await store.deleteCutRecord(budgetId.value, dateToDelete)
    showDeleteModal.value = false
    toastStore.push({ type: 'success', title: t('currentSituation.deleteSuccess') })
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

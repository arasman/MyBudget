import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { CutRecordResponse, UpsertCutRecordDto } from '../types/cutRecord'
import * as api from '../api/cutRecordApi'

export const useCutRecordStore = defineStore('cutRecord', () => {
  // ---------------------------------------------------------------------------
  // State
  // ---------------------------------------------------------------------------
  const currentRecord = ref<CutRecordResponse | null>(null)
  const cutDates = ref<string[]>([])
  const currentDateIndex = ref<number>(-1)
  const loading = ref(false)
  const saveLoading = ref(false)
  const error = ref<string | null>(null)
  const saveError = ref<string | null>(null)

  // ---------------------------------------------------------------------------
  // Computed
  // ---------------------------------------------------------------------------
  const hasPrevious = computed(() => currentDateIndex.value > 0)
  const hasNext = computed(() => currentDateIndex.value < cutDates.value.length - 1)

  const previousDate = computed<string | null>(() =>
    hasPrevious.value ? cutDates.value[currentDateIndex.value - 1] : null,
  )

  const nextDate = computed<string | null>(() =>
    hasNext.value ? cutDates.value[currentDateIndex.value + 1] : null,
  )

  const currentDate = computed<string | null>(() =>
    currentDateIndex.value >= 0 ? cutDates.value[currentDateIndex.value] : null,
  )

  // ---------------------------------------------------------------------------
  // Actions
  // ---------------------------------------------------------------------------

  async function fetchCutDates(budgetId: string): Promise<void> {
    try {
      cutDates.value = await api.listCutDates(budgetId)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load cut dates'
    }
  }

  async function fetchCutRecord(budgetId: string, date: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      currentRecord.value = await api.getCutRecord(budgetId, date)
      // Sync index to the fetched date (may be a draft not in dates list)
      const idx = cutDates.value.indexOf(date)
      currentDateIndex.value = idx
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load cut record'
    } finally {
      loading.value = false
    }
  }

  async function upsertCutRecord(
    budgetId: string,
    date: string,
    payload: UpsertCutRecordDto,
  ): Promise<void> {
    saveLoading.value = true
    saveError.value = null
    try {
      await api.upsertCutRecord(budgetId, date, payload)
      // Refresh dates list and reload the record
      await fetchCutDates(budgetId)
      await fetchCutRecord(budgetId, date)
    } catch (e: unknown) {
      if (
        e &&
        typeof e === 'object' &&
        'response' in e &&
        (e as { response?: { status?: number } }).response?.status === 422
      ) {
        saveError.value = 'noActivePeriod'
      } else {
        saveError.value = e instanceof Error ? e.message : 'Failed to save cut record'
      }
      throw e
    } finally {
      saveLoading.value = false
    }
  }

  async function deleteCutRecord(budgetId: string, date: string): Promise<void> {
    await api.deleteCutRecord(budgetId, date)
    cutDates.value = cutDates.value.filter((d) => d !== date)
    currentRecord.value = null
    // Navigate to the previous date if available
    if (cutDates.value.length > 0) {
      const newIndex = Math.max(0, currentDateIndex.value - 1)
      currentDateIndex.value = newIndex
    } else {
      currentDateIndex.value = -1
    }
  }

  function navigateToPrevious(budgetId: string): void {
    if (hasPrevious.value && previousDate.value) {
      void fetchCutRecord(budgetId, previousDate.value)
    }
  }

  function navigateToNext(budgetId: string): void {
    if (hasNext.value && nextDate.value) {
      void fetchCutRecord(budgetId, nextDate.value)
    }
  }

  function clearSaveError(): void {
    saveError.value = null
  }

  // ---------------------------------------------------------------------------
  // Expose
  // ---------------------------------------------------------------------------
  return {
    currentRecord,
    cutDates,
    currentDateIndex,
    loading,
    saveLoading,
    error,
    saveError,
    hasPrevious,
    hasNext,
    previousDate,
    nextDate,
    currentDate,
    fetchCutDates,
    fetchCutRecord,
    upsertCutRecord,
    deleteCutRecord,
    navigateToPrevious,
    navigateToNext,
    clearSaveError,
  }
})

import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { LifetimeCutTotalsResponse, CutTotalsBandResponse, BudgetLineSeriesResponse } from '../types/dashboard'
import * as api from '../api/dashboardApi'

export const useDashboardStore = defineStore('dashboard', () => {
  // ---------------------------------------------------------------------------
  // State — one slot + one loading flag + one error per request (DASH-1/2/4-6)
  // ---------------------------------------------------------------------------
  const series = ref<LifetimeCutTotalsResponse | null>(null)
  const band = ref<CutTotalsBandResponse | null>(null)
  const lineSeries = ref<BudgetLineSeriesResponse | null>(null)

  const seriesLoading = ref(false)
  const bandLoading = ref(false)
  const lineSeriesLoading = ref(false)

  const seriesError = ref<string | null>(null)
  const bandError = ref<string | null>(null)
  const lineSeriesError = ref<string | null>(null)

  // ---------------------------------------------------------------------------
  // Actions
  // ---------------------------------------------------------------------------

  /** DASH-1: lifetime CutRecord totals series, ordered by CutDate. */
  async function fetchSeries(budgetId: string): Promise<void> {
    seriesLoading.value = true
    seriesError.value = null
    try {
      series.value = await api.getLifetimeCutTotalsSeries(budgetId)
    } catch (e) {
      seriesError.value = e instanceof Error ? e.message : 'Failed to load lifetime totals series'
    } finally {
      seriesLoading.value = false
    }
  }

  /** DASH-2/3/11: period-averaged AVG/MIN/MAX deviation band. */
  async function fetchBand(budgetId: string): Promise<void> {
    bandLoading.value = true
    bandError.value = null
    try {
      band.value = await api.getCutTotalsBand(budgetId)
    } catch (e) {
      bandError.value = e instanceof Error ? e.message : 'Failed to load totals band'
    } finally {
      bandLoading.value = false
    }
  }

  /** DASH-4/5/6/12: per-BudgetLine per-Period series (within-cycle or cross-cycle). */
  async function fetchLineSeries(budgetId: string, lineIds: string[], periodIds: string[]): Promise<void> {
    lineSeriesLoading.value = true
    lineSeriesError.value = null
    try {
      lineSeries.value = await api.getBudgetLineSeries(budgetId, lineIds, periodIds)
    } catch (e) {
      lineSeriesError.value = e instanceof Error ? e.message : 'Failed to load budget line series'
    } finally {
      lineSeriesLoading.value = false
    }
  }

  /** Clears all fetched data and errors — used when switching budgets. */
  function reset(): void {
    series.value = null
    band.value = null
    lineSeries.value = null
    seriesError.value = null
    bandError.value = null
    lineSeriesError.value = null
  }

  // ---------------------------------------------------------------------------
  // Expose
  // ---------------------------------------------------------------------------
  return {
    series,
    band,
    lineSeries,
    seriesLoading,
    bandLoading,
    lineSeriesLoading,
    seriesError,
    bandError,
    lineSeriesError,
    fetchSeries,
    fetchBand,
    fetchLineSeries,
    reset,
  }
})

import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { PeriodTotalsDto, ExecutionRecordDto, CreateExecutionRequest, UpdateExecutionRequest } from './types'
import type { PeriodSummary, LineType } from '@/features/budget-structure/types'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import * as executionTotalsApi from './api/executionTotals.api'
import * as executionsApi from './api/executions.api'

export const useBudgetMatrixStore = defineStore('budgetMatrix', () => {
  // ---------------------------------------------------------------------------
  // State
  // ---------------------------------------------------------------------------

  // Cycle context
  const budgetId = ref<string | null>(null)
  const cycleId = ref<string | null>(null)
  const exchangeRate = ref<number | null>(null)
  const alternateCurrencyId = ref<string | null>(null)

  // Period navigation
  const allPeriods = ref<PeriodSummary[]>([])
  const visiblePeriodOffset = ref(0)
  const visibleWindowSize = ref(3)

  // Per-period data — Record<string, T> is reactive in Pinia (AD-2)
  const periodTotals = ref<Record<string, PeriodTotalsDto>>({})
  const loadingPeriods = ref<Record<string, boolean>>({})

  // Collapse state — Set with ref reassignment for reactivity (AD-2)
  const collapsedGroupIds = ref(new Set<string>())
  const collapsedCategoryIds = ref(new Set<string>())

  // Display preferences
  const showDeleted = ref(false)
  const displayCurrency = ref<'default' | 'alternate'>('default')

  // Execution modal
  const openModalLineId = ref<string | null>(null)
  const openModalPeriodId = ref<string | null>(null)
  const showDeletedInModal = ref(false)
  const executionRecords = ref<Record<string, ExecutionRecordDto[]>>({})
  const loadingExecutions = ref<Record<string, boolean>>({})
  const modalError = ref<string | null>(null)

  // Global state
  const loading = ref(false)
  const error = ref<string | null>(null)

  // ---------------------------------------------------------------------------
  // Actions
  // ---------------------------------------------------------------------------

  async function initMatrix(newBudgetId: string, newCycleId: string): Promise<void> {
    budgetId.value = newBudgetId
    cycleId.value = newCycleId
    visiblePeriodOffset.value = 0

    const structureStore = useBudgetStructureStore()

    // Read cycle context (exchangeRate, alternateCurrencyId) from structure store
    const cycle = structureStore.currentCycle
    if (cycle) {
      exchangeRate.value = cycle.exchangeRate ?? null
      alternateCurrencyId.value = cycle.alternateCurrencyId ?? (cycle.alternateCurrency ? (cycle.alternateCurrency as { code?: string }).code ?? 'alternate' : null)
    }

    // Read periods from structure store
    allPeriods.value = structureStore.periods

    await loadVisiblePeriods()
  }

  async function loadVisiblePeriods(): Promise<void> {
    const offset = visiblePeriodOffset.value
    const size = visibleWindowSize.value
    const visible = allPeriods.value.slice(offset, offset + size)

    await Promise.all(visible.map((p) => loadPeriodTotals(p.id)))
  }

  async function loadPeriodTotals(periodId: string): Promise<void> {
    if (!budgetId.value) return

    loadingPeriods.value = { ...loadingPeriods.value, [periodId]: true }

    try {
      const totals = await executionTotalsApi.getPeriodTotals(budgetId.value, periodId)
      periodTotals.value = { ...periodTotals.value, [periodId]: totals }
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load period totals'
    } finally {
      loadingPeriods.value = { ...loadingPeriods.value, [periodId]: false }
    }
  }

  function navigatePrev(): void {
    visiblePeriodOffset.value = Math.max(0, visiblePeriodOffset.value - 1)
    void loadVisiblePeriods()
  }

  function navigateNext(): void {
    const maxOffset = Math.max(0, allPeriods.value.length - visibleWindowSize.value)
    visiblePeriodOffset.value = Math.min(maxOffset, visiblePeriodOffset.value + 1)
    void loadVisiblePeriods()
  }

  function toggleGroupCollapse(groupId: string): void {
    if (collapsedGroupIds.value.has(groupId)) {
      collapsedGroupIds.value = new Set([...collapsedGroupIds.value].filter((id) => id !== groupId))
    } else {
      collapsedGroupIds.value = new Set([...collapsedGroupIds.value, groupId])
    }
  }

  function toggleCategoryCollapse(categoryId: string): void {
    if (collapsedCategoryIds.value.has(categoryId)) {
      collapsedCategoryIds.value = new Set(
        [...collapsedCategoryIds.value].filter((id) => id !== categoryId),
      )
    } else {
      collapsedCategoryIds.value = new Set([...collapsedCategoryIds.value, categoryId])
    }
  }

  async function openExecutionModal(lineId: string, periodId: string): Promise<void> {
    openModalLineId.value = lineId
    openModalPeriodId.value = periodId
    modalError.value = null

    await _fetchModalRecords(lineId, periodId)
  }

  async function _fetchModalRecords(lineId: string, periodId: string): Promise<void> {
    const key = `${lineId}:${periodId}:${showDeletedInModal.value}`

    // Cache-first: skip fetch if records already loaded (AD-5)
    if (executionRecords.value[key] !== undefined) return

    if (!budgetId.value) return

    loadingExecutions.value = { ...loadingExecutions.value, [key]: true }

    try {
      const records = await executionsApi.list(budgetId.value, periodId, lineId, showDeletedInModal.value)
      executionRecords.value = { ...executionRecords.value, [key]: records }
    } catch (e) {
      // Modal-scoped error — does NOT kill the matrix view
      modalError.value = e instanceof Error ? e.message : 'Failed to load execution records'
    } finally {
      loadingExecutions.value = { ...loadingExecutions.value, [key]: false }
    }
  }

  async function toggleShowDeletedInModal(): Promise<void> {
    showDeletedInModal.value = !showDeletedInModal.value
    const lineId = openModalLineId.value
    const periodId = openModalPeriodId.value
    if (lineId && periodId) {
      await _fetchModalRecords(lineId, periodId)
    }
  }

  function closeExecutionModal(): void {
    openModalLineId.value = null
    openModalPeriodId.value = null
    showDeletedInModal.value = false
    // Cache is NOT cleared (AD-5)
  }

  async function createExecution(
    bId: string,
    periodId: string,
    lineId: string,
    payload: CreateExecutionRequest,
  ): Promise<void> {
    await executionsApi.create(bId, periodId, lineId, payload)
    await _invalidateAndRefresh(lineId, periodId)
  }

  async function updateExecution(
    bId: string,
    periodId: string,
    lineId: string,
    executionId: string,
    payload: UpdateExecutionRequest,
  ): Promise<void> {
    await executionsApi.update(bId, periodId, lineId, executionId, payload)
    await _invalidateAndRefresh(lineId, periodId)
  }

  async function deleteExecution(
    bId: string,
    periodId: string,
    lineId: string,
    executionId: string,
  ): Promise<void> {
    await executionsApi.remove(bId, periodId, lineId, executionId)
    await _invalidateAndRefresh(lineId, periodId)
  }

  async function restoreExecution(
    bId: string,
    periodId: string,
    lineId: string,
    executionId: string,
  ): Promise<void> {
    await executionsApi.restore(bId, periodId, lineId, executionId)
    await _invalidateAndRefresh(lineId, periodId)
  }

  // Invalidate cache and re-fetch after any CRUD mutation (AD-5)
  async function _invalidateAndRefresh(lineId: string, periodId: string): Promise<void> {
    // Invalidate both cache variants (with and without deleted)
    const keyActive = `${lineId}:${periodId}:false`
    const keyDeleted = `${lineId}:${periodId}:true`
    const { [keyActive]: _ra, [keyDeleted]: _rd, ...restRecords } = executionRecords.value
    executionRecords.value = restRecords

    const { [periodId]: _t, ...restTotals } = periodTotals.value
    periodTotals.value = restTotals

    // Fetch period totals FIRST so matrix cells show updated amounts
    // before the record list re-appears in the modal (avoids skeleton flash)
    await loadPeriodTotals(periodId)

    // Then re-fetch records so the modal list updates
    if (budgetId.value) {
      const records = await executionsApi.list(budgetId.value, periodId, lineId, showDeletedInModal.value)
      const key = `${lineId}:${periodId}:${showDeletedInModal.value}`
      executionRecords.value = { ...executionRecords.value, [key]: records }
    }
  }

  async function refreshPeriod(periodId: string): Promise<void> {
    // Force re-fetch regardless of cache
    const { [periodId]: _t, ...rest } = periodTotals.value
    periodTotals.value = rest
    await loadPeriodTotals(periodId)
  }

  async function invalidateAllPeriods(): Promise<void> {
    periodTotals.value = {}
    await loadVisiblePeriods()
  }

  function setDisplayCurrency(currency: 'default' | 'alternate'): void {
    displayCurrency.value = currency
  }

  // ---------------------------------------------------------------------------
  // Getters
  // ---------------------------------------------------------------------------

  /**
   * Returns budgeted and executed subtotals for a given lineType and periodId.
   * - budgeted: sum of budgetedAmount for non-deleted lines matching lineType
   * - executed: sum of netTotal for categoryTotals whose categories match lineType
   */
  function subtotalByLineType(
    periodId: string,
    lineType: LineType,
  ): { budgeted: number; executed: number } {
    const structureStore = useBudgetStructureStore()

    const budgeted = structureStore.budgetLines
      .filter((l) => l.lineType === lineType && !l.deletedAt)
      .reduce((sum, l) => sum + (l.budgetedAmount ?? 0), 0)

    // Collect matching categoryIds from budget lines
    const matchingCategoryIds = new Set<string>()
    for (const line of structureStore.budgetLines) {
      if (line.lineType === lineType && line.categoryId) {
        matchingCategoryIds.add(line.categoryId)
      }
    }

    const totals = periodTotals.value[periodId]
    const executed = totals
      ? totals.categoryTotals
          .filter((ct) => ct.categoryId !== null && matchingCategoryIds.has(ct.categoryId))
          .reduce((sum, ct) => sum + ct.netTotal, 0)
      : 0

    return { budgeted, executed }
  }

  /**
   * Syncs exchangeRate from structureStore.currentCycle to this store.
   * Called after updateCycle to keep useCurrencyDisplay in sync.
   */
  function syncExchangeRate(): void {
    const structureStore = useBudgetStructureStore()
    const rate = structureStore.currentCycle?.exchangeRate
    exchangeRate.value = rate ?? null
  }

  function setShowDeleted(value: boolean): void {
    showDeleted.value = value
    // Clear totals cache and reload
    periodTotals.value = {}
    void loadVisiblePeriods()

    if (budgetId.value) {
      const structureStore = useBudgetStructureStore()
      // Reload groups (and their categories) with includeDeleted flag
      void structureStore.loadGroups(budgetId.value, value)
      // Reload all budget lines (budget-scoped, no periodId) — REQ-BL-STORE-1
      void structureStore.loadLines(budgetId.value, value)
    }
  }

  // ---------------------------------------------------------------------------
  // Expose
  // ---------------------------------------------------------------------------

  return {
    // State
    budgetId,
    cycleId,
    exchangeRate,
    alternateCurrencyId,
    allPeriods,
    visiblePeriodOffset,
    visibleWindowSize,
    periodTotals,
    loadingPeriods,
    collapsedGroupIds,
    collapsedCategoryIds,
    showDeleted,
    displayCurrency,
    openModalLineId,
    openModalPeriodId,
    showDeletedInModal,
    executionRecords,
    loadingExecutions,
    modalError,
    loading,
    error,
    // Actions
    initMatrix,
    loadVisiblePeriods,
    loadPeriodTotals,
    navigatePrev,
    navigateNext,
    toggleGroupCollapse,
    toggleCategoryCollapse,
    openExecutionModal,
    closeExecutionModal,
    toggleShowDeletedInModal,
    createExecution,
    updateExecution,
    deleteExecution,
    restoreExecution,
    refreshPeriod,
    invalidateAllPeriods,
    setDisplayCurrency,
    setShowDeleted,
    subtotalByLineType,
    syncExchangeRate,
  }
})

import { defineStore } from 'pinia'
import { ref } from 'vue'
import type {
  CycleListItem,
  CycleDetail,
  PeriodSummary,
  CategoryGroupResponse,
  BudgetLineResponse,
  BudgetLineRevisionResponse,
  CreateCyclePayload,
  UpdateCyclePayload,
  CreatePeriodPayload,
  UpdatePeriodPayload,
  PatchPeriodStatusPayload,
  CreateGroupPayload,
  UpdateGroupPayload,
  CreateCategoryPayload,
  UpdateCategoryPayload,
  CreateBudgetLinePayload,
  UpdateBudgetLinePayload,
} from './types'
import type { CreateRevisionPayload } from './api/budgetLines.api'
import * as cyclesApi from './api/cycles.api'
import * as periodsApi from './api/periods.api'
import * as groupsApi from './api/categoryGroups.api'
import * as categoriesApi from './api/categories.api'
import * as budgetLinesApi from './api/budgetLines.api'

export const useBudgetStructureStore = defineStore('budgetStructure', () => {
  // ---------------------------------------------------------------------------
  // State
  // ---------------------------------------------------------------------------

  const cycles = ref<CycleListItem[]>([])
  const currentCycle = ref<CycleDetail | null>(null)
  const periods = ref<PeriodSummary[]>([])
  const categoryGroups = ref<CategoryGroupResponse[]>([])
  const budgetLines = ref<BudgetLineResponse[]>([])
  // Revisions — loaded on-demand in customizations view (REQ-BLR-05)
  const revisions = ref<BudgetLineRevisionResponse[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  // Show-deleted toggle state (session-scoped, default OFF)
  const showDeletedCycles = ref(false)
  const showDeletedPeriods = ref(false)
  const showDeletedCategoryGroups = ref(false)
  const showDeletedBudgetLines = ref(false)

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  function _wrap<T>(fn: () => Promise<T>): Promise<T> {
    loading.value = true
    error.value = null
    return fn()
      .catch((e: unknown) => {
        const msg = e instanceof Error ? e.message : String(e)
        error.value = msg
        throw e
      })
      .finally(() => {
        loading.value = false
      })
  }

  // ---------------------------------------------------------------------------
  // Cycles — implemented in PR2
  // ---------------------------------------------------------------------------

  async function loadCycles(budgetId: string, includeDeleted?: boolean): Promise<void> {
    const deleted = includeDeleted ?? showDeletedCycles.value
    await _wrap(async () => {
      cycles.value = await cyclesApi.list(budgetId, { includeDeleted: deleted })
    })
  }

  async function loadCycleDetail(budgetId: string, cycleId: string): Promise<void> {
    await _wrap(async () => {
      currentCycle.value = await cyclesApi.get(budgetId, cycleId)
      periods.value = currentCycle.value.periods
    })
  }

  async function createCycle(budgetId: string, payload: CreateCyclePayload): Promise<void> {
    await _wrap(async () => {
      await cyclesApi.create(budgetId, payload)
      cycles.value = await cyclesApi.list(budgetId)
    })
  }

  async function updateCycle(
    budgetId: string,
    cycleId: string,
    payload: UpdateCyclePayload,
  ): Promise<void> {
    await _wrap(async () => {
      await cyclesApi.update(budgetId, cycleId, payload)
      const idx = cycles.value.findIndex((c) => c.id === cycleId)
      if (idx !== -1) {
        cycles.value[idx] = { ...cycles.value[idx]!, ...payload }
      }
    })
  }

  async function deleteCycle(budgetId: string, cycleId: string): Promise<void> {
    await _wrap(async () => {
      await cyclesApi.remove(budgetId, cycleId)
      if (showDeletedCycles.value) {
        // keep row but mark deleted
        const idx = cycles.value.findIndex((c) => c.id === cycleId)
        if (idx !== -1) {
          cycles.value[idx] = { ...cycles.value[idx]!, deletedAt: new Date().toISOString() }
        }
      } else {
        cycles.value = cycles.value.filter((c) => c.id !== cycleId)
      }
    })
  }

  async function restoreCycle(budgetId: string, cycleId: string): Promise<void> {
    await _wrap(async () => {
      await cyclesApi.restore(budgetId, cycleId)
      cycles.value = await cyclesApi.list(budgetId, { includeDeleted: showDeletedCycles.value })
    })
  }

  async function setActiveCycle(budgetId: string, cycleId: string): Promise<void> {
    await _wrap(async () => {
      await cyclesApi.setActive(budgetId, cycleId)
      cycles.value = cycles.value.map((c) => ({ ...c, isActive: c.id === cycleId }))
    })
  }

  // ---------------------------------------------------------------------------
  // Periods — implemented in PR3
  // ---------------------------------------------------------------------------

  async function loadPeriods(budgetId: string, cycleId: string, includeDeleted?: boolean): Promise<void> {
    const deleted = includeDeleted ?? showDeletedPeriods.value
    await _wrap(async () => {
      currentCycle.value = await cyclesApi.get(budgetId, cycleId)
      if (deleted) {
        // Load periods separately with includeDeleted flag
        periods.value = await periodsApi.list(budgetId, cycleId, { includeDeleted: true })
      } else {
        periods.value = currentCycle.value.periods
      }
    })
  }

  async function createPeriod(
    budgetId: string,
    cycleId: string,
    payload: Omit<CreatePeriodPayload, 'periodNumber'>,
  ): Promise<void> {
    await _wrap(async () => {
      const fullPayload: CreatePeriodPayload = {
        ...payload,
        periodNumber: periods.value.length + 1,
      }
      const { id } = await periodsApi.create(budgetId, cycleId, fullPayload)
      periods.value.push({
        id,
        name: payload.name,
        periodNumber: fullPayload.periodNumber,
        startDate: payload.startDate,
        endDate: payload.endDate,
        isClosed: false,
      })
    })
  }

  async function updatePeriod(
    budgetId: string,
    cycleId: string,
    periodId: string,
    payload: UpdatePeriodPayload,
  ): Promise<void> {
    await _wrap(async () => {
      const existing = periods.value.find((p) => p.id === periodId)
      const periodNumber = existing?.periodNumber ?? 1
      const enriched = { ...payload, periodNumber }
      await periodsApi.update(budgetId, cycleId, periodId, enriched)
      const idx = periods.value.findIndex((p) => p.id === periodId)
      if (idx !== -1) {
        periods.value[idx] = { ...periods.value[idx]!, ...payload }
      }
    })
  }

  async function patchPeriodStatus(
    budgetId: string,
    cycleId: string,
    periodId: string,
    payload: PatchPeriodStatusPayload,
  ): Promise<void> {
    await _wrap(async () => {
      await periodsApi.patchStatus(budgetId, cycleId, periodId, payload)
      const idx = periods.value.findIndex((p) => p.id === periodId)
      if (idx !== -1) {
        periods.value[idx] = { ...periods.value[idx]!, isClosed: payload.status === 'Closed' }
      }
    })
  }

  async function deletePeriod(
    budgetId: string,
    cycleId: string,
    periodId: string,
  ): Promise<void> {
    await _wrap(async () => {
      await periodsApi.remove(budgetId, cycleId, periodId)
      if (showDeletedPeriods.value) {
        const idx = periods.value.findIndex((p) => p.id === periodId)
        if (idx !== -1) {
          periods.value[idx] = { ...periods.value[idx]!, deletedAt: new Date().toISOString() }
        }
      } else {
        periods.value = periods.value.filter((p) => p.id !== periodId)
      }
    })
  }

  async function restorePeriod(budgetId: string, cycleId: string, periodId: string): Promise<void> {
    await _wrap(async () => {
      await periodsApi.restore(budgetId, cycleId, periodId)
      periods.value = await periodsApi.list(budgetId, cycleId, { includeDeleted: showDeletedPeriods.value })
    })
  }

  // ---------------------------------------------------------------------------
  // Category groups — implemented in PR2 (load) and PR4 (mutations)
  // ---------------------------------------------------------------------------

  async function loadGroups(budgetId: string, includeDeleted?: boolean): Promise<void> {
    const deleted = includeDeleted ?? showDeletedCategoryGroups.value
    await _wrap(async () => {
      categoryGroups.value = await groupsApi.list(budgetId, deleted)
    })
  }

  async function createGroup(budgetId: string, payload: CreateGroupPayload): Promise<void> {
    await _wrap(async () => {
      const displayOrder =
        categoryGroups.value.length > 0
          ? Math.max(...categoryGroups.value.map((g) => g.displayOrder)) + 1
          : 1
      const { id } = await groupsApi.create(budgetId, { ...payload, displayOrder })
      categoryGroups.value = [
        ...categoryGroups.value,
        { id, name: payload.name, displayOrder, categories: [] },
      ]
    })
  }

  async function updateGroup(
    budgetId: string,
    groupId: string,
    payload: UpdateGroupPayload,
  ): Promise<void> {
    await _wrap(async () => {
      const existing = categoryGroups.value.find((g) => g.id === groupId)
      const displayOrder = existing?.displayOrder ?? 1
      await groupsApi.update(budgetId, groupId, { ...payload, displayOrder })
      const idx = categoryGroups.value.findIndex((g) => g.id === groupId)
      if (idx !== -1) {
        categoryGroups.value[idx] = { ...categoryGroups.value[idx]!, ...payload }
      }
    })
  }

  async function deleteGroup(budgetId: string, groupId: string): Promise<void> {
    await _wrap(async () => {
      await groupsApi.remove(budgetId, groupId)
      const now = new Date().toISOString()
      const idx = categoryGroups.value.findIndex((g) => g.id === groupId)
      if (idx !== -1) {
        const group = categoryGroups.value[idx]!
        const categoryIds = group.categories.map((c) => c.id)
        // Cascade: mark group + all its categories as deleted
        categoryGroups.value[idx] = {
          ...group,
          deletedAt: now,
          categories: group.categories.map((c) => ({ ...c, deletedAt: now })),
        }
        // Cascade: mark all budget lines in those categories as deleted
        budgetLines.value = budgetLines.value.map((l) =>
          l.categoryId !== undefined && categoryIds.includes(l.categoryId)
            ? { ...l, deletedAt: now }
            : l,
        )
      }
    })
  }

  async function restoreGroup(
    budgetId: string,
    groupId: string,
    includeExecutionRecords: boolean,
  ): Promise<void> {
    await _wrap(async () => {
      await groupsApi.restore(budgetId, groupId, includeExecutionRecords)
      const idx = categoryGroups.value.findIndex((g) => g.id === groupId)
      if (idx !== -1) {
        const group = categoryGroups.value[idx]!
        const categoryIds = group.categories.map((c) => c.id)
        // Cascade: restore group + all its categories
        categoryGroups.value[idx] = {
          ...group,
          deletedAt: null,
          categories: group.categories.map((c) => ({ ...c, deletedAt: null })),
        }
        // Cascade: restore all budget lines in those categories
        budgetLines.value = budgetLines.value.map((l) =>
          l.categoryId !== undefined && categoryIds.includes(l.categoryId)
            ? { ...l, deletedAt: null }
            : l,
        )
      }
    })
  }

  async function reorderGroups(budgetId: string, orderedIds: string[]): Promise<void> {
    await _wrap(async () => {
      await groupsApi.reorder(budgetId, orderedIds)
      const sorted = orderedIds
        .map((id) => categoryGroups.value.find((g) => g.id === id))
        .filter((g): g is (typeof categoryGroups.value)[number] => g !== undefined)
      categoryGroups.value = sorted
    })
  }

  // ---------------------------------------------------------------------------
  // Categories — implemented in PR4
  // ---------------------------------------------------------------------------

  async function createCategory(
    budgetId: string,
    groupId: string,
    payload: CreateCategoryPayload,
  ): Promise<void> {
    await _wrap(async () => {
      const group = categoryGroups.value.find((g) => g.id === groupId)
      const displayOrder =
        group && group.categories.length > 0
          ? Math.max(...group.categories.map((c) => c.displayOrder)) + 1
          : 1
      const { id } = await categoriesApi.create(budgetId, groupId, { ...payload, displayOrder })
      if (group) {
        group.categories = [...group.categories, { id, name: payload.name, displayOrder }]
      }
    })
  }

  async function updateCategory(
    budgetId: string,
    groupId: string,
    categoryId: string,
    payload: UpdateCategoryPayload,
  ): Promise<void> {
    await _wrap(async () => {
      const group = categoryGroups.value.find((g) => g.id === groupId)
      const existing = group?.categories.find((c) => c.id === categoryId)
      const displayOrder = existing?.displayOrder ?? 1
      await categoriesApi.update(budgetId, groupId, categoryId, { ...payload, displayOrder })
      if (group) {
        const idx = group.categories.findIndex((c) => c.id === categoryId)
        if (idx !== -1) {
          group.categories[idx] = { ...group.categories[idx]!, ...payload }
        }
      }
    })
  }

  async function deleteCategory(
    budgetId: string,
    groupId: string,
    categoryId: string,
  ): Promise<void> {
    await _wrap(async () => {
      await categoriesApi.remove(budgetId, groupId, categoryId)
      const now = new Date().toISOString()
      const group = categoryGroups.value.find((g) => g.id === groupId)
      if (group) {
        const idx = group.categories.findIndex((c) => c.id === categoryId)
        if (idx !== -1) {
          group.categories[idx] = { ...group.categories[idx]!, deletedAt: now }
        }
      }
      // Cascade: mark all budget lines in this category as deleted
      budgetLines.value = budgetLines.value.map((l) =>
        l.categoryId === categoryId ? { ...l, deletedAt: now } : l,
      )
    })
  }

  async function restoreCategory(
    budgetId: string,
    groupId: string,
    categoryId: string,
    includeExecutionRecords: boolean,
  ): Promise<void> {
    await _wrap(async () => {
      await categoriesApi.restore(budgetId, groupId, categoryId, includeExecutionRecords)
      const group = categoryGroups.value.find((g) => g.id === groupId)
      if (group) {
        const idx = group.categories.findIndex((c) => c.id === categoryId)
        if (idx !== -1) {
          group.categories[idx] = { ...group.categories[idx]!, deletedAt: null }
        }
      }
      // Cascade: restore all budget lines in this category
      budgetLines.value = budgetLines.value.map((l) =>
        l.categoryId === categoryId ? { ...l, deletedAt: null } : l,
      )
    })
  }

  async function reorderCategories(
    budgetId: string,
    groupId: string,
    orderedIds: string[],
  ): Promise<void> {
    await _wrap(async () => {
      await categoriesApi.reorder(budgetId, groupId, orderedIds)
      const group = categoryGroups.value.find((g) => g.id === groupId)
      if (group) {
        const sorted = orderedIds
          .map((id) => group.categories.find((c) => c.id === id))
          .filter((c): c is (typeof group.categories)[number] => c !== undefined)
        group.categories = sorted
      }
    })
  }

  // ---------------------------------------------------------------------------
  // Budget lines — budget-scoped (no periodId) — REQ-BL-STORE-1
  // ---------------------------------------------------------------------------

  async function loadLines(budgetId: string, includeDeleted?: boolean): Promise<void> {
    const deleted = includeDeleted ?? showDeletedBudgetLines.value
    await _wrap(async () => {
      budgetLines.value = await budgetLinesApi.list(budgetId, deleted)
    })
  }

  async function createLine(
    budgetId: string,
    payload: CreateBudgetLinePayload,
    includeDeleted = false,
  ): Promise<void> {
    await _wrap(async () => {
      await budgetLinesApi.create(budgetId, payload)
      budgetLines.value = await budgetLinesApi.list(budgetId, includeDeleted)
    })
  }

  async function updateLine(
    budgetId: string,
    lineId: string,
    payload: UpdateBudgetLinePayload,
  ): Promise<void> {
    await _wrap(async () => {
      await budgetLinesApi.update(budgetId, lineId, payload)
      const idx = budgetLines.value.findIndex((l) => l.id === lineId)
      if (idx !== -1) {
        budgetLines.value[idx] = { ...budgetLines.value[idx]!, ...payload }
      }
    })
  }

  async function deleteLine(budgetId: string, lineId: string): Promise<void> {
    await _wrap(async () => {
      await budgetLinesApi.remove(budgetId, lineId)
      const idx = budgetLines.value.findIndex((l) => l.id === lineId)
      if (idx !== -1) {
        budgetLines.value[idx] = { ...budgetLines.value[idx]!, deletedAt: new Date().toISOString() }
      }
    })
  }

  async function restoreLine(
    budgetId: string,
    lineId: string,
    includeExecutionRecords: boolean,
  ): Promise<void> {
    await _wrap(async () => {
      await budgetLinesApi.restore(budgetId, lineId, includeExecutionRecords)
      const idx = budgetLines.value.findIndex((l) => l.id === lineId)
      if (idx !== -1) {
        budgetLines.value[idx] = { ...budgetLines.value[idx]!, deletedAt: null }
      }
    })
  }

  // ---------------------------------------------------------------------------
  // Budget line revisions — loaded on-demand (REQ-BLR-01, REQ-BLR-02, REQ-BLR-03)
  // ---------------------------------------------------------------------------

  async function fetchRevisions(budgetId: string, lineId: string): Promise<void> {
    await _wrap(async () => {
      revisions.value = await budgetLinesApi.listRevisions(budgetId, lineId)
    })
  }

  async function createRevision(
    budgetId: string,
    lineId: string,
    payload: CreateRevisionPayload,
  ): Promise<void> {
    await _wrap(async () => {
      await budgetLinesApi.createRevision(budgetId, lineId, payload)
      revisions.value = await budgetLinesApi.listRevisions(budgetId, lineId)
    })
  }

  async function deleteRevision(
    budgetId: string,
    lineId: string,
    revisionId: string,
  ): Promise<void> {
    await _wrap(async () => {
      await budgetLinesApi.deleteRevision(budgetId, lineId, revisionId)
      revisions.value = await budgetLinesApi.listRevisions(budgetId, lineId)
    })
  }

  // ---------------------------------------------------------------------------
  // Expose
  // ---------------------------------------------------------------------------

  return {
    // State
    cycles,
    currentCycle,
    periods,
    categoryGroups,
    budgetLines,
    revisions,
    loading,
    error,
    // Show-deleted toggles
    showDeletedCycles,
    showDeletedPeriods,
    showDeletedCategoryGroups,
    showDeletedBudgetLines,
    // Cycles
    loadCycles,
    loadCycleDetail,
    createCycle,
    updateCycle,
    deleteCycle,
    restoreCycle,
    setActiveCycle,
    // Periods
    loadPeriods,
    createPeriod,
    updatePeriod,
    patchPeriodStatus,
    deletePeriod,
    restorePeriod,
    // Groups
    loadGroups,
    createGroup,
    updateGroup,
    deleteGroup,
    restoreGroup,
    reorderGroups,
    // Categories
    createCategory,
    updateCategory,
    deleteCategory,
    restoreCategory,
    reorderCategories,
    // Lines
    loadLines,
    createLine,
    updateLine,
    deleteLine,
    restoreLine,
    // Revisions (REQ-BLR-01, REQ-BLR-02, REQ-BLR-03)
    fetchRevisions,
    createRevision,
    deleteRevision,
  }
})

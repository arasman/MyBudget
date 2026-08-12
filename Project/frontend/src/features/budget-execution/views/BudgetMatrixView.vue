<template>
  <div class="container mx-auto px-4 py-6">
    <!-- Navigation tabs -->
    <BudgetTabs
      :budget-id="budgetId"
      class="mb-6"
    />

    <!-- Controls bar (T-5.2) -->
    <MatrixControls />

    <!-- Non-blocking reorder error (dismissible) -->
    <div
      v-if="reorderError"
      class="alert alert-warning mb-2 flex justify-between items-center"
    >
      <span>{{ reorderError }}</span>
      <button
        type="button"
        class="btn btn-xs btn-ghost"
        @click="reorderError = null"
      >
        ✕
      </button>
    </div>

    <!-- Loading state -->
    <div
      v-if="structureStore.loading || matrixStore.loading"
      class="flex justify-center py-8"
    >
      <span class="loading loading-spinner loading-md" />
    </div>

    <!-- Critical error state (init failures only) -->
    <div
      v-else-if="structureStore.error || matrixStore.error"
      class="alert alert-error mb-4"
    >
      <span>{{ structureStore.error ?? matrixStore.error }}</span>
    </div>

    <!-- Empty state -->
    <div
      v-else-if="structureStore.categoryGroups.length === 0"
      class="flex flex-col items-center gap-4 py-16 text-base-content/60"
    >
      <p class="text-lg font-semibold">
        {{ t('budgetMatrix.empty.title') }}
      </p>
      <p class="text-sm">
        {{ t('budgetMatrix.empty.description') }}
      </p>
      <RouterLink
        :to="{ name: 'CategoryTree', params: { budgetId } }"
        class="btn btn-primary btn-sm"
      >
        {{ t('budgetMatrix.empty.action') }}
      </RouterLink>
    </div>

    <!-- Matrix table -->
    <template v-else>
      <!-- Period navigation -->
      <div class="flex items-center gap-2 mb-2">
        <button
          data-testid="period-prev-btn"
          type="button"
          class="btn btn-sm btn-ghost"
          :disabled="!canGoPrev"
          :title="t('budgetMatrix.navigation.prevPeriod')"
          @click="goPrev"
        >
          &#8592; {{ t('budgetMatrix.navigation.prevPeriod') }}
        </button>
        <div class="flex-1" />
        <button
          data-testid="period-next-btn"
          type="button"
          class="btn btn-sm btn-ghost"
          :disabled="!canGoNext"
          :title="t('budgetMatrix.navigation.nextPeriod')"
          @click="goNext"
        >
          {{ t('budgetMatrix.navigation.nextPeriod') }} &#8594;
        </button>
      </div>

      <!-- Horizontal scroll wrapper -->
      <div class="overflow-x-auto select-none">
        <table class="w-full border-collapse text-sm">
          <thead>
            <MatrixPeriodHeader :periods="visiblePeriods" />
          </thead>

          <!-- All rows in a single tbody to preserve group→category→line nesting -->
          <tbody ref="matrixTbody">
            <template
              v-for="(group, groupIndex) in draggableGroups"
              :key="group.id"
            >
              <MatrixGroupRow
                :data-group-id="group.id"
                :group="group"
                :budget-id="budgetId"
                :visible-periods="visiblePeriods"
                :collapsed="matrixStore.collapsedGroupIds.has(group.id)"
                :is-first="groupIndex === 0"
                :is-last="groupIndex === draggableGroups.length - 1"
                @toggle-collapse="matrixStore.toggleGroupCollapse(group.id)"
                @move-up="handleGroupMoveUp(group.id, groupIndex)"
                @move-down="handleGroupMoveDown(group.id, groupIndex)"
                @add-category="startAddCategory(group.id)"
              />

              <!-- Inline add-category row -->
              <tr
                v-if="addingCategoryForGroup === group.id"
                data-testid="add-category-row"
              >
                <td
                  class="sticky left-0 z-10 bg-base-200 px-3 py-2 border-b border-base-300"
                  :colspan="1 + visiblePeriods.length * 3"
                >
                  <div class="flex items-center gap-1 pl-10">
                    <input
                      ref="addCategoryInput"
                      v-model="newCategoryName"
                      type="text"
                      :placeholder="t('budgetMatrix.rows.newCategoryName')"
                      class="input input-xs input-bordered flex-1 min-w-0"
                      @keydown.enter="confirmAddCategory(group.id)"
                      @keydown.escape="cancelAdd"
                    >
                    <button
                      type="button"
                      class="btn btn-xs btn-success"
                      :disabled="addActing"
                      @click="confirmAddCategory(group.id)"
                    >
                      <span
                        v-if="addActing"
                        class="loading loading-spinner loading-xs"
                      />
                      <span v-else>{{ t('common.save') }}</span>
                    </button>
                    <button
                      type="button"
                      class="btn btn-xs btn-ghost"
                      @click="cancelAdd"
                    >
                      {{ t('common.cancel') }}
                    </button>
                  </div>
                </td>
              </tr>

              <!-- Category rows -->
              <template
                v-for="(category, catIndex) in group.categories"
                :key="category.id"
              >
                <MatrixCategoryRow
                  :category="category"
                  :group-id="group.id"
                  :budget-id="budgetId"
                  :visible-periods="visiblePeriods"
                  :collapsed="matrixStore.collapsedGroupIds.has(group.id)"
                  :category-collapsed="matrixStore.collapsedCategoryIds.has(category.id)"
                  :is-first="catIndex === 0"
                  :is-last="catIndex === group.categories.length - 1"
                  :parent-deleted="!!group.deletedAt"
                  @toggle-category-collapse="matrixStore.toggleCategoryCollapse(category.id)"
                  @move-up="handleCategoryMoveUp(group.id, group.categories, catIndex)"
                  @move-down="handleCategoryMoveDown(group.id, group.categories, catIndex)"
                  @add-line="startAddLine(category.id, group.id)"
                />

                <!-- Inline add-line row (with category select filtered by group) -->
                <tr
                  v-if="addingLineForCategory === category.id && !matrixStore.collapsedGroupIds.has(group.id)"
                  data-testid="add-line-row"
                >
                  <td
                    class="sticky left-0 z-10 bg-base-100 px-3 py-2 border-b border-base-300"
                    :colspan="1 + visiblePeriods.length * 3"
                  >
                    <div class="flex items-center gap-1 pl-14">
                      <input
                        ref="addLineInput"
                        v-model="newLineName"
                        type="text"
                        :placeholder="t('budgetMatrix.rows.newLineName')"
                        class="input input-xs input-bordered flex-1 min-w-0"
                        @keydown.enter="confirmAddLine(category.id, group.id)"
                        @keydown.escape="cancelAdd"
                      >
                      <!-- Category selector filtered by parent group -->
                      <select
                        v-model="newLineCategoryId"
                        class="select select-xs select-bordered min-w-0 w-32"
                      >
                        <option
                          v-for="cat in group.categories.filter((c) => !c.deletedAt)"
                          :key="cat.id"
                          :value="cat.id"
                        >
                          {{ cat.name }}
                        </option>
                      </select>
                      <button
                        type="button"
                        class="btn btn-xs btn-success"
                        :disabled="addActing"
                        @click="confirmAddLine(category.id, group.id)"
                      >
                        <span
                          v-if="addActing"
                          class="loading loading-spinner loading-xs"
                        />
                        <span v-else>{{ t('common.save') }}</span>
                      </button>
                      <button
                        type="button"
                        class="btn btn-xs btn-ghost"
                        @click="cancelAdd"
                      >
                        {{ t('common.cancel') }}
                      </button>
                    </div>
                  </td>
                </tr>

                <!-- Line rows for this category -->
                <template
                  v-if="!matrixStore.collapsedGroupIds.has(group.id) && !matrixStore.collapsedCategoryIds.has(category.id)"
                >
                  <template
                    v-for="(line, lineIndex) in getLinesForCategory(category.id)"
                    :key="line.id"
                  >
                    <MatrixLineRow
                      :line="line"
                      :budget-id="budgetId"
                      :category-collapsed="matrixStore.collapsedCategoryIds.has(category.id)"
                      :visible-periods="visiblePeriods"
                      :is-first="lineIndex === 0"
                      :is-last="lineIndex === getLinesForCategory(category.id).length - 1"
                      :parent-deleted="!!group.deletedAt || !!category.deletedAt"
                      @move-up="handleLineMoveUp(category.id, line.id)"
                      @move-down="handleLineMoveDown(category.id, line.id)"
                    />
                  </template>
                </template>
              </template>
            </template>
          </tbody>

          <tbody>
            <!-- Inline add-group row -->
            <tr
              v-if="addingGroup"
              data-testid="add-group-row"
            >
              <td
                class="sticky left-0 z-10 bg-base-200 px-3 py-2 border-b border-base-300"
                :colspan="1 + visiblePeriods.length * 3"
              >
                <div class="flex items-center gap-1">
                  <input
                    ref="addGroupInput"
                    v-model="newGroupName"
                    type="text"
                    :placeholder="t('budgetMatrix.rows.newGroupName')"
                    class="input input-xs input-bordered flex-1 min-w-0"
                    @keydown.enter="confirmAddGroup"
                    @keydown.escape="cancelAdd"
                  >
                  <button
                    type="button"
                    class="btn btn-xs btn-success"
                    :disabled="addActing"
                    @click="confirmAddGroup"
                  >
                    <span
                      v-if="addActing"
                      class="loading loading-spinner loading-xs"
                    />
                    <span v-else>{{ t('common.save') }}</span>
                  </button>
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost"
                    @click="cancelAdd"
                  >
                    {{ t('common.cancel') }}
                  </button>
                </div>
              </td>
            </tr>

            <!-- Add group trigger row -->
            <tr v-else>
              <td
                class="sticky left-0 z-10 bg-base-100 px-3 py-2"
                :colspan="1 + visiblePeriods.length * 3"
              >
                <button
                  type="button"
                  class="btn btn-xs btn-ghost gap-1 text-base-content/50"
                  data-testid="add-group-btn"
                  @click="startAddGroup"
                >
                  <span class="text-lg leading-none">+</span>
                  {{ t('budgetMatrix.rows.addGroup') }}
                </button>
              </td>
            </tr>
          </tbody>

          <tfoot>
            <!-- Summary rows: Expenses → PreventiveSavings → LongTermSavings + Total -->
            <MatrixSummaryRow
              :line-type="1"
              :label="t('budgetMatrix.summary.expensesSubTotal')"
              :visible-periods="visiblePeriods"
            />
            <MatrixSummaryRow
              :line-type="3"
              :label="t('budgetMatrix.summary.preventiveSavingsSubTotal')"
              :visible-periods="visiblePeriods"
            />
            <MatrixSummaryRow
              :line-type="2"
              :label="t('budgetMatrix.summary.longTermSavingsSubTotal')"
              :visible-periods="visiblePeriods"
            />
            <MatrixTotalRow
              :label="t('budgetMatrix.summary.total')"
              :visible-periods="visiblePeriods"
            />
          </tfoot>
        </table>
      </div>
    </template>

    <!-- Execution list modal — one instance for the whole view -->
    <ExecutionListModal :budget-id="budgetId" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted, onUnmounted, watch } from 'vue'
import Sortable from 'sortablejs'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import { useBudgetMatrixStore } from '../store'
import { useToastStore } from '@/stores/toast.store'
import { useMatrixNavigation } from '../composables/useMatrixNavigation'
import BudgetTabs from '@/features/budget-structure/components/BudgetTabs.vue'
import MatrixPeriodHeader from '../components/MatrixPeriodHeader.vue'
import MatrixGroupRow from '../components/MatrixGroupRow.vue'
import MatrixCategoryRow from '../components/MatrixCategoryRow.vue'
import MatrixLineRow from '../components/MatrixLineRow.vue'
import MatrixControls from '../components/MatrixControls.vue'
import MatrixSummaryRow from '../components/MatrixSummaryRow.vue'
import MatrixTotalRow from '../components/MatrixTotalRow.vue'
import ExecutionListModal from '../components/ExecutionListModal.vue'
import * as budgetLinesApi from '@/features/budget-structure/api/budgetLines.api'

import type { CategoryGroupResponse, CategoryItem, BudgetLineResponse } from '@/features/budget-structure/types'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const budgetId = computed(() => route.params.budgetId as string)
const cycleId = computed(() => route.params.cycleId as string)

const structureStore = useBudgetStructureStore()
const matrixStore = useBudgetMatrixStore()
const toast = useToastStore()
const { visiblePeriods, canGoPrev, canGoNext, goPrev, goNext } = useMatrixNavigation(matrixStore)

// Non-blocking error for non-critical operations (reorder, etc.)
const reorderError = ref<string | null>(null)

// Inline add state
const addingGroup = ref(false)
const newGroupName = ref('')
const addGroupInput = ref<HTMLInputElement | null>(null)
const addingCategoryForGroup = ref<string | null>(null)
const newCategoryName = ref('')
const addingLineForCategory = ref<string | null>(null)
const addingLineForGroup = ref<string | null>(null)
const newLineName = ref('')
const newLineCategoryId = ref<string>('')
const addActing = ref(false)
const addCategoryInput = ref<HTMLInputElement[]>([])
const addLineInput = ref<HTMLInputElement[]>([])

// Draggable copy of groups (kept in sync with store)
const draggableGroups = ref<CategoryGroupResponse[]>([])

// Ref for the matrix tbody — used to initialize SortableJS for group DnD
const matrixTbody = ref<HTMLElement | null>(null)
let sortableInstance: Sortable | null = null

watch(matrixTbody, (el) => {
  if (el && !sortableInstance) {
    sortableInstance = Sortable.create(el, {
      handle: '.group-drag-handle',
      draggable: '[data-testid="matrix-group-row"]',
      animation: 100,
      onEnd() {
        if (!matrixTbody.value) return
        // Read new group order from DOM after SortableJS moves the row
        const rows = Array.from(
          matrixTbody.value.querySelectorAll('[data-testid="matrix-group-row"]'),
        )
        const orderedIds = rows
          .map((el) => (el as HTMLElement).dataset.groupId)
          .filter(Boolean) as string[]
        const current = structureStore.categoryGroups
        const reordered = orderedIds
          .map((id) => current.find((g) => g.id === id))
          .filter(Boolean) as CategoryGroupResponse[]
        if (orderedIds.join(',') !== current.map((g) => g.id).join(',')) {
          draggableGroups.value = reordered
          void onGroupsDragEnd()
        }
      },
    })
  }
  if (!el && sortableInstance) {
    sortableInstance.destroy()
    sortableInstance = null
  }
})

// Sync draggableGroups from store whenever store data changes
watch(
  () => structureStore.categoryGroups,
  (groups) => { draggableGroups.value = [...groups] },
  { immediate: true, deep: true },
)

onUnmounted(() => {
  sortableInstance?.destroy()
  sortableInstance = null
})

onMounted(async () => {
  try {
    await structureStore.loadCycleDetail(budgetId.value, cycleId.value)
    await structureStore.loadGroups(budgetId.value)
    await matrixStore.initMatrix(budgetId.value, cycleId.value)
  } catch (err: unknown) {
    const status = (err as { response?: { status?: number } })?.response?.status
    if (status === 403) {
      await router.push({ name: 'BudgetSelection' })
    }
    return
  }

  // Load all budget lines for the budget (budget-scoped, no periodId — REQ-BL-STORE-1)
  // Errors here are non-critical; matrix renders without line rows
  try {
    await structureStore.loadLines(budgetId.value)
  } catch {
    // non-critical — matrix renders without line rows
  }
})

// Derive lines per category from the store's budgetLines flat array
function getLinesForCategory(categoryId: string): BudgetLineResponse[] {
  return structureStore.budgetLines.filter((l) => l.categoryId === categoryId)
}

// -------------------------------------------------------------------------
// Inline add group
// -------------------------------------------------------------------------

function startAddGroup(): void {
  addingCategoryForGroup.value = null
  addingLineForCategory.value = null
  newCategoryName.value = ''
  newLineName.value = ''
  addingGroup.value = true
  newGroupName.value = ''
  nextTick(() => addGroupInput.value?.focus())
}

async function confirmAddGroup(): Promise<void> {
  const name = newGroupName.value.trim()
  if (!name) return
  addActing.value = true
  try {
    await structureStore.createGroup(budgetId.value, { name })
    await matrixStore.invalidateAllPeriods()
    toast.push({ type: 'success', title: t('budgetMatrix.rows.createGroupSuccess') })
    addingGroup.value = false
    newGroupName.value = ''
  } finally {
    addActing.value = false
  }
}

// -------------------------------------------------------------------------
// Inline add category
// -------------------------------------------------------------------------

function startAddCategory(groupId: string): void {
  addingLineForCategory.value = null
  newLineName.value = ''
  addingCategoryForGroup.value = groupId
  newCategoryName.value = ''
  nextTick(() => addCategoryInput.value[0]?.focus())
}

async function confirmAddCategory(groupId: string): Promise<void> {
  const name = newCategoryName.value.trim()
  if (!name) return
  addActing.value = true
  try {
    await structureStore.createCategory(budgetId.value, groupId, { name })
    await matrixStore.invalidateAllPeriods()
    toast.push({ type: 'success', title: t('budgetMatrix.rows.createCategorySuccess') })
    addingCategoryForGroup.value = null
    newCategoryName.value = ''
  } finally {
    addActing.value = false
  }
}

// -------------------------------------------------------------------------
// Inline add line
// -------------------------------------------------------------------------

function startAddLine(categoryId: string, groupId: string): void {
  addingCategoryForGroup.value = null
  newCategoryName.value = ''
  addingLineForCategory.value = categoryId
  addingLineForGroup.value = groupId
  newLineName.value = ''
  newLineCategoryId.value = categoryId
  nextTick(() => addLineInput.value[0]?.focus())
}

async function confirmAddLine(categoryId: string, groupId: string): Promise<void> {
  const name = newLineName.value.trim()
  if (!name) return
  // Use the selected category from dropdown (defaults to the triggering category)
  const selectedCategoryId = newLineCategoryId.value || categoryId
  const categoryGroupId = groupId
  // Default startDate = today (budget-scoped, no periodId — REQ-BL-02)
  const todayStr = new Date().toISOString().slice(0, 10)
  addActing.value = true
  try {
    await structureStore.createLine(
      budgetId.value,
      {
        name,
        categoryId: selectedCategoryId,
        categoryGroupId,
        lineType: 'Expense',
        startDate: todayStr,
        initialAmount: 0,
        currencyId: '',
      },
      matrixStore.showDeleted,
    )
    await matrixStore.invalidateAllPeriods()
    toast.push({ type: 'success', title: t('budgetMatrix.rows.createLineSuccess') })
    addingLineForCategory.value = null
    addingLineForGroup.value = null
    newLineName.value = ''
    newLineCategoryId.value = ''
  } finally {
    addActing.value = false
  }
}

function cancelAdd(): void {
  addingGroup.value = false
  addingCategoryForGroup.value = null
  addingLineForCategory.value = null
  addingLineForGroup.value = null
  newGroupName.value = ''
  newCategoryName.value = ''
  newLineName.value = ''
  newLineCategoryId.value = ''
}

// -------------------------------------------------------------------------
// Group reorder helpers
// -------------------------------------------------------------------------

// -------------------------------------------------------------------------
// Group DnD drag-end (vue-draggable-plus fires @end after DOM reorder)
// draggableGroups already reflects the new order — just persist it.
// -------------------------------------------------------------------------

async function onGroupsDragEnd(): Promise<void> {
  const orderedIds = draggableGroups.value.map((g) => g.id)
  try {
    await structureStore.reorderGroups(budgetId.value, orderedIds)
  } catch {
    // Revert by restoring from store
    draggableGroups.value = [...structureStore.categoryGroups]
    reorderError.value = 'Could not save group order. Changes reverted.'
  }
}

async function handleGroupMoveUp(_groupId: string, currentIndex: number): Promise<void> {
  if (currentIndex === 0) return
  const groups = structureStore.categoryGroups
  const orderedIds = groups.map((g) => g.id)
  ;[orderedIds[currentIndex - 1], orderedIds[currentIndex]] = [
    orderedIds[currentIndex]!,
    orderedIds[currentIndex - 1]!,
  ]
  await structureStore.reorderGroups(budgetId.value, orderedIds)
}

async function handleGroupMoveDown(_groupId: string, currentIndex: number): Promise<void> {
  const groups = structureStore.categoryGroups
  if (currentIndex === groups.length - 1) return
  const orderedIds = groups.map((g) => g.id)
  ;[orderedIds[currentIndex], orderedIds[currentIndex + 1]] = [
    orderedIds[currentIndex + 1]!,
    orderedIds[currentIndex]!,
  ]
  await structureStore.reorderGroups(budgetId.value, orderedIds)
}

// -------------------------------------------------------------------------
// Category reorder helpers
// -------------------------------------------------------------------------

async function handleCategoryMoveUp(
  groupId: string,
  categories: CategoryItem[],
  currentIndex: number,
): Promise<void> {
  if (currentIndex === 0) return
  const orderedIds = categories.map((c) => c.id)
  ;[orderedIds[currentIndex - 1], orderedIds[currentIndex]] = [
    orderedIds[currentIndex]!,
    orderedIds[currentIndex - 1]!,
  ]
  await structureStore.reorderCategories(budgetId.value, groupId, orderedIds)
}

async function handleCategoryMoveDown(
  groupId: string,
  categories: CategoryItem[],
  currentIndex: number,
): Promise<void> {
  if (currentIndex === categories.length - 1) return
  const orderedIds = categories.map((c) => c.id)
  ;[orderedIds[currentIndex], orderedIds[currentIndex + 1]] = [
    orderedIds[currentIndex + 1]!,
    orderedIds[currentIndex]!,
  ]
  await structureStore.reorderCategories(budgetId.value, groupId, orderedIds)
}

// -------------------------------------------------------------------------
// Line reorder helpers (T-4.6)
// Per AD-8: call PUT /order for EACH visible period with that category's lines
// Optimistic: update local order immediately; revert all on any failure
// -------------------------------------------------------------------------

async function handleLineMoveUp(categoryId: string, lineId: string): Promise<void> {
  await reorderLinesForAllPeriods(categoryId, lineId, 'up')
}

async function handleLineMoveDown(categoryId: string, lineId: string): Promise<void> {
  await reorderLinesForAllPeriods(categoryId, lineId, 'down')
}

async function reorderLinesForAllPeriods(
  categoryId: string,
  lineId: string,
  direction: 'up' | 'down',
): Promise<void> {
  // Current ordered lines for this category (from flat budgetLines list)
  const categoryLines = getLinesForCategory(categoryId)
  const currentIdx = categoryLines.findIndex((l) => l.id === lineId)
  if (currentIdx === -1) return

  if (direction === 'up' && currentIdx === 0) return
  if (direction === 'down' && currentIdx === categoryLines.length - 1) return

  // Compute new order (optimistic local update)
  const newOrder = categoryLines.map((l) => l.id)
  const swapIdx = direction === 'up' ? currentIdx - 1 : currentIdx + 1
  ;[newOrder[currentIdx], newOrder[swapIdx]] = [newOrder[swapIdx]!, newOrder[currentIdx]!]

  // Optimistic: reorder in-place in structureStore.budgetLines
  const previousOrder = structureStore.budgetLines.map((l) => l.id)
  const reorderedLines = [...structureStore.budgetLines].sort((a, b) => {
    const ai = newOrder.indexOf(a.id)
    const bi = newOrder.indexOf(b.id)
    // Lines not in this category keep their relative positions
    if (ai === -1 && bi === -1) return 0
    if (ai === -1) return 1
    if (bi === -1) return -1
    return ai - bi
  })
  structureStore.budgetLines = reorderedLines

  // Reorder is now budget-scoped (no periodId) — REQ-BL-05
  try {
    await budgetLinesApi.reorder(budgetId.value, newOrder)
  } catch {
    // Revert optimistic update — non-blocking warning, does not kill the view
    structureStore.budgetLines = structureStore.budgetLines.filter((l) =>
      previousOrder.includes(l.id),
    )
    reorderError.value = 'Could not save new line order. Changes reverted.'
  }
}
</script>

<template>
  <div class="container mx-auto px-4 py-6">
    <!-- Controls bar (T-5.2) -->
    <MatrixControls />

    <!-- Loading state -->
    <div v-if="structureStore.loading || matrixStore.loading" class="flex justify-center py-8">
      <span class="loading loading-spinner loading-md" />
    </div>

    <!-- Error state -->
    <div v-else-if="structureStore.error || matrixStore.error" class="alert alert-error mb-4">
      <span>{{ structureStore.error ?? matrixStore.error }}</span>
    </div>

    <!-- Empty state -->
    <div
      v-else-if="structureStore.categoryGroups.length === 0"
      class="flex flex-col items-center gap-4 py-16 text-base-content/60"
    >
      <p class="text-lg font-semibold">{{ t('budgetMatrix.empty.title') }}</p>
      <p class="text-sm">{{ t('budgetMatrix.empty.description') }}</p>
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
      <div class="overflow-x-auto">
        <table class="w-full border-collapse text-sm">
          <thead>
            <MatrixPeriodHeader :periods="visiblePeriods" />
          </thead>
          <tbody>
            <template
              v-for="(group, groupIndex) in structureStore.categoryGroups"
              :key="group.id"
            >
              <!-- Group row -->
              <MatrixGroupRow
                :group="group"
                :visible-periods="visiblePeriods"
                :collapsed="matrixStore.collapsedGroupIds.has(group.id)"
                :is-first="groupIndex === 0"
                :is-last="groupIndex === structureStore.categoryGroups.length - 1"
                @toggle-collapse="matrixStore.toggleGroupCollapse(group.id)"
                @move-up="handleGroupMoveUp(group.id, groupIndex)"
                @move-down="handleGroupMoveDown(group.id, groupIndex)"
              />

              <!-- Category rows (shown when group is not collapsed) -->
              <template
                v-for="(category, catIndex) in group.categories"
                :key="category.id"
              >
                <MatrixCategoryRow
                  :category="category"
                  :group-id="group.id"
                  :visible-periods="visiblePeriods"
                  :collapsed="matrixStore.collapsedGroupIds.has(group.id)"
                  :category-collapsed="matrixStore.collapsedCategoryIds.has(category.id)"
                  :is-first="catIndex === 0"
                  :is-last="catIndex === group.categories.length - 1"
                  @toggle-category-collapse="matrixStore.toggleCategoryCollapse(category.id)"
                  @move-up="handleCategoryMoveUp(group.id, group.categories, catIndex)"
                  @move-down="handleCategoryMoveDown(group.id, group.categories, catIndex)"
                />

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
                      :category-collapsed="matrixStore.collapsedCategoryIds.has(category.id)"
                      :visible-periods="visiblePeriods"
                      :is-first="lineIndex === 0"
                      :is-last="lineIndex === getLinesForCategory(category.id).length - 1"
                      @move-up="handleLineMoveUp(category.id, line.id)"
                      @move-down="handleLineMoveDown(category.id, line.id)"
                    />
                    <MatrixEstimatedRow
                      :line="line"
                      :category-collapsed="matrixStore.collapsedCategoryIds.has(category.id)"
                      :visible-periods="visiblePeriods"
                    />
                  </template>
                </template>
              </template>
            </template>
          </tbody>
          <tfoot>
            <!-- Summary rows: one per LineType (T-5.1) -->
            <MatrixSummaryRow
              :line-type="1"
              :label="t('budgetMatrix.summary.expenses')"
              :visible-periods="visiblePeriods"
            />
            <MatrixSummaryRow
              :line-type="2"
              :label="t('budgetMatrix.summary.longTermSavings')"
              :visible-periods="visiblePeriods"
            />
            <MatrixSummaryRow
              :line-type="3"
              :label="t('budgetMatrix.summary.preventiveSavings')"
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
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import { useBudgetMatrixStore } from '../store'
import { useMatrixNavigation } from '../composables/useMatrixNavigation'
import MatrixPeriodHeader from '../components/MatrixPeriodHeader.vue'
import MatrixGroupRow from '../components/MatrixGroupRow.vue'
import MatrixCategoryRow from '../components/MatrixCategoryRow.vue'
import MatrixLineRow from '../components/MatrixLineRow.vue'
import MatrixEstimatedRow from '../components/MatrixEstimatedRow.vue'
import MatrixControls from '../components/MatrixControls.vue'
import MatrixSummaryRow from '../components/MatrixSummaryRow.vue'
import ExecutionListModal from '../components/ExecutionListModal.vue'
import * as budgetLinesApi from '@/features/budget-structure/api/budgetLines.api'
import type { CategoryItem, BudgetLineResponse } from '@/features/budget-structure/types'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const budgetId = computed(() => route.params.budgetId as string)
const cycleId = computed(() => route.params.cycleId as string)

const structureStore = useBudgetStructureStore()
const matrixStore = useBudgetMatrixStore()
const { visiblePeriods, canGoPrev, canGoNext, goPrev, goNext } = useMatrixNavigation(matrixStore)

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
  }
})

// Derive lines per category from the store's budgetLines flat array
function getLinesForCategory(categoryId: string): BudgetLineResponse[] {
  return structureStore.budgetLines.filter((l) => l.categoryId === categoryId)
}

// -------------------------------------------------------------------------
// Group reorder helpers
// -------------------------------------------------------------------------

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

  try {
    // Call PUT /order for each visible period — N calls (one per period)
    await Promise.all(
      visiblePeriods.value.map((period) =>
        budgetLinesApi.reorder(budgetId.value, period.id, newOrder),
      ),
    )
  } catch {
    // Revert on error
    structureStore.budgetLines = structureStore.budgetLines.filter((l) =>
      previousOrder.includes(l.id),
    )
    matrixStore.error = 'Failed to reorder lines. Changes reverted.'
  }
}
</script>

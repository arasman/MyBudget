<template>
  <div class="container mx-auto px-4 py-6">
    <!-- Controls placeholder — MatrixControls component added in PR5 -->
    <div class="mb-4 flex items-center justify-between">
      <h2 class="text-xl font-bold">{{ t('budgetMatrix.title') }}</h2>
    </div>

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

                <!-- Line rows for this category — wired in PR4 -->
                <!-- MatrixLineRow + MatrixEstimatedRow will be rendered here when lines are available -->
              </template>
            </template>
          </tbody>
          <tfoot>
            <!-- MatrixSummaryRow added in PR5 -->
          </tfoot>
        </table>
      </div>
    </template>
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
import type { CategoryItem } from '@/features/budget-structure/types'

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

// Group reorder helpers
async function handleGroupMoveUp(_groupId: string, currentIndex: number): Promise<void> {
  if (currentIndex === 0) return
  const groups = structureStore.categoryGroups
  const orderedIds = groups.map((g) => g.id)
  // Swap with previous
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

// Category reorder helpers
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
</script>

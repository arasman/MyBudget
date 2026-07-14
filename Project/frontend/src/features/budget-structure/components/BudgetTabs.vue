<template>
  <div role="tablist" class="tabs tabs-border">
    <RouterLink
      :to="{ name: 'CycleList', params: { budgetId } }"
      role="tab"
      class="tab"
      :class="{ 'tab-active': isActive('CycleList') }"
    >
      Cycles
    </RouterLink>
    <RouterLink
      :to="{ name: 'CategoryTree', params: { budgetId } }"
      role="tab"
      class="tab"
      :class="{ 'tab-active': isActive('CategoryTree') }"
    >
      Categories
    </RouterLink>
    <RouterLink
      v-if="cycleId"
      :to="{ name: 'BudgetMatrix', params: { budgetId, cycleId } }"
      role="tab"
      class="tab"
      :class="{ 'tab-active': isActive('BudgetMatrix') }"
    >
      {{ t('budgetMatrix.title') }}
    </RouterLink>
  </div>
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'

const props = defineProps<{
  budgetId: string
  cycleId?: string
}>()

const route = useRoute()
const { t } = useI18n()

// A tab is active when the current route name matches or is a descendant.
// CycleList tab covers CycleList and CycleDetail routes.
const CYCLE_ROUTE_NAMES = new Set(['CycleList', 'CycleDetail', 'BudgetLines'])
const CATEGORY_ROUTE_NAMES = new Set(['CategoryTree'])
const MATRIX_ROUTE_NAMES = new Set(['BudgetMatrix'])

function isActive(tab: 'CycleList' | 'CategoryTree' | 'BudgetMatrix'): boolean {
  const name = route.name as string | undefined
  if (!name) return false
  if (tab === 'CycleList') return CYCLE_ROUTE_NAMES.has(name)
  if (tab === 'CategoryTree') return CATEGORY_ROUTE_NAMES.has(name)
  return MATRIX_ROUTE_NAMES.has(name)
}
</script>

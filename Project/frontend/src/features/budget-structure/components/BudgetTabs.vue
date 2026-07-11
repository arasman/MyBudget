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
  </div>
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router'

const props = defineProps<{
  budgetId: string
}>()

const route = useRoute()

// A tab is active when the current route name matches or is a descendant.
// CycleList tab covers CycleList and CycleDetail routes.
const CYCLE_ROUTE_NAMES = new Set(['CycleList', 'CycleDetail', 'BudgetLines'])
const CATEGORY_ROUTE_NAMES = new Set(['CategoryTree'])

function isActive(tab: 'CycleList' | 'CategoryTree'): boolean {
  const name = route.name as string | undefined
  if (!name) return false
  if (tab === 'CycleList') return CYCLE_ROUTE_NAMES.has(name)
  return CATEGORY_ROUTE_NAMES.has(name)
}
</script>

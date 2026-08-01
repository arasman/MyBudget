<template>
  <div>
    <div class="px-1 pb-1">
      <RouterLink
        :to="{ name: 'BudgetSelection', query: { manage: '1' } }"
        class="text-xs text-base-content/50 hover:text-base-content transition-colors"
      >
        ← {{ t('nav.budgets') }}
      </RouterLink>
    </div>
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
        :to="{ name: 'BudgetLines', params: { budgetId } }"
        role="tab"
        class="tab"
        :class="{ 'tab-active': isActive('BudgetLines') }"
      >
        {{ t('budgetStructure.budgetLines.title') }}
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
      <RouterLink
        :to="{ name: 'BankAccounts', params: { budgetId } }"
        role="tab"
        class="tab"
        :class="{ 'tab-active': isActive('BankAccounts') }"
      >
        {{ t('bankAccount.title') }}
      </RouterLink>
      <RouterLink
        :to="{ name: 'CurrentSituation', params: { budgetId } }"
        role="tab"
        class="tab"
        :class="{ 'tab-active': isActive('CurrentSituation') }"
      >
        {{ t('currentSituation.tabTitle') }}
      </RouterLink>
      <RouterLink
        :to="{ name: 'Dashboard', params: { budgetId } }"
        role="tab"
        class="tab"
        :class="{ 'tab-active': isActive('Dashboard') }"
        :aria-selected="isActive('Dashboard')"
      >
        {{ t('dashboard.tabTitle') }}
      </RouterLink>
    </div>
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
const CYCLE_ROUTE_NAMES = new Set(['CycleList', 'CycleDetail'])
const CATEGORY_ROUTE_NAMES = new Set(['CategoryTree'])
const BUDGET_LINES_ROUTE_NAMES = new Set(['BudgetLines', 'BudgetLineCustomizations'])
const MATRIX_ROUTE_NAMES = new Set(['BudgetMatrix'])
const CURRENT_SITUATION_ROUTE_NAMES = new Set(['CurrentSituation'])
const BANK_ACCOUNTS_ROUTE_NAMES = new Set(['BankAccounts'])
const DASHBOARD_ROUTE_NAMES = new Set(['Dashboard'])

function isActive(
  tab:
    | 'CycleList'
    | 'CategoryTree'
    | 'BudgetLines'
    | 'BudgetMatrix'
    | 'BankAccounts'
    | 'CurrentSituation'
    | 'Dashboard',
): boolean {
  const name = route.name as string | undefined
  if (!name) return false
  if (tab === 'CycleList') return CYCLE_ROUTE_NAMES.has(name)
  if (tab === 'CategoryTree') return CATEGORY_ROUTE_NAMES.has(name)
  if (tab === 'BudgetLines') return BUDGET_LINES_ROUTE_NAMES.has(name)
  if (tab === 'BankAccounts') return BANK_ACCOUNTS_ROUTE_NAMES.has(name)
  if (tab === 'CurrentSituation') return CURRENT_SITUATION_ROUTE_NAMES.has(name)
  if (tab === 'Dashboard') return DASHBOARD_ROUTE_NAMES.has(name)
  return MATRIX_ROUTE_NAMES.has(name)
}
</script>

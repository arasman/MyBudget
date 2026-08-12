<template>
  <tr>
    <!-- Sticky label column -->
    <th class="sticky left-0 z-10 bg-base-100 text-left px-3 py-2 min-w-[200px] border-b border-base-300">
      {{ t('budgetMatrix.columns.label') }}
    </th>

    <!-- One group of 3 sub-columns per visible period -->
    <template
      v-for="period in periods"
      :key="period.id"
    >
      <th
        data-testid="period-header"
        colspan="3"
        class="text-center px-3 py-1 min-w-[270px] border-b border-base-300 font-semibold"
      >
        <div
          v-if="matrixStore.loadingPeriods[period.id]"
          class="skeleton h-4 w-full"
        />
        <template v-else>
          <span>{{ period.name }}</span>
          <MatrixRefreshIcon
            :period-id="period.id"
            :period-status="period.isClosed ? 'Closed' : 'Open'"
          />
        </template>
      </th>
    </template>
  </tr>
  <tr>
    <!-- Empty label cell for alignment -->
    <th class="sticky left-0 z-10 bg-base-100 border-b border-base-300" />

    <!-- Budgeted / Executed / Difference sub-column headers -->
    <template
      v-for="period in periods"
      :key="period.id"
    >
      <th class="text-right px-3 py-1 text-xs font-medium text-base-content/70 border-b border-base-300 min-w-[90px]">
        {{ t('budgetMatrix.columns.budgeted') }}
      </th>
      <th class="text-right px-3 py-1 text-xs font-medium text-base-content/70 border-b border-base-300 min-w-[90px]">
        {{ t('budgetMatrix.columns.executed') }}
      </th>
      <th class="text-right px-3 py-1 text-xs font-medium text-base-content/70 border-b border-base-300 min-w-[90px]">
        {{ t('budgetMatrix.columns.difference') }}
      </th>
    </template>
  </tr>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useBudgetMatrixStore } from '../store'
import MatrixRefreshIcon from './MatrixRefreshIcon.vue'
import type { PeriodSummary } from '@/features/budget-structure/types'

defineProps<{
  periods: PeriodSummary[]
}>()

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()
</script>

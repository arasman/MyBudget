<template>
  <div class="container mx-auto px-4 py-6">
    <BudgetTabs
      :budget-id="budgetId"
      class="mb-6"
    />

    <h2 class="text-xl font-semibold mb-6">
      {{ t('dashboard.title') }}
    </h2>

    <!-- DASH-7: lifetime trend is the default landing content — it renders
         first, not last-cut KPI tiles. The average-band and BudgetLine
         comparison widgets are on the same page (no tab switching), each
         self-contained with its own picker/mode controls. -->
    <div class="flex flex-col gap-8">
      <section>
        <h3 class="text-lg font-medium mb-3">
          {{ t('dashboard.lifetime.title') }}
        </h3>
        <LifetimeTotalsChart :budget-id="budgetId" />
      </section>

      <section>
        <h3 class="text-lg font-medium mb-3">
          {{ t('dashboard.band.title') }}
        </h3>
        <TotalsBandChart :budget-id="budgetId" />
      </section>

      <section>
        <h3 class="text-lg font-medium mb-3">
          {{ t('dashboard.lineSeries.title') }}
        </h3>
        <BudgetLineSeriesChart :budget-id="budgetId" />
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import BudgetTabs from '@/features/budget-structure/components/BudgetTabs.vue'
import LifetimeTotalsChart from '../components/LifetimeTotalsChart.vue'
import TotalsBandChart from '../components/TotalsBandChart.vue'
import BudgetLineSeriesChart from '../components/BudgetLineSeriesChart.vue'

const route = useRoute()
const { t } = useI18n()

const budgetId = computed(() => route.params['budgetId'] as string)
</script>

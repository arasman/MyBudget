<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-4">
    <div class="lg:col-span-8">
      <BaseChart
        type="line"
        :series="chartSeries"
        :labels="chartLabels"
        :axis-label="t('dashboard.lifetime.axisLabel')"
        conversion-basis="cut-frozen"
        :loading="store.seriesLoading"
        :empty="isEmpty"
      />
    </div>
    <div class="lg:col-span-4">
      <SeriesPicker v-model="selected" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseChart from './BaseChart.vue'
import SeriesPicker from './SeriesPicker.vue'
import { useDashboardStore } from '../store/useDashboardStore'
import { useSeriesSelection } from '../composables/useSeriesSelection'
import { buildLifetimeSeries } from '../utils/seriesMapping'
import type { TotalKey } from '../types/dashboard'

const props = defineProps<{ budgetId: string }>()

const { t } = useI18n()
const store = useDashboardStore()
const { selected } = useSeriesSelection('dashboard.lifetimeTotals.selection')

function labelFor(key: TotalKey): string {
  return t(`dashboard.series.${key}`)
}

const chartLabels = computed(() => store.series?.points.map((p) => p.cutDate) ?? [])
const chartSeries = computed(() => buildLifetimeSeries(store.series?.points ?? [], selected.value, labelFor))
const isEmpty = computed(() => !store.seriesLoading && (chartLabels.value.length === 0 || selected.value.length === 0))

async function load(): Promise<void> {
  await store.fetchSeries(props.budgetId)
}

onMounted(load)
watch(() => props.budgetId, load)
</script>

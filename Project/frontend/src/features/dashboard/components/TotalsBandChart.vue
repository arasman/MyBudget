<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-4">
    <div class="lg:col-span-8">
      <InsufficientDataState v-if="isInsufficient" />
      <BaseChart
        v-else
        type="line"
        :series="chartSeries"
        :labels="chartLabels"
        :axis-label="t('dashboard.band.axisLabel')"
        conversion-basis="cut-frozen"
        :loading="store.bandLoading"
        :empty="isEmptySelection"
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
import InsufficientDataState from './InsufficientDataState.vue'
import { useDashboardStore } from '../store/useDashboardStore'
import { useSeriesSelection } from '../composables/useSeriesSelection'
import { useChartTheme } from '../composables/useChartTheme'
import { buildBandChartSeries } from '../utils/seriesMapping'
import type { TotalKey } from '../types/dashboard'

const props = defineProps<{ budgetId: string }>()

const { t } = useI18n()
const store = useDashboardStore()
const { selected } = useSeriesSelection('dashboard.totalsBand.selection')
const { theme } = useChartTheme()

function labelFor(key: TotalKey): string {
  return t(`dashboard.series.${key}`)
}

// DASH-3: fewer than 2 periods with recorded cuts is not enough history to
// show a meaningful average-behavior band (design.md Decision 8).
const isInsufficient = computed(() => !store.bandLoading && (store.band?.periodCount ?? 0) < 2)
const isEmptySelection = computed(() => selected.value.length === 0)

const chartLabels = computed(() => store.band?.periods.map((p) => p.periodStart) ?? [])
const chartSeries = computed(() =>
  store.band ? buildBandChartSeries(store.band.periods, store.band.band, selected.value, labelFor, theme.value.palette) : [],
)

async function load(): Promise<void> {
  await store.fetchBand(props.budgetId)
}

onMounted(load)
watch(() => props.budgetId, load)
</script>

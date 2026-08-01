<template>
  <div class="card bg-base-200 p-4 h-[18rem] md:h-[24rem] flex flex-col">
    <div v-if="loading" class="flex flex-1 items-center justify-center gap-2 text-base-content/50 text-sm">
      <span class="loading loading-spinner loading-md" />
      {{ t('dashboard.chart.loading') }}
    </div>
    <div v-else-if="empty" class="flex flex-1 items-center justify-center text-base-content/50 text-sm">
      {{ t('dashboard.chart.empty') }}
    </div>
    <div v-else class="relative flex-1 min-h-0">
      <Chart :type="type" :data="chartData" :options="chartOptions" />
    </div>

    <!-- DASH-9: mandatory conversion-basis caption on every dashboard chart -->
    <p class="text-xs text-base-content/40 mt-2">
      {{ conversionBasisLabel }}
    </p>
  </div>
</template>

<script lang="ts">
/**
 * Chart-agnostic series input consumed by BaseChart. Chart.js's own dataset
 * shape never leaks past this component (design.md Decision 6).
 */
export interface ChartSeriesInput {
  key: string
  label: string
  data: number[]
  color?: string
  /** Overrides `color` for the line/point border specifically. */
  borderColor?: string
  /** Overrides `color` for the fill/point background specifically. */
  backgroundColor?: string
  /** Chart.js point radius override — e.g. `0` to hide points on a band's min/max edges. */
  pointRadius?: number
  /** Chart.js `fill` option — e.g. `'-1'` to shade the area toward the previous dataset (band charts). */
  fill?: boolean | string
}

/** Subset of Chart.js chart types this dashboard's widgets need. */
export type BaseChartType = 'line' | 'bar'
</script>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Chart } from 'vue-chartjs'
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  BarElement,
  LineController,
  BarController,
  Filler,
  Tooltip,
  Legend,
} from 'chart.js'
import type { ChartData, ChartOptions } from 'chart.js'
import type { ConversionBasis } from '../types/dashboard'
import { useChartTheme } from '../composables/useChartTheme'

// BaseChart is the sole `vue-chartjs`/`chart.js` import point in the
// dashboard module (design.md Decision 6) — controllers are registered once
// here, covering the line + bar chart family the dashboard's widgets need.
ChartJS.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  BarElement,
  LineController,
  BarController,
  Filler,
  Tooltip,
  Legend,
)

const props = withDefaults(
  defineProps<{
    type: BaseChartType
    series: ChartSeriesInput[]
    labels: string[]
    axisLabel?: string
    conversionBasis: ConversionBasis
    loading?: boolean
    empty?: boolean
  }>(),
  {
    axisLabel: undefined,
    loading: false,
    empty: false,
  },
)

const { t } = useI18n()
const { theme } = useChartTheme()

const conversionBasisLabel = computed(() =>
  props.conversionBasis === 'cut-frozen'
    ? t('dashboard.conversionBasis.cutFrozen')
    : t('dashboard.conversionBasis.transactionTime'),
)

const chartData = computed<ChartData>(() => ({
  labels: props.labels,
  datasets: props.series.map((s, index) => {
    const color = s.color ?? theme.value.palette[index % theme.value.palette.length]
    return {
      label: s.label,
      data: s.data,
      borderColor: s.borderColor ?? color,
      backgroundColor: s.backgroundColor ?? color,
      ...(s.pointRadius !== undefined ? { pointRadius: s.pointRadius } : {}),
      ...(s.fill !== undefined ? { fill: s.fill } : {}),
    }
  }),
}))

const chartOptions = computed<ChartOptions>(() => ({
  responsive: true,
  maintainAspectRatio: false,
  scales: {
    x: {
      ticks: { color: theme.value.textColor },
      grid: { color: theme.value.gridColor },
    },
    y: {
      ticks: { color: theme.value.textColor },
      grid: { color: theme.value.gridColor },
      title: props.axisLabel ? { display: true, text: props.axisLabel, color: theme.value.textColor } : undefined,
    },
  },
  plugins: {
    legend: { labels: { color: theme.value.textColor } },
  },
}))
</script>

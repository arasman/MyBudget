<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-4">
    <div class="lg:col-span-8">
      <!-- DASH-12: mismatch blocks the chart entirely — never one blended-axis chart. -->
      <CurrencyMismatchWarning v-if="mismatch.hasMismatch" />
      <BaseChart
        v-else
        type="line"
        :series="chartSeries"
        :labels="chartLabels"
        :axis-label="t('dashboard.lineSeries.axisLabel')"
        conversion-basis="transaction-time"
        :loading="store.lineSeriesLoading"
        :empty="isEmpty"
      />
    </div>
    <div class="lg:col-span-4 flex flex-col gap-4">
      <BudgetLinePicker :lines="structureStore.budgetLines" v-model="selectedLineIds" />
      <ComparisonModeSwitch
        :key="props.budgetId"
        :cycles="cycles"
        :mode="mode"
        :initial-selected-cycle-id="selectedCycleId"
        :initial-within-period-ids="withinPeriodIds"
        :initial-cross-cycle-ids="crossCycleIds"
        @update:mode="mode = $event"
        @update:selected-cycle-id="selectedCycleId = $event"
        @update:within-period-ids="withinPeriodIds = $event"
        @update:cross-cycle-ids="crossCycleIds = $event"
        @update:selectedPeriodIds="selectedPeriodIds = $event"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseChart from './BaseChart.vue'
import BudgetLinePicker from './BudgetLinePicker.vue'
import ComparisonModeSwitch from './ComparisonModeSwitch.vue'
import CurrencyMismatchWarning from './CurrencyMismatchWarning.vue'
import { useDashboardStore } from '../store/useDashboardStore'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import { useCycleOptions } from '../composables/useCycleOptions'
import { useChartTheme } from '../composables/useChartTheme'
import { useLineSeriesSelection } from '../composables/useLineSeriesSelection'
import { detectCurrencyMismatch } from '../utils/currencyGuard'
import { buildLineSeries } from '../utils/seriesMapping'

const props = defineProps<{ budgetId: string }>()

const { t } = useI18n()
const store = useDashboardStore()
const structureStore = useBudgetStructureStore()
const { cycles, load: loadCycleOptions } = useCycleOptions()
const { theme } = useChartTheme()

const { selectedLineIds, mode, selectedCycleId, withinPeriodIds, crossCycleIds } = useLineSeriesSelection(
  () => props.budgetId,
)
// Derived fresh from ComparisonModeSwitch's resolved emit — not itself
// persisted (see useLineSeriesSelection.ts doc comment).
const selectedPeriodIds = ref<string[]>([])

async function loadPickerData(): Promise<void> {
  await Promise.all([structureStore.loadLines(props.budgetId), loadCycleOptions(props.budgetId)])
}

async function maybeFetchLineSeries(): Promise<void> {
  if (selectedLineIds.value.length === 0 || selectedPeriodIds.value.length === 0) return
  await store.fetchLineSeries(props.budgetId, selectedLineIds.value, selectedPeriodIds.value)
}

onMounted(loadPickerData)
watch(() => props.budgetId, loadPickerData)
watch([selectedLineIds, selectedPeriodIds], maybeFetchLineSeries)

// DASH-12: guard reads the periods actually returned by the last fetch —
// this is the exact defaultCurrencyId set that would be plotted together.
const mismatch = computed(() => detectCurrencyMismatch(store.lineSeries?.periods ?? []))

const chartLabels = computed(() => store.lineSeries?.periods.map((p) => p.periodStart) ?? [])
const chartSeries = computed(() =>
  store.lineSeries ? buildLineSeries(store.lineSeries.rows, store.lineSeries.periods, selectedLineIds.value, theme.value.palette) : [],
)
const isEmpty = computed(
  () => !store.lineSeriesLoading && (selectedLineIds.value.length === 0 || selectedPeriodIds.value.length === 0),
)
</script>

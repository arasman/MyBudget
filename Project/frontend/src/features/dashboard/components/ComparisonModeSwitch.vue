<template>
  <div class="card bg-base-200 p-4 flex flex-col gap-3">
    <div class="join">
      <button
        type="button"
        class="btn btn-sm join-item"
        :class="mode === 'within-cycle' ? 'btn-active' : 'btn-ghost'"
        @click="setMode('within-cycle')"
      >
        {{ t('dashboard.comparisonMode.withinCycle') }}
      </button>
      <button
        type="button"
        class="btn btn-sm join-item"
        :class="mode === 'cross-cycle' ? 'btn-active' : 'btn-ghost'"
        @click="setMode('cross-cycle')"
      >
        {{ t('dashboard.comparisonMode.crossCycle') }}
      </button>
    </div>

    <!-- DASH-5: within-cycle — pick one Cycle, then 2+ Periods inside it. -->
    <div
      v-if="mode === 'within-cycle'"
      class="flex flex-col gap-2"
    >
      <label
        class="label-text text-xs"
        for="comparison-cycle-select"
      >{{ t('dashboard.comparisonMode.cycleLabel') }}</label>
      <select
        id="comparison-cycle-select"
        class="select select-sm"
        :aria-label="t('dashboard.comparisonMode.cycleLabel')"
        :value="selectedCycleId ?? ''"
        @change="onCycleChange(($event.target as HTMLSelectElement).value)"
      >
        <option
          v-for="cycle in cycles"
          :key="cycle.id"
          :value="cycle.id"
        >
          {{ cycle.name }}
        </option>
      </select>
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-x-3 gap-y-1 max-h-40 overflow-y-auto pr-1">
        <label
          v-for="period in withinCyclePeriods"
          :key="period.id"
          class="label cursor-pointer justify-start gap-2 py-1"
        >
          <input
            type="checkbox"
            class="checkbox checkbox-sm"
            :aria-label="period.name"
            :checked="withinPeriodIds.includes(period.id)"
            @change="togglePeriod(period.id)"
          >
          <span class="label-text text-sm">{{ period.name }}</span>
        </label>
      </div>
    </div>

    <!-- DASH-6: cross-cycle — pick 2+ Cycles; every Period of each is compared. -->
    <div
      v-else
      class="flex flex-col gap-2"
    >
      <span class="label-text text-xs">{{ t('dashboard.comparisonMode.cyclesLabel') }}</span>
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-x-3 gap-y-1 max-h-40 overflow-y-auto pr-1">
        <label
          v-for="cycle in cycles"
          :key="cycle.id"
          class="label cursor-pointer justify-start gap-2 py-1"
        >
          <input
            type="checkbox"
            class="checkbox checkbox-sm"
            :aria-label="cycle.name"
            :checked="crossCycleIds.includes(cycle.id)"
            @change="toggleCycle(cycle.id)"
          >
          <span class="label-text text-sm">{{ cycle.name }}</span>
        </label>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { resolvePeriodIds, type ComparisonMode } from '../utils/comparisonResolution'
import type { CycleOption } from '../composables/useCycleOptions'

const props = defineProps<{
  cycles: CycleOption[]
  mode: ComparisonMode
  initialSelectedCycleId?: string | null
  initialWithinPeriodIds?: string[]
  initialCrossCycleIds?: string[]
}>()
const emit = defineEmits<{
  'update:mode': [value: ComparisonMode]
  'update:selectedPeriodIds': [value: string[]]
  'update:selectedCycleId': [value: string | null]
  'update:withinPeriodIds': [value: string[]]
  'update:crossCycleIds': [value: string[]]
}>()

const { t } = useI18n()

// within-cycle state — seeded from `initial*` props so a caller (e.g. a
// persistence composable) can restore a previous pick; every mutation below
// also emits upward so that caller can persist it.
const selectedCycleId = ref<string | null>(props.initialSelectedCycleId ?? props.cycles[0]?.id ?? null)
const withinPeriodIds = ref<string[]>([...(props.initialWithinPeriodIds ?? [])])

// cross-cycle state
const crossCycleIds = ref<string[]>([...(props.initialCrossCycleIds ?? [])])

const withinCyclePeriods = computed(() => props.cycles.find((c) => c.id === selectedCycleId.value)?.periods ?? [])

function emitResolved(): void {
  const selection =
    props.mode === 'within-cycle'
      ? { mode: 'within-cycle' as const, cycleId: selectedCycleId.value, periodIds: withinPeriodIds.value }
      : { mode: 'cross-cycle' as const, cycleIds: crossCycleIds.value }
  emit('update:selectedPeriodIds', resolvePeriodIds(props.cycles, selection))
}

function setMode(next: ComparisonMode): void {
  if (next !== props.mode) emit('update:mode', next)
}

function onCycleChange(cycleId: string): void {
  selectedCycleId.value = cycleId
  withinPeriodIds.value = []
  emit('update:selectedCycleId', cycleId)
  emit('update:withinPeriodIds', [])
  emitResolved()
}

function togglePeriod(periodId: string): void {
  withinPeriodIds.value = withinPeriodIds.value.includes(periodId)
    ? withinPeriodIds.value.filter((id) => id !== periodId)
    : [...withinPeriodIds.value, periodId]
  emit('update:withinPeriodIds', withinPeriodIds.value)
  emitResolved()
}

function toggleCycle(cycleId: string): void {
  crossCycleIds.value = crossCycleIds.value.includes(cycleId)
    ? crossCycleIds.value.filter((id) => id !== cycleId)
    : [...crossCycleIds.value, cycleId]
  emit('update:crossCycleIds', crossCycleIds.value)
  emitResolved()
}

// Switching mode discards the other mode's in-progress selection so the
// resolved periodIds never mixes a stale within-cycle pick with a
// newly-entered cross-cycle pick (or vice versa).
watch(
  () => props.mode,
  () => {
    withinPeriodIds.value = []
    crossCycleIds.value = []
    selectedCycleId.value = props.cycles[0]?.id ?? null
    emit('update:selectedCycleId', selectedCycleId.value)
    emit('update:withinPeriodIds', [])
    emit('update:crossCycleIds', [])
    emitResolved()
  },
)

// The caller (BudgetLineSeriesChart.vue) mounts this component before its
// `useCycleOptions().load()` resolves — `cycles` starts as `[]` and arrives
// later. `selectedCycleId` is initialized once from `initialSelectedCycleId`
// or `props.cycles[0]` at setup time, both of which are empty on that first
// render, so it must be recovered once real cycles arrive. Only auto-picks
// when nothing is selected yet, so it never clobbers a cycle the user
// already chose from the dropdown. Also re-resolves unconditionally: a
// restored cross-cycle selection can't resolve to real periodIds until
// `cycles` itself arrives.
watch(
  () => props.cycles,
  (newCycles) => {
    if (selectedCycleId.value === null && newCycles.length > 0) {
      selectedCycleId.value = newCycles[0]!.id
      emit('update:selectedCycleId', selectedCycleId.value)
    }
    emitResolved()
  },
)

// Emit once at setup so a picker restored from persisted state (DASH-13)
// immediately reports its resolved periodIds upward — otherwise the parent
// only learns about them after the next user interaction.
emitResolved()
</script>

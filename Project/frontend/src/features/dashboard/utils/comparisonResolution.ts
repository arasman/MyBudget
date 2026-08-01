import type { CycleOption } from '../composables/useCycleOptions'

export type ComparisonMode = 'within-cycle' | 'cross-cycle'

/** DASH-5: user picks one Cycle, then 2+ Periods inside it. */
export interface WithinCycleSelection {
  mode: 'within-cycle'
  cycleId: string | null
  periodIds: string[]
}

/** DASH-6: user picks 2+ Cycles; every Period of each selected Cycle is compared. */
export interface CrossCycleSelection {
  mode: 'cross-cycle'
  cycleIds: string[]
}

export type ComparisonSelection = WithinCycleSelection | CrossCycleSelection

/**
 * Resolves the flat `periodIds` array sent to `GetBudgetLineSeries`
 * (design.md: "Within-cycle and cross-cycle are the SAME query — mode is a
 * presentation concern that only changes which periodIds the client
 * sends"). Within-cycle mode passes through the explicit period picks;
 * cross-cycle mode expands each selected cycle to the full set of its
 * period ids (DASH-6: cycles are compared as aggregated wholes).
 */
export function resolvePeriodIds(cycles: CycleOption[], selection: ComparisonSelection): string[] {
  if (selection.mode === 'within-cycle') {
    return selection.periodIds
  }

  const selectedCycleIds = new Set(selection.cycleIds)
  return cycles.filter((c) => selectedCycleIds.has(c.id)).flatMap((c) => c.periods.map((p) => p.id))
}

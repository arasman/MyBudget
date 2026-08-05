import { describe, it, expect, beforeEach } from 'vitest'
import { ref, nextTick } from 'vue'
import { useLineSeriesSelection } from '../composables/useLineSeriesSelection'

describe('useLineSeriesSelection', () => {
  beforeEach(() => {
    window.localStorage.clear()
  })

  it('defaults to empty lines, within-cycle mode, and no cycle/period picks when nothing is stored', () => {
    const { selectedLineIds, mode, selectedCycleId, withinPeriodIds, crossCycleIds } = useLineSeriesSelection(
      () => 'budget-1',
    )

    expect(selectedLineIds.value).toEqual([])
    expect(mode.value).toBe('within-cycle')
    expect(selectedCycleId.value).toBeNull()
    expect(withinPeriodIds.value).toEqual([])
    expect(crossCycleIds.value).toEqual([])
  })

  it('persists every field under a budgetId-scoped storage key', () => {
    const { selectedLineIds, mode, selectedCycleId, withinPeriodIds } = useLineSeriesSelection(() => 'budget-1')

    selectedLineIds.value = ['l1']
    mode.value = 'within-cycle'
    selectedCycleId.value = 'c1'
    withinPeriodIds.value = ['p1', 'p2']

    const raw = window.localStorage.getItem('dashboard.lineSeries.budget-1.selection')
    expect(raw).not.toBeNull()
    expect(JSON.parse(raw!)).toEqual({
      selectedLineIds: ['l1'],
      mode: 'within-cycle',
      selectedCycleId: 'c1',
      withinPeriodIds: ['p1', 'p2'],
      crossCycleIds: [],
    })
  })

  it('restores a previously persisted selection for the same budgetId', () => {
    window.localStorage.setItem(
      'dashboard.lineSeries.budget-1.selection',
      JSON.stringify({
        selectedLineIds: ['l2'],
        mode: 'cross-cycle',
        selectedCycleId: null,
        withinPeriodIds: [],
        crossCycleIds: ['c1', 'c2'],
      }),
    )

    const { selectedLineIds, mode, crossCycleIds } = useLineSeriesSelection(() => 'budget-1')

    expect(selectedLineIds.value).toEqual(['l2'])
    expect(mode.value).toBe('cross-cycle')
    expect(crossCycleIds.value).toEqual(['c1', 'c2'])
  })

  it('does not leak a selection stored under a different budgetId', () => {
    window.localStorage.setItem(
      'dashboard.lineSeries.budget-1.selection',
      JSON.stringify({
        selectedLineIds: ['l1'],
        mode: 'within-cycle',
        selectedCycleId: 'c1',
        withinPeriodIds: ['p1'],
        crossCycleIds: [],
      }),
    )

    const { selectedLineIds, selectedCycleId } = useLineSeriesSelection(() => 'budget-2')

    expect(selectedLineIds.value).toEqual([])
    expect(selectedCycleId.value).toBeNull()
  })

  it('ignores a corrupted stored value and falls back to defaults', () => {
    window.localStorage.setItem('dashboard.lineSeries.budget-1.selection', 'not-json')

    const { selectedLineIds, mode } = useLineSeriesSelection(() => 'budget-1')

    expect(selectedLineIds.value).toEqual([])
    expect(mode.value).toBe('within-cycle')
  })

  it('ignores a stored value with the wrong shape and falls back to defaults', () => {
    window.localStorage.setItem(
      'dashboard.lineSeries.budget-1.selection',
      JSON.stringify({ selectedLineIds: ['l1'], mode: 'not-a-mode' }),
    )

    const { selectedLineIds, mode } = useLineSeriesSelection(() => 'budget-1')

    expect(selectedLineIds.value).toEqual([])
    expect(mode.value).toBe('within-cycle')
  })

  // DASH-13: BudgetLineSeriesChart.vue stays mounted across a budgetId prop
  // change (it watches budgetId instead of remounting) — the storage key
  // must switch live, or a budget switch would keep writing into budget A's
  // key and never show budget B's own (possibly empty) selection.
  it('switches to the new budgetId key when budgetId changes reactively, without remounting', async () => {
    const budgetIdRef = ref('budget-1')
    const { selectedLineIds } = useLineSeriesSelection(() => budgetIdRef.value)

    selectedLineIds.value = ['l1']
    expect(JSON.parse(window.localStorage.getItem('dashboard.lineSeries.budget-1.selection')!).selectedLineIds).toEqual(['l1'])

    budgetIdRef.value = 'budget-2'
    await nextTick()

    expect(selectedLineIds.value).toEqual([])

    selectedLineIds.value = ['l9']
    await nextTick()

    expect(JSON.parse(window.localStorage.getItem('dashboard.lineSeries.budget-1.selection')!).selectedLineIds).toEqual(['l1'])
    expect(JSON.parse(window.localStorage.getItem('dashboard.lineSeries.budget-2.selection')!).selectedLineIds).toEqual(['l9'])
  })
})

import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import { nextTick } from 'vue'
import type { CycleOption } from '../composables/useCycleOptions'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        'dashboard.comparisonMode.withinCycle': 'Within cycle',
        'dashboard.comparisonMode.crossCycle': 'Cross cycle',
        'dashboard.comparisonMode.cycleLabel': 'Cycle',
        'dashboard.comparisonMode.periodsLabel': 'Periods',
        'dashboard.comparisonMode.cyclesLabel': 'Cycles',
      }
      return map[key] ?? key
    },
  }),
}))

import ComparisonModeSwitch from '../components/ComparisonModeSwitch.vue'

function cycles(): CycleOption[] {
  return [
    {
      id: 'c1',
      name: 'Cycle 1',
      defaultCurrencyId: 'usd',
      periods: [
        { id: 'p1', name: 'Period 1', startDate: '2026-01-01' },
        { id: 'p2', name: 'Period 2', startDate: '2026-02-01' },
      ],
    },
    {
      id: 'c2',
      name: 'Cycle 2',
      defaultCurrencyId: 'eur',
      periods: [{ id: 'p3', name: 'Period 1', startDate: '2026-04-01' }],
    },
  ]
}

describe('ComparisonModeSwitch (DASH-5/DASH-6)', () => {
  it('renders "Within cycle" and "Cross cycle" mode buttons', () => {
    render(ComparisonModeSwitch, { props: { cycles: cycles(), mode: 'within-cycle' } })

    expect(screen.getByRole('button', { name: 'Within cycle' })).not.toBeNull()
    expect(screen.getByRole('button', { name: 'Cross cycle' })).not.toBeNull()
  })

  it('clicking "Cross cycle" emits update:mode with cross-cycle', async () => {
    const { emitted } = render(ComparisonModeSwitch, { props: { cycles: cycles(), mode: 'within-cycle' } })

    await fireEvent.click(screen.getByRole('button', { name: 'Cross cycle' }))

    expect(emitted()['update:mode']![0]).toEqual(['cross-cycle'])
  })

  it('within-cycle mode: lists periods only for the first cycle by default', () => {
    render(ComparisonModeSwitch, { props: { cycles: cycles(), mode: 'within-cycle' } })

    expect(screen.getByLabelText('Period 1')).not.toBeNull()
    expect(screen.getByLabelText('Period 2')).not.toBeNull()
    expect(screen.getAllByRole('checkbox')).toHaveLength(2)
  })

  it('within-cycle mode: checking 2 periods emits their resolved periodIds (DASH-5)', async () => {
    const { emitted } = render(ComparisonModeSwitch, { props: { cycles: cycles(), mode: 'within-cycle' } })

    await fireEvent.click(screen.getByLabelText('Period 1'))
    await fireEvent.click(screen.getByLabelText('Period 2'))

    const calls = emitted()['update:selectedPeriodIds'] as unknown as unknown[][]
    expect(calls.at(-1)![0]).toEqual(['p1', 'p2'])
  })

  it('within-cycle mode: switching the selected cycle resets the period selection', async () => {
    const { emitted } = render(ComparisonModeSwitch, { props: { cycles: cycles(), mode: 'within-cycle' } })

    await fireEvent.click(screen.getByLabelText('Period 1'))
    await fireEvent.update(screen.getByLabelText('Cycle'), 'c2')

    const calls = emitted()['update:selectedPeriodIds'] as unknown as unknown[][]
    expect(calls.at(-1)![0]).toEqual([])
    expect(screen.queryByLabelText('Period 2')).toBeNull()
  })

  it('cross-cycle mode: lists every cycle as a checkbox', () => {
    render(ComparisonModeSwitch, { props: { cycles: cycles(), mode: 'cross-cycle' } })

    expect(screen.getByLabelText('Cycle 1')).not.toBeNull()
    expect(screen.getByLabelText('Cycle 2')).not.toBeNull()
  })

  it('cross-cycle mode: checking 2 cycles emits every period of both, resolved (DASH-6)', async () => {
    const { emitted } = render(ComparisonModeSwitch, { props: { cycles: cycles(), mode: 'cross-cycle' } })

    await fireEvent.click(screen.getByLabelText('Cycle 1'))
    await fireEvent.click(screen.getByLabelText('Cycle 2'))

    const calls = emitted()['update:selectedPeriodIds'] as unknown as unknown[][]
    expect(calls.at(-1)![0]).toEqual(['p1', 'p2', 'p3'])
  })

  // BudgetLineSeriesChart.vue mounts ComparisonModeSwitch with `cycles={[]}`
  // (useCycleOptions().load() is async, still in flight at mount) — the real
  // app never renders this component with data already present at setup
  // time like every test above. selectedCycleId must recover once the
  // cycles prop actually arrives, or within-cycle mode is permanently
  // unusable in production (no period list ever renders).
  it('within-cycle mode: picks the first cycle once cycles arrive asynchronously after mount', async () => {
    const { rerender } = render(ComparisonModeSwitch, { props: { cycles: [], mode: 'within-cycle' } })

    expect(screen.queryByLabelText('Period 1')).toBeNull()

    await rerender({ cycles: cycles(), mode: 'within-cycle' })
    await nextTick()

    expect(screen.getByLabelText('Period 1')).not.toBeNull()
    expect(screen.getByLabelText('Period 2')).not.toBeNull()
  })

  // DASH-13: BudgetLineSeriesChart.vue restores this picker's own UI (which
  // cycle/periods/cycles are checked) from a persistence composable via
  // these `initial*` props, and listens to the `update:*` emits to persist
  // subsequent changes — this is additive to the existing mode/periodIds
  // contract proven above.
  describe('restoring and reporting internal picker state (DASH-13)', () => {
    it('seeds the cycle dropdown and checked periods from initial* props', () => {
      render(ComparisonModeSwitch, {
        props: {
          cycles: cycles(),
          mode: 'within-cycle',
          initialSelectedCycleId: 'c2',
          initialWithinPeriodIds: ['p3'],
        },
      })

      expect((screen.getByLabelText('Cycle') as HTMLSelectElement).value).toBe('c2')
      expect((screen.getByLabelText('Period 1') as HTMLInputElement).checked).toBe(true)
    })

    it('seeds checked cycles in cross-cycle mode from initialCrossCycleIds', () => {
      render(ComparisonModeSwitch, {
        props: { cycles: cycles(), mode: 'cross-cycle', initialCrossCycleIds: ['c2'] },
      })

      expect((screen.getByLabelText('Cycle 2') as HTMLInputElement).checked).toBe(true)
      expect((screen.getByLabelText('Cycle 1') as HTMLInputElement).checked).toBe(false)
    })

    it('emits the resolved periodIds once at setup, reflecting the restored state', () => {
      const { emitted } = render(ComparisonModeSwitch, {
        props: {
          cycles: cycles(),
          mode: 'within-cycle',
          initialSelectedCycleId: 'c1',
          initialWithinPeriodIds: ['p1', 'p2'],
        },
      })

      const calls = emitted()['update:selectedPeriodIds'] as unknown as unknown[][]
      expect(calls[0]![0]).toEqual(['p1', 'p2'])
    })

    it('emits update:selectedCycleId and update:withinPeriodIds when the cycle changes', async () => {
      const { emitted } = render(ComparisonModeSwitch, {
        props: { cycles: cycles(), mode: 'within-cycle', initialWithinPeriodIds: ['p1'] },
      })

      await fireEvent.update(screen.getByLabelText('Cycle'), 'c2')

      expect(emitted()['update:selectedCycleId']!.at(-1)).toEqual(['c2'])
      expect(emitted()['update:withinPeriodIds']!.at(-1)).toEqual([[]])
    })

    it('emits update:crossCycleIds when a cycle checkbox is toggled', async () => {
      const { emitted } = render(ComparisonModeSwitch, { props: { cycles: cycles(), mode: 'cross-cycle' } })

      await fireEvent.click(screen.getByLabelText('Cycle 1'))

      expect(emitted()['update:crossCycleIds']!.at(-1)).toEqual([['c1']])
    })
  })
})

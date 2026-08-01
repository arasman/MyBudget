import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
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
})

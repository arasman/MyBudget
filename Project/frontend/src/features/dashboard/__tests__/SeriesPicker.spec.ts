import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import { TOTAL_KEYS } from '../types/dashboard'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (key: string) => {
      const labels: Record<string, string> = {
        'dashboard.seriesPicker.title': 'Series',
        'dashboard.seriesPicker.selectAll': 'Select all',
        'dashboard.seriesPicker.clearAll': 'Clear all',
      }
      if (labels[key]) return labels[key]
      const seriesKey = key.replace('dashboard.series.', '')
      return `Label:${seriesKey}`
    },
  }),
}))

import SeriesPicker from '../components/SeriesPicker.vue'

describe('SeriesPicker (DASH-2)', () => {
  it('lists all 16 total concepts as labeled checkboxes', () => {
    render(SeriesPicker, { props: { modelValue: [] } })

    for (const key of TOTAL_KEYS) {
      expect(screen.getByLabelText(`Label:${key}`)).not.toBeNull()
    }
    expect(screen.getAllByRole('checkbox')).toHaveLength(16)
  })

  it('checks the boxes that are present in modelValue', () => {
    render(SeriesPicker, { props: { modelValue: ['totalNet', 'totalAvailable'] } })

    expect((screen.getByLabelText('Label:totalNet') as HTMLInputElement).checked).toBe(true)
    expect((screen.getByLabelText('Label:totalAvailable') as HTMLInputElement).checked).toBe(true)
    expect((screen.getByLabelText('Label:totalPositive') as HTMLInputElement).checked).toBe(false)
  })

  it('emits update:modelValue with the key added when an unchecked box is clicked', async () => {
    const { emitted } = render(SeriesPicker, { props: { modelValue: ['totalNet'] } })

    await fireEvent.click(screen.getByLabelText('Label:totalAvailable'))

    expect(emitted()['update:modelValue']![0]).toEqual([['totalNet', 'totalAvailable']])
  })

  it('emits update:modelValue with the key removed when a checked box is clicked', async () => {
    const { emitted } = render(SeriesPicker, { props: { modelValue: ['totalNet', 'totalAvailable'] } })

    await fireEvent.click(screen.getByLabelText('Label:totalNet'))

    expect(emitted()['update:modelValue']![0]).toEqual([['totalAvailable']])
  })

  it('"select all" emits every one of the 16 total keys', async () => {
    const { emitted } = render(SeriesPicker, { props: { modelValue: [] } })

    await fireEvent.click(screen.getByRole('button', { name: 'Select all' }))

    const calls = emitted()['update:modelValue'] as unknown as unknown[][]
    expect(calls[0]![0]).toEqual([...TOTAL_KEYS])
  })

  it('"clear all" emits an empty array', async () => {
    const { emitted } = render(SeriesPicker, { props: { modelValue: [...TOTAL_KEYS] } })

    await fireEvent.click(screen.getByRole('button', { name: 'Clear all' }))

    expect(emitted()['update:modelValue']![0]).toEqual([[]])
  })
})

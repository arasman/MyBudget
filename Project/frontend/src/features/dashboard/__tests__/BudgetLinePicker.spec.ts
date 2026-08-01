import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        'dashboard.linePicker.title': 'Budget Lines',
        'dashboard.linePicker.selectAll': 'Select all',
        'dashboard.linePicker.clearAll': 'Clear all',
      }
      return map[key] ?? key
    },
  }),
}))

import BudgetLinePicker from '../components/BudgetLinePicker.vue'

const lines = [
  { id: 'l1', name: 'Groceries' },
  { id: 'l2', name: 'Rent' },
  { id: 'l3', name: 'Utilities' },
]

describe('BudgetLinePicker (supports DASH-4/5/6 BudgetLine multi-select)', () => {
  it('renders one checkbox per BudgetLine, labeled by name', () => {
    render(BudgetLinePicker, { props: { lines, modelValue: [] } })

    expect(screen.getByLabelText('Groceries')).not.toBeNull()
    expect(screen.getByLabelText('Rent')).not.toBeNull()
    expect(screen.getByLabelText('Utilities')).not.toBeNull()
    expect(screen.getAllByRole('checkbox')).toHaveLength(3)
  })

  it('reflects modelValue as checked state', () => {
    render(BudgetLinePicker, { props: { lines, modelValue: ['l2'] } })

    expect((screen.getByLabelText('Rent') as HTMLInputElement).checked).toBe(true)
    expect((screen.getByLabelText('Groceries') as HTMLInputElement).checked).toBe(false)
  })

  it('emits modelValue with the line id added when an unchecked box is clicked', async () => {
    const { emitted } = render(BudgetLinePicker, { props: { lines, modelValue: ['l1'] } })

    await fireEvent.click(screen.getByLabelText('Rent'))

    expect(emitted()['update:modelValue']![0]).toEqual([['l1', 'l2']])
  })

  it('emits modelValue with the line id removed when a checked box is clicked', async () => {
    const { emitted } = render(BudgetLinePicker, { props: { lines, modelValue: ['l1', 'l2'] } })

    await fireEvent.click(screen.getByLabelText('Groceries'))

    expect(emitted()['update:modelValue']![0]).toEqual([['l2']])
  })

  it('"select all" emits every BudgetLine id', async () => {
    const { emitted } = render(BudgetLinePicker, { props: { lines, modelValue: [] } })

    await fireEvent.click(screen.getByRole('button', { name: 'Select all' }))

    expect(emitted()['update:modelValue']![0]).toEqual([['l1', 'l2', 'l3']])
  })

  it('"clear all" emits an empty array', async () => {
    const { emitted } = render(BudgetLinePicker, { props: { lines, modelValue: ['l1', 'l2', 'l3'] } })

    await fireEvent.click(screen.getByRole('button', { name: 'Clear all' }))

    expect(emitted()['update:modelValue']![0]).toEqual([[]])
  })
})

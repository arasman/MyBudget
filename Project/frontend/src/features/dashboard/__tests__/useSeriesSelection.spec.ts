import { describe, it, expect, beforeEach } from 'vitest'
import { useSeriesSelection } from '../composables/useSeriesSelection'
import type { TotalKey } from '../types/dashboard'

const STORAGE_KEY = 'dashboard.testSelection'

describe('useSeriesSelection', () => {
  beforeEach(() => {
    window.localStorage.clear()
  })

  it('preselects the given default keys when nothing is stored', () => {
    const { selected } = useSeriesSelection(STORAGE_KEY, ['totalNet', 'totalAvailable'])

    expect(selected.value).toEqual(['totalNet', 'totalAvailable'])
  })

  it('falls back to a built-in default when no explicit default is passed', () => {
    const { selected } = useSeriesSelection(STORAGE_KEY)

    expect(selected.value.length).toBeGreaterThan(0)
  })

  it('persists the selection to localStorage under the given key when setSelected is called', () => {
    const { setSelected } = useSeriesSelection(STORAGE_KEY, ['totalNet'])

    setSelected(['totalNet', 'totalBudgeted'] as TotalKey[])

    expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY)!)).toEqual(['totalNet', 'totalBudgeted'])
  })

  it('restores a previously persisted selection on init instead of the default', () => {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(['totalRegistered']))

    const { selected } = useSeriesSelection(STORAGE_KEY, ['totalNet'])

    expect(selected.value).toEqual(['totalRegistered'])
  })

  it('ignores a corrupted stored value and falls back to the default', () => {
    window.localStorage.setItem(STORAGE_KEY, 'not-json')

    const { selected } = useSeriesSelection(STORAGE_KEY, ['totalNet'])

    expect(selected.value).toEqual(['totalNet'])
  })

  it('ignores an empty stored array and falls back to the default', () => {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify([]))

    const { selected } = useSeriesSelection(STORAGE_KEY, ['totalNet'])

    expect(selected.value).toEqual(['totalNet'])
  })
})

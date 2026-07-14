import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { ref } from 'vue'
import { useMatrixNavigation } from '../composables/useMatrixNavigation'

// ---------------------------------------------------------------------------
// Minimal store stub — no API calls needed for navigation tests
// ---------------------------------------------------------------------------

function makeStoreStub(periods: string[], offset = 0, windowSize = 3) {
  const periodObjects = periods.map((id, i) => ({
    id,
    name: `Period ${i + 1}`,
    periodNumber: i + 1,
    startDate: '2026-01-01' as import('@/features/budget-structure/types').DateString,
    endDate: '2026-01-31' as import('@/features/budget-structure/types').DateString,
    status: 'Open',
  }))

  // Reactive-like stub using a plain object (composable reads properties directly)
  const stub = {
    allPeriods: periodObjects,
    visiblePeriodOffset: offset,
    visibleWindowSize: windowSize,
    navigatePrev: vi.fn(() => {
      stub.visiblePeriodOffset = Math.max(0, stub.visiblePeriodOffset - 1)
    }),
    navigateNext: vi.fn(() => {
      const max = Math.max(0, stub.allPeriods.length - stub.visibleWindowSize)
      stub.visiblePeriodOffset = Math.min(max, stub.visiblePeriodOffset + 1)
    }),
  }

  return stub as unknown as ReturnType<typeof import('../store').useBudgetMatrixStore>
}

describe('useMatrixNavigation', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  // -------------------------------------------------------------------------
  // canGoPrev
  // -------------------------------------------------------------------------

  it('canGoPrev is false when offset is 0', () => {
    const store = makeStoreStub(['p1', 'p2', 'p3', 'p4'], 0)
    const { canGoPrev } = useMatrixNavigation(store)
    expect(canGoPrev.value).toBe(false)
  })

  it('canGoPrev is true when offset > 0', () => {
    const store = makeStoreStub(['p1', 'p2', 'p3', 'p4'], 1)
    const { canGoPrev } = useMatrixNavigation(store)
    expect(canGoPrev.value).toBe(true)
  })

  // -------------------------------------------------------------------------
  // canGoNext
  // -------------------------------------------------------------------------

  it('canGoNext is false when offset is at max (allPeriods.length - windowSize)', () => {
    // 4 periods, windowSize=3 → max offset = 1
    const store = makeStoreStub(['p1', 'p2', 'p3', 'p4'], 1)
    const { canGoNext } = useMatrixNavigation(store)
    expect(canGoNext.value).toBe(false)
  })

  it('canGoNext is true when offset < max', () => {
    // 5 periods, windowSize=3, offset=0 → max=2, canGoNext=true
    const store = makeStoreStub(['p1', 'p2', 'p3', 'p4', 'p5'], 0)
    const { canGoNext } = useMatrixNavigation(store)
    expect(canGoNext.value).toBe(true)
  })

  // -------------------------------------------------------------------------
  // visiblePeriods — 3-window slice
  // -------------------------------------------------------------------------

  it('visiblePeriods returns correct 3-period slice at offset 0', () => {
    const store = makeStoreStub(['p1', 'p2', 'p3', 'p4', 'p5'], 0)
    const { visiblePeriods } = useMatrixNavigation(store)
    const ids = visiblePeriods.value.map((p) => p.id)
    expect(ids).toEqual(['p1', 'p2', 'p3'])
  })

  it('visiblePeriods returns correct 3-period slice at offset 2', () => {
    const store = makeStoreStub(['p1', 'p2', 'p3', 'p4', 'p5'], 2)
    const { visiblePeriods } = useMatrixNavigation(store)
    const ids = visiblePeriods.value.map((p) => p.id)
    expect(ids).toEqual(['p3', 'p4', 'p5'])
  })

  // -------------------------------------------------------------------------
  // Edge cases — fewer than 3 periods
  // -------------------------------------------------------------------------

  it('fewer than 3 periods: canGoNext is false and canGoPrev is false', () => {
    const store = makeStoreStub(['p1', 'p2'], 0)
    const { canGoPrev, canGoNext } = useMatrixNavigation(store)
    expect(canGoPrev.value).toBe(false)
    expect(canGoNext.value).toBe(false)
  })

  it('fewer than 3 periods: visiblePeriods returns all available periods', () => {
    const store = makeStoreStub(['p1', 'p2'], 0)
    const { visiblePeriods } = useMatrixNavigation(store)
    const ids = visiblePeriods.value.map((p) => p.id)
    expect(ids).toEqual(['p1', 'p2'])
  })

  // -------------------------------------------------------------------------
  // Exactly 3 periods
  // -------------------------------------------------------------------------

  it('exactly 3 periods: canGoPrev and canGoNext are both false', () => {
    const store = makeStoreStub(['p1', 'p2', 'p3'], 0)
    const { canGoPrev, canGoNext } = useMatrixNavigation(store)
    expect(canGoPrev.value).toBe(false)
    expect(canGoNext.value).toBe(false)
  })
})

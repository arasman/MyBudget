import { describe, it, expect } from 'vitest'
import { useCurrencyDisplay } from '../composables/useCurrencyDisplay'
import type { useBudgetMatrixStore } from '../store'

// ---------------------------------------------------------------------------
// Minimal store stub — only displayCurrency and exchangeRate are needed
// ---------------------------------------------------------------------------

function makeStoreStub(
  displayCurrency: 'default' | 'alternate',
  exchangeRate: number | null,
) {
  return {
    displayCurrency,
    exchangeRate,
  } as unknown as ReturnType<typeof useBudgetMatrixStore>
}

describe('useCurrencyDisplay', () => {
  // -------------------------------------------------------------------------
  // convert — alternate mode with valid exchange rate
  // -------------------------------------------------------------------------

  it('convert(750) with alternate mode and rate 7.5 returns 100', () => {
    const store = makeStoreStub('alternate', 7.5)
    const { convert } = useCurrencyDisplay(store)
    expect(convert(750)).toBe(100)
  })

  // -------------------------------------------------------------------------
  // convert — default mode (no conversion)
  // -------------------------------------------------------------------------

  it('convert(750) with default mode returns 750 unchanged', () => {
    const store = makeStoreStub('default', 7.5)
    const { convert } = useCurrencyDisplay(store)
    expect(convert(750)).toBe(750)
  })

  // -------------------------------------------------------------------------
  // convert — null exchangeRate guard
  // -------------------------------------------------------------------------

  it('convert with null exchangeRate returns amount unchanged even in alternate mode', () => {
    const store = makeStoreStub('alternate', null)
    const { convert } = useCurrencyDisplay(store)
    expect(convert(750)).toBe(750)
  })

  // -------------------------------------------------------------------------
  // convert — rounding precision
  // -------------------------------------------------------------------------

  it('convert(100) with rate 3 rounds to 33.33 (not 33.333...)', () => {
    const store = makeStoreStub('alternate', 3)
    const { convert } = useCurrencyDisplay(store)
    expect(convert(100)).toBe(33.33)
  })

  // -------------------------------------------------------------------------
  // formatAmount — symbol prefix and en-US locale
  // -------------------------------------------------------------------------

  it('formatAmount prefixes symbol and formats with 2 decimal places', () => {
    const store = makeStoreStub('default', null)
    const { formatAmount } = useCurrencyDisplay(store)
    expect(formatAmount(1500, 'Q')).toBe('Q 1,500.00')
  })

  it('formatAmount converts amount when in alternate mode', () => {
    const store = makeStoreStub('alternate', 7.5)
    const { formatAmount } = useCurrencyDisplay(store)
    // 750 / 7.5 = 100 → formatted as "$ 100.00"
    expect(formatAmount(750, '$')).toBe('$ 100.00')
  })
})

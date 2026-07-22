// REQ-BLR-05: BudgetLineRevisionResponse type
import { describe, it, expect } from 'vitest'
import { toDateString, formatDate, type DateString, type BudgetLineRevisionResponse } from '../types'

describe('BudgetLineRevisionResponse — type shape (REQ-BLR-05)', () => {
  it('has required fields id, budgetedAmount, currencyId, validFrom', () => {
    const revision: BudgetLineRevisionResponse = {
      id: 'rev-1',
      budgetedAmount: 1500,
      currencyId: 'currency-gtq',
      validFrom: '2025-01-01' as DateString,
      validTo: null,
    }
    expect(revision.id).toBe('rev-1')
    expect(revision.budgetedAmount).toBe(1500)
    expect(revision.currencyId).toBe('currency-gtq')
    expect(revision.validFrom).toBe('2025-01-01')
    expect(revision.validTo).toBeNull()
  })

  it('allows optional currencyCode, currencySymbol, note', () => {
    const revision: BudgetLineRevisionResponse = {
      id: 'rev-2',
      budgetedAmount: 2000,
      currencyId: 'currency-gtq',
      validFrom: '2025-06-01' as DateString,
      validTo: '2025-12-31' as DateString,
      currencyCode: 'GTQ',
      currencySymbol: 'Q',
      note: 'Salary raise',
    }
    expect(revision.currencyCode).toBe('GTQ')
    expect(revision.currencySymbol).toBe('Q')
    expect(revision.note).toBe('Salary raise')
    expect(revision.validTo).toBe('2025-12-31')
  })
})

describe('DateString utils', () => {
  describe('toDateString', () => {
    it('formats year/month/day as YYYY-MM-DD', () => {
      const result = toDateString(2024, 1, 15)
      expect(result).toBe('2024-01-15')
    })

    it('pads month and day with leading zeros', () => {
      expect(toDateString(2024, 3, 5)).toBe('2024-03-05')
    })

    it('handles December 31', () => {
      expect(toDateString(2024, 12, 31)).toBe('2024-12-31')
    })

    it('returns a value typed as DateString (assignable check)', () => {
      const ds: DateString = toDateString(2024, 1, 15)
      expect(ds).toBe('2024-01-15')
    })
  })

  describe('formatDate', () => {
    it('returns a non-empty string for en locale', () => {
      const result = formatDate('2024-01-15' as DateString, 'en')
      expect(result).toBeTruthy()
      expect(typeof result).toBe('string')
    })

    it('returns a non-empty string for es locale', () => {
      const result = formatDate('2024-12-31' as DateString, 'es')
      expect(result).toBeTruthy()
      expect(typeof result).toBe('string')
    })

    it('includes the year in the formatted output', () => {
      const result = formatDate('2024-06-15' as DateString, 'en')
      expect(result).toContain('2024')
    })

    it('avoids timezone shift — Jan 1 should not display as Dec 31', () => {
      const result = formatDate('2024-01-01' as DateString, 'en')
      // Should contain January (en) representation, not December
      expect(result).not.toMatch(/Dec/i)
    })
  })
})

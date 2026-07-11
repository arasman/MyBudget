import { describe, it, expect } from 'vitest'
import { toDateString, formatDate, type DateString } from '../types'

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

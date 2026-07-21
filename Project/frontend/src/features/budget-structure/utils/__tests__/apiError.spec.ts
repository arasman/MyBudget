import { describe, it, expect } from 'vitest'
import { extractApiErrorCode } from '../apiError'

describe('extractApiErrorCode', () => {
  it('returns null for null input', () => {
    expect(extractApiErrorCode(null)).toBeNull()
  })

  it('returns null for non-object input', () => {
    expect(extractApiErrorCode('some string')).toBeNull()
    expect(extractApiErrorCode(42)).toBeNull()
    expect(extractApiErrorCode(undefined)).toBeNull()
  })

  it('returns null when response is missing', () => {
    expect(extractApiErrorCode({})).toBeNull()
    expect(extractApiErrorCode(new Error('oops'))).toBeNull()
  })

  it('returns null when response.data is missing', () => {
    expect(extractApiErrorCode({ response: {} })).toBeNull()
  })

  it('extracts code from { error: "CODE" } shape', () => {
    const err = {
      response: {
        data: { error: 'CATEGORY_GROUP_NAME_DUPLICATE' },
      },
    }
    expect(extractApiErrorCode(err)).toBe('CATEGORY_GROUP_NAME_DUPLICATE')
  })

  it('extracts code from ProblemDetails { detail: "CODE" } shape', () => {
    const err = {
      response: {
        data: { detail: 'BUDGET_NAME_DUPLICATE' },
      },
    }
    expect(extractApiErrorCode(err)).toBe('BUDGET_NAME_DUPLICATE')
  })

  it('prefers error over detail when both present', () => {
    const err = {
      response: {
        data: { error: 'FIRST_CODE', detail: 'SECOND_CODE' },
      },
    }
    expect(extractApiErrorCode(err)).toBe('FIRST_CODE')
  })

  it('returns null when error is empty string', () => {
    const err = {
      response: {
        data: { error: '' },
      },
    }
    expect(extractApiErrorCode(err)).toBeNull()
  })

  it('returns detail when error field is empty string', () => {
    const err = {
      response: {
        data: { error: '', detail: 'CYCLE_NAME_DUPLICATE' },
      },
    }
    expect(extractApiErrorCode(err)).toBe('CYCLE_NAME_DUPLICATE')
  })

  it('handles all known backend error codes', () => {
    const codes = [
      'BUDGET_NAME_DUPLICATE',
      'CYCLE_NAME_DUPLICATE',
      'PERIOD_NAME_DUPLICATE',
      'BUDGET_LINE_NAME_DUPLICATE',
      'CATEGORY_GROUP_NAME_DUPLICATE',
      'CATEGORY_NAME_DUPLICATE',
      'OPERATION_DATE_OUT_OF_RANGE',
      'NOTE_REQUIRED',
    ]
    for (const code of codes) {
      expect(extractApiErrorCode({ response: { data: { error: code } } })).toBe(code)
    }
  })
})

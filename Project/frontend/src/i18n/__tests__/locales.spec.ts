import { describe, it, expect } from 'vitest'
import en from '../locales/en.json'
import es from '../locales/es.json'

// REQ-BLR-04: 5 error keys for budget-line-customizations must exist in both locales
describe('i18n locale keys — budget-line-customizations error codes (REQ-BLR-04)', () => {
  const errorKeys = [
    'rangeWouldOrphanRevision',
    'rangeWouldOrphanExecution',
    'revisionHasActiveExecutions',
    'cannotDeleteOriginalRevision',
    'executionOutOfDateRange',
  ] as const

  it.each(errorKeys)('en.json contains budgetStructure.budgetLines.errors.%s', (key) => {
    expect(en.budgetStructure.budgetLines.errors).toHaveProperty(key)
    expect(typeof (en.budgetStructure.budgetLines.errors as Record<string, string>)[key]).toBe('string')
    expect(((en.budgetStructure.budgetLines.errors as Record<string, string>)[key] as string).length).toBeGreaterThan(0)
  })

  it.each(errorKeys)('es.json contains budgetStructure.budgetLines.errors.%s', (key) => {
    expect(es.budgetStructure.budgetLines.errors).toHaveProperty(key)
    expect(typeof (es.budgetStructure.budgetLines.errors as Record<string, string>)[key]).toBe('string')
    expect(((es.budgetStructure.budgetLines.errors as Record<string, string>)[key] as string).length).toBeGreaterThan(0)
  })
})

// REQ-BLR-05: customizations section keys must exist in both locales
describe('i18n locale keys — customizations view (REQ-BLR-05)', () => {
  const customizationKeys = [
    'title',
    'backToLines',
    'revisions',
    'noRevisions',
    'validFrom',
    'validTo',
    'amount',
    'currency',
    'deleteRevision',
    'confirmDeleteRevision',
  ] as const

  it.each(customizationKeys)('en.json contains budgetStructure.budgetLines.customizations.%s', (key) => {
    const section = (en.budgetStructure.budgetLines as Record<string, unknown>)['customizations'] as Record<string, string>
    expect(section).toHaveProperty(key)
    expect(typeof section[key]).toBe('string')
    expect(section[key].length).toBeGreaterThan(0)
  })

  it.each(customizationKeys)('es.json contains budgetStructure.budgetLines.customizations.%s', (key) => {
    const section = (es.budgetStructure.budgetLines as Record<string, unknown>)['customizations'] as Record<string, string>
    expect(section).toHaveProperty(key)
    expect(typeof section[key]).toBe('string')
    expect(section[key].length).toBeGreaterThan(0)
  })
})

// REQ-I18N-KEYS: all 8 new keys must exist in both en.json and es.json
describe('i18n locale keys — global-toast-audit', () => {
  it('en.json contains budgetStructure.selection.renameSuccess', () => {
    expect(en.budgetStructure.selection).toHaveProperty('renameSuccess')
    expect(typeof en.budgetStructure.selection.renameSuccess).toBe('string')
    expect(en.budgetStructure.selection.renameSuccess.length).toBeGreaterThan(0)
  })

  it('es.json contains budgetStructure.selection.renameSuccess', () => {
    expect(es.budgetStructure.selection).toHaveProperty('renameSuccess')
    expect(typeof es.budgetStructure.selection.renameSuccess).toBe('string')
    expect(es.budgetStructure.selection.renameSuccess.length).toBeGreaterThan(0)
  })

  const budgetMatrixRowKeys = [
    'createGroupSuccess',
    'updateGroupSuccess',
    'deleteSuccess',
    'restoreSuccess',
    'createCategorySuccess',
    'updateCategorySuccess',
    'createLineSuccess',
  ] as const

  it.each(budgetMatrixRowKeys)('en.json contains budgetMatrix.rows.%s', (key) => {
    expect(en.budgetMatrix.rows).toHaveProperty(key)
    expect(typeof en.budgetMatrix.rows[key]).toBe('string')
    expect(en.budgetMatrix.rows[key].length).toBeGreaterThan(0)
  })

  it.each(budgetMatrixRowKeys)('es.json contains budgetMatrix.rows.%s', (key) => {
    expect(es.budgetMatrix.rows).toHaveProperty(key)
    expect(typeof es.budgetMatrix.rows[key]).toBe('string')
    expect(es.budgetMatrix.rows[key].length).toBeGreaterThan(0)
  })
})

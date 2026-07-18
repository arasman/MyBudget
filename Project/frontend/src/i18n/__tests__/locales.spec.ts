import { describe, it, expect } from 'vitest'
import en from '../locales/en.json'
import es from '../locales/es.json'

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

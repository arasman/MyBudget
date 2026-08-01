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

// cut-record-totals-persistence (proposal.md success criterion): snapshot semantics
// copy must exist in both locales under currentSituation.totals
describe('i18n locale keys — current-situation totals snapshot notice (cut-record-totals-persistence)', () => {
  it('en.json contains currentSituation.totals.snapshotNotice', () => {
    expect(en.currentSituation.totals).toHaveProperty('snapshotNotice')
    expect(typeof en.currentSituation.totals.snapshotNotice).toBe('string')
    expect(en.currentSituation.totals.snapshotNotice.length).toBeGreaterThan(0)
  })

  it('es.json contains currentSituation.totals.snapshotNotice', () => {
    expect(es.currentSituation.totals).toHaveProperty('snapshotNotice')
    expect(typeof es.currentSituation.totals.snapshotNotice).toBe('string')
    expect(es.currentSituation.totals.snapshotNotice.length).toBeGreaterThan(0)
  })
})

// dashboard (DASH-9/DASH-10): conversion-basis captions and generic chart
// loading/empty states must exist in both locales as soon as BaseChart.vue
// introduces them (PR4), not deferred to the PR7 assembly i18n task.
describe('i18n locale keys — dashboard conversion-basis captions and chart states (DASH-9/DASH-10)', () => {
  const conversionBasisKeys = ['cutFrozen', 'transactionTime'] as const
  const chartKeys = ['loading', 'empty'] as const

  it.each(conversionBasisKeys)('en.json contains dashboard.conversionBasis.%s', (key) => {
    expect(en.dashboard.conversionBasis).toHaveProperty(key)
    expect(typeof en.dashboard.conversionBasis[key]).toBe('string')
    expect(en.dashboard.conversionBasis[key].length).toBeGreaterThan(0)
  })

  it.each(conversionBasisKeys)('es.json contains dashboard.conversionBasis.%s', (key) => {
    expect(es.dashboard.conversionBasis).toHaveProperty(key)
    expect(typeof es.dashboard.conversionBasis[key]).toBe('string')
    expect(es.dashboard.conversionBasis[key].length).toBeGreaterThan(0)
  })

  it.each(chartKeys)('en.json contains dashboard.chart.%s', (key) => {
    expect(en.dashboard.chart).toHaveProperty(key)
    expect(typeof en.dashboard.chart[key]).toBe('string')
    expect(en.dashboard.chart[key].length).toBeGreaterThan(0)
  })

  it.each(chartKeys)('es.json contains dashboard.chart.%s', (key) => {
    expect(es.dashboard.chart).toHaveProperty(key)
    expect(typeof es.dashboard.chart[key]).toBe('string')
    expect(es.dashboard.chart[key].length).toBeGreaterThan(0)
  })
})

// dashboard (DASH-2/DASH-3/DASH-7/DASH-9/DASH-10, PR5): series-picker labels
// for the 16 CutRecord total concepts, series-picker controls, and the
// lifetime/band widget titles + insufficient-data empty state copy.
describe('i18n locale keys — dashboard lifetime/band widgets (DASH-2/3/7/9)', () => {
  const seriesKeys = [
    'totalPositive',
    'totalPositiveAlt',
    'totalNegative',
    'totalNegativeAlt',
    'totalDeudaEnCurso',
    'totalDeudaEnCursoAlt',
    'totalBudgeted',
    'totalBudgetedAlt',
    'totalRegistered',
    'totalRegisteredAlt',
    'remaining',
    'remainingAlt',
    'totalAvailable',
    'totalAvailableAlt',
    'totalNet',
    'totalNetAlt',
  ] as const

  it.each(seriesKeys)('en.json contains dashboard.series.%s', (key) => {
    expect(en.dashboard.series).toHaveProperty(key)
    expect(typeof en.dashboard.series[key]).toBe('string')
    expect(en.dashboard.series[key].length).toBeGreaterThan(0)
  })

  it.each(seriesKeys)('es.json contains dashboard.series.%s', (key) => {
    expect(es.dashboard.series).toHaveProperty(key)
    expect(typeof es.dashboard.series[key]).toBe('string')
    expect(es.dashboard.series[key].length).toBeGreaterThan(0)
  })

  const seriesPickerKeys = ['title', 'selectAll', 'clearAll'] as const

  it.each(seriesPickerKeys)('en.json contains dashboard.seriesPicker.%s', (key) => {
    expect(en.dashboard.seriesPicker).toHaveProperty(key)
    expect(typeof en.dashboard.seriesPicker[key]).toBe('string')
    expect(en.dashboard.seriesPicker[key].length).toBeGreaterThan(0)
  })

  it.each(seriesPickerKeys)('es.json contains dashboard.seriesPicker.%s', (key) => {
    expect(es.dashboard.seriesPicker).toHaveProperty(key)
    expect(typeof es.dashboard.seriesPicker[key]).toBe('string')
    expect(es.dashboard.seriesPicker[key].length).toBeGreaterThan(0)
  })

  it('en.json and es.json contain dashboard.lifetime.title/axisLabel', () => {
    expect(en.dashboard.lifetime.title.length).toBeGreaterThan(0)
    expect(en.dashboard.lifetime.axisLabel.length).toBeGreaterThan(0)
    expect(es.dashboard.lifetime.title.length).toBeGreaterThan(0)
    expect(es.dashboard.lifetime.axisLabel.length).toBeGreaterThan(0)
  })

  it('en.json and es.json contain dashboard.band.title/axisLabel/insufficientData', () => {
    expect(en.dashboard.band.title.length).toBeGreaterThan(0)
    expect(en.dashboard.band.axisLabel.length).toBeGreaterThan(0)
    expect(en.dashboard.band.insufficientData.title.length).toBeGreaterThan(0)
    expect(en.dashboard.band.insufficientData.description.length).toBeGreaterThan(0)
    expect(es.dashboard.band.title.length).toBeGreaterThan(0)
    expect(es.dashboard.band.axisLabel.length).toBeGreaterThan(0)
    expect(es.dashboard.band.insufficientData.title.length).toBeGreaterThan(0)
    expect(es.dashboard.band.insufficientData.description.length).toBeGreaterThan(0)
  })
})

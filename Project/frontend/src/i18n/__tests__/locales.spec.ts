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

// dashboard (DASH-4/DASH-5/DASH-6/DASH-9/DASH-10/DASH-12, PR6): BudgetLine
// per-period widget, BudgetLine multi-select, within-cycle/cross-cycle mode
// switch labels, and the safety-critical currency-mismatch warning copy.
describe('i18n locale keys — dashboard BudgetLine series + currency-mismatch guard (DASH-4/5/6/12)', () => {
  it('en.json and es.json contain dashboard.lineSeries.title/axisLabel', () => {
    expect(en.dashboard.lineSeries.title.length).toBeGreaterThan(0)
    expect(en.dashboard.lineSeries.axisLabel.length).toBeGreaterThan(0)
    expect(es.dashboard.lineSeries.title.length).toBeGreaterThan(0)
    expect(es.dashboard.lineSeries.axisLabel.length).toBeGreaterThan(0)
  })

  const linePickerKeys = ['title', 'selectAll', 'clearAll'] as const

  it.each(linePickerKeys)('en.json contains dashboard.linePicker.%s', (key) => {
    expect(en.dashboard.linePicker).toHaveProperty(key)
    expect(typeof en.dashboard.linePicker[key]).toBe('string')
    expect(en.dashboard.linePicker[key].length).toBeGreaterThan(0)
  })

  it.each(linePickerKeys)('es.json contains dashboard.linePicker.%s', (key) => {
    expect(es.dashboard.linePicker).toHaveProperty(key)
    expect(typeof es.dashboard.linePicker[key]).toBe('string')
    expect(es.dashboard.linePicker[key].length).toBeGreaterThan(0)
  })

  const comparisonModeKeys = ['withinCycle', 'crossCycle', 'cycleLabel', 'periodsLabel', 'cyclesLabel'] as const

  it.each(comparisonModeKeys)('en.json contains dashboard.comparisonMode.%s', (key) => {
    expect(en.dashboard.comparisonMode).toHaveProperty(key)
    expect(typeof en.dashboard.comparisonMode[key]).toBe('string')
    expect(en.dashboard.comparisonMode[key].length).toBeGreaterThan(0)
  })

  it.each(comparisonModeKeys)('es.json contains dashboard.comparisonMode.%s', (key) => {
    expect(es.dashboard.comparisonMode).toHaveProperty(key)
    expect(typeof es.dashboard.comparisonMode[key]).toBe('string')
    expect(es.dashboard.comparisonMode[key].length).toBeGreaterThan(0)
  })

  // DASH-12 is safety-critical copy: it must exist, be non-empty, and be a
  // clear explanatory message (not a bare generic error string) in both locales.
  it('en.json and es.json contain a clear dashboard.currencyMismatch.title/description', () => {
    expect(en.dashboard.currencyMismatch.title.length).toBeGreaterThan(0)
    expect(en.dashboard.currencyMismatch.description.length).toBeGreaterThan(10)
    expect(es.dashboard.currencyMismatch.title.length).toBeGreaterThan(0)
    expect(es.dashboard.currencyMismatch.description.length).toBeGreaterThan(10)
  })
})

// dashboard (DASH-7/DASH-10, PR7): page-level assembly copy — the
// DashboardView heading and the BudgetTabs nav entry label.
describe('i18n locale keys — dashboard page assembly (DASH-7/10)', () => {
  it('en.json and es.json contain dashboard.title/tabTitle', () => {
    expect(en.dashboard.title.length).toBeGreaterThan(0)
    expect(en.dashboard.tabTitle.length).toBeGreaterThan(0)
    expect(es.dashboard.title.length).toBeGreaterThan(0)
    expect(es.dashboard.tabTitle.length).toBeGreaterThan(0)
  })
})

// budget-member-administration (WU0/ACCEPT-1): duplicate-membership guard error copy
// must exist in both locales for AcceptInvitationView's AUTH_ALREADY_MEMBER branch.
describe('i18n locale keys — accept invitation already-member error (WU0)', () => {
  it('en.json contains invitation.accept.error.alreadyMember', () => {
    expect(en.invitation.accept.error).toHaveProperty('alreadyMember')
    expect(typeof en.invitation.accept.error.alreadyMember).toBe('string')
    expect(en.invitation.accept.error.alreadyMember.length).toBeGreaterThan(0)
  })

  it('es.json contains invitation.accept.error.alreadyMember', () => {
    expect(es.invitation.accept.error).toHaveProperty('alreadyMember')
    expect(typeof es.invitation.accept.error.alreadyMember).toBe('string')
    expect(es.invitation.accept.error.alreadyMember.length).toBeGreaterThan(0)
  })
})

// LAYOUT-4 / LANDING-6: AppFooter copy must exist in both locales
describe('i18n locale keys — footer (LAYOUT-4/LANDING-6)', () => {
  it('en.json contains footer.poweredBy', () => {
    expect(en.footer).toHaveProperty('poweredBy')
    expect(typeof en.footer.poweredBy).toBe('string')
    expect(en.footer.poweredBy.length).toBeGreaterThan(0)
  })

  it('es.json contains footer.poweredBy', () => {
    expect(es.footer).toHaveProperty('poweredBy')
    expect(typeof es.footer.poweredBy).toBe('string')
    expect(es.footer.poweredBy.length).toBeGreaterThan(0)
  })
})

// LANDING-2/LANDING-3/LANDING-4/LANDING-6: landing page copy (hero, 9 curated
// showcase tiles per features/landing/config/showcase.ts's i18nKeys, CTA,
// outbound links) must exist in both locales.
describe('i18n locale keys — landing page (LANDING-2/LANDING-3/LANDING-4/LANDING-6)', () => {
  it('en.json and es.json contain landing.hero.title/subtitle', () => {
    expect(en.landing.hero.title.length).toBeGreaterThan(0)
    expect(en.landing.hero.subtitle.length).toBeGreaterThan(0)
    expect(es.landing.hero.title.length).toBeGreaterThan(0)
    expect(es.landing.hero.subtitle.length).toBeGreaterThan(0)
  })

  // Mirrors the 9 i18nKey suffixes in features/landing/config/showcase.ts
  const showcaseKeys = [
    'auth',
    'bankAccounts',
    'budgetExecution',
    'budgetManagement',
    'budgetStructureCategories',
    'budgetStructureCycles',
    'budgetStructurePeriodsLines',
    'currentSituation',
    'dashboard',
  ] as const

  it.each(showcaseKeys)('en.json contains landing.showcase.%s.title/caption', (key) => {
    expect(en.landing.showcase[key].title.length).toBeGreaterThan(0)
    expect(en.landing.showcase[key].caption.length).toBeGreaterThan(0)
  })

  it.each(showcaseKeys)('es.json contains landing.showcase.%s.title/caption', (key) => {
    expect(es.landing.showcase[key].title.length).toBeGreaterThan(0)
    expect(es.landing.showcase[key].caption.length).toBeGreaterThan(0)
  })

  it('en.json and es.json contain landing.cta.primary/secondary', () => {
    expect(en.landing.cta.primary.length).toBeGreaterThan(0)
    expect(en.landing.cta.secondary.length).toBeGreaterThan(0)
    expect(es.landing.cta.primary.length).toBeGreaterThan(0)
    expect(es.landing.cta.secondary.length).toBeGreaterThan(0)
  })

  it('en.json and es.json contain landing.links.github/readme/deck', () => {
    expect(en.landing.links.github.length).toBeGreaterThan(0)
    expect(en.landing.links.readme.length).toBeGreaterThan(0)
    expect(en.landing.links.deck.length).toBeGreaterThan(0)
    expect(es.landing.links.github.length).toBeGreaterThan(0)
    expect(es.landing.links.readme.length).toBeGreaterThan(0)
    expect(es.landing.links.deck.length).toBeGreaterThan(0)
  })
})

// LANDING-9: showcase tile enlarge-on-interaction copy (button aria-label +
// visually-hidden dismiss hint) must exist in both locales.
describe('i18n locale keys — showcase tile enlarge on interaction (LANDING-9)', () => {
  it('en.json and es.json contain landing.showcase.enlarge', () => {
    expect(en.landing.showcase.enlarge.length).toBeGreaterThan(0)
    expect(es.landing.showcase.enlarge.length).toBeGreaterThan(0)
  })

  it('en.json and es.json contain landing.showcase.dismissHint', () => {
    expect(en.landing.showcase.dismissHint.length).toBeGreaterThan(0)
    expect(es.landing.showcase.dismissHint.length).toBeGreaterThan(0)
  })
})

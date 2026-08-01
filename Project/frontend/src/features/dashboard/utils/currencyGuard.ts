import type { PeriodSeries } from '../types/dashboard'

export interface CurrencyMismatchResult {
  hasMismatch: boolean
  currencyIds: string[]
}

/**
 * DASH-12: `DefaultCurrencyId` lives on `Cycle`, not `Budget`, so a
 * period-vs-period or cycle-vs-cycle comparison MAY involve periods whose
 * cycles use different currencies. WHEN the periods about to be plotted
 * together carry more than one distinct `defaultCurrencyId`, the caller
 * MUST warn the user and MUST NOT render one chart blending both currencies
 * on a shared axis (spec DASH-12 scenario, design.md "Cross-cycle currency
 * risk"). Pure function — no chart/axis decision happens here, only
 * detection, so it is testable without mocking BaseChart/vue-chartjs.
 */
export function detectCurrencyMismatch(periods: Pick<PeriodSeries, 'defaultCurrencyId'>[]): CurrencyMismatchResult {
  const currencyIds = Array.from(new Set(periods.map((p) => p.defaultCurrencyId)))
  return { hasMismatch: currencyIds.length > 1, currencyIds }
}

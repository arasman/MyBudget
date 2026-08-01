import { z } from 'zod'

// ---------------------------------------------------------------------------
// Conversion basis (DASH-9) — every dashboard response declares which
// exchange-rate basis its values use. A single chart must never blend both.
// ---------------------------------------------------------------------------
export const ConversionBasisSchema = z.enum(['cut-frozen', 'transaction-time'])
export type ConversionBasis = z.infer<typeof ConversionBasisSchema>

// ---------------------------------------------------------------------------
// The 16 CutRecord total concepts (8 concepts x primary/alt currency),
// mirrored 1:1 from CutTotalsPointDto / ConceptTotalsDto (backend).
// ---------------------------------------------------------------------------
export const TOTAL_KEYS = [
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

export type TotalKey = (typeof TOTAL_KEYS)[number]

const conceptTotalsShape = Object.fromEntries(TOTAL_KEYS.map((key) => [key, z.number()])) as Record<
  TotalKey,
  z.ZodNumber
>

// ---------------------------------------------------------------------------
// GET /api/budgets/:id/dashboard/cut-totals-series (DASH-1)
// ---------------------------------------------------------------------------
export const CutTotalsPointSchema = z.object({
  cutDate: z.string(),
  exchangeRate: z.number(),
  ...conceptTotalsShape,
})
export type CutTotalsPoint = z.infer<typeof CutTotalsPointSchema>

export const LifetimeCutTotalsResponseSchema = z.object({
  conversionBasis: z.literal('cut-frozen'),
  points: z.array(CutTotalsPointSchema),
})
export type LifetimeCutTotalsResponse = z.infer<typeof LifetimeCutTotalsResponseSchema>

// ---------------------------------------------------------------------------
// GET /api/budgets/:id/dashboard/cut-totals-band (DASH-2/3/11)
// ---------------------------------------------------------------------------
export const ConceptTotalsSchema = z.object(conceptTotalsShape)
export type ConceptTotals = z.infer<typeof ConceptTotalsSchema>

export const PeriodAverageSchema = z.object({
  periodId: z.string(),
  periodStart: z.string(),
  periodEnd: z.string(),
  avg: ConceptTotalsSchema,
})
export type PeriodAverage = z.infer<typeof PeriodAverageSchema>

export const BandValueSchema = z.object({
  avg: z.number(),
  min: z.number(),
  max: z.number(),
})
export type BandValue = z.infer<typeof BandValueSchema>

const totalsBandShape = Object.fromEntries(TOTAL_KEYS.map((key) => [key, BandValueSchema])) as Record<
  TotalKey,
  typeof BandValueSchema
>

export const TotalsBandSchema = z.object(totalsBandShape)
export type TotalsBand = z.infer<typeof TotalsBandSchema>

export const CutTotalsBandResponseSchema = z.object({
  conversionBasis: z.literal('cut-frozen'),
  periodCount: z.number().int().nonnegative(),
  periods: z.array(PeriodAverageSchema),
  band: TotalsBandSchema,
})
export type CutTotalsBandResponse = z.infer<typeof CutTotalsBandResponseSchema>

// ---------------------------------------------------------------------------
// GET /api/budgets/:id/dashboard/line-series (DASH-4/5/6/12)
// ---------------------------------------------------------------------------
export const PeriodSeriesSchema = z.object({
  periodId: z.string(),
  cycleId: z.string(),
  periodStart: z.string(),
  defaultCurrencyId: z.string(),
})
export type PeriodSeries = z.infer<typeof PeriodSeriesSchema>

export const BudgetLineSeriesRowSchema = z.object({
  budgetLineId: z.string(),
  budgetLineName: z.string(),
  periodId: z.string(),
  budgetedAmount: z.number(),
  netTotal: z.number(),
})
export type BudgetLineSeriesRow = z.infer<typeof BudgetLineSeriesRowSchema>

export const BudgetLineSeriesResponseSchema = z.object({
  conversionBasis: z.literal('transaction-time'),
  periods: z.array(PeriodSeriesSchema),
  rows: z.array(BudgetLineSeriesRowSchema),
})
export type BudgetLineSeriesResponse = z.infer<typeof BudgetLineSeriesResponseSchema>

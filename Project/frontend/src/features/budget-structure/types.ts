// Branded type for date-only strings received from the API (format: YYYY-MM-DD).
// Using a brand prevents accidental assignment of arbitrary strings.
export type DateString = string & { __brand: 'DateOnly' }

export type LineType = 'Expense' | 'LongTermSavings' | 'PreventiveSavings'

// ---------------------------------------------------------------------------
// DateString helpers
// ---------------------------------------------------------------------------

/**
 * Constructs a DateString from year/month/day integers.
 * Month is 1-indexed (January = 1).
 */
export function toDateString(year: number, month: number, day: number): DateString {
  const y = String(year).padStart(4, '0')
  const m = String(month).padStart(2, '0')
  const d = String(day).padStart(2, '0')
  return `${y}-${m}-${d}` as DateString
}

/**
 * Formats a DateString for display using Intl.DateTimeFormat.
 * Parses the YYYY-MM-DD parts directly — never calls `new Date(string)`
 * to avoid timezone-offset issues on date-only values.
 */
export function formatDate(date: DateString, locale: string): string {
  const [year, month, day] = date.split('-').map(Number) as [number, number, number]
  return new Intl.DateTimeFormat(locale, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(year, month - 1, day)))
}

// ---------------------------------------------------------------------------
// Cycle entities
// ---------------------------------------------------------------------------

export interface CurrencyItem {
  id: string
  code: string
  name: string
  symbol: string
}

export interface CycleListItem {
  id: string
  name: string
  startDate: DateString
  endDate: DateString
  isActive: boolean
  periodCount: number
  defaultCurrency?: CurrencyItem
  alternateCurrency?: CurrencyItem | null
  alternateCurrencyId?: string | null
  exchangeRate?: number | null
}

export interface CycleDetail extends Omit<CycleListItem, 'periodCount'> {
  periods: PeriodSummary[]
}

export interface PeriodSummary {
  id: string
  name: string
  periodNumber: number
  startDate: DateString
  endDate: DateString
  status: string
}

// ---------------------------------------------------------------------------
// Category entities
// ---------------------------------------------------------------------------

export interface CategoryItem {
  id: string
  name: string
  displayOrder: number
}

export interface CategoryGroupResponse {
  id: string
  name: string
  displayOrder: number
  categories: CategoryItem[]
  deletedAt?: string | null
}

// ---------------------------------------------------------------------------
// Budget line entities
// ---------------------------------------------------------------------------

export interface BudgetLineResponse {
  id: string
  name: string
  lineType: LineType
  isRecurring: boolean
  categoryGroupId: string
  categoryId?: string
  budgetedAmount?: number
  currencyCode?: string
  currencySymbol?: string
  revisedAt?: DateString
  note?: string
}

// ---------------------------------------------------------------------------
// API payload shapes
// ---------------------------------------------------------------------------

export interface CreateCyclePayload {
  name: string
  startDate: DateString
  endDate: DateString
  defaultCurrencyId: string
  alternateCurrencyId?: string
  exchangeRate?: number
}

export interface UpdateCyclePayload {
  name: string
  startDate: DateString
  endDate: DateString
  defaultCurrencyId: string
  alternateCurrencyId?: string
  exchangeRate?: number
}

export interface CreatePeriodPayload {
  name: string
  periodNumber: number
  startDate: DateString
  endDate: DateString
}

export interface UpdatePeriodPayload {
  name: string
  startDate: DateString
  endDate: DateString
}

export interface PatchPeriodStatusPayload {
  status: string
}

export interface CreateGroupPayload {
  name: string
}

export interface UpdateGroupPayload {
  name: string
}

export interface CreateCategoryPayload {
  name: string
}

export interface UpdateCategoryPayload {
  name: string
}

export interface CreateBudgetLinePayload {
  name: string
  lineType: LineType
  isRecurring: boolean
  categoryGroupId?: string
  categoryId?: string
  budgetedAmount?: number
  currency?: string
  note?: string
}

export interface UpdateBudgetLinePayload {
  name: string
  lineType: LineType
  isRecurring: boolean
  categoryGroupId?: string
  categoryId?: string
  budgetedAmount?: number
  currency?: string
  note?: string
}

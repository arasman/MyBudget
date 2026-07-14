// ---------------------------------------------------------------------------
// Execution entry type
// ---------------------------------------------------------------------------

export enum EntryType {
  Expense = 1,
  CreditNote = 2,
  DebitNote = 3,
}

// ---------------------------------------------------------------------------
// Execution record
// ---------------------------------------------------------------------------

export interface ExecutionRecordDto {
  id: string
  entryType: EntryType
  amount: number
  currencyId: string
  exchangeRate: number | null
  exchangeRateTo: number | null
  accountId: string | null
  paymentMethodId: string | null
  note: string | null
  createdAt: string // ISO 8601
  updatedAt: string | null
  deletedAt: string | null
}

export interface CreateExecutionRequest {
  entryType: EntryType
  amount: number
  currencyId: string
  exchangeRate?: number | null
  exchangeRateTo?: number | null
  note?: string | null
  accountId?: string | null
  paymentMethodId?: string | null
}

// UpdateExecutionRequest has the same shape as CreateExecutionRequest
export type UpdateExecutionRequest = CreateExecutionRequest

// ---------------------------------------------------------------------------
// Period execution totals
// ---------------------------------------------------------------------------

export interface LineTotalDto {
  budgetLineId: string
  budgetedAmount: number
  netExecuted: number
  variance: number
}

export interface CategoryTotalDto {
  categoryId: string
  categoryName: string
  categoryGroupId: string
  categoryGroupName: string
  budgetedAmount: number
  netExecuted: number
  variance: number
}

export interface PeriodTotalsDto {
  lineTotals: LineTotalDto[]
  categoryTotals: CategoryTotalDto[]
}

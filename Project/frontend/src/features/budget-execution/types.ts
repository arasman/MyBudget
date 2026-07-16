// ---------------------------------------------------------------------------
// Execution entry type
// ---------------------------------------------------------------------------

export const EntryType = {
  Expense:    1,
  CreditNote: 2,
  DebitNote:  3,
} as const

export type EntryType = typeof EntryType[keyof typeof EntryType]

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
  operationDate: string | null // YYYY-MM-DD or null
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
  operationDate?: string | null // YYYY-MM-DD
}

// UpdateExecutionRequest has the same shape as CreateExecutionRequest
export type UpdateExecutionRequest = CreateExecutionRequest

// ---------------------------------------------------------------------------
// Period execution totals
// ---------------------------------------------------------------------------

export interface LineTotalDto {
  budgetLineId: string
  budgetLineName: string
  totalExpenses: number
  totalCreditNotes: number
  totalDebitNotes: number
  netTotal: number
}

export interface CategoryTotalDto {
  categoryGroupId: string
  categoryGroupName: string
  categoryId: string | null
  categoryName: string | null
  totalExpenses: number
  totalCreditNotes: number
  totalDebitNotes: number
  netTotal: number
}

export interface PeriodTotalsDto {
  lineTotals: LineTotalDto[]
  categoryTotals: CategoryTotalDto[]
}

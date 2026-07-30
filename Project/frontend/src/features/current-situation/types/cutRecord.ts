export interface CutBankAccountDto {
  bankAccountId: string
  alias: string
  currencyId: string
  isPositive: boolean
  displayOrder: number
  balance: number
  balanceInPrimary: number
}

export interface BudgetExecutionSummaryDto {
  totalBudgeted: number
  totalRegistered: number
  remaining: number
}

export interface CutTotalsDto {
  totalPositive: number
  totalNegative: number
  totalDeudaEnCurso: number
  totalPositiveAlt: number
  totalNegativeAlt: number
  totalDeudaEnCursoAlt: number
}

export interface CutRecordResponse {
  isDraft: boolean
  cutRecordId: string | null
  cutDate: string
  exchangeRate: number
  projectionsJson: string | null
  primaryCurrencyId: string | null
  executionSummary: BudgetExecutionSummaryDto
  accounts: CutBankAccountDto[]
  totals: CutTotalsDto
}

export interface UpsertCutBankAccountItem {
  bankAccountId: string
  balance: number
}

export interface UpsertCutRecordDto {
  exchangeRate: number
  projectionsJson?: string | null
  accounts: UpsertCutBankAccountItem[]
}

export interface BankAccount {
  id: string
  budgetId: string
  currencyId: string
  alias: string
  isPositive: boolean
  displayOrder: number
  deletedAt: string | null
  createdAt: string
  updatedAt: string
}

export interface CreateBankAccountDto {
  alias: string
  currencyId: string
  isPositive: boolean
  displayOrder: number
}

export interface UpdateBankAccountDto {
  alias: string
  isPositive: boolean
  displayOrder: number
}

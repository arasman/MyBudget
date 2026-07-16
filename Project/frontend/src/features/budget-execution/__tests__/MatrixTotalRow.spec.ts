import { describe, it, expect, beforeEach } from 'vitest'
import { render } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import MatrixTotalRow from '../components/MatrixTotalRow.vue'
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import type { PeriodSummary } from '@/features/budget-structure/types'

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en: {} } })

const period1: PeriodSummary = {
  id: 'p1',
  name: 'January',
  periodNumber: 1,
  startDate: '2025-01-01' as never,
  endDate: '2025-01-31' as never,
  isClosed: false,
}

// Budget lines fixture:
// - Expense: cat-1 (budgeted 1000)
// - PreventiveSavings: cat-2 (budgeted 200)
// - LongTermSavings: cat-3 (budgeted 300)
const budgetLines = [
  { id: 'l1', name: 'Food', lineType: 'Expense' as const, isRecurring: false, categoryGroupId: 'g1', categoryId: 'cat-1', budgetedAmount: 1000, deletedAt: null },
  { id: 'l2', name: 'Preventive', lineType: 'PreventiveSavings' as const, isRecurring: false, categoryGroupId: 'g2', categoryId: 'cat-2', budgetedAmount: 200, deletedAt: null },
  { id: 'l3', name: 'LongTerm', lineType: 'LongTermSavings' as const, isRecurring: false, categoryGroupId: 'g3', categoryId: 'cat-3', budgetedAmount: 300, deletedAt: null },
]

const categoryTotals = [
  { categoryGroupId: 'g1', categoryGroupName: 'G1', categoryId: 'cat-1', categoryName: 'Food', totalExpenses: 800, totalCreditNotes: 0, totalDebitNotes: 0, netTotal: 800 },
  { categoryGroupId: 'g2', categoryGroupName: 'G2', categoryId: 'cat-2', categoryName: 'Preventive', totalExpenses: 150, totalCreditNotes: 0, totalDebitNotes: 0, netTotal: 150 },
  { categoryGroupId: 'g3', categoryGroupName: 'G3', categoryId: 'cat-3', categoryName: 'LongTerm', totalExpenses: 250, totalCreditNotes: 0, totalDebitNotes: 0, netTotal: 250 },
]

function renderRow() {
  const pinia = createPinia()
  setActivePinia(pinia)

  const matrixStore = useBudgetMatrixStore()
  const structureStore = useBudgetStructureStore()

  matrixStore.$patch({
    periodTotals: { p1: { lineTotals: [], categoryTotals } },
    displayCurrency: 'default',
    exchangeRate: null,
  })

  structureStore.$patch({
    budgetLines,
    currentCycle: {
      id: 'cycle-1',
      name: 'Test',
      startDate: '2025-01-01' as never,
      endDate: '2025-12-31' as never,
      isActive: true,
      defaultCurrency: { id: 'gtq', code: 'GTQ', symbol: 'Q' },
      alternateCurrency: { id: 'usd', code: 'USD', symbol: '$' },
    } as never,
  })

  return render(MatrixTotalRow, {
    props: { label: 'Total', visiblePeriods: [period1] },
    global: { plugins: [pinia, i18n] },
  })
}

describe('MatrixTotalRow', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('total budgeted = Expense.budgeted + PreventiveSavings.budgeted + LongTermSavings.budgeted', () => {
    const { getAllByText } = renderRow()
    // 1000 + 200 + 300 = 1500 — displayed by formatAmount with default currency (no conversion)
    // The symbol is 'Q', so it renders as "Q 1,500.00"
    const matches = getAllByText((_, element) => {
      return element?.textContent?.replace(/\s+/g, ' ').trim() === 'Q 1,500.00'
    })
    expect(matches.length).toBeGreaterThanOrEqual(1)
  })

  it('total executed = Expense.executed + PreventiveSavings.executed + LongTermSavings.executed', () => {
    const { getAllByText } = renderRow()
    // 800 + 150 + 250 = 1200
    const matches = getAllByText((_, element) => {
      return element?.textContent?.replace(/\s+/g, ' ').trim() === 'Q 1,200.00'
    })
    expect(matches.length).toBeGreaterThanOrEqual(1)
  })

  it('renders the label in the sticky header cell', () => {
    const { getByText } = renderRow()
    expect(getByText('Total')).not.toBeNull()
  })
})

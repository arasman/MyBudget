import { describe, it, expect, beforeEach } from 'vitest'
import { render } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import MatrixSummaryRow from '../components/MatrixSummaryRow.vue'
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import type { PeriodSummary } from '@/features/budget-structure/types'

// ---------------------------------------------------------------------------
// i18n stub
// ---------------------------------------------------------------------------

const i18n = createI18n({
  legacy: false,
  locale: 'en',
  messages: { en: {} },
})

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const period1: PeriodSummary = {
  id: 'p1',
  name: 'January',
  periodNumber: 1,
  startDate: '2025-01-01' as never,
  endDate: '2025-01-31' as never,
  isClosed: false,
}

// CategoryTotalDto shapes for period p1
const categoryTotals = [
  {
    categoryGroupId: 'grp-1',
    categoryGroupName: 'Expenses',
    categoryId: 'cat-expense',
    categoryName: 'Food',
    totalExpenses: 450,
    totalCreditNotes: 0,
    totalDebitNotes: 0,
    netTotal: 450,
  },
  {
    categoryGroupId: 'grp-2',
    categoryGroupName: 'Savings',
    categoryId: 'cat-savings',
    categoryName: 'Emergency Fund',
    totalExpenses: 180,
    totalCreditNotes: 0,
    totalDebitNotes: 0,
    netTotal: 180,
  },
]

// BudgetLineResponse shapes — lineType tells us which category maps to which type
const budgetLines = [
  {
    id: 'line-1',
    name: 'Groceries',
    lineType: 'Expense' as const,
    isRecurring: false,
    categoryGroupId: 'grp-1',
    categoryId: 'cat-expense',
    budgetedAmount: 500,
  },
  {
    id: 'line-2',
    name: 'Emergency Fund',
    lineType: 'LongTermSavings' as const,
    isRecurring: false,
    categoryGroupId: 'grp-2',
    categoryId: 'cat-savings',
    budgetedAmount: 200,
  },
]

// ---------------------------------------------------------------------------
// Helper
// ---------------------------------------------------------------------------

function renderRow(lineType: number, label: string, displayCurrency: 'default' | 'alternate' = 'default') {
  const pinia = createPinia()
  setActivePinia(pinia)

  const matrixStore = useBudgetMatrixStore()
  const structureStore = useBudgetStructureStore()

  // Seed matrix store with period totals
  matrixStore.$patch({
    periodTotals: {
      p1: {
        lineTotals: [],
        categoryTotals,
      },
    },
    displayCurrency,
    exchangeRate: displayCurrency === 'alternate' ? 7.5 : null,
  })

  // Seed structure store with budget lines and cycle currencies
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

  return render(MatrixSummaryRow, {
    props: {
      lineType,
      label,
      visiblePeriods: [period1],
    },
    global: {
      plugins: [pinia, i18n],
    },
  })
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('MatrixSummaryRow', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('applies error color class for Expense row (lineType=1)', () => {
    const { container } = renderRow(1, 'Total Expenses')
    const tr = container.querySelector('tr')
    expect(tr?.className).toContain('text-error')
    expect(tr?.className).toContain('bg-error/10')
  })

  it('applies success color class for LongTermSavings row (lineType=2)', () => {
    const { container } = renderRow(2, 'Total Long-term Savings')
    const tr = container.querySelector('tr')
    expect(tr?.className).toContain('text-success')
    expect(tr?.className).toContain('bg-success/10')
  })

  it('applies warning color class for PreventiveSavings row (lineType=3)', () => {
    const { container } = renderRow(3, 'Total Preventive Savings')
    const tr = container.querySelector('tr')
    expect(tr?.className).toContain('text-warning')
    expect(tr?.className).toContain('bg-warning/10')
  })

  it('shows correct executed total for Expense categories', () => {
    const { getByText } = renderRow(1, 'Total Expenses')
    // cat-expense has netTotal=450; formatAmount with symbol 'Q' → 'Q 450.00'
    expect(getByText('Q 450.00')).not.toBeNull()
  })

  it('shows correct totals for LongTermSavings categories', () => {
    const { getByText } = renderRow(2, 'Total Long-term Savings')
    // cat-savings has netTotal=180; formatAmount with symbol 'Q' → 'Q 180.00'
    expect(getByText('Q 180.00')).not.toBeNull()
  })

  it('displays zero amounts as "Q 0.00" when no matching categories have data', () => {
    const { getAllByText } = renderRow(3, 'Total Preventive Savings')
    // lineType=3 (PreventiveSavings) — no budget lines of that type in fixture
    const zeroElements = getAllByText('Q 0.00')
    // 2 columns (budgeted + executed) × 1 period = 2 zeros
    expect(zeroElements.length).toBeGreaterThanOrEqual(2)
  })

  it('shows alternate currency symbol when displayCurrency = "alternate"', () => {
    // 450 GTQ / 7.5 = 60.00 USD → displayed as '$ 60.00'
    const { getByText } = renderRow(1, 'Total Expenses', 'alternate')
    expect(getByText('$ 60.00')).not.toBeNull()
  })

  it('renders the label in the sticky header cell', () => {
    const { getByText } = renderRow(1, 'Total Expenses')
    expect(getByText('Total Expenses')).not.toBeNull()
  })
})

import { useBudgetMatrixStore } from '../store'

/**
 * Currency conversion and formatting utilities.
 * Reads displayCurrency and exchangeRate from the matrix store.
 * Pure client-side conversion — no API call (AD-4).
 */
export function useCurrencyDisplay(store: ReturnType<typeof useBudgetMatrixStore>) {
  /**
   * Convert an amount according to the current display currency mode.
   * When mode is 'alternate' and exchangeRate is truthy:
   *   result = Math.round((amount / exchangeRate) * 100) / 100
   * When mode is 'default' or exchangeRate is null: returns amount unchanged.
   */
  function convert(amount: number): number {
    if (store.displayCurrency === 'alternate' && store.exchangeRate) {
      return Math.round((amount / store.exchangeRate) * 100) / 100
    }
    return amount
  }

  /**
   * Format an amount with a currency symbol using en-US locale.
   * Applies conversion based on current store display mode.
   */
  function formatAmount(amount: number, symbol: string): string {
    const converted = convert(amount)
    return `${symbol} ${converted.toLocaleString('en-US', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })}`
  }

  return { convert, formatAmount }
}

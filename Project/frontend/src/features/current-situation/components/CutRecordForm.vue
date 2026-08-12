<template>
  <div class="card bg-base-200 p-4">
    <!-- Date + exchange rate row -->
    <div
      v-if="isDraft"
      class="badge badge-warning mb-3"
    >
      {{ t('currentSituation.draft') }}
    </div>
    <div class="flex flex-col sm:flex-row sm:items-end gap-4 mb-4">
      <div class="flex flex-col gap-1">
        <span class="label-text text-sm">{{ t('currentSituation.form.cutDate') }}</span>
        <input
          v-model="localDate"
          type="date"
          class="input input-bordered input-sm w-full sm:w-40"
          @change="emit('date-change', localDate)"
        >
      </div>
      <div class="flex flex-col gap-1">
        <span class="label-text text-sm">{{ t('currentSituation.form.exchangeRate') }}</span>
        <input
          v-model.number="localExchangeRate"
          type="number"
          class="input input-bordered input-sm w-full sm:w-40"
          min="0.000001"
          step="0.000001"
          :placeholder="t('currentSituation.form.exchangeRatePlaceholder')"
        >
      </div>
    </div>

    <!-- Unified accounts list -->
    <div
      v-if="positiveAccounts.length > 0 || negativeAccounts.length > 0"
      class="mb-4 flex flex-col gap-1"
    >
      <!-- Assets section -->
      <template v-if="positiveAccounts.length > 0">
        <div class="text-xs font-semibold text-success bg-success/5 py-1 px-2 rounded">
          {{ t('currentSituation.form.positiveAccounts') }}
        </div>
        <div
          v-for="acc in positiveAccounts"
          :key="acc.bankAccountId"
          class="grid grid-cols-1 sm:grid-cols-[2fr_1fr] gap-y-1 sm:gap-x-3 sm:items-center py-2 border-b border-base-300"
        >
          <span class="text-sm">{{ acc.alias }}</span>
          <label class="input input-bordered input-xs flex items-center gap-1 w-full">
            <span class="text-xs text-base-content/50 shrink-0">{{ getCurrencyCode(acc.currencyId) }}</span>
            <input
              :value="formatBalanceDisplay(acc.bankAccountId)"
              type="text"
              inputmode="decimal"
              class="grow text-right bg-transparent outline-none min-w-0"
              @focus="onBalanceFocus(acc.bankAccountId, $event)"
              @blur="onBalanceBlur(acc.bankAccountId, $event)"
            >
          </label>
        </div>
      </template>

      <!-- Liabilities section -->
      <template v-if="negativeAccounts.length > 0">
        <div class="text-xs font-semibold text-error bg-error/5 py-1 px-2 rounded mt-2">
          {{ t('currentSituation.form.negativeAccounts') }}
        </div>
        <div
          v-for="acc in negativeAccounts"
          :key="acc.bankAccountId"
          class="grid grid-cols-1 sm:grid-cols-[2fr_1fr] gap-y-1 sm:gap-x-3 sm:items-center py-2 border-b border-base-300"
        >
          <span class="text-sm">{{ acc.alias }}</span>
          <label class="input input-bordered input-xs flex items-center gap-1 w-full">
            <span class="text-xs text-base-content/50 shrink-0">{{ getCurrencyCode(acc.currencyId) }}</span>
            <input
              :value="formatBalanceDisplay(acc.bankAccountId)"
              type="text"
              inputmode="decimal"
              class="grow text-right bg-transparent outline-none min-w-0"
              @focus="onBalanceFocus(acc.bankAccountId, $event)"
              @blur="onBalanceBlur(acc.bankAccountId, $event)"
            >
          </label>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CutBankAccountDto, CutTotalsDto } from '../types/cutRecord'
import type { CurrencyItem } from '@/features/budget-structure/types'

const props = defineProps<{
  accounts: CutBankAccountDto[]
  exchangeRate: number
  isDraft: boolean
  currencies: CurrencyItem[]
  remaining: number
  primaryCurrencyId: string | null
  cutDate: string
}>()

const emit = defineEmits<{
  save: [payload: { exchangeRate: number; accounts: { bankAccountId: string; balance: number }[] }]
  'update:liveTotals': [totals: CutTotalsDto]
  'update:liveExchangeRate': [rate: number]
  'date-change': [date: string]
}>()

const { t } = useI18n()

const localDate = ref(props.cutDate)
const localExchangeRate = ref(props.exchangeRate)

watch(
  () => props.cutDate,
  (d) => {
    localDate.value = d
  },
)

const balances = ref<Record<string, number>>({})

watch(
  () => props.accounts,
  (accounts) => {
    const map: Record<string, number> = {}
    for (const acc of accounts) {
      map[acc.bankAccountId] = acc.balance
    }
    balances.value = map
  },
  { immediate: true },
)

watch(
  () => props.exchangeRate,
  (rate) => {
    localExchangeRate.value = rate
  },
)

watch(
  localExchangeRate,
  (rate) => {
    const safeRate = Number.isFinite(rate) && rate > 0 ? rate : 1
    emit('update:liveExchangeRate', safeRate)
  },
  { immediate: true },
)

const positiveAccounts = computed(() =>
  props.accounts.filter((a) => a.isPositive).sort((a, b) => a.displayOrder - b.displayOrder),
)

const negativeAccounts = computed(() =>
  props.accounts.filter((a) => !a.isPositive).sort((a, b) => a.displayOrder - b.displayOrder),
)

function getCurrencyCode(currencyId: string): string {
  return props.currencies.find((c) => c.id === currencyId)?.code ?? currencyId.slice(0, 8)
}

function safeBalance(id: string): number {
  const val = balances.value[id]
  return Number.isFinite(val) ? val : 0
}

function formatBalanceDisplay(id: string): string {
  return safeBalance(id).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function onBalanceFocus(id: string, e: FocusEvent): void {
  const input = e.target as HTMLInputElement
  const val = safeBalance(id)
  input.value = val === 0 ? '' : String(val)
  input.select()
}

function onBalanceBlur(id: string, e: FocusEvent): void {
  const raw = (e.target as HTMLInputElement).value.replace(/,/g, '').trim()
  const parsed = parseFloat(raw)
  balances.value[id] = Number.isFinite(parsed) ? parsed : 0
}

function toBalanceInPrimary(acc: CutBankAccountDto, balance: number): number {
  if (!props.primaryCurrencyId || acc.currencyId === props.primaryCurrencyId) return balance
  const er = localExchangeRate.value > 0 ? localExchangeRate.value : 1
  return balance * er
}

const liveTotals = computed<CutTotalsDto>(() => {
  const er = localExchangeRate.value > 0 ? localExchangeRate.value : 1
  let totalPositive = 0
  let totalNegative = 0

  for (const acc of props.accounts) {
    const balance = safeBalance(acc.bankAccountId)
    const bip = toBalanceInPrimary(acc, balance)
    if (acc.isPositive) totalPositive += bip
    else totalNegative += bip
  }

  const totalDeudaEnCurso = props.remaining + totalNegative

  return {
    totalPositive,
    totalNegative,
    totalDeudaEnCurso,
    totalPositiveAlt: totalPositive / er,
    totalNegativeAlt: totalNegative / er,
    totalDeudaEnCursoAlt: totalDeudaEnCurso / er,
  }
})

watch(liveTotals, (totals) => {
  emit('update:liveTotals', totals)
}, { immediate: true })

function handleSave(): void {
  emit('save', {
    exchangeRate: localExchangeRate.value,
    accounts: Object.entries(balances.value).map(([bankAccountId, balance]) => ({
      bankAccountId,
      balance: Number.isFinite(balance) ? balance : 0,
    })),
  })
}

defineExpose({ triggerSave: handleSave })
</script>

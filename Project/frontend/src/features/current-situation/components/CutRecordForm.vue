<template>
  <div class="card bg-base-200 p-4">
    <!-- Date + exchange rate row -->
    <div class="flex items-end gap-4 mb-4">
      <div class="form-control">
        <label class="label">
          <span class="label-text">{{ t('currentSituation.form.cutDate') }}</span>
        </label>
        <input
          v-model="localDate"
          type="date"
          class="input input-bordered input-sm w-40"
          @change="emit('date-change', localDate)"
        />
      </div>
      <div class="form-control">
        <label class="label">
          <span class="label-text">{{ t('currentSituation.form.exchangeRate') }}</span>
        </label>
        <input
          v-model.number="localExchangeRate"
          type="number"
          class="input input-bordered input-sm w-40"
          min="0.000001"
          step="0.000001"
          :placeholder="t('currentSituation.form.exchangeRatePlaceholder')"
        />
      </div>
      <div v-if="isDraft" class="badge badge-warning mb-2">
        {{ t('currentSituation.draft') }}
      </div>
    </div>

    <!-- Unified accounts table -->
    <div v-if="positiveAccounts.length > 0 || negativeAccounts.length > 0" class="overflow-x-auto mb-4">
      <table class="table table-sm w-full">
        <colgroup>
          <col class="w-1/2" />
          <col class="w-24" />
          <col class="w-36" />
        </colgroup>
        <thead>
          <tr>
            <th>{{ t('currentSituation.form.accountAlias') }}</th>
            <th>{{ t('currentSituation.form.currency') }}</th>
            <th>{{ t('currentSituation.form.balance') }}</th>
          </tr>
        </thead>
        <tbody>
          <!-- Assets section -->
          <tr v-if="positiveAccounts.length > 0">
            <td colspan="3" class="text-xs font-semibold text-success bg-success/5 py-1 px-2">
              {{ t('currentSituation.form.positiveAccounts') }}
            </td>
          </tr>
          <tr v-for="acc in positiveAccounts" :key="acc.bankAccountId">
            <td>{{ acc.alias }}</td>
            <td>
              <span class="badge badge-success badge-sm">{{ getCurrencyCode(acc.currencyId) }}</span>
            </td>
            <td>
              <input
                v-model.number="balances[acc.bankAccountId]"
                type="number"
                class="input input-bordered input-xs w-32"
                min="0"
                step="0.01"
              />
            </td>
          </tr>
          <!-- Liabilities section -->
          <tr v-if="negativeAccounts.length > 0">
            <td colspan="3" class="text-xs font-semibold text-error bg-error/5 py-1 px-2">
              {{ t('currentSituation.form.negativeAccounts') }}
            </td>
          </tr>
          <tr v-for="acc in negativeAccounts" :key="acc.bankAccountId">
            <td>{{ acc.alias }}</td>
            <td>
              <span class="badge badge-error badge-sm">{{ getCurrencyCode(acc.currencyId) }}</span>
            </td>
            <td>
              <input
                v-model.number="balances[acc.bankAccountId]"
                type="number"
                class="input input-bordered input-xs w-32"
                min="0"
                step="0.01"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Save error -->
    <div v-if="saveError" class="alert alert-error alert-sm mb-3 text-sm">
      {{
        saveError === 'noActivePeriod'
          ? t('currentSituation.errors.noActivePeriod')
          : saveError
      }}
    </div>

    <div class="flex justify-end">
      <button class="btn btn-primary btn-sm" :disabled="saveLoading" @click="handleSave">
        <span v-if="saveLoading" class="loading loading-spinner loading-xs"></span>
        {{ t('common.save') }}
      </button>
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
  saveLoading: boolean
  saveError: string | null
  remaining: number
  primaryCurrencyId: string | null
  cutDate: string
}>()

const emit = defineEmits<{
  save: [payload: { exchangeRate: number; accounts: { bankAccountId: string; balance: number }[] }]
  'update:liveTotals': [totals: CutTotalsDto]
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

const positiveAccounts = computed(() =>
  props.accounts.filter((a) => a.isPositive).sort((a, b) => a.displayOrder - b.displayOrder),
)

const negativeAccounts = computed(() =>
  props.accounts.filter((a) => !a.isPositive).sort((a, b) => a.displayOrder - b.displayOrder),
)

function getCurrencyCode(currencyId: string): string {
  return props.currencies.find((c) => c.id === currencyId)?.code ?? currencyId.slice(0, 8)
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
    const balance = balances.value[acc.bankAccountId] ?? 0
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
      balance,
    })),
  })
}
</script>

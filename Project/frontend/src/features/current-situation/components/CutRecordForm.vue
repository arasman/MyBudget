<template>
  <div class="card bg-base-200 p-4">
    <!-- Header: exchange rate + draft badge -->
    <div class="flex items-center gap-4 mb-4">
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

      <div v-if="isDraft" class="badge badge-warning gap-1 self-end mb-2">
        {{ t('currentSituation.draft') }}
      </div>
    </div>

    <!-- Account rows -->
    <div v-if="positiveAccounts.length > 0" class="mb-4">
      <h4 class="text-sm font-semibold mb-2 text-success">
        {{ t('currentSituation.form.positiveAccounts') }}
      </h4>
      <div class="overflow-x-auto">
        <table class="table table-sm">
          <thead>
            <tr>
              <th>{{ t('currentSituation.form.accountAlias') }}</th>
              <th>{{ t('currentSituation.form.currency') }}</th>
              <th>{{ t('currentSituation.form.balance') }}</th>
            </tr>
          </thead>
          <tbody>
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
          </tbody>
        </table>
      </div>
    </div>

    <div v-if="negativeAccounts.length > 0" class="mb-4">
      <h4 class="text-sm font-semibold mb-2 text-error">
        {{ t('currentSituation.form.negativeAccounts') }}
      </h4>
      <div class="overflow-x-auto">
        <table class="table table-sm">
          <thead>
            <tr>
              <th>{{ t('currentSituation.form.accountAlias') }}</th>
              <th>{{ t('currentSituation.form.currency') }}</th>
              <th>{{ t('currentSituation.form.balance') }}</th>
            </tr>
          </thead>
          <tbody>
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
import type { CutBankAccountDto } from '../types/cutRecord'
import type { CurrencyItem } from '@/features/budget-structure/types'

const props = defineProps<{
  accounts: CutBankAccountDto[]
  exchangeRate: number
  isDraft: boolean
  currencies: CurrencyItem[]
  saveLoading: boolean
  saveError: string | null
}>()

const emit = defineEmits<{
  save: [payload: { exchangeRate: number; accounts: { bankAccountId: string; balance: number }[] }]
}>()

const { t } = useI18n()

const localExchangeRate = ref(props.exchangeRate)
const balances = ref<Record<string, number>>({})

// Initialize balances from accounts
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

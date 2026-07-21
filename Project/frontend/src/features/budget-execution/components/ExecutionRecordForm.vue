<template>
  <form data-testid="execution-record-form" class="space-y-3" novalidate @submit.prevent="handleSubmit">
    <!-- Operation date -->
    <div class="form-control">
      <label class="label" for="exec-operation-date">
        <span class="label-text">{{ t('budgetExecution.form.operationDate') }}</span>
      </label>
      <input
        id="exec-operation-date"
        v-model="form.operationDate"
        type="date"
        class="input input-bordered input-sm w-full"
        :class="{ 'input-error': errors.operationDate }"
      />
      <span v-if="errors.operationDate" class="label-text-alt text-error mt-1">{{ errors.operationDate }}</span>
    </div>

    <!-- Entry type -->
    <div class="form-control">
      <label class="label" for="exec-entry-type">
        <span class="label-text">{{ t('budgetExecution.form.entryType') }} *</span>
      </label>
      <select
        id="exec-entry-type"
        data-testid="entry-type-select"
        v-model.number="form.entryType"
        class="select select-bordered select-sm w-full"
      >
        <option :value="EntryType.Expense">{{ t('budgetExecution.form.entryTypes.expense') }}</option>
        <option :value="EntryType.CreditNote">{{ t('budgetExecution.form.entryTypes.creditNote') }}</option>
        <option :value="EntryType.DebitNote">{{ t('budgetExecution.form.entryTypes.debitNote') }}</option>
      </select>
    </div>

    <!-- Currency -->
    <div class="form-control">
      <label class="label" for="exec-currency">
        <span class="label-text">{{ t('budgetExecution.form.currency') }}</span>
      </label>
      <select id="exec-currency" v-model="form.currencyId" class="select select-bordered select-sm w-full">
        <option
          v-for="currency in availableCurrencies"
          :key="currency.id"
          :value="currency.id"
        >
          {{ currency.code }} — {{ currency.name ?? currency.symbol }}
        </option>
      </select>
    </div>

    <!-- Amount -->
    <div class="form-control">
      <label class="label" for="exec-amount">
        <span class="label-text">{{ t('budgetExecution.form.amount') }} *</span>
      </label>
      <input
        id="exec-amount"
        data-testid="amount-input"
        v-model.number="form.amount"
        type="number"
        step="0.01"
        min="0.01"
        class="input input-bordered input-sm w-full"
        :class="{ 'input-error': errors.amount }"
      />
      <span v-if="errors.amount" class="label-text-alt text-error mt-1">{{ errors.amount }}</span>
    </div>

    <!-- Exchange rate (only when currency differs from default) -->
    <div v-if="showExchangeRate" class="form-control">
      <label class="label" for="exec-exchange-rate">
        <span class="label-text">{{ t('budgetExecution.form.exchangeRate') }} *</span>
      </label>
      <input
        id="exec-exchange-rate"
        v-model.number="form.exchangeRate"
        type="number"
        step="0.000001"
        min="0.000001"
        class="input input-bordered input-sm w-full"
        :class="{ 'input-error': errors.exchangeRate }"
      />
      <span v-if="errors.exchangeRate" class="label-text-alt text-error mt-1">{{ errors.exchangeRate }}</span>
    </div>

    <!-- Calculated amount (read-only, shown when exchange rate is set) -->
    <div v-if="showExchangeRate && form.amount && form.exchangeRate" class="form-control">
      <label class="label">
        <span class="label-text text-base-content/60">{{ t('budgetExecution.form.calculatedAmount') }}</span>
      </label>
      <input
        type="text"
        class="input input-bordered input-sm w-full bg-base-200"
        :value="calculatedAmount"
        readonly
        tabindex="-1"
      />
    </div>

    <!-- Note (always required) -->
    <div class="form-control">
      <label class="label" for="exec-note">
        <span class="label-text">{{ t('budgetExecution.form.note') }} *</span>
      </label>
      <input
        id="exec-note"
        v-model="form.note"
        type="text"
        maxlength="500"
        class="input input-bordered input-sm w-full"
        :class="{ 'input-error': errors.note }"
      />
      <span v-if="errors.note" data-testid="note-error" class="label-text-alt text-error mt-1">{{ errors.note }}</span>
    </div>

    <!-- Actions -->
    <div class="flex gap-2 justify-end pt-1">
      <button
        v-if="editRecord"
        type="button"
        class="btn btn-ghost btn-sm"
        @click="$emit('cancelled')"
      >
        {{ t('budgetExecution.form.cancel') }}
      </button>
      <button type="submit" data-testid="execution-form-submit" class="btn btn-primary btn-sm" :disabled="submitting">
        <span v-if="submitting" class="loading loading-spinner loading-xs" />
        {{ t('budgetExecution.form.save') }}
      </button>
    </div>
  </form>
</template>

<script setup lang="ts">
import { reactive, ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { EntryType } from '../types'
import type { CurrencyItem } from '@/features/budget-structure/types'
import type { ExecutionRecordDto } from '../types'
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import { useToastStore } from '@/stores/toast.store'
import { extractApiErrorCode } from '@/features/budget-structure/utils/apiError'

const props = defineProps<{
  budgetId: string
  periodId: string
  lineId: string
  editRecord?: ExecutionRecordDto
}>()

const emit = defineEmits<{
  saved: []
  cancelled: []
}>()

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()
const structureStore = useBudgetStructureStore()
const toastStore = useToastStore()

const submitting = ref(false)

/** Returns today as YYYY-MM-DD without timezone distortion. */
function todayString(): string {
  const now = new Date()
  const y = now.getFullYear()
  const m = String(now.getMonth() + 1).padStart(2, '0')
  const d = String(now.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

/** Currencies available from the current cycle (default + optional alternate). */
const availableCurrencies = computed((): CurrencyItem[] => {
  const cycle = structureStore.currentCycle
  if (!cycle) return []
  const currencies: CurrencyItem[] = []
  if (cycle.defaultCurrency) currencies.push(cycle.defaultCurrency)
  if (cycle.alternateCurrency) currencies.push(cycle.alternateCurrency)
  return currencies
})

const defaultCurrencyId = computed(() => structureStore.currentCycle?.defaultCurrency?.id ?? '')

/** Show exchange rate field only when the selected currency differs from the cycle default. */
const showExchangeRate = computed(
  () => form.currencyId && form.currencyId !== defaultCurrencyId.value,
)

/** Calculated amount: amount × exchangeRate, formatted for display. */
const calculatedAmount = computed(() => {
  if (!form.amount || !form.exchangeRate) return ''
  const result = form.amount * form.exchangeRate
  const defaultCurrency = structureStore.currentCycle?.defaultCurrency
  const prefix = defaultCurrency ? `${defaultCurrency.code} ` : ''
  return `${prefix}${result.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
})

const form = reactive({
  entryType: props.editRecord?.entryType ?? EntryType.Expense,
  amount: props.editRecord?.amount ?? (null as number | null),
  note: props.editRecord?.note ?? '',
  operationDate: props.editRecord?.operationDate ?? todayString(),
  currencyId: props.editRecord?.currencyId ?? defaultCurrencyId.value,
  exchangeRate: props.editRecord?.exchangeRate ?? (null as number | null),
  exchangeRateTo: props.editRecord?.exchangeRateTo ?? (null as number | null),
})

const errors = reactive({
  amount: '' as string,
  note: '' as string,
  exchangeRate: '' as string,
  operationDate: '' as string,
})

/** Checks that a number has at most maxDecimals decimal places. */
function hasMaxDecimals(value: number, maxDecimals: number): boolean {
  const str = value.toString()
  const dotIndex = str.indexOf('.')
  if (dotIndex === -1) return true
  return str.length - dotIndex - 1 <= maxDecimals
}

function validate(): boolean {
  errors.amount = ''
  errors.note = ''
  errors.exchangeRate = ''
  errors.operationDate = ''

  let valid = true

  if (!form.amount || form.amount <= 0) {
    errors.amount = t('budgetExecution.form.validation.amountRequired')
    valid = false
  } else if (!hasMaxDecimals(form.amount, 2)) {
    errors.amount = t('budgetExecution.form.validation.amountDecimals')
    valid = false
  }

  if (showExchangeRate.value) {
    if (!form.exchangeRate || form.exchangeRate <= 0) {
      errors.exchangeRate = t('budgetExecution.form.validation.exchangeRateRequired')
      valid = false
    } else if (!hasMaxDecimals(form.exchangeRate, 6)) {
      errors.exchangeRate = t('budgetExecution.form.validation.exchangeRateDecimals')
      valid = false
    }
  }

  if (!form.note?.trim()) {
    errors.note = t('budgetExecution.form.validation.noteRequiredAlways')
    valid = false
  }

  if (!form.operationDate) {
    errors.operationDate = t('budgetExecution.form.validation.operationDateRequired')
    valid = false
  }

  // Best-effort period-range check (requires currentCycle + period context not available here;
  // server is authoritative — client sends a toast on OPERATION_DATE_OUT_OF_RANGE API error)

  return valid
}

async function handleSubmit(): Promise<void> {
  if (!validate()) return

  submitting.value = true

  const currencyId = form.currencyId || defaultCurrencyId.value
  const isSameCurrency = currencyId === defaultCurrencyId.value

  // Backend requires both ExchangeRate and ExchangeRateTo when currency differs.
  // ExchangeRateTo is the inverse rate (1 / exchangeRate).
  const exchangeRate = isSameCurrency ? null : (form.exchangeRate ?? null)
  const exchangeRateTo =
    isSameCurrency || !form.exchangeRate || form.exchangeRate === 0
      ? null
      : parseFloat((1 / form.exchangeRate).toFixed(6))

  const payload = {
    entryType: form.entryType,
    amount: form.amount!,
    currencyId,
    note: form.note?.trim() || null,
    exchangeRate,
    exchangeRateTo,
    operationDate: form.operationDate || null,
  }

  try {
    if (props.editRecord) {
      await matrixStore.updateExecution(
        props.budgetId,
        props.periodId,
        props.lineId,
        props.editRecord.id,
        payload,
      )
      toastStore.push({ type: 'success', title: t('budgetExecution.record.updateSuccess') })
    } else {
      await matrixStore.createExecution(props.budgetId, props.periodId, props.lineId, payload)
      toastStore.push({ type: 'success', title: t('budgetExecution.record.createSuccess') })
    }
    emit('saved')

    // Reset form after successful create (not edit — parent closes on saved)
    if (!props.editRecord) {
      form.entryType = EntryType.Expense
      form.amount = null
      form.note = ''
      form.operationDate = todayString()
      form.currencyId = defaultCurrencyId.value
      form.exchangeRate = null
      form.exchangeRateTo = null
    }
  } catch (e) {
    const code = extractApiErrorCode(e)
    if (code === 'OPERATION_DATE_OUT_OF_RANGE') {
      toastStore.push({ type: 'error', title: t('budgetExecution.form.errors.operationDateOutOfRange') })
    } else {
      toastStore.push({ type: 'error', title: t('common.errors.serverError') })
    }
  } finally {
    submitting.value = false
  }
}
</script>

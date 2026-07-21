<template>
  <dialog class="modal modal-open">
    <div class="modal-box w-full max-w-md">
      <h3 class="font-bold text-lg mb-4">
        {{ modelValue ? t('budgetStructure.cycles.edit') : t('budgetStructure.cycles.create') }}
      </h3>

      <form @submit.prevent="handleSubmit">
        <!-- Name -->
        <div class="form-control mb-4">
          <label class="label" for="cycle-name">
            <span class="label-text">{{ t('budgetStructure.cycles.name') }}</span>
          </label>
          <input
            id="cycle-name"
            v-model="form.name"
            type="text"
            class="input input-bordered w-full"
            :class="{ 'input-error': errors.name }"
            maxlength="200"
            required
          />
          <div v-if="errors.name" class="label">
            <span class="label-text-alt text-error">{{ errors.name }}</span>
          </div>
        </div>

        <!-- Start Date -->
        <div class="form-control mb-4">
          <label class="label" for="cycle-start">
            <span class="label-text">{{ t('budgetStructure.cycles.startDate') }}</span>
          </label>
          <input
            id="cycle-start"
            v-model="form.startDate"
            type="date"
            class="input input-bordered w-full"
            :class="{ 'input-error': errors.startDate }"
            required
          />
          <div v-if="errors.startDate" class="label">
            <span class="label-text-alt text-error">{{ errors.startDate }}</span>
          </div>
        </div>

        <!-- End Date -->
        <div class="form-control mb-4">
          <label class="label" for="cycle-end">
            <span class="label-text">{{ t('budgetStructure.cycles.endDate') }}</span>
          </label>
          <input
            id="cycle-end"
            v-model="form.endDate"
            type="date"
            class="input input-bordered w-full"
            :class="{ 'input-error': errors.endDate }"
            required
          />
          <div v-if="errors.endDate" class="label">
            <span class="label-text-alt text-error">{{ errors.endDate }}</span>
          </div>
        </div>

        <!-- Default Currency -->
        <div class="form-control mb-4">
          <label class="label" for="cycle-currency">
            <span class="label-text">{{ t('budgetStructure.cycles.defaultCurrency') }}</span>
          </label>
          <select
            id="cycle-currency"
            v-model="form.defaultCurrencyId"
            class="select select-bordered w-full"
            required
          >
            <option v-for="c in currencies" :key="c.id" :value="c.id">
              {{ c.symbol }} {{ c.name }} ({{ c.code }})
            </option>
          </select>
        </div>

        <!-- Alternate Currency -->
        <div class="form-control mb-4">
          <label class="label" for="cycle-alt-currency">
            <span class="label-text">{{ t('budgetStructure.cycles.alternateCurrency') }}</span>
          </label>
          <select
            id="cycle-alt-currency"
            v-model="form.alternateCurrencyId"
            class="select select-bordered w-full"
          >
            <option value="">{{ t('budgetStructure.cycles.noneSelected') }}</option>
            <option v-for="c in currencies" :key="c.id" :value="c.id">
              {{ c.symbol }} {{ c.name }} ({{ c.code }})
            </option>
          </select>
        </div>

        <!-- Exchange Rate — only shown when alternate currency is selected -->
        <div v-if="form.alternateCurrencyId" class="form-control mb-4">
          <label class="label" for="cycle-exchange-rate">
            <span class="label-text">{{ exchangeRateLabel }}</span>
          </label>
          <input
            id="cycle-exchange-rate"
            v-model.number="form.exchangeRate"
            type="number"
            min="0.0001"
            step="0.0001"
            class="input input-bordered w-full"
            :class="{ 'input-error': errors.pairValidation }"
          />
        </div>

        <!-- Pair validation error -->
        <div v-if="errors.pairValidation" class="label mb-4">
          <span class="label-text-alt text-error">{{ errors.pairValidation }}</span>
        </div>

        <div class="modal-action">
          <button type="button" class="btn btn-ghost" @click="emit('cancel')">
            {{ t('budgetStructure.common.cancel') }}
          </button>
          <button type="submit" class="btn btn-primary">
            {{ t('budgetStructure.common.save') }}
          </button>
        </div>
      </form>
    </div>
    <div class="modal-backdrop" @click="emit('cancel')" />
  </dialog>
</template>

<script setup lang="ts">
import { reactive, watch, ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CycleListItem, CurrencyItem, DateString } from '../types'
import { listCurrencies } from '../api/currencies.api'

// GTQ seed ID — used as fallback default when no cycle is being edited
const GTQ_SEED_ID = '11111111-1111-1111-1111-111111111111'

interface CycleFormPayload {
  name: string
  startDate: DateString
  endDate: DateString
  defaultCurrencyId: string
  alternateCurrencyId?: string
  exchangeRate?: number
}

const props = defineProps<{
  modelValue: CycleListItem | null
  budgetId: string
}>()

const emit = defineEmits<{
  submit: [payload: CycleFormPayload]
  cancel: []
}>()

const { t } = useI18n()

const FALLBACK_CURRENCIES: CurrencyItem[] = [
  { id: '11111111-1111-1111-1111-111111111111', code: 'GTQ', name: 'Quetzal', symbol: 'Q' },
  { id: '22222222-2222-2222-2222-222222222222', code: 'USD', name: 'US Dollar', symbol: '$' },
  { id: '33333333-3333-3333-3333-333333333333', code: 'EUR', name: 'Euro', symbol: '€' },
]

const currencies = ref<CurrencyItem[]>([...FALLBACK_CURRENCIES])

const form = reactive({
  name: '',
  startDate: '',
  endDate: '',
  defaultCurrencyId: GTQ_SEED_ID,
  alternateCurrencyId: '' as string,
  exchangeRate: null as number | null,
})

const errors = reactive({
  name: '',
  startDate: '',
  endDate: '',
  pairValidation: '',
})

// Dynamic exchange rate label: "X GTQ per 1 USD"
const exchangeRateLabel = computed(() => {
  const defaultCurrency = currencies.value.find((c) => c.id === form.defaultCurrencyId)
  const alternateCurrency = currencies.value.find((c) => c.id === form.alternateCurrencyId)
  return t('budgetStructure.cycles.exchangeRateLabel', {
    defaultCurrency: defaultCurrency?.code ?? '',
    alternateCurrency: alternateCurrency?.code ?? '',
  })
})

onMounted(async () => {
  try {
    currencies.value = await listCurrencies(props.budgetId)
  } catch {
    // fallback: leave currencies empty, form still works with hardcoded default
  }
})

// Populate form when editing an existing cycle.
watch(
  () => props.modelValue,
  (cycle) => {
    if (cycle) {
      form.name = cycle.name
      form.startDate = cycle.startDate
      form.endDate = cycle.endDate
      form.defaultCurrencyId = cycle.defaultCurrency?.id ?? GTQ_SEED_ID
      form.alternateCurrencyId = cycle.alternateCurrency?.id ?? ''
      form.exchangeRate = cycle.exchangeRate ?? null
    } else {
      form.name = ''
      form.startDate = ''
      form.endDate = ''
      form.defaultCurrencyId = GTQ_SEED_ID
      form.alternateCurrencyId = ''
      form.exchangeRate = null
    }
    errors.name = ''
    errors.startDate = ''
    errors.endDate = ''
    errors.pairValidation = ''
  },
  { immediate: true },
)

function validate(): boolean {
  if (!form.name.trim()) {
    errors.name = t('budgetStructure.cycles.validation.nameRequired')
  } else if (form.name.trim().length > 200) {
    errors.name = t('budgetStructure.cycles.validation.nameTooLong')
  } else {
    errors.name = ''
  }
  errors.startDate = form.startDate ? '' : t('budgetStructure.periods.validation.startDateRequired')
  errors.endDate = form.endDate ? '' : t('budgetStructure.periods.validation.endDateRequired')

  if (!errors.endDate && !errors.startDate && form.endDate <= form.startDate) {
    errors.endDate = t('budgetStructure.periods.validation.dateOrder')
  }

  // Pair validation: both filled or both empty
  const hasAlternate = !!form.alternateCurrencyId
  const hasRate = form.exchangeRate != null && form.exchangeRate > 0
  if (hasAlternate !== hasRate) {
    errors.pairValidation = t('budgetStructure.cycles.pairValidationError')
  } else {
    errors.pairValidation = ''
  }

  return !errors.name && !errors.startDate && !errors.endDate && !errors.pairValidation
}

function handleSubmit(): void {
  if (!validate()) return

  const payload: CycleFormPayload = {
    name: form.name.trim(),
    startDate: form.startDate as DateString,
    endDate: form.endDate as DateString,
    defaultCurrencyId: form.defaultCurrencyId,
  }

  if (form.alternateCurrencyId) {
    payload.alternateCurrencyId = form.alternateCurrencyId
  }
  if (form.exchangeRate != null && form.exchangeRate > 0) {
    payload.exchangeRate = form.exchangeRate
  }

  emit('submit', payload)
}
</script>

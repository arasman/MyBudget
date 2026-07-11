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
        <div class="form-control mb-6">
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
import { reactive, watch, ref, onMounted } from 'vue'
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
})

const errors = reactive({
  name: '',
  startDate: '',
  endDate: '',
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
    } else {
      form.name = ''
      form.startDate = ''
      form.endDate = ''
      form.defaultCurrencyId = GTQ_SEED_ID
    }
    errors.name = ''
    errors.startDate = ''
    errors.endDate = ''
  },
  { immediate: true },
)

function validate(): boolean {
  errors.name = form.name.trim() ? '' : 'Name is required'
  errors.startDate = form.startDate ? '' : 'Start date is required'
  errors.endDate = form.endDate ? '' : 'End date is required'

  if (!errors.endDate && !errors.startDate && form.endDate <= form.startDate) {
    errors.endDate = 'End date must be after start date'
  }

  return !errors.name && !errors.startDate && !errors.endDate
}

function handleSubmit(): void {
  if (!validate()) return

  emit('submit', {
    name: form.name.trim(),
    startDate: form.startDate as DateString,
    endDate: form.endDate as DateString,
    defaultCurrencyId: form.defaultCurrencyId,
  })
}
</script>

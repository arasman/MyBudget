<template>
  <form data-testid="execution-record-form" class="space-y-3" @submit.prevent="handleSubmit">
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

    <!-- Note -->
    <div class="form-control">
      <label class="label" for="exec-note">
        <span class="label-text">
          {{ t('budgetExecution.form.note') }}
          <span v-if="noteRequired" class="text-error">*</span>
        </span>
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

    <!-- Error banner -->
    <div v-if="submitError" class="alert alert-error py-2 text-sm">
      <span>{{ submitError }}</span>
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
import type { ExecutionRecordDto } from '../types'
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'

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

const submitting = ref(false)
const submitError = ref<string | null>(null)

const form = reactive({
  entryType: props.editRecord?.entryType ?? EntryType.Expense,
  amount: props.editRecord?.amount ?? (null as number | null),
  note: props.editRecord?.note ?? '',
})

const errors = reactive({
  amount: '' as string,
  note: '' as string,
})

const noteRequired = computed(
  () => form.entryType === EntryType.CreditNote || form.entryType === EntryType.DebitNote,
)

function validate(): boolean {
  errors.amount = ''
  errors.note = ''

  let valid = true

  if (!form.amount || form.amount <= 0) {
    errors.amount = t('budgetExecution.form.validation.amountRequired')
    valid = false
  }

  if (noteRequired.value && !form.note?.trim()) {
    errors.note = t('budgetExecution.form.validation.noteRequired')
    valid = false
  }

  return valid
}

async function handleSubmit(): Promise<void> {
  if (!validate()) return

  submitting.value = true
  submitError.value = null

  // Derive currencyId from current cycle default currency
  const cycle = structureStore.currentCycle
  const currencyId = cycle?.defaultCurrency?.id ?? ''

  const payload = {
    entryType: form.entryType,
    amount: form.amount!,
    currencyId,
    note: form.note?.trim() || null,
    exchangeRate: null,
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
    } else {
      await matrixStore.createExecution(props.budgetId, props.periodId, props.lineId, payload)
    }
    emit('saved')

    // Reset form after successful create (not edit — parent closes on saved)
    if (!props.editRecord) {
      form.entryType = EntryType.Expense
      form.amount = null
      form.note = ''
    }
  } catch (e) {
    submitError.value = e instanceof Error ? e.message : t('budgetExecution.form.error')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <dialog class="modal modal-open">
    <div class="modal-box w-full max-w-md">
      <h3 class="font-bold text-lg mb-4">
        {{ modelValue ? t('budgetStructure.periods.edit') : t('budgetStructure.periods.create') }}
      </h3>

      <form @submit.prevent="handleSubmit">
        <!-- Name -->
        <div class="form-control mb-4">
          <label class="label" for="period-name">
            <span class="label-text">{{ t('budgetStructure.periods.name') }}</span>
          </label>
          <input
            id="period-name"
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
          <label class="label" for="period-start">
            <span class="label-text">{{ t('budgetStructure.periods.startDate') }}</span>
          </label>
          <input
            id="period-start"
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
          <label class="label" for="period-end">
            <span class="label-text">{{ t('budgetStructure.periods.endDate') }}</span>
          </label>
          <input
            id="period-end"
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

        <!-- Status (edit mode only) -->
        <div v-if="modelValue" class="form-control mb-6">
          <label class="label" for="period-status">
            <span class="label-text">{{ t('budgetStructure.periods.status') }}</span>
          </label>
          <select
            id="period-status"
            v-model="form.status"
            class="select select-bordered w-full"
          >
            <option value="Open">Open</option>
            <option value="Closed">Closed</option>
            <option value="Locked">Locked</option>
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
import { reactive, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { PeriodSummary, DateString } from '../types'

interface PeriodFormPayload {
  name: string
  startDate: DateString
  endDate: DateString
  status?: string
}

const props = defineProps<{
  modelValue: PeriodSummary | null
}>()

const emit = defineEmits<{
  submit: [payload: PeriodFormPayload]
  cancel: []
}>()

const { t } = useI18n()

const form = reactive({
  name: '',
  startDate: '',
  endDate: '',
  status: 'Open',
})

const errors = reactive({
  name: '',
  startDate: '',
  endDate: '',
})

// Populate form when editing an existing period.
watch(
  () => props.modelValue,
  (period) => {
    if (period) {
      form.name = period.name
      form.startDate = period.startDate
      form.endDate = period.endDate
      form.status = period.isClosed ? 'Closed' : 'Open'
    } else {
      form.name = ''
      form.startDate = ''
      form.endDate = ''
      form.status = 'Open'
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

  const payload: PeriodFormPayload = {
    name: form.name.trim(),
    startDate: form.startDate as DateString,
    endDate: form.endDate as DateString,
  }

  // Include status only in edit mode.
  if (props.modelValue) {
    payload.status = form.status
  }

  emit('submit', payload)
}
</script>

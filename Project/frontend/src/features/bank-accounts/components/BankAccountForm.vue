<template>
  <form @submit.prevent="handleSubmit">
    <div class="form-control mb-4">
      <label class="label">
        <span class="label-text">{{ t('bankAccount.form.alias') }}</span>
      </label>
      <input
        v-model="form.alias"
        type="text"
        class="input input-bordered"
        :class="{ 'input-error': errors.alias }"
        :placeholder="t('bankAccount.form.aliasPlaceholder')"
        maxlength="100"
      />
      <span v-if="errors.alias" class="label-text-alt text-error mt-1">{{ errors.alias }}</span>
    </div>

    <div class="form-control mb-4">
      <label class="label">
        <span class="label-text">{{ t('bankAccount.form.currency') }}</span>
      </label>
      <select
        v-model="form.currencyId"
        class="select select-bordered"
        :class="{ 'select-error': errors.currencyId }"
        :disabled="isEdit"
      >
        <option value="">{{ t('bankAccount.form.selectCurrency') }}</option>
        <option v-for="c in currencies" :key="c.id" :value="c.id">{{ c.code }}</option>
      </select>
      <span v-if="errors.currencyId" class="label-text-alt text-error mt-1">{{
        errors.currencyId
      }}</span>
    </div>

    <div class="form-control mb-4">
      <label class="label cursor-pointer">
        <span class="label-text">{{ t('bankAccount.form.isPositive') }}</span>
        <input v-model="form.isPositive" type="checkbox" class="toggle toggle-primary" />
      </label>
      <span class="text-xs text-base-content/60 mt-1">{{
        form.isPositive
          ? t('bankAccount.form.isPositiveHint')
          : t('bankAccount.form.isNegativeHint')
      }}</span>
    </div>

    <div class="form-control mb-4">
      <label class="label">
        <span class="label-text">{{ t('bankAccount.form.displayOrder') }}</span>
      </label>
      <input
        v-model.number="form.displayOrder"
        type="number"
        class="input input-bordered"
        :class="{ 'input-error': errors.displayOrder }"
        min="0"
        step="1"
      />
      <span v-if="errors.displayOrder" class="label-text-alt text-error mt-1">{{
        errors.displayOrder
      }}</span>
    </div>

    <div class="flex gap-2 justify-end">
      <button type="button" class="btn btn-ghost" @click="emit('cancel')">
        {{ t('common.cancel') }}
      </button>
      <button type="submit" class="btn btn-primary">
        {{ t('common.save') }}
      </button>
    </div>
  </form>
</template>

<script setup lang="ts">
import { reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CreateBankAccountDto, UpdateBankAccountDto } from '../types/bankAccount'

interface CurrencyOption {
  id: string
  code: string
}

const props = defineProps<{
  initialValues?: Partial<CreateBankAccountDto>
  currencies: CurrencyOption[]
  isEdit?: boolean
}>()

const emit = defineEmits<{
  submit: [payload: CreateBankAccountDto | UpdateBankAccountDto]
  cancel: []
}>()

const { t } = useI18n()

const form = reactive({
  alias: props.initialValues?.alias ?? '',
  currencyId: props.initialValues?.currencyId ?? '',
  isPositive: props.initialValues?.isPositive ?? true,
  displayOrder: props.initialValues?.displayOrder ?? 0,
})

const errors = reactive({
  alias: '',
  currencyId: '',
  displayOrder: '',
})

function validate(): boolean {
  errors.alias = ''
  errors.currencyId = ''
  errors.displayOrder = ''

  let valid = true

  if (!form.alias.trim()) {
    errors.alias = t('bankAccount.validation.aliasRequired')
    valid = false
  } else if (form.alias.length > 100) {
    errors.alias = t('bankAccount.validation.aliasTooLong')
    valid = false
  }

  if (!props.isEdit && !form.currencyId) {
    errors.currencyId = t('bankAccount.validation.currencyRequired')
    valid = false
  }

  if (form.displayOrder < 0) {
    errors.displayOrder = t('bankAccount.validation.displayOrderMin')
    valid = false
  }

  return valid
}

function handleSubmit(): void {
  if (!validate()) return

  if (props.isEdit) {
    emit('submit', {
      alias: form.alias.trim(),
      isPositive: form.isPositive,
      displayOrder: form.displayOrder,
    } as UpdateBankAccountDto)
  } else {
    emit('submit', {
      alias: form.alias.trim(),
      currencyId: form.currencyId,
      isPositive: form.isPositive,
      displayOrder: form.displayOrder,
    } as CreateBankAccountDto)
  }
}
</script>

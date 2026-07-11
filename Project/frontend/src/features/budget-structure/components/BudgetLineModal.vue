<template>
  <dialog class="modal modal-open">
    <div class="modal-box w-11/12 max-w-lg">
      <h3 class="font-bold text-lg mb-4">
        {{
          isEditMode
            ? t('budgetStructure.budgetLines.edit')
            : t('budgetStructure.budgetLines.create')
        }}
      </h3>

      <form @submit.prevent="handleSubmit">
        <!-- Name -->
        <div class="form-control mb-3">
          <label class="label" for="line-name">
            <span class="label-text">{{ t('budgetStructure.budgetLines.name') }} *</span>
          </label>
          <input
            id="line-name"
            v-model="form.name"
            type="text"
            class="input input-bordered w-full"
            required
          />
        </div>

        <!-- Line type -->
        <div class="form-control mb-3">
          <label class="label" for="line-type">
            <span class="label-text">{{ t('budgetStructure.budgetLines.lineType') }} *</span>
          </label>
          <select id="line-type" v-model="form.lineType" class="select select-bordered w-full" required>
            <option value="Expense">{{ t('budgetStructure.budgetLines.types.expense') }}</option>
            <option value="LongTermSavings">{{ t('budgetStructure.budgetLines.types.longTermSavings') }}</option>
            <option value="PreventiveSavings">{{ t('budgetStructure.budgetLines.types.preventiveSavings') }}</option>
          </select>
        </div>

        <!-- Is recurring -->
        <div class="form-control mb-3">
          <label class="label cursor-pointer justify-start gap-3">
            <input id="line-recurring" v-model="form.isRecurring" type="checkbox" class="checkbox" />
            <span class="label-text">{{ t('budgetStructure.budgetLines.isRecurring') }}</span>
          </label>
        </div>

        <!-- Category group -->
        <div class="form-control mb-3">
          <label class="label" for="line-group">
            <span class="label-text">{{ t('budgetStructure.categoryGroups.title') }} *</span>
          </label>
          <select
            id="line-group"
            v-model="form.categoryGroupId"
            class="select select-bordered w-full"
            @change="form.categoryId = undefined"
          >
            <option value="" disabled>— select —</option>
            <option v-for="group in categoryGroups" :key="group.id" :value="group.id">
              {{ group.name }}
            </option>
          </select>
        </div>

        <!-- Category (filtered by selected group) -->
        <div class="form-control mb-3">
          <label class="label" for="line-category">
            <span class="label-text">{{ t('budgetStructure.categories.edit') }}</span>
          </label>
          <select id="line-category" v-model="form.categoryId" class="select select-bordered w-full">
            <option :value="undefined">— none —</option>
            <option
              v-for="cat in filteredCategories"
              :key="cat.id"
              :value="cat.id"
            >
              {{ cat.name }}
            </option>
          </select>
        </div>

        <!-- Budgeted amount -->
        <div class="form-control mb-3">
          <label class="label" for="line-amount">
            <span class="label-text">{{ t('budgetStructure.budgetLines.budgetedAmount') }}</span>
          </label>
          <input
            id="line-amount"
            v-model.number="form.budgetedAmount"
            type="number"
            step="0.01"
            min="0"
            class="input input-bordered w-full"
          />
        </div>

        <!-- Currency -->
        <div class="form-control mb-3">
          <label class="label" for="line-currency">
            <span class="label-text">{{ t('budgetStructure.budgetLines.currency') }}</span>
          </label>
          <select id="line-currency" v-model="form.currency" class="select select-bordered w-full">
            <option value="GTQ">GTQ — Quetzal</option>
            <option value="USD">USD — US Dollar</option>
          </select>
        </div>

        <!-- Note -->
        <div class="form-control mb-4">
          <label class="label" for="line-note">
            <span class="label-text">{{ t('budgetStructure.budgetLines.note') }}</span>
          </label>
          <textarea
            id="line-note"
            v-model="form.note"
            class="textarea textarea-bordered w-full"
            rows="3"
          />
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
import { reactive, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { BudgetLineResponse, CategoryGroupResponse, CreateBudgetLinePayload, LineType } from '../types'

const props = defineProps<{
  modelValue: BudgetLineResponse | null
  categoryGroups: CategoryGroupResponse[]
}>()

const emit = defineEmits<{
  submit: [payload: CreateBudgetLinePayload]
  cancel: []
}>()

const { t } = useI18n()

const isEditMode = computed(() => props.modelValue !== null)

const form = reactive<{
  name: string
  lineType: LineType
  isRecurring: boolean
  categoryGroupId: string
  categoryId: string | undefined
  budgetedAmount: number | undefined
  currency: string | undefined
  note: string | undefined
}>({
  name: props.modelValue?.name ?? '',
  lineType: props.modelValue?.lineType ?? 'Expense',
  isRecurring: props.modelValue?.isRecurring ?? false,
  categoryGroupId: props.modelValue?.categoryGroupId ?? '',
  categoryId: props.modelValue?.categoryId,
  budgetedAmount: props.modelValue?.budgetedAmount,
  currency: props.modelValue?.currencyCode ?? 'GTQ',
  note: props.modelValue?.note,
})

const filteredCategories = computed(() => {
  if (!form.categoryGroupId) return []
  const group = props.categoryGroups.find((g) => g.id === form.categoryGroupId)
  return group?.categories ?? []
})

function handleSubmit(): void {
  const payload: CreateBudgetLinePayload = {
    name: form.name,
    lineType: form.lineType,
    isRecurring: form.isRecurring,
    categoryGroupId: form.categoryGroupId || undefined,
    categoryId: form.categoryId || undefined,
    budgetedAmount: form.budgetedAmount != null ? Number(form.budgetedAmount) : undefined,
    currency: form.currency?.trim().toUpperCase() || undefined,
    note: form.note?.trim() || undefined,
  }
  emit('submit', payload)
}
</script>

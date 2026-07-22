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

      <form novalidate @submit.prevent="handleSubmit">
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
            :class="{ 'input-error': errors.name }"
            maxlength="200"
          />
          <div v-if="errors.name" class="label">
            <span class="label-text-alt text-error">{{ errors.name }}</span>
          </div>
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

        <!-- Start date (required in create; read-only in edit) -->
        <div class="form-control mb-3">
          <label class="label" for="line-startDate">
            <span class="label-text">{{ t('budgetStructure.budgetLines.startDate') }} *</span>
          </label>
          <template v-if="isEditMode">
            <div class="input input-bordered w-full flex items-center text-base-content/60 bg-base-200 cursor-not-allowed">
              {{ form.startDate || '—' }}
            </div>
          </template>
          <template v-else>
            <input
              id="line-startDate"
              v-model="form.startDate"
              type="date"
              class="input input-bordered w-full"
              :class="{ 'input-error': errors.startDate }"
            />
            <div v-if="errors.startDate" class="label">
              <span class="label-text-alt text-error">{{ errors.startDate }}</span>
            </div>
          </template>
        </div>

        <!-- End date (optional — null means perpetual; read-only in edit) -->
        <div class="form-control mb-3">
          <label class="label" for="line-endDate">
            <span class="label-text">{{ t('budgetStructure.budgetLines.endDate') }}</span>
          </label>
          <template v-if="isEditMode">
            <div class="input input-bordered w-full flex items-center text-base-content/60 bg-base-200 cursor-not-allowed">
              {{ form.endDate || t('budgetStructure.budgetLines.endDatePlaceholder', 'Perpetual') }}
            </div>
          </template>
          <template v-else>
            <input
              id="line-endDate"
              v-model="form.endDate"
              type="date"
              class="input input-bordered w-full"
              :class="{ 'input-error': errors.endDate }"
              :placeholder="t('budgetStructure.budgetLines.endDatePlaceholder', 'Perpetual / No expiry')"
            />
            <div v-if="errors.endDate" class="label">
              <span class="label-text-alt text-error">{{ errors.endDate }}</span>
            </div>
          </template>
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

        <!-- Initial / Budgeted amount (read-only in edit — changes require a revision) -->
        <div class="form-control mb-3">
          <label class="label" for="line-initialAmount">
            <span class="label-text">{{ t('budgetStructure.budgetLines.budgetedAmount') }}</span>
          </label>
          <template v-if="isEditMode">
            <div class="input input-bordered w-full flex items-center text-base-content/60 bg-base-200 cursor-not-allowed">
              {{ form.initialAmount ?? '—' }}
            </div>
            <div class="label">
              <span class="label-text-alt text-base-content/50">{{ t('budgetStructure.budgetLines.customizations.managedViaCustomizations') }}</span>
            </div>
          </template>
          <template v-else>
            <input
              id="line-initialAmount"
              v-model.number="form.initialAmount"
              type="number"
              step="0.01"
              min="0"
              class="input input-bordered w-full"
              :class="{ 'input-error': errors.initialAmount }"
            />
            <div v-if="errors.initialAmount" class="label">
              <span class="label-text-alt text-error">{{ errors.initialAmount }}</span>
            </div>
          </template>
        </div>

        <!-- Currency (read-only in edit mode) -->
        <div class="form-control mb-3">
          <label class="label" for="line-currency">
            <span class="label-text">{{ t('budgetStructure.budgetLines.currency') }}</span>
          </label>
          <template v-if="isEditMode">
            <div class="input input-bordered w-full flex items-center text-base-content/60 bg-base-200 cursor-not-allowed">
              {{ props.modelValue?.currencyCode ?? '—' }}
            </div>
          </template>
          <template v-else>
            <select id="line-currency" v-model="form.currencyId" class="select select-bordered w-full">
              <option :value="undefined">— none —</option>
              <option
                v-for="currency in availableCurrencies"
                :key="currency.id"
                :value="currency.id"
              >
                {{ currency.code }} — {{ currency.name ?? currency.symbol }}
              </option>
            </select>
          </template>
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
import type {
  BudgetLineResponse,
  CategoryGroupResponse,
  CreateBudgetLinePayload,
  UpdateBudgetLinePayload,
  CurrencyItem,
  LineType,
} from '../types'
import { useBudgetStructureStore } from '../store'

const props = defineProps<{
  modelValue: BudgetLineResponse | null
  categoryGroups: CategoryGroupResponse[]
}>()

const emit = defineEmits<{
  submit: [payload: CreateBudgetLinePayload | UpdateBudgetLinePayload]
  cancel: []
}>()

const { t } = useI18n()
const structureStore = useBudgetStructureStore()

const isEditMode = computed(() => props.modelValue !== null)

/** Currencies available from the current cycle (default + optional alternate).
 *  Falls back to the active cycle in the list when no cycle detail is loaded. */
const availableCurrencies = computed((): CurrencyItem[] => {
  const cycle =
    structureStore.currentCycle ??
    structureStore.cycles.find(c => c.isActive) ??
    structureStore.cycles[0]
  if (!cycle) return []
  const currencies: CurrencyItem[] = []
  if (cycle.defaultCurrency) currencies.push(cycle.defaultCurrency)
  if (cycle.alternateCurrency) currencies.push(cycle.alternateCurrency)
  return currencies
})

const errors = reactive({
  name: '',
  startDate: '',
  endDate: '',
  initialAmount: '',
})

const form = reactive<{
  name: string
  lineType: LineType
  startDate: string
  endDate: string
  categoryGroupId: string
  categoryId: string | undefined
  initialAmount: number | undefined
  currencyId: string | undefined
  note: string | undefined
}>({
  name: props.modelValue?.name ?? '',
  lineType: props.modelValue?.lineType ?? 'Expense',
  startDate: props.modelValue?.startDate ?? '',
  endDate: props.modelValue?.endDate ?? '',
  categoryGroupId: props.modelValue?.categoryGroupId ?? '',
  categoryId: props.modelValue?.categoryId,
  initialAmount: props.modelValue?.budgetedAmount,
  currencyId: props.modelValue?.currencyId,
  note: props.modelValue?.note,
})

const filteredCategories = computed(() => {
  if (!form.categoryGroupId) return []
  const group = props.categoryGroups.find((g) => g.id === form.categoryGroupId)
  return group?.categories ?? []
})

function validate(): boolean {
  // Reset all errors
  errors.name = ''
  errors.startDate = ''
  errors.endDate = ''
  errors.initialAmount = ''

  if (!form.name.trim()) {
    errors.name = t('budgetStructure.budgetLines.validation.nameRequired')
  } else if (form.name.trim().length > 200) {
    errors.name = t('budgetStructure.budgetLines.validation.nameTooLong')
  }

  if (!isEditMode.value) {
    // Create mode: startDate required
    if (!form.startDate) {
      errors.startDate = t('budgetStructure.budgetLines.validation.startDateRequired')
    }

    // Validate initialAmount when provided
    if (form.initialAmount != null && form.initialAmount <= 0) {
      errors.initialAmount = t('budgetStructure.budgetLines.validation.amountPositive')
    }

    // Validate endDate after startDate
    if (form.endDate && form.startDate && form.endDate < form.startDate) {
      errors.endDate = t('budgetStructure.budgetLines.validation.endDateAfterStartDate')
    }
  }

  return !errors.name && !errors.startDate && !errors.endDate && !errors.initialAmount
}

function handleSubmit(): void {
  if (!validate()) return

  if (!isEditMode.value) {
    const payload: CreateBudgetLinePayload = {
      name: form.name.trim(),
      lineType: form.lineType,
      startDate: form.startDate,
      endDate: form.endDate || undefined,
      initialAmount: form.initialAmount ?? 0,
      currencyId: form.currencyId || undefined,
      categoryGroupId: form.categoryGroupId || undefined,
      categoryId: form.categoryId || undefined,
      note: form.note?.trim() || undefined,
    }
    emit('submit', payload)
  } else {
    const payload: UpdateBudgetLinePayload = {
      name: form.name.trim(),
      lineType: form.lineType,
      categoryGroupId: form.categoryGroupId || undefined,
      categoryId: form.categoryId || undefined,
      note: form.note?.trim() || undefined,
      currencyId: form.currencyId || undefined,
    }
    emit('submit', payload)
  }
}
</script>

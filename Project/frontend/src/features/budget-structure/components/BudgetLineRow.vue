<template>
  <tr
    class="hover select-none"
    :class="{
      'cursor-pointer': !readonly && !editing && !line.deletedAt,
      'opacity-60': !!line.deletedAt,
    }"
    @dblclick="onRowDblClick"
  >
    <!-- Group cell -->
    <td>
      <template v-if="editing">
        <select
          v-model="form.categoryGroupId"
          class="select select-xs select-bordered w-full"
          @change="form.categoryId = undefined"
        >
          <option value="" disabled>—</option>
          <option v-for="g in props.categoryGroups" :key="g.id" :value="g.id">{{ g.name }}</option>
        </select>
      </template>
      <template v-else>{{ groupName }}</template>
    </td>

    <!-- Category cell -->
    <td>
      <template v-if="editing">
        <select v-model="form.categoryId" class="select select-xs select-bordered w-full">
          <option :value="undefined">—</option>
          <option v-for="cat in filteredCategories" :key="cat.id" :value="cat.id">{{ cat.name }}</option>
        </select>
      </template>
      <template v-else>{{ categoryName }}</template>
    </td>

    <!-- Line type cell -->
    <td>
      <template v-if="editing">
        <select v-model="form.lineType" class="select select-xs select-bordered w-full">
          <option value="Expense">{{ t('budgetStructure.budgetLines.types.expense') }}</option>
          <option value="LongTermSavings">{{ t('budgetStructure.budgetLines.types.longTermSavings') }}</option>
          <option value="PreventiveSavings">{{ t('budgetStructure.budgetLines.types.preventiveSavings') }}</option>
        </select>
      </template>
      <template v-else>
        <span class="badge badge-sm">
          {{ t(`budgetStructure.budgetLines.types.${line.lineType.charAt(0).toLowerCase() + line.lineType.slice(1)}`) }}
        </span>
      </template>
    </td>

    <!-- Name cell -->
    <td class="font-medium">
      <template v-if="editing">
        <input
          v-model="form.name"
          type="text"
          class="input input-xs input-bordered w-full"
          :placeholder="t('budgetStructure.budgetLines.name')"
        />
      </template>
      <template v-else>
        {{ line.name }}
        <span v-if="line.deletedAt" class="badge badge-error badge-xs ml-1">{{ t('budgetStructure.common.deleted') }}</span>
      </template>
    </td>

    <!-- Currency cell -->
    <td>
      <template v-if="editing">
        <select v-model="form.currencyId" class="select select-xs select-bordered">
          <option :value="undefined">—</option>
          <option
            v-for="currency in availableCurrencies"
            :key="currency.id"
            :value="currency.id"
          >
            {{ currency.code }}
          </option>
        </select>
      </template>
      <template v-else>{{ line.currencyCode ?? '—' }}</template>
    </td>

    <!-- Budgeted amount cell -->
    <td>
      <template v-if="editing">
        <input
          v-model.number="form.budgetedAmount"
          type="number"
          step="0.01"
          class="input input-xs input-bordered w-24"
        />
      </template>
      <template v-else>
        <span v-if="line.budgetedAmount != null">
          {{ formatAmount(line.budgetedAmount) }}
        </span>
        <span v-else class="text-base-content/30">—</span>
      </template>
    </td>

    <!-- Recurring cell -->
    <td>
      <template v-if="editing">
        <input v-model="form.isRecurring" type="checkbox" class="checkbox checkbox-xs" />
      </template>
      <template v-else>
        <span v-if="line.isRecurring" class="text-base-content/70" title="Recurring">↻</span>
        <span v-else class="text-base-content/30">—</span>
      </template>
    </td>

    <!-- Note cell -->
    <td class="max-w-xs truncate text-sm text-base-content/60" :title="editing ? '' : (line.note ?? '')">
      <template v-if="editing">
        <input
          v-model="form.note"
          type="text"
          class="input input-xs input-bordered w-full"
          :placeholder="t('budgetStructure.budgetLines.note')"
        />
      </template>
      <template v-else>{{ line.note ? truncate(line.note, 40) : '—' }}</template>
    </td>

    <!-- Actions cell -->
    <td v-if="!readonly">
      <div class="flex gap-1">
        <template v-if="editing">
          <button
            type="button"
            class="btn btn-xs btn-ghost btn-square text-success"
            :title="t('budgetStructure.common.save')"
            @click.stop="onInlineSave"
          >
            <Check :size="14" />
          </button>
          <button
            type="button"
            class="btn btn-xs btn-ghost btn-square"
            :title="t('budgetStructure.common.cancel')"
            @click.stop="emit('inlineCancel', props.line.id)"
          >
            <X :size="14" />
          </button>
        </template>
        <!-- Deleted line: restore only -->
        <template v-else-if="line.deletedAt">
          <button
            type="button"
            class="btn btn-success btn-xs"
            @click.stop="emit('restore', line.id)"
          >
            <RotateCcw :size="14" />
            {{ t('budgetStructure.common.restore') }}
          </button>
        </template>
        <!-- Active line: edit + delete -->
        <template v-else>
          <button
            type="button"
            class="btn btn-xs btn-ghost btn-square"
            :title="t('budgetStructure.budgetLines.edit')"
            @click.stop="emit('edit', line)"
          >
            <Pencil :size="14" />
          </button>
          <button
            type="button"
            class="btn btn-xs btn-ghost btn-square text-error"
            :title="t('budgetStructure.budgetLines.delete')"
            @click.stop="emit('delete', line.id)"
          >
            <Trash2 :size="14" />
          </button>
        </template>
      </div>
    </td>
    <td v-else />
  </tr>
</template>

<script setup lang="ts">
import { reactive, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { Pencil, RotateCcw, Trash2, Check, X } from 'lucide-vue-next'
import type { BudgetLineResponse, CategoryGroupResponse, CurrencyItem, LineType, UpdateBudgetLinePayload } from '../types'
import { useBudgetStructureStore } from '../store'

const props = defineProps<{
  line: BudgetLineResponse
  readonly: boolean
  editing: boolean
  categoryGroups: CategoryGroupResponse[]
}>()

const emit = defineEmits<{
  edit: [line: BudgetLineResponse]
  delete: [lineId: string]
  restore: [lineId: string]
  startEdit: [line: BudgetLineResponse]
  inlineSave: [lineId: string, payload: UpdateBudgetLinePayload]
  inlineCancel: [lineId: string]
}>()

const { t } = useI18n()
const structureStore = useBudgetStructureStore()

const availableCurrencies = computed((): CurrencyItem[] => {
  const cycle = structureStore.currentCycle
  if (!cycle) return []
  const currencies: CurrencyItem[] = []
  if (cycle.defaultCurrency) currencies.push(cycle.defaultCurrency)
  if (cycle.alternateCurrency) currencies.push(cycle.alternateCurrency)
  return currencies
})

const filteredCategories = computed(() => {
  if (!form.categoryGroupId) return []
  return props.categoryGroups.find((g) => g.id === form.categoryGroupId)?.categories.filter((c) => !c.deletedAt) ?? []
})

const groupName = computed(() =>
  props.categoryGroups.find((g) => g.id === props.line.categoryGroupId)?.name ?? '—',
)

const categoryName = computed(() => {
  const group = props.categoryGroups.find((g) => g.id === props.line.categoryGroupId)
  return group?.categories.find((c) => c.id === props.line.categoryId)?.name ?? '—'
})

const form = reactive({
  name: '',
  lineType: 'Expense' as LineType,
  isRecurring: false,
  budgetedAmount: null as number | null,
  currencyId: undefined as string | undefined,
  note: '',
  categoryGroupId: undefined as string | undefined,
  categoryId: undefined as string | undefined,
})

function resetForm(): void {
  form.name = props.line.name
  form.lineType = props.line.lineType
  form.isRecurring = props.line.isRecurring
  form.budgetedAmount = props.line.budgetedAmount ?? null
  form.currencyId = props.line.currencyId
  form.note = props.line.note ?? ''
  form.categoryGroupId = props.line.categoryGroupId
  form.categoryId = props.line.categoryId
}

watch(
  () => props.editing,
  (val) => {
    if (val) resetForm()
  },
)

function onRowDblClick(): void {
  if (!props.readonly && !props.editing && !props.line.deletedAt) {
    emit('startEdit', props.line)
  }
}

function onInlineSave(): void {
  const payload: UpdateBudgetLinePayload = {
    name: form.name,
    lineType: form.lineType,
    isRecurring: form.isRecurring,
    budgetedAmount: form.budgetedAmount ?? undefined,
    currencyId: form.currencyId || undefined,
    note: form.note || undefined,
    categoryGroupId: form.categoryGroupId,
    categoryId: form.categoryId || undefined,
  }
  emit('inlineSave', props.line.id, payload)
}

function formatAmount(amount: number): string {
  return new Intl.NumberFormat(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount)
}

function truncate(text: string, maxLength: number): string {
  return text.length > maxLength ? `${text.slice(0, maxLength)}…` : text
}
</script>

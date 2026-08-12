<template>
  <div
    data-testid="execution-record-row"
    class="flex items-start gap-2 py-2 px-1 border-b border-base-300 last:border-0"
    :class="{ 'opacity-60': record.deletedAt }"
  >
    <!-- Entry type badge -->
    <span
      class="badge badge-sm shrink-0 mt-0.5"
      :class="entryTypeBadgeClass"
    >
      {{ entryTypeLabel }}
    </span>

    <!-- Amount -->
    <span
      class="font-mono text-sm shrink-0"
      :class="[{ 'line-through text-base-content/50': record.deletedAt }, amountColorClass]"
    >
      {{ formattedAmount }}
    </span>

    <!-- Note -->
    <span
      v-if="record.note"
      class="text-sm text-base-content/70 flex-1 truncate"
    >
      {{ record.note }}
    </span>
    <span
      v-else
      class="flex-1"
    />

    <!-- Date -->
    <span class="text-xs text-base-content/50 shrink-0">
      {{ formatDate(record.createdAt) }}
    </span>

    <!-- Actions -->
    <div
      v-if="!periodClosed && canWrite"
      class="flex items-center gap-1 shrink-0"
    >
      <!-- Edit button (not shown for deleted records or confirm mode) -->
      <button
        v-if="!record.deletedAt && !confirmingDelete"
        type="button"
        class="btn btn-xs btn-ghost"
        :title="t('budgetExecution.row.edit')"
        @click="$emit('edit', record)"
      >
        {{ t('budgetExecution.row.edit') }}
      </button>

      <!-- Delete — two-step confirm (not shown for deleted records) -->
      <template v-if="!record.deletedAt">
        <!-- Confirm state: show "Delete this entry?" label + confirm/cancel buttons -->
        <template v-if="confirmingDelete">
          <span class="text-xs text-error">{{ t('budgetExecution.record.confirmDelete') }}</span>
          <button
            type="button"
            data-testid="delete-record-confirm-btn"
            class="btn btn-xs btn-error"
            :disabled="deleting"
            @click="handleDelete"
          >
            <span
              v-if="deleting"
              class="loading loading-spinner loading-xs"
            />
            <span v-else>{{ t('budgetExecution.row.delete') }}</span>
          </button>
          <button
            type="button"
            data-testid="delete-record-cancel-btn"
            class="btn btn-xs btn-ghost"
            @click="confirmingDelete = false"
          >
            {{ t('common.cancel') }}
          </button>
        </template>

        <!-- Normal state: show delete button -->
        <button
          v-else
          type="button"
          data-testid="delete-record-btn"
          class="btn btn-xs btn-ghost text-error"
          :title="t('budgetExecution.row.delete')"
          @click="confirmingDelete = true"
        >
          {{ t('budgetExecution.row.delete') }}
        </button>
      </template>

      <!-- Restore button (shown only for deleted records) -->
      <button
        v-if="record.deletedAt"
        type="button"
        class="btn btn-xs btn-ghost text-success"
        :title="t('budgetExecution.row.restore')"
        :disabled="restoring"
        @click="handleRestore"
      >
        <span
          v-if="restoring"
          class="loading loading-spinner loading-xs"
        />
        <span v-else>{{ t('budgetExecution.row.restore') }}</span>
      </button>
    </div>

    <!-- Restore button even when period is closed (for admins who can still restore) -->
    <div
      v-else-if="record.deletedAt && canWrite"
      class="shrink-0"
    >
      <button
        type="button"
        class="btn btn-xs btn-ghost text-success"
        :title="t('budgetExecution.row.restore')"
        :disabled="restoring"
        @click="handleRestore"
      >
        <span
          v-if="restoring"
          class="loading loading-spinner loading-xs"
        />
        <span v-else>{{ t('budgetExecution.row.restore') }}</span>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { EntryType } from '../types'
import type { ExecutionRecordDto } from '../types'
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import { useRoleGate } from '@/features/budget-structure/composables/useRoleGate'
import { useToastStore } from '@/stores/toast.store'

const props = defineProps<{
  record: ExecutionRecordDto
  periodClosed: boolean
  budgetId: string
  periodId: string
  lineId: string
}>()

defineEmits<{
  edit: [record: ExecutionRecordDto]
}>()

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()
const structureStore = useBudgetStructureStore()
const toastStore = useToastStore()
const { isOperator } = useRoleGate(props.budgetId)

const canWrite = isOperator
const deleting = ref(false)
const restoring = ref(false)
const confirmingDelete = ref(false)

const entryTypeLabel = computed(() => {
  switch (props.record.entryType) {
    case EntryType.Expense:
      return t('budgetExecution.form.entryTypes.expense')
    case EntryType.CreditNote:
      return t('budgetExecution.form.entryTypes.creditNote')
    case EntryType.DebitNote:
      return t('budgetExecution.form.entryTypes.debitNote')
    default:
      return String(props.record.entryType)
  }
})

const entryTypeBadgeClass = computed(() => {
  switch (props.record.entryType) {
    case EntryType.CreditNote:
      return 'badge-success'
    case EntryType.DebitNote:
      return 'badge-warning'
    default:
      return 'badge-neutral'
  }
})

const amountColorClass = computed(() => {
  if (props.record.deletedAt) return ''
  return props.record.entryType === EntryType.CreditNote ? 'text-success' : ''
})

const currencyCode = computed(() => {
  const cycle = structureStore.currentCycle
  if (!cycle || !props.record.currencyId) return ''
  if (cycle.defaultCurrency?.id === props.record.currencyId) return cycle.defaultCurrency.code
  if (cycle.alternateCurrency?.id === props.record.currencyId) return cycle.alternateCurrency.code
  return ''
})

// Expense and DebitNote show positive amount; CreditNote shows negative sign
const formattedAmount = computed(() => {
  const sign = props.record.entryType === EntryType.CreditNote ? '-' : ''
  const code = currencyCode.value ? `${currencyCode.value} ` : ''
  return `${code}${sign}${props.record.amount.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`
})

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
}

async function handleDelete(): Promise<void> {
  deleting.value = true
  try {
    await matrixStore.deleteExecution(props.budgetId, props.periodId, props.lineId, props.record.id)
    confirmingDelete.value = false
    toastStore.push({ type: 'success', title: t('budgetExecution.record.deleteSuccess') })
  } catch {
    // Store handles error state
  } finally {
    deleting.value = false
  }
}

async function handleRestore(): Promise<void> {
  restoring.value = true
  try {
    await matrixStore.restoreExecution(
      props.budgetId,
      props.periodId,
      props.lineId,
      props.record.id,
    )
    toastStore.push({ type: 'success', title: t('budgetExecution.record.restoreSuccess') })
  } catch {
    // Store handles error state
  } finally {
    restoring.value = false
  }
}
</script>

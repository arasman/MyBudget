<template>
  <td
    data-testid="matrix-cell-ejecutado"
    class="text-right cursor-pointer select-none hover:bg-base-200 transition-colors px-3 py-2"
    :class="{ 'opacity-50 line-through': deleted }"
    @dblclick="onDblClick"
  >
    <div
      v-if="loading"
      class="skeleton h-4 w-16 ml-auto"
    />
    <span v-else>{{ formatAmount(amount, currencySymbol) }}</span>
  </td>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'
import { useCurrencyDisplay } from '../composables/useCurrencyDisplay'

defineProps<{
  amount: number
  loading: boolean
  deleted?: boolean
}>()

const emit = defineEmits<{
  dblclick: []
}>()

function onDblClick(): void {
  window.getSelection()?.removeAllRanges()
  emit('dblclick')
}

const matrixStore = useBudgetMatrixStore()
const structureStore = useBudgetStructureStore()
const { formatAmount } = useCurrencyDisplay(matrixStore)

/** Currency symbol derived from cycle based on the active display currency. */
const currencySymbol = computed<string>(() =>
  matrixStore.displayCurrency === 'alternate'
    ? structureStore.currentCycle?.alternateCurrency?.symbol ?? ''
    : structureStore.currentCycle?.defaultCurrency?.symbol ?? '',
)
</script>

<template>
  <td
    class="text-right cursor-pointer hover:bg-base-200 transition-colors px-3 py-2"
    :class="{ 'opacity-50 line-through': deleted }"
    @dblclick="$emit('dblclick')"
  >
    <div v-if="loading" class="skeleton h-4 w-16 ml-auto" />
    <span v-else>{{ formatAmount(amount, currencySymbol) }}</span>
  </td>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useBudgetMatrixStore } from '../store'
import { useCurrencyDisplay } from '../composables/useCurrencyDisplay'

const props = defineProps<{
  amount: number
  loading: boolean
  deleted?: boolean
}>()

defineEmits<{
  dblclick: []
}>()

const matrixStore = useBudgetMatrixStore()
const { formatAmount } = useCurrencyDisplay(matrixStore)

// Derive currency symbol from the cycle's default currency
const currencySymbol = computed<string>(() => {
  // The currency symbol comes from the structure store's currentCycle
  // At this level we use a simple placeholder; MatrixControls (PR5) will
  // expose the full symbol. For the skeleton we fall back to an empty string.
  return ''
})
</script>

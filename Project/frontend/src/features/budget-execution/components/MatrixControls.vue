<template>
  <div class="flex items-center gap-4 p-2 bg-base-200 rounded-lg mb-2">
    <!-- Cycle name -->
    <span class="font-bold text-lg">{{ structureStore.currentCycle?.name }}</span>

    <!-- Spacer -->
    <div class="flex-1" />

    <!-- Currency toggle -->
    <div class="flex items-center gap-2">
      <span class="text-sm text-base-content/60">
        {{ t('budgetMatrix.controls.currency') }}
      </span>
      <div class="join">
        <button
          data-testid="currency-gtq-btn"
          type="button"
          class="join-item btn btn-sm"
          :class="{ 'btn-active': matrixStore.displayCurrency === 'default' }"
          @click="matrixStore.setDisplayCurrency('default')"
        >
          GTQ
        </button>
        <button
          data-testid="currency-usd-btn"
          type="button"
          class="join-item btn btn-sm"
          :class="{ 'btn-active': matrixStore.displayCurrency === 'alternate' }"
          :disabled="!structureStore.currentCycle?.alternateCurrency && !matrixStore.alternateCurrencyId"
          @click="matrixStore.setDisplayCurrency('alternate')"
        >
          USD
        </button>
      </div>
      <!-- Exchange rate display — visible only when alternate currency is active -->
      <span
        v-if="structureStore.currentCycle?.exchangeRate && matrixStore.displayCurrency === 'alternate'"
        class="text-xs text-base-content/60"
      >
        {{ structureStore.currentCycle.exchangeRate }} GTQ = 1 USD
      </span>
    </div>

    <!-- Include deleted checkbox -->
    <label class="flex items-center gap-2 cursor-pointer">
      <input
        data-testid="include-deleted-checkbox"
        type="checkbox"
        class="checkbox checkbox-sm"
        :checked="matrixStore.showDeleted"
        @change="matrixStore.setShowDeleted(($event.target as HTMLInputElement).checked)"
      />
      <span class="text-sm">{{ t('budgetMatrix.controls.includeDeleted') }}</span>
    </label>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()
const structureStore = useBudgetStructureStore()
</script>

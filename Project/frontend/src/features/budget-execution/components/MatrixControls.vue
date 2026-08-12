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
      <!-- Exchange rate input — visible only when alternate currency is active -->
      <template v-if="matrixStore.displayCurrency === 'alternate'">
        <input
          data-testid="exchange-rate-input"
          type="text"
          inputmode="decimal"
          class="input input-xs w-24"
          :value="localExchangeRate"
          :readonly="allPeriodsClosed"
          @input="localExchangeRate = ($event.target as HTMLInputElement).value"
          @blur="saveExchangeRate"
          @keydown.enter="saveExchangeRate"
        >
        <span class="text-xs text-base-content/60">GTQ = 1 USD</span>
      </template>
    </div>

    <!-- Include deleted checkbox -->
    <label class="flex items-center gap-2 cursor-pointer">
      <input
        data-testid="include-deleted-checkbox"
        type="checkbox"
        class="checkbox checkbox-sm"
        :checked="matrixStore.showDeleted"
        @change="matrixStore.setShowDeleted(($event.target as HTMLInputElement).checked)"
      >
      <span class="text-sm">{{ t('budgetMatrix.controls.includeDeleted') }}</span>
    </label>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useBudgetMatrixStore } from '../store'
import { useBudgetStructureStore } from '@/features/budget-structure/store'

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()
const structureStore = useBudgetStructureStore()

/** Local copy of exchange rate as string — avoids browser number-input intermediate-state issues.
 *  Kept in sync with matrixStore.exchangeRate via watch so changes from cycle maintenance reflect here. */
const localExchangeRate = ref<string>('')

watch(
  () => matrixStore.exchangeRate,
  (rate) => {
    if (rate !== null && rate !== undefined) {
      localExchangeRate.value = String(rate)
    }
  },
  { immediate: true },
)

/** True when every visible period is closed — makes the input read-only. */
const allPeriodsClosed = computed<boolean>(() =>
  matrixStore.allPeriods.length > 0 &&
  matrixStore.allPeriods.every((p) => p.isClosed),
)

/** Save the exchange rate: freshness guard → update → re-fetch → sync. */
async function saveExchangeRate(): Promise<void> {
  const parsed = parseFloat(localExchangeRate.value)
  if (!isFinite(parsed) || parsed <= 0) return

  const bId = matrixStore.budgetId
  const cId = matrixStore.cycleId
  const cycle = structureStore.currentCycle

  if (!bId || !cId || !cycle) return

  // 1. Freshness guard — re-fetch cycle before sending the update
  await structureStore.loadCycleDetail(bId, cId)

  const freshCycle = structureStore.currentCycle
  if (!freshCycle) return

  // 2. Update cycle with the new exchange rate
  await structureStore.updateCycle(bId, cId, {
    name: freshCycle.name,
    startDate: freshCycle.startDate,
    endDate: freshCycle.endDate,
    defaultCurrencyId: freshCycle.defaultCurrency?.id ?? '',
    alternateCurrencyId: freshCycle.alternateCurrency?.id ?? freshCycle.alternateCurrencyId ?? undefined,
    exchangeRate: parsed,
  })

  // 3. Re-fetch updated cycle so currentCycle reflects the new rate
  await structureStore.loadCycleDetail(bId, cId)

  // 4. Sync matrixStore.exchangeRate so useCurrencyDisplay re-computes
  matrixStore.syncExchangeRate()
}
</script>

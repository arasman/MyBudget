<template>
  <div class="card bg-base-200 p-4">
    <h3 class="text-sm font-semibold mb-3 text-base-content/70 uppercase tracking-wide">
      {{ t('currentSituation.totals.title') }}
    </h3>
    <table class="w-full text-sm">
      <thead>
        <tr class="text-xs text-base-content/40 border-b border-base-300">
          <th class="text-left font-medium pb-1" />
          <th class="text-right font-medium pb-1 pl-4">
            Q
          </th>
          <th
            v-if="hasAltRate"
            class="text-right font-medium pb-1 pl-4"
          >
            USD
          </th>
        </tr>
      </thead>
      <tbody>
        <!-- Assets -->
        <tr>
          <td class="py-1 text-success">
            {{ t('currentSituation.totals.positive') }}
          </td>
          <td class="py-1 text-right font-mono pl-4">
            {{ formatAmount(totals.totalPositive) }}
          </td>
          <td
            v-if="hasAltRate"
            class="py-1 text-right font-mono text-base-content/50 pl-4"
          >
            {{ formatAmount(totals.totalPositiveAlt) }}
          </td>
        </tr>
        <!-- Liabilities -->
        <tr>
          <td class="py-1 text-error">
            {{ t('currentSituation.totals.negative') }}
          </td>
          <td class="py-1 text-right font-mono pl-4">
            - {{ formatAmount(totals.totalNegative) }}
          </td>
          <td
            v-if="hasAltRate"
            class="py-1 text-right font-mono text-base-content/50 pl-4"
          >
            - {{ formatAmount(totals.totalNegativeAlt) }}
          </td>
        </tr>

        <!-- Budget execution section -->
        <tr class="border-t border-base-300">
          <td class="py-1 pt-3 text-base-content/70">
            {{ t('currentSituation.executionSummary.totalBudgeted') }}
          </td>
          <td class="py-1 pt-3 text-right font-mono pl-4">
            {{ formatAmount(executionSummary.totalBudgeted) }}
          </td>
          <td
            v-if="hasAltRate"
            class="py-1 pt-3 text-right font-mono text-base-content/50 pl-4"
          >
            {{ formatAmount(executionSummary.totalBudgeted / safeExchangeRate) }}
          </td>
        </tr>
        <tr>
          <td class="py-1 text-base-content/70">
            {{ t('currentSituation.executionSummary.totalRegistered') }}
          </td>
          <td class="py-1 text-right font-mono pl-4">
            {{ formatAmount(executionSummary.totalRegistered) }}
          </td>
          <td
            v-if="hasAltRate"
            class="py-1 text-right font-mono text-base-content/50 pl-4"
          >
            {{ formatAmount(executionSummary.totalRegistered / safeExchangeRate) }}
          </td>
        </tr>
        <!-- Budget Commitment -->
        <tr>
          <td class="py-1 text-error">
            {{ t('currentSituation.executionSummary.remaining') }}
          </td>
          <td class="py-1 text-right font-mono pl-4">
            - {{ formatAmount(executionSummary.remaining) }}
          </td>
          <td
            v-if="hasAltRate"
            class="py-1 text-right font-mono text-base-content/50 pl-4"
          >
            - {{ formatAmount(executionSummary.remaining / safeExchangeRate) }}
          </td>
        </tr>

        <!-- Summary section -->
        <tr class="border-t border-base-300">
          <td class="py-1 pt-3 text-success">
            {{ t('currentSituation.totals.totalAvailable') }}
          </td>
          <td class="py-1 pt-3 text-right font-mono pl-4">
            {{ formatAmount(totalAvailable) }}
          </td>
          <td
            v-if="hasAltRate"
            class="py-1 pt-3 text-right font-mono text-base-content/50 pl-4"
          >
            {{ formatAmount(totalAvailableAlt) }}
          </td>
        </tr>
        <!-- Total Debt -->
        <tr>
          <td class="py-1 text-error">
            {{ t('currentSituation.totals.deudaEnCurso') }}
          </td>
          <td class="py-1 text-right font-mono pl-4">
            - {{ formatAmount(totals.totalDeudaEnCurso) }}
          </td>
          <td
            v-if="hasAltRate"
            class="py-1 text-right font-mono text-base-content/50 pl-4"
          >
            - {{ formatAmount(totals.totalDeudaEnCursoAlt) }}
          </td>
        </tr>
        <!-- Total Net -->
        <tr class="border-t border-base-300 font-bold">
          <td
            class="py-1 pt-3"
            :class="totalNet > 0 ? 'text-success' : totalNet < 0 ? 'text-error' : 'text-base-content'"
          >
            {{ t('currentSituation.totals.totalNet') }}
          </td>
          <td
            class="py-1 pt-3 text-right font-mono pl-4"
            :class="totalNet > 0 ? 'text-success' : totalNet < 0 ? 'text-error' : 'text-base-content'"
          >
            {{ formatAmount(totalNet) }}
          </td>
          <td
            v-if="hasAltRate"
            class="py-1 pt-3 text-right font-mono text-base-content/50 pl-4"
          >
            {{ formatAmount(totalNetAlt) }}
          </td>
        </tr>
      </tbody>
    </table>
    <p class="text-xs text-base-content/40 mt-3">
      {{ t('currentSituation.totals.snapshotNotice') }}
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CutTotalsDto, BudgetExecutionSummaryDto } from '../types/cutRecord'

const props = defineProps<{
  totals: CutTotalsDto
  executionSummary: BudgetExecutionSummaryDto
  exchangeRate: number
}>()

const { t } = useI18n()

const safeExchangeRate = computed(() =>
  Number.isFinite(props.exchangeRate) && props.exchangeRate > 0 ? props.exchangeRate : 1,
)

const hasAltRate = computed(() => safeExchangeRate.value !== 1)

const totalAvailable = computed(() => props.totals.totalPositive)
const totalAvailableAlt = computed(() => props.totals.totalPositiveAlt)
const totalNet = computed(() => props.totals.totalPositive - props.totals.totalDeudaEnCurso)
const totalNetAlt = computed(() => props.totals.totalPositiveAlt - props.totals.totalDeudaEnCursoAlt)

function formatAmount(value: number): string {
  return value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
</script>

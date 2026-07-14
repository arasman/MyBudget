<template>
  <button
    v-if="periodStatus === 'Closed'"
    type="button"
    class="btn btn-xs btn-ghost btn-circle"
    :title="t('budgetExecution.refresh.label')"
    :disabled="loading"
    @click="handleRefresh"
  >
    <RefreshCw
      :size="12"
      :class="{ 'animate-spin': loading }"
    />
  </button>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { RefreshCw } from 'lucide-vue-next'
import { useBudgetMatrixStore } from '../store'

const props = defineProps<{
  periodId: string
  periodStatus: string
}>()

const { t } = useI18n()
const matrixStore = useBudgetMatrixStore()

const loading = computed(() => matrixStore.loadingPeriods[props.periodId] ?? false)

async function handleRefresh(): Promise<void> {
  await matrixStore.refreshPeriod(props.periodId)
}
</script>

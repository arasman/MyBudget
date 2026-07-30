<template>
  <dialog open class="modal modal-open">
    <div class="modal-box">
      <h3 class="font-bold text-lg mb-2">{{ t('currentSituation.loadStrategy.title') }}</h3>
      <p class="text-sm text-base-content/70 mb-4">
        {{ t('currentSituation.loadStrategy.subtitle', { date: targetDate }) }}
      </p>

      <!-- Strategy radio cards -->
      <div class="flex flex-col gap-3 mb-4">
        <!-- blank -->
        <label
          class="flex items-start gap-3 p-3 border rounded-lg cursor-pointer"
          :class="strategy === 'blank' ? 'border-primary bg-primary/5' : 'border-base-300'"
        >
          <input
            v-model="strategy"
            type="radio"
            value="blank"
            class="radio radio-primary mt-0.5"
          />
          <div>
            <div class="font-medium text-sm">{{ t('currentSituation.loadStrategy.blank') }}</div>
            <div class="text-xs text-base-content/60">
              {{ t('currentSituation.loadStrategy.blankHint') }}
            </div>
          </div>
        </label>

        <!-- clone -->
        <label
          class="flex items-start gap-3 p-3 border rounded-lg cursor-pointer"
          :class="strategy === 'clone' ? 'border-primary bg-primary/5' : 'border-base-300'"
        >
          <input
            v-model="strategy"
            type="radio"
            value="clone"
            class="radio radio-primary mt-0.5"
          />
          <div>
            <div class="font-medium text-sm">{{ t('currentSituation.loadStrategy.clone') }}</div>
            <div class="text-xs text-base-content/60">
              {{ t('currentSituation.loadStrategy.cloneHint') }}
            </div>
          </div>
        </label>

        <!-- from-date -->
        <label
          class="flex items-start gap-3 p-3 border rounded-lg"
          :class="[
            cutDates.length === 0 ? 'cursor-not-allowed opacity-50' : 'cursor-pointer',
            strategy === 'from-date' ? 'border-primary bg-primary/5' : 'border-base-300',
          ]"
        >
          <input
            v-model="strategy"
            type="radio"
            value="from-date"
            class="radio radio-primary mt-0.5"
            :disabled="cutDates.length === 0"
          />
          <div class="flex-1">
            <div class="font-medium text-sm">
              {{ t('currentSituation.loadStrategy.fromDate') }}
            </div>
            <div class="text-xs text-base-content/60 mb-2">
              {{ t('currentSituation.loadStrategy.fromDateHint') }}
            </div>
            <select
              v-if="strategy === 'from-date'"
              v-model="sourceDate"
              class="select select-bordered select-sm w-full"
              :disabled="cutDates.length === 0"
            >
              <option value="" disabled>
                {{ t('currentSituation.loadStrategy.selectDate') }}
              </option>
              <option v-for="d in sortedDates" :key="d" :value="d">{{ d }}</option>
            </select>
            <span v-if="cutDates.length === 0" class="text-xs text-base-content/50">
              {{ t('currentSituation.loadStrategy.noExistingDates') }}
            </span>
          </div>
        </label>
      </div>

      <div class="flex justify-end gap-2">
        <button class="btn btn-ghost" @click="emit('cancel')">
          {{ t('common.cancel') }}
        </button>
        <button
          class="btn btn-primary"
          :disabled="!canConfirm || loading"
          @click="handleConfirm"
        >
          <span v-if="loading" class="loading loading-spinner loading-xs"></span>
          {{ t('currentSituation.loadStrategy.load') }}
        </button>
      </div>
    </div>
    <form method="dialog" class="modal-backdrop" @click="emit('cancel')">
      <button>close</button>
    </form>
  </dialog>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps<{
  targetDate: string
  cutDates: string[]
  loading: boolean
}>()

const emit = defineEmits<{
  select: [strategy: 'blank' | 'clone' | 'from-date', sourceDate?: string]
  cancel: []
}>()

const { t } = useI18n()

const strategy = ref<'blank' | 'clone' | 'from-date'>('clone')
const sourceDate = ref('')

const sortedDates = computed(() => [...props.cutDates].sort((a, b) => b.localeCompare(a)))

const canConfirm = computed(() => strategy.value !== 'from-date' || sourceDate.value !== '')

function handleConfirm(): void {
  emit('select', strategy.value, strategy.value === 'from-date' ? sourceDate.value : undefined)
}
</script>

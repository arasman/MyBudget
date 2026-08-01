<template>
  <div class="card bg-base-200 p-4">
    <div class="flex items-center justify-between mb-2 gap-2">
      <h3 class="text-sm font-semibold text-base-content/70 uppercase tracking-wide">
        {{ t('dashboard.seriesPicker.title') }}
      </h3>
      <div class="flex gap-1 shrink-0">
        <button type="button" class="btn btn-ghost btn-xs" @click="selectAll">
          {{ t('dashboard.seriesPicker.selectAll') }}
        </button>
        <button type="button" class="btn btn-ghost btn-xs" @click="clearAll">
          {{ t('dashboard.seriesPicker.clearAll') }}
        </button>
      </div>
    </div>
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-x-3 gap-y-1 max-h-64 overflow-y-auto pr-1">
      <label
        v-for="key in totalKeys"
        :key="key"
        class="label cursor-pointer justify-start gap-2 py-1"
      >
        <input
          type="checkbox"
          class="checkbox checkbox-sm"
          :aria-label="t(`dashboard.series.${key}`)"
          :checked="isSelected(key)"
          @change="toggle(key)"
        />
        <span class="label-text text-sm">{{ t(`dashboard.series.${key}`) }}</span>
      </label>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { TOTAL_KEYS, type TotalKey } from '../types/dashboard'

const props = defineProps<{ modelValue: TotalKey[] }>()
const emit = defineEmits<{ 'update:modelValue': [value: TotalKey[]] }>()

const { t } = useI18n()
const totalKeys = TOTAL_KEYS

function isSelected(key: TotalKey): boolean {
  return props.modelValue.includes(key)
}

function toggle(key: TotalKey): void {
  const next = isSelected(key) ? props.modelValue.filter((k) => k !== key) : [...props.modelValue, key]
  emit('update:modelValue', next)
}

function selectAll(): void {
  emit('update:modelValue', [...TOTAL_KEYS])
}

function clearAll(): void {
  emit('update:modelValue', [])
}
</script>

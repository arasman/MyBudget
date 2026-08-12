<template>
  <div class="card bg-base-200 p-4">
    <div class="flex items-center justify-between mb-2 gap-2">
      <h3 class="text-sm font-semibold text-base-content/70 uppercase tracking-wide">
        {{ t('dashboard.linePicker.title') }}
      </h3>
      <div class="flex gap-1 shrink-0">
        <button
          type="button"
          class="btn btn-ghost btn-xs"
          @click="selectAll"
        >
          {{ t('dashboard.linePicker.selectAll') }}
        </button>
        <button
          type="button"
          class="btn btn-ghost btn-xs"
          @click="clearAll"
        >
          {{ t('dashboard.linePicker.clearAll') }}
        </button>
      </div>
    </div>
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-x-3 gap-y-1 max-h-64 overflow-y-auto pr-1">
      <label
        v-for="line in lines"
        :key="line.id"
        class="label cursor-pointer justify-start gap-2 py-1"
      >
        <input
          type="checkbox"
          class="checkbox checkbox-sm"
          :aria-label="line.name"
          :checked="isSelected(line.id)"
          @change="toggle(line.id)"
        >
        <span class="label-text text-sm truncate">{{ line.name }}</span>
      </label>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'

/** Minimal shape this picker needs — the caller passes budget-structure's `BudgetLineResponse[]` as-is. */
export interface BudgetLinePickerItem {
  id: string
  name: string
}

const props = defineProps<{ lines: BudgetLinePickerItem[]; modelValue: string[] }>()
const emit = defineEmits<{ 'update:modelValue': [value: string[]] }>()

const { t } = useI18n()

function isSelected(id: string): boolean {
  return props.modelValue.includes(id)
}

function toggle(id: string): void {
  const next = isSelected(id) ? props.modelValue.filter((v) => v !== id) : [...props.modelValue, id]
  emit('update:modelValue', next)
}

function selectAll(): void {
  emit('update:modelValue', props.lines.map((l) => l.id))
}

function clearAll(): void {
  emit('update:modelValue', [])
}
</script>

<template>
  <dialog class="modal modal-open">
    <div class="modal-box w-full max-w-md">
      <h3 class="font-bold text-lg mb-4">
        {{
          modelValue
            ? t('budgetStructure.categoryGroups.edit')
            : t('budgetStructure.categoryGroups.create')
        }}
      </h3>

      <form @submit.prevent="handleSubmit">
        <!-- Name -->
        <div class="form-control mb-6">
          <label
            class="label"
            for="group-name"
          >
            <span class="label-text">{{ t('budgetStructure.categoryGroups.name') }}</span>
          </label>
          <input
            id="group-name"
            v-model="form.name"
            type="text"
            class="input input-bordered w-full"
            :class="{ 'input-error': errors.name }"
            maxlength="200"
          >
          <div
            v-if="errors.name"
            class="label"
          >
            <span class="label-text-alt text-error">{{ errors.name }}</span>
          </div>
        </div>

        <div class="modal-action">
          <button
            type="button"
            class="btn btn-ghost"
            @click="emit('cancel')"
          >
            {{ t('budgetStructure.common.cancel') }}
          </button>
          <button
            type="submit"
            class="btn btn-primary"
          >
            {{ t('budgetStructure.common.save') }}
          </button>
        </div>
      </form>
    </div>
    <div
      class="modal-backdrop"
      @click="emit('cancel')"
    />
  </dialog>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CategoryGroupResponse } from '../types'

interface GroupFormPayload {
  name: string
}

const props = defineProps<{
  modelValue: CategoryGroupResponse | null
}>()

const emit = defineEmits<{
  submit: [payload: GroupFormPayload]
  cancel: []
}>()

const { t } = useI18n()

const form = reactive({ name: '' })
const errors = reactive({ name: '' })

watch(
  () => props.modelValue,
  (group) => {
    form.name = group ? group.name : ''
    errors.name = ''
  },
  { immediate: true },
)

function validate(): boolean {
  if (!form.name.trim()) {
    errors.name = t('budgetStructure.categoryGroups.validation.nameRequired')
  } else if (form.name.trim().length > 200) {
    errors.name = t('budgetStructure.categoryGroups.validation.nameTooLong')
  } else {
    errors.name = ''
  }
  return !errors.name
}

function handleSubmit(): void {
  if (!validate()) return
  emit('submit', { name: form.name.trim() })
}
</script>

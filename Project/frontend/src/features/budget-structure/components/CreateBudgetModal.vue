<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { createBudget } from '../api/budgets.api'

const emit = defineEmits<{
  created: [{ id: string; name: string }]
}>()

const { t } = useI18n()

const modal = ref<HTMLDialogElement>()

const form = reactive({
  name: '',
})

const nameError = ref<string | null>(null)
const serverError = ref<string | null>(null)
const isSubmitting = ref(false)

function open() {
  form.name = ''
  nameError.value = null
  serverError.value = null
  modal.value?.showModal()
}

function close() {
  modal.value?.close()
}

defineExpose({ open })

async function onSubmit() {
  nameError.value = null
  serverError.value = null

  const trimmed = form.name.trim()
  if (!trimmed) {
    nameError.value = t('budgetStructure.selection.budgetNameRequired')
    return
  }
  if (trimmed.length > 200) {
    nameError.value = t('budgetStructure.selection.budgetNameTooLong')
    return
  }

  isSubmitting.value = true
  try {
    const result = await createBudget(trimmed)
    emit('created', result)
    close()
  } catch (err: unknown) {
    const axiosErr = err as { response?: { data?: { detail?: string }; status?: number } }
    const detail = axiosErr.response?.data?.detail ?? ''
    if (detail.includes('BUDGET_NAME_TOO_LONG')) {
      nameError.value = t('budgetStructure.selection.budgetNameTooLong')
    } else if (detail.includes('BUDGET_NAME_REQUIRED')) {
      nameError.value = t('budgetStructure.selection.budgetNameRequired')
    } else {
      serverError.value = t('common.error')
    }
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <dialog ref="modal" class="modal">
    <div class="modal-box">
      <h3 class="font-bold text-lg mb-4">{{ t('budgetStructure.selection.createBudgetTitle') }}</h3>

      <!-- Server error -->
      <div v-if="serverError" role="alert" class="alert alert-error mb-4">
        <span>{{ serverError }}</span>
      </div>

      <form @submit.prevent="onSubmit" class="space-y-4" novalidate>
        <!-- Budget name -->
        <div class="form-control">
          <label class="label" for="budget-name">
            <span class="label-text">{{ t('budgetStructure.selection.budgetNameLabel') }}</span>
          </label>
          <input
            id="budget-name"
            v-model="form.name"
            type="text"
            class="input input-bordered w-full"
            :class="{ 'input-error': nameError }"
            :placeholder="t('budgetStructure.selection.budgetNamePlaceholder')"
            maxlength="200"
            autocomplete="off"
            required
          />
          <label v-if="nameError" class="label">
            <span class="label-text-alt text-error">{{ nameError }}</span>
          </label>
        </div>

        <div class="modal-action">
          <button type="button" class="btn btn-ghost" @click="close">
            {{ t('common.cancel') }}
          </button>
          <button type="submit" class="btn btn-primary" :disabled="isSubmitting">
            <span v-if="isSubmitting" class="loading loading-spinner loading-sm" />
            {{ t('budgetStructure.selection.createBudget') }}
          </button>
        </div>
      </form>
    </div>
    <form method="dialog" class="modal-backdrop">
      <button>close</button>
    </form>
  </dialog>
</template>

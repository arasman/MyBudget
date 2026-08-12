<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth.store'
import { useToastStore } from '@/stores/toast.store'

const { t } = useI18n()
const authStore = useAuthStore()
const toast = useToastStore()

const modal = ref<HTMLDialogElement>()

const form = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
})

const isSubmitting = ref(false)
const currentPasswordError = ref<string | null>(null)
const validationError = ref<string | null>(null)

function open() {
  form.currentPassword = ''
  form.newPassword = ''
  form.confirmPassword = ''
  currentPasswordError.value = null
  validationError.value = null
  modal.value?.showModal()
}

function close() {
  modal.value?.close()
}

defineExpose({ open })

async function onSubmit() {
  currentPasswordError.value = null
  validationError.value = null

  if (form.newPassword.length < 8) {
    validationError.value = t('auth.password.passwordTooShort')
    return
  }
  if (form.newPassword !== form.confirmPassword) {
    validationError.value = t('auth.password.passwordMismatch')
    return
  }

  isSubmitting.value = true
  try {
    await authStore.changePassword(form.currentPassword, form.newPassword)
    toast.push({ type: 'success', title: t('auth.password.changeSuccess') })
    close()
  } catch (err: unknown) {
    const axiosErr = err as { response?: { data?: { detail?: string } } }
    const detail = axiosErr.response?.data?.detail ?? ''
    if (detail.includes('PWD_CURRENT_INCORRECT')) {
      currentPasswordError.value = t('auth.password.currentIncorrect')
    } else if (detail.includes('PWD_SAME_AS_CURRENT')) {
      validationError.value = t('auth.password.sameAsCurrent')
    } else if (detail.includes('PWD_PREVIOUSLY_USED')) {
      validationError.value = t('auth.password.previouslyUsed')
    } else {
      validationError.value = t('common.error')
    }
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <dialog
    ref="modal"
    class="modal"
  >
    <div class="modal-box">
      <h3 class="font-bold text-lg mb-4">
        {{ t('auth.password.changeTitle') }}
      </h3>

      <div
        v-if="validationError"
        role="alert"
        class="alert alert-error mb-4"
      >
        <span>{{ validationError }}</span>
      </div>

      <form
        class="space-y-4"
        novalidate
        @submit.prevent="onSubmit"
      >
        <!-- Current password -->
        <div class="form-control">
          <label class="label">
            <span class="label-text">{{ t('auth.password.currentPasswordLabel') }}</span>
          </label>
          <input
            v-model="form.currentPassword"
            type="password"
            class="input input-bordered w-full"
            :class="{ 'input-error': currentPasswordError }"
            autocomplete="current-password"
            required
          >
          <label
            v-if="currentPasswordError"
            class="label"
          >
            <span class="label-text-alt text-error">{{ currentPasswordError }}</span>
          </label>
        </div>

        <!-- New password -->
        <div class="form-control">
          <label class="label">
            <span class="label-text">{{ t('auth.password.newPasswordLabel') }}</span>
          </label>
          <input
            v-model="form.newPassword"
            type="password"
            class="input input-bordered w-full"
            autocomplete="new-password"
            required
          >
        </div>

        <!-- Confirm new password -->
        <div class="form-control">
          <label class="label">
            <span class="label-text">{{ t('auth.password.confirmPasswordLabel') }}</span>
          </label>
          <input
            v-model="form.confirmPassword"
            type="password"
            class="input input-bordered w-full"
            autocomplete="new-password"
            required
          >
        </div>

        <div class="modal-action">
          <button
            type="submit"
            class="btn btn-primary"
            :disabled="isSubmitting"
          >
            <span
              v-if="isSubmitting"
              class="loading loading-spinner loading-sm"
            />
            {{ t('auth.password.changeSubmit') }}
          </button>
          <button
            type="button"
            class="btn"
            @click="close"
          >
            {{ t('common.cancel') }}
          </button>
        </div>
      </form>
    </div>
    <form
      method="dialog"
      class="modal-backdrop"
    >
      <button>close</button>
    </form>
  </dialog>
</template>

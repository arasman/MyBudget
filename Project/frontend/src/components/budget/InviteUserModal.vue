<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { z } from 'zod'
import http from '@/api/axios'

const props = defineProps<{ budgetId: string }>()
const emit = defineEmits<{ invited: [] }>()

const { t } = useI18n()

const modal = ref<HTMLDialogElement>()

const form = reactive({
  email: '',
  role: 'operator' as 'admin' | 'operator' | 'read-only',
})

const fieldErrors = reactive<Record<string, string>>({})
const serverError = ref<string | null>(null)
const isSubmitting = ref(false)
const successMessage = ref<string | null>(null)

// Zod schema for client-side email validation
const emailSchema = z.string().email()

function clearState() {
  Object.keys(fieldErrors).forEach((k) => delete fieldErrors[k])
  serverError.value = null
  successMessage.value = null
}

function open() {
  form.email = ''
  form.role = 'operator'
  clearState()
  modal.value?.showModal()
}

function close() {
  modal.value?.close()
}

async function onSubmit() {
  clearState()

  // Client-side Zod validation — prevent unnecessary API call
  const emailResult = emailSchema.safeParse(form.email)
  if (!emailResult.success) {
    fieldErrors['email'] = emailResult.error.errors[0]?.message ?? 'Invalid email'
    return
  }

  isSubmitting.value = true

  try {
    await http.post(`/api/budgets/${props.budgetId}/invitations`, {
      email: form.email,
      role: form.role,
    })
    successMessage.value = t('invitation.modal.successMessage')
    emit('invited')
    // Auto-close after short delay
    setTimeout(() => close(), 1500)
  } catch (err: unknown) {
    const axiosError = err as { response?: { status: number; data?: { error?: string } } }
    const errorCode = axiosError.response?.data?.error

    if (errorCode === 'AUTH_ALREADY_MEMBER') {
      serverError.value = t('invitation.modal.error.alreadyMember')
    } else if (axiosError.response?.status === 422) {
      serverError.value = t('invitation.modal.error.ownerRoleForbidden')
    } else if (axiosError.response?.status === 403) {
      serverError.value = t('invitation.modal.error.forbidden')
    } else {
      serverError.value = t('common.error')
    }
  } finally {
    isSubmitting.value = false
  }
}

defineExpose({ open })
</script>

<template>
  <dialog
    ref="modal"
    class="modal"
  >
    <div class="modal-box">
      <h3 class="font-bold text-lg mb-4">
        {{ t('invitation.modal.title') }}
      </h3>

      <!-- Success message -->
      <div
        v-if="successMessage"
        class="alert alert-success mb-4"
      >
        <span>{{ successMessage }}</span>
      </div>

      <!-- Server error -->
      <div
        v-if="serverError"
        class="alert alert-error mb-4"
      >
        <span>{{ serverError }}</span>
      </div>

      <form
        class="space-y-4"
        novalidate
        @submit.prevent="onSubmit"
      >
        <!-- Email -->
        <div class="form-control">
          <label class="label">
            <span class="label-text">{{ t('invitation.modal.emailLabel') }}</span>
          </label>
          <input
            v-model="form.email"
            type="email"
            class="input input-bordered w-full"
            :class="{ 'input-error': fieldErrors['email'] }"
            placeholder="invitee@example.com"
            required
          >
          <label
            v-if="fieldErrors['email']"
            class="label"
          >
            <span class="label-text-alt text-error">{{ fieldErrors['email'] }}</span>
          </label>
        </div>

        <!-- Role -->
        <div class="form-control">
          <label class="label">
            <span class="label-text">{{ t('invitation.modal.roleLabel') }}</span>
          </label>
          <select
            v-model="form.role"
            class="select select-bordered w-full"
          >
            <option value="admin">
              {{ t('enums.role.admin') }}
            </option>
            <option value="operator">
              {{ t('enums.role.operator') }}
            </option>
            <option value="read-only">
              {{ t('enums.role.readOnly') }}
            </option>
          </select>
        </div>

        <div class="modal-action">
          <button
            type="button"
            class="btn btn-ghost"
            @click="close"
          >
            {{ t('common.cancel') }}
          </button>
          <button
            type="submit"
            class="btn btn-primary"
            :disabled="isSubmitting"
          >
            <span
              v-if="isSubmitting"
              class="loading loading-spinner loading-sm"
            />
            {{ t('invitation.modal.submit') }}
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

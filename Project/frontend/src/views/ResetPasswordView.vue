<script setup lang="ts">
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth.store'

const { t } = useI18n()
const route = useRoute()
const authStore = useAuthStore()

const token = (route.query.token as string) ?? ''
const email = (route.query.email as string) ?? ''

const newPassword = ref('')
const confirmPassword = ref('')
const isSubmitting = ref(false)
const submitted = ref(false)
const validationError = ref<string | null>(null)
const tokenError = ref(false)

async function onSubmit() {
  validationError.value = null
  tokenError.value = false

  if (newPassword.value.length < 8) {
    validationError.value = t('auth.password.passwordTooShort')
    return
  }
  if (newPassword.value !== confirmPassword.value) {
    validationError.value = t('auth.password.passwordMismatch')
    return
  }

  isSubmitting.value = true
  try {
    await authStore.resetPassword(token, email, newPassword.value)
    submitted.value = true
  } catch (err: unknown) {
    const axiosErr = err as { response?: { data?: { detail?: string; error?: string } } }
    const detail = axiosErr.response?.data?.detail ?? axiosErr.response?.data?.error ?? ''
    if (detail.includes('PWD_TOKEN_INVALID') || detail.includes('PWD_TOKEN_EXPIRED')) {
      tokenError.value = true
    } else if (detail.includes('PWD_SAME_AS_CURRENT')) {
      validationError.value = t('auth.password.sameAsCurrent')
    } else if (detail.includes('PWD_PREVIOUSLY_USED')) {
      validationError.value = t('auth.password.previouslyUsed')
    } else {
      validationError.value = t('auth.password.tokenInvalid')
    }
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <h1 class="card-title text-2xl justify-center mb-4">{{ t('auth.password.resetTitle') }}</h1>

  <div v-if="submitted" role="status" class="alert alert-success mb-4">
    <div>
      <span>{{ t('auth.password.resetSuccess') }}</span>
      <p class="mt-2">
        <router-link to="/login" class="link link-primary">
          {{ t('auth.login.submit') }}
        </router-link>
      </p>
    </div>
  </div>

  <div v-else-if="tokenError" role="alert" class="alert alert-error mb-4">
    <div>
      <span>{{ t('auth.password.tokenInvalid') }}</span>
      <p class="mt-2">
        <router-link to="/forgot-password" class="link">
          {{ t('auth.password.sendLink') }}
        </router-link>
      </p>
    </div>
  </div>

  <template v-else>
    <div v-if="validationError" role="alert" class="alert alert-error mb-4">
      <span>{{ validationError }}</span>
    </div>

    <form @submit.prevent="onSubmit" class="space-y-4" novalidate>
      <div class="form-control">
        <label class="label">
          <span class="label-text">{{ t('auth.password.newPasswordLabel') }}</span>
        </label>
        <input
          v-model="newPassword"
          type="password"
          class="input input-bordered w-full"
          autocomplete="new-password"
          required
        />
      </div>

      <div class="form-control">
        <label class="label">
          <span class="label-text">{{ t('auth.password.confirmPasswordLabel') }}</span>
        </label>
        <input
          v-model="confirmPassword"
          type="password"
          class="input input-bordered w-full"
          autocomplete="new-password"
          required
        />
      </div>

      <button type="submit" class="btn btn-primary w-full" :disabled="isSubmitting">
        <span v-if="isSubmitting" class="loading loading-spinner loading-sm" />
        {{ t('auth.password.resetSubmit') }}
      </button>
    </form>
  </template>
</template>

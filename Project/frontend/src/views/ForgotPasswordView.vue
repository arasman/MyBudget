<script setup lang="ts">
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth.store'

const { t } = useI18n()
const route = useRoute()
const authStore = useAuthStore()

const email = ref('')
const isSubmitting = ref(false)
const submitted = ref(false)

const isForceChange = route.query.reason === 'force'

async function onSubmit() {
  isSubmitting.value = true
  try {
    await authStore.requestPasswordReset(email.value)
  } catch {
    // Always show success state — anti-enumeration
  } finally {
    isSubmitting.value = false
    submitted.value = true
  }
}
</script>

<template>
  <h1 class="card-title text-2xl justify-center mb-4">{{ t('auth.password.forgotTitle') }}</h1>

  <div v-if="isForceChange" role="alert" class="alert alert-warning mb-4">
    <span>{{ t('auth.password.forceChangeNotice') }}</span>
  </div>

  <div v-if="submitted" role="status" class="alert alert-success mb-4">
    <span>{{ t('auth.password.linkSent') }}</span>
  </div>

  <template v-else>
    <p class="text-sm text-base-content/70 mb-4">{{ t('auth.password.forgotDescription') }}</p>

    <form @submit.prevent="onSubmit" class="space-y-4" novalidate>
      <div class="form-control">
        <label class="label">
          <span class="label-text">{{ t('auth.password.emailLabel') }}</span>
        </label>
        <input
          v-model="email"
          type="email"
          class="input input-bordered w-full"
          autocomplete="email"
          required
        />
      </div>

      <button type="submit" class="btn btn-primary w-full" :disabled="isSubmitting">
        <span v-if="isSubmitting" class="loading loading-spinner loading-sm" />
        {{ t('auth.password.sendLink') }}
      </button>
    </form>
  </template>

  <p class="text-center text-sm mt-4">
    <router-link to="/login" class="link link-primary">
      {{ t('auth.login.submit') }}
    </router-link>
  </p>
</template>

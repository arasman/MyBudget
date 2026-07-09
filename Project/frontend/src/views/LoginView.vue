<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth.store'

const { t } = useI18n()
const router = useRouter()
const authStore = useAuthStore()

const form = reactive({ email: '', password: '' })
const error = ref<string | null>(null)
const isSubmitting = ref(false)

async function onSubmit() {
  error.value = null
  isSubmitting.value = true
  try {
    await authStore.login(form.email, form.password)
    router.push('/')
  } catch {
    error.value = t('auth.login.error.invalidCredentials')
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-base-200 px-4">
    <div class="card w-full max-w-md bg-base-100 shadow-xl">
      <div class="card-body">
        <h1 class="card-title text-2xl justify-center mb-4">{{ t('auth.login.title') }}</h1>

        <div v-if="error" role="alert" class="alert alert-error mb-4">
          <span>{{ error }}</span>
        </div>

        <form @submit.prevent="onSubmit" class="space-y-4" novalidate>
          <div class="form-control">
            <label class="label">
              <span class="label-text">{{ t('auth.emailLabel') }}</span>
            </label>
            <input
              v-model="form.email"
              type="email"
              class="input input-bordered"
              :placeholder="t('auth.login.emailPlaceholder')"
              autocomplete="email"
              required
            />
          </div>

          <div class="form-control">
            <label class="label">
              <span class="label-text">{{ t('auth.passwordLabel') }}</span>
            </label>
            <input
              v-model="form.password"
              type="password"
              class="input input-bordered"
              :placeholder="t('auth.login.passwordPlaceholder')"
              autocomplete="current-password"
              required
            />
          </div>

          <button type="submit" class="btn btn-primary w-full" :disabled="isSubmitting">
            <span v-if="isSubmitting" class="loading loading-spinner loading-sm" />
            {{ t('auth.login.submit') }}
          </button>
        </form>

        <p class="text-center text-sm mt-4">
          <router-link to="/register" class="link link-primary">
            {{ t('auth.login.registerLink') }}
          </router-link>
        </p>
      </div>
    </div>
  </div>
</template>

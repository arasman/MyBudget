<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth.store'
import PasswordStrength from '@/components/PasswordStrength.vue'

const { t } = useI18n()
const router = useRouter()
const authStore = useAuthStore()

const form = reactive({
  email: '',
  password: '',
  firstName: '',
  lastName: '',
  preferredLocale: 'en' as 'en' | 'es',
})

const fieldErrors = reactive<Record<string, string>>({})
const globalError = ref<string | null>(null)
const isSubmitting = ref(false)

function clearErrors() {
  Object.keys(fieldErrors).forEach((k) => delete fieldErrors[k])
  globalError.value = null
}

async function onSubmit() {
  clearErrors()
  isSubmitting.value = true

  try {
    await authStore.register({
      email: form.email,
      password: form.password,
      firstName: form.firstName,
      lastName: form.lastName,
      preferredLocale: form.preferredLocale,
    })
    router.push('/')
  } catch (err: unknown) {
    const axiosError = err as { response?: { status: number; data?: { detail?: string; errors?: Record<string, string[]> } } }
    const status = axiosError.response?.status
    const data = axiosError.response?.data
    if (status === 422) {
      const fieldErrs = data?.errors ?? {}
      if (Object.keys(fieldErrs).length > 0) {
        for (const [field, messages] of Object.entries(fieldErrs)) {
          fieldErrors[field.toLowerCase()] = Array.isArray(messages) ? messages[0] : String(messages)
        }
      } else {
        const detail = data?.detail ?? ''
        globalError.value = detail.includes('AUTH_PASSWORD_TOO_WEAK')
          ? t('auth.register.passwordTooWeak')
          : t('common.error')
      }
    } else if (status === 409) {
      globalError.value = t('auth.register.emailTaken')
    } else {
      globalError.value = t('common.error')
    }
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <h1 class="card-title text-2xl justify-center mb-4">{{ t('auth.register.title') }}</h1>

  <div v-if="globalError" role="alert" class="alert alert-error mb-4">
    <span>{{ globalError }}</span>
  </div>

  <form @submit.prevent="onSubmit" class="space-y-4" novalidate>
    <!-- First Name -->
    <div class="form-control">
      <label class="label">
        <span class="label-text">{{ t('auth.register.firstNamePlaceholder') }}</span>
      </label>
      <input
        v-model="form.firstName"
        type="text"
        class="input input-bordered w-full"
        :class="{ 'input-error': fieldErrors['firstname'] }"
        :placeholder="t('auth.register.firstNamePlaceholder')"
        autocomplete="given-name"
        required
      />
      <label v-if="fieldErrors['firstname']" class="label">
        <span class="label-text-alt text-error">{{ fieldErrors['firstname'] }}</span>
      </label>
    </div>

    <!-- Last Name -->
    <div class="form-control">
      <label class="label">
        <span class="label-text">{{ t('auth.register.lastNamePlaceholder') }}</span>
      </label>
      <input
        v-model="form.lastName"
        type="text"
        class="input input-bordered w-full"
        :class="{ 'input-error': fieldErrors['lastname'] }"
        :placeholder="t('auth.register.lastNamePlaceholder')"
        autocomplete="family-name"
        required
      />
      <label v-if="fieldErrors['lastname']" class="label">
        <span class="label-text-alt text-error">{{ fieldErrors['lastname'] }}</span>
      </label>
    </div>

    <!-- Email -->
    <div class="form-control">
      <label class="label">
        <span class="label-text">{{ t('auth.emailLabel') }}</span>
      </label>
      <input
        v-model="form.email"
        type="email"
        class="input input-bordered w-full"
        :class="{ 'input-error': fieldErrors['email'] }"
        :placeholder="t('auth.register.emailPlaceholder')"
        autocomplete="email"
        required
      />
      <label v-if="fieldErrors['email']" class="label">
        <span class="label-text-alt text-error">{{ fieldErrors['email'] }}</span>
      </label>
    </div>

    <!-- Password -->
    <div class="form-control">
      <label class="label">
        <span class="label-text">{{ t('auth.passwordLabel') }}</span>
      </label>
      <input
        v-model="form.password"
        type="password"
        class="input input-bordered w-full"
        :class="{ 'input-error': fieldErrors['password'] }"
        :placeholder="t('auth.register.passwordPlaceholder')"
        autocomplete="new-password"
        required
      />
      <PasswordStrength :password="form.password" />
      <label v-if="fieldErrors['password']" class="label">
        <span class="label-text-alt text-error">{{ fieldErrors['password'] }}</span>
      </label>
    </div>

    <!-- Preferred Locale -->
    <div class="form-control">
      <label class="label">
        <span class="label-text">{{ t('auth.register.languageLabel') }}</span>
      </label>
      <select v-model="form.preferredLocale" class="select select-bordered w-full">
        <option value="en">English</option>
        <option value="es">Español</option>
      </select>
    </div>

    <button
      type="submit"
      class="btn btn-primary w-full"
      :disabled="isSubmitting"
    >
      <span v-if="isSubmitting" class="loading loading-spinner loading-sm" />
      {{ t('auth.register.submit') }}
    </button>
  </form>

  <p class="text-center text-sm mt-4">
    <router-link to="/login" class="link link-primary">
      {{ t('auth.register.loginLink') }}
    </router-link>
  </p>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth.store'
import http from '@/api/axios'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const status = ref<'loading' | 'success' | 'error'>('loading')
const errorKey = ref<string>('common.error')
const acceptedBudgetId = ref<string | null>(null)

onMounted(async () => {
  const token = route.query['token'] as string | undefined

  if (!token) {
    errorKey.value = 'common.error'
    status.value = 'error'
    return
  }

  // If not authenticated, redirect to login with redirect URL preserved
  if (!authStore.isAuthenticated) {
    const redirectUrl = `/invitations/accept?token=${encodeURIComponent(token)}`
    router.push(`/login?redirect=${encodeURIComponent(redirectUrl)}`)
    return
  }

  try {
    const { data } = await http.post<{ budgetId: string; role: string }>(
      '/api/auth/invitations/accept',
      { token },
    )
    acceptedBudgetId.value = data.budgetId
    status.value = 'success'
  } catch (err: unknown) {
    const axiosError = err as { response?: { status: number; data?: { detail?: string } } }
    const detail = axiosError.response?.data?.detail ?? ''

    if (detail === 'AUTH_INVITATION_EXPIRED') {
      errorKey.value = 'invitation.accept.error.expired'
    } else if (detail === 'AUTH_INVITATION_ALREADY_USED') {
      errorKey.value = 'invitation.accept.error.alreadyUsed'
    } else if (detail === 'AUTH_INVITATION_EMAIL_MISMATCH') {
      errorKey.value = 'invitation.accept.error.mismatch'
    } else {
      errorKey.value = 'common.error'
    }
    status.value = 'error'
  }
})
</script>

<template>
  <div class="text-center">
    <h1 class="card-title text-2xl justify-center mb-4">
      {{ t('invitation.accept.title') }}
    </h1>

    <!-- Loading -->
    <div
      v-if="status === 'loading'"
      class="flex flex-col items-center gap-4"
    >
      <span class="loading loading-spinner loading-lg" />
      <p class="text-base-content/70">
        {{ t('invitation.accept.loading') }}
      </p>
    </div>

    <!-- Success -->
    <div
      v-else-if="status === 'success'"
      class="space-y-4"
    >
      <div class="alert alert-success">
        <span>{{ t('invitation.accept.successMessage') }}</span>
      </div>
      <router-link
        to="/"
        class="btn btn-primary w-full"
      >
        Go to Dashboard
      </router-link>
    </div>

    <!-- Error -->
    <div
      v-else
      class="alert alert-error"
    >
      <span>{{ t(errorKey) }}</span>
    </div>
  </div>
</template>

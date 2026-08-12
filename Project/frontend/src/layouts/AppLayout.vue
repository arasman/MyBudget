<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth.store'
import { useLayoutStore } from '@/stores/layout.store'
import { useNotificationStore } from '@/stores/notification.store'
import type { PageAction } from '@/stores/layout.store'
import { toRoleKey } from '@/utils/enum-key'
import ChangePasswordModal from '@/components/auth/ChangePasswordModal.vue'
import AppToast from '@/components/AppToast.vue'
import LanguageSwitcher from '@/components/LanguageSwitcher.vue'

const router = useRouter()
const route = useRoute()
const { t } = useI18n()
const authStore = useAuthStore()
const layoutStore = useLayoutStore()
const notificationStore = useNotificationStore()

// On mount: restore activeBudgetName from memberships when missing (e.g. after page reload)
onMounted(() => {
  if (!layoutStore.activeBudgetName) {
    const budgetId = route.params['budgetId']
    if (typeof budgetId === 'string' && authStore.user) {
      const membership = authStore.user.memberships.find((m) => m.budgetId === budgetId)
      if (membership && !membership.isDeleted) {
        layoutStore.setActiveBudget(budgetId, membership.budgetName)
      }
    }
  }
})

const changePasswordModal = ref<InstanceType<typeof ChangePasswordModal>>()

// User initials derived from firstName + lastName
const userInitials = computed(() => {
  const user = authStore.user
  if (!user) return '?'
  return `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase()
})

// Active membership (matching activeBudgetId)
const activeMembership = computed(() => {
  const user = authStore.user
  const budgetId = layoutStore.activeBudgetId
  if (!user || !budgetId) return null
  return user.memberships.find((m) => m.budgetId === budgetId) ?? null
})

// Role badge label for the active membership (translated via i18n)
const activeRoleBadge = computed(() => {
  const role = activeMembership.value?.role
  if (!role) return null
  return t('enums.role.' + toRoleKey(role))
})

// Budget switcher: active memberships only (deleted budgets excluded)
const memberships = computed(() => authStore.user?.memberships.filter((m) => !m.isDeleted) ?? [])

function switchBudget(budgetId: string, budgetName: string): void {
  layoutStore.setActiveBudget(budgetId, budgetName)
  router.push(`/budgets/${budgetId}/cycles`)
}

function goHome(): void {
  layoutStore.clearActiveBudget()
  router.push('/')
}

async function onLogout(): Promise<void> {
  layoutStore.clearActiveBudget()
  layoutStore.clearPageActions()
  await authStore.logout()
  router.push('/login')
}

function variantClass(action: PageAction): string {
  switch (action.variant) {
    case 'primary':
      return 'btn-primary'
    case 'error':
      return 'btn-error'
    default:
      return 'btn-ghost'
  }
}
</script>

<template>
  <div class="min-h-screen bg-base-200 flex flex-col">
    <!-- Top Navbar -->
    <nav class="navbar bg-base-100 shadow px-4 sticky top-0 z-50">
      <!-- Left: App name / back home -->
      <div class="flex-none">
        <button class="btn btn-ghost text-lg font-bold" @click="goHome">
          {{ $t('common.appName') }}
        </button>
      </div>

      <!-- Center: Budget switcher -->
      <div class="flex-1 px-2">
        <div v-if="memberships.length > 0 && route.name !== 'BudgetSelection'" class="dropdown">
          <label tabindex="0" class="btn btn-ghost gap-1">
            <span class="font-medium">
              {{ layoutStore.activeBudgetName ?? $t('common.appName') }}
            </span>
            <svg
              xmlns="http://www.w3.org/2000/svg"
              class="h-4 w-4"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                stroke-width="2"
                d="M19 9l-7 7-7-7"
              />
            </svg>
          </label>
          <ul
            tabindex="0"
            class="dropdown-content menu bg-base-100 rounded-box z-[1] w-52 p-2 shadow"
          >
            <li v-for="membership in memberships" :key="membership.budgetId">
              <button
                @click="switchBudget(membership.budgetId, membership.budgetName)"
                :class="{ active: membership.budgetId === layoutStore.activeBudgetId }"
              >
                {{ membership.budgetName }}
              </button>
            </li>
            <li class="divider" />
            <li>
              <button @click="goHome">{{ $t('nav.backToHome') }}</button>
            </li>
          </ul>
        </div>
      </div>

      <!-- Right: Page actions (desktop) + notification bell + user dropdown -->
      <div class="flex-none flex items-center gap-2">
        <!-- Page actions — visible at sm+ breakpoint -->
        <div class="hidden sm:flex items-center gap-1">
          <button
            v-for="action in layoutStore.pageActions"
            :key="action.key"
            :disabled="action.disabled"
            :class="['btn btn-sm', variantClass(action)]"
            @click="action.action()"
          >
            {{ action.label }}
          </button>
        </div>

        <!-- Page actions — collapsed to ⋮ dropdown on mobile -->
        <div v-if="layoutStore.pageActions.length > 0" class="dropdown dropdown-end sm:hidden">
          <label tabindex="0" class="btn btn-ghost btn-sm">⋮</label>
          <ul
            tabindex="0"
            class="dropdown-content menu bg-base-100 rounded-box z-[1] w-40 p-2 shadow"
          >
            <li v-for="action in layoutStore.pageActions" :key="action.key">
              <button :disabled="action.disabled" @click="action.action()">
                {{ action.label }}
              </button>
            </li>
          </ul>
        </div>

        <!-- Notification bell -->
        <div class="dropdown dropdown-end">
          <label tabindex="0" class="btn btn-ghost btn-circle">
            <div class="indicator">
              <svg
                xmlns="http://www.w3.org/2000/svg"
                class="h-5 w-5"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2"
                  d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"
                />
              </svg>
              <span
                v-if="notificationStore.unreadCount > 0"
                class="badge badge-xs badge-primary indicator-item"
              >
                {{ notificationStore.unreadCount }}
              </span>
            </div>
          </label>
          <div
            tabindex="0"
            class="dropdown-content card card-compact bg-base-100 z-[1] w-64 p-2 shadow"
          >
            <div class="card-body">
              <p v-if="notificationStore.notifications.length === 0" class="text-sm text-base-content/60">
                {{ $t('common.noNotifications') }}
              </p>
              <ul v-else class="space-y-1">
                <li
                  v-for="notification in notificationStore.notifications.slice(0, 5)"
                  :key="notification.id"
                  class="text-sm"
                  :class="{ 'opacity-60': notification.read }"
                >
                  <button
                    class="w-full text-left hover:bg-base-200 rounded p-1"
                    @click="notificationStore.markRead(notification.id)"
                  >
                    <span class="font-medium">{{ notification.title }}</span>
                    <span class="block text-base-content/70 text-xs">{{ notification.message }}</span>
                  </button>
                </li>
              </ul>
            </div>
          </div>
        </div>

        <!-- User dropdown -->
        <div class="dropdown dropdown-end">
          <label tabindex="0" class="btn btn-ghost btn-circle avatar placeholder">
            <div class="bg-neutral text-neutral-content rounded-full w-8">
              <span class="text-xs">{{ userInitials }}</span>
            </div>
          </label>
          <ul
            tabindex="0"
            class="dropdown-content menu bg-base-100 rounded-box z-[1] w-52 p-2 shadow"
          >
            <li class="menu-title px-4 py-2">
              <div>
                <p class="font-medium text-sm">
                  {{ authStore.user?.firstName }} {{ authStore.user?.lastName }}
                </p>
                <p class="text-xs text-base-content/60">{{ authStore.user?.email }}</p>
                <span v-if="activeRoleBadge" class="badge badge-outline badge-sm mt-1">
                  {{ activeRoleBadge }}
                </span>
              </div>
            </li>
            <li class="divider" />
            <li>
              <button @click="changePasswordModal?.open()">{{ $t('auth.password.changePassword') }}</button>
            </li>
            <li class="px-4 py-2">
              <LanguageSwitcher />
            </li>
            <li>
              <button @click="onLogout">{{ $t('auth.logoutLabel') }}</button>
            </li>
          </ul>
        </div>
      </div>
    </nav>

    <ChangePasswordModal ref="changePasswordModal" />

    <!-- Main content -->
    <main>
      <slot>
        <RouterView />
      </slot>
    </main>

    <!-- Ephemeral toast overlay (bottom-right, above modals) -->
    <AppToast />
  </div>
</template>

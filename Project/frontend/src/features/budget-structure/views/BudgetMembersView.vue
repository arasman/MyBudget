<template>
  <div class="container mx-auto px-4 py-6">
    <BudgetTabs
      :budget-id="budgetId"
      class="mb-6"
    />

    <h1 class="text-2xl font-semibold mb-4">
      {{ t('budgetStructure.members.title') }}
    </h1>

    <!-- Loading indicator -->
    <div
      v-if="loading"
      class="flex justify-center py-8"
    >
      <span class="loading loading-spinner loading-md" />
    </div>

    <table
      v-else
      class="table"
    >
      <thead>
        <tr>
          <th>{{ t('budgetStructure.members.columns.name') }}</th>
          <th>{{ t('budgetStructure.members.columns.email') }}</th>
          <th>{{ t('budgetStructure.members.columns.role') }}</th>
          <th>{{ t('budgetStructure.members.columns.joinedAt') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="m in visibleMembers"
          :key="m.userId"
        >
          <td>{{ m.firstName }} {{ m.lastName }}</td>
          <td>{{ m.email }}</td>
          <td>
            <select
              v-if="canActOn(m)"
              :value="m.role"
              class="select select-bordered select-sm"
              :disabled="actionInProgress === m.userId"
              :aria-label="t('budgetStructure.members.actions.changeRole')"
              @change="onRoleChange(m, ($event.target as HTMLSelectElement).value as MemberRole)"
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
            <span
              v-else
              class="badge badge-outline"
            >{{ t('enums.role.' + toRoleKey(m.role)) }}</span>
          </td>
          <td>{{ formatJoinedAt(m.joinedAt) }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth.store'
import { useToastStore } from '@/stores/toast.store'
import { useRoleGate } from '../composables/useRoleGate'
import { toRoleKey } from '@/utils/enum-key'
import { listMembers, updateMemberRole } from '../api/budgetMembers.api'
import BudgetTabs from '../components/BudgetTabs.vue'
import type { MemberDto, MemberRole } from '../types'

const route = useRoute()
const { t, locale } = useI18n()
const authStore = useAuthStore()
const toastStore = useToastStore()

const budgetId = route.params.budgetId as string
const { isAdmin, isOwner } = useRoleGate(budgetId)

const members = ref<MemberDto[]>([])
const loading = ref(false)
const actionInProgress = ref<string | null>(null)

// Owner row is excluded entirely — no role selector, no action control ever rendered for it.
const visibleMembers = computed(() => members.value.filter((m) => m.role !== 'owner'))

/**
 * Frontend row gate — mirrors design.md's Interfaces/Contracts snippet exactly.
 * Server-side MemberActionPolicy is the source of truth (MEM-SC-2); this only
 * hides controls that would otherwise 403.
 */
function canActOn(m: MemberDto): boolean {
  if (!isAdmin.value) return false
  if (m.userId === authStore.user?.id) return false
  if (m.role === 'owner') return false
  if (!isOwner.value && m.role === 'admin') return false
  return true
}

function formatJoinedAt(joinedAt: string): string {
  return new Intl.DateTimeFormat(locale.value, { year: 'numeric', month: 'short', day: 'numeric' }).format(
    new Date(joinedAt),
  )
}

async function loadMembers(): Promise<void> {
  loading.value = true
  try {
    members.value = await listMembers(budgetId)
  } finally {
    loading.value = false
  }
}

async function onRoleChange(m: MemberDto, newRole: MemberRole): Promise<void> {
  actionInProgress.value = m.userId
  try {
    await updateMemberRole(budgetId, m.userId, newRole)
    await loadMembers()
    toastStore.push({ type: 'success', title: t('budgetStructure.members.confirmations.roleChangeSuccess') })
  } catch {
    toastStore.push({ type: 'error', title: t('budgetStructure.members.confirmations.roleChangeError') })
  } finally {
    actionInProgress.value = null
  }
}

onMounted(loadMembers)
</script>

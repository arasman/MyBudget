<template>
  <div class="container mx-auto px-4 py-6">
    <BudgetTabs
      :budget-id="budgetId"
      class="mb-6"
    />

    <div class="flex items-center justify-between mb-4">
      <h1 class="text-2xl font-semibold">
        {{ t('budgetStructure.members.title') }}
      </h1>

      <div
        v-if="isAdmin"
        class="flex items-center gap-2"
      >
        <input
          id="members-show-deleted"
          v-model="showDeleted"
          type="checkbox"
          class="checkbox checkbox-sm"
          @change="loadMembers"
        >
        <label
          for="members-show-deleted"
          class="label-text cursor-pointer"
        >
          {{ t('budgetStructure.members.showDeleted') }}
        </label>
      </div>
    </div>

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
          <th />
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="m in visibleMembers"
          :key="m.userId"
          :class="m.isDeleted ? 'opacity-60' : ''"
        >
          <td>{{ m.firstName }} {{ m.lastName }}</td>
          <td>{{ m.email }}</td>
          <td>
            <select
              v-if="!m.isDeleted && canActOn(m)"
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
          <td>
            <!-- Active row: Remove -->
            <button
              v-if="!m.isDeleted && canActOn(m)"
              type="button"
              class="btn btn-xs btn-ghost text-error"
              :disabled="actionInProgress === m.userId"
              @click="onRemoveClick(m)"
            >
              {{ t('budgetStructure.members.actions.remove') }}
            </button>
            <!-- Soft-deleted row: Restore -->
            <button
              v-else-if="m.isDeleted && canActOn(m)"
              type="button"
              class="btn btn-xs btn-success"
              :disabled="actionInProgress === m.userId"
              @click="onRestore(m)"
            >
              <span
                v-if="actionInProgress === m.userId"
                class="loading loading-spinner loading-xs"
              />
              {{ t('budgetStructure.members.actions.restore') }}
            </button>
          </td>
        </tr>
      </tbody>
    </table>

    <!-- Remove confirmation dialog -->
    <dialog
      v-if="pendingRemove"
      open
      class="modal modal-open"
    >
      <div class="modal-box">
        <h3 class="font-bold text-lg mb-2">
          {{ t('budgetStructure.members.removeConfirmTitle') }}
        </h3>
        <p class="text-base-content/70">
          {{ t('budgetStructure.members.removeConfirm') }}
        </p>
        <div class="modal-action">
          <button
            type="button"
            class="btn btn-ghost"
            @click="cancelRemove"
          >
            {{ t('common.cancel') }}
          </button>
          <button
            type="button"
            class="btn btn-error"
            @click="confirmRemove"
          >
            {{ t('common.confirm') }}
          </button>
        </div>
      </div>
      <div
        class="modal-backdrop"
        @click="cancelRemove"
      />
    </dialog>
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
import { listMembers, updateMemberRole, removeMember, restoreMember } from '../api/budgetMembers.api'
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
const showDeleted = ref(false)
const pendingRemove = ref<MemberDto | null>(null)

// Owner row is excluded entirely — no role selector, no action control ever rendered for it.
const visibleMembers = computed(() => members.value.filter((m) => m.role !== 'owner'))

/**
 * Frontend row gate — mirrors design.md's Interfaces/Contracts snippet exactly.
 * Server-side MemberActionPolicy is the source of truth (MEM-SC-2); this only
 * hides controls that would otherwise 403. Applies identically to soft-deleted rows
 * (Restore) and active rows (role select / Remove) — the underlying matrix is the same.
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
    members.value = await listMembers(budgetId, { includeDeleted: showDeleted.value })
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

// ── Remove (soft-delete) ────────────────────────────────────────────────────

function onRemoveClick(m: MemberDto): void {
  pendingRemove.value = m
}

function cancelRemove(): void {
  if (actionInProgress.value) return
  pendingRemove.value = null
}

async function confirmRemove(): Promise<void> {
  const m = pendingRemove.value
  if (!m) return
  actionInProgress.value = m.userId
  try {
    await removeMember(budgetId, m.userId)
    pendingRemove.value = null
    await loadMembers()
    toastStore.push({ type: 'success', title: t('budgetStructure.members.confirmations.removeSuccess') })
  } finally {
    actionInProgress.value = null
  }
}

// ── Restore ────────────────────────────────────────────────────────────────

async function onRestore(m: MemberDto): Promise<void> {
  actionInProgress.value = m.userId
  try {
    await restoreMember(budgetId, m.userId)
    await loadMembers()
    toastStore.push({ type: 'success', title: t('budgetStructure.members.confirmations.restoreSuccess') })
  } finally {
    actionInProgress.value = null
  }
}

onMounted(loadMembers)
</script>

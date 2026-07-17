<template>
  <div class="container mx-auto max-w-2xl px-4 py-8">
    <!-- Header row: title + New Budget button -->
    <div class="flex items-center justify-between mb-6">
      <h1 class="text-2xl font-semibold">{{ t('budgetStructure.selection.title') }}</h1>
      <button type="button" class="btn btn-primary btn-sm" @click="openCreateModal">
        {{ t('budgetStructure.selection.createBudget') }}
      </button>
    </div>

    <!-- Show deleted toggle -->
    <div class="flex items-center gap-2 mb-4">
      <input
        id="show-deleted"
        v-model="showDeleted"
        type="checkbox"
        class="checkbox checkbox-sm"
      />
      <label for="show-deleted" class="label-text cursor-pointer">
        {{ t('budgetStructure.selection.showDeleted') }}
      </label>
    </div>

    <!-- No budgets -->
    <div v-if="visibleMemberships.length === 0" class="text-center py-16">
      <p class="text-base-content/60 text-lg">
        {{ t('budgetStructure.selection.noBudgets') }}
      </p>
    </div>

    <!-- Budget list -->
    <ul v-else class="space-y-3">
      <li v-for="m in visibleMemberships" :key="m.budgetId">
        <div
          class="card card-border w-full select-none"
          :class="[m.isDeleted ? 'opacity-60' : '', !m.isDeleted && inlineEditingBudgetId !== m.budgetId ? 'cursor-pointer' : '']"
          @dblclick="canEdit(m) && inlineEditingBudgetId !== m.budgetId ? startInlineEdit(m.budgetId, m.budgetName) : undefined"
        >
          <div class="card-body flex-row items-center justify-between py-4">
            <!-- Name area -->
            <div class="flex items-center gap-2 flex-1 min-w-0">
              <!-- Inline edit input -->
              <template v-if="inlineEditingBudgetId === m.budgetId">
                <input
                  v-model="inlineEditName"
                  type="text"
                  class="input input-xs input-bordered flex-1"
                  maxlength="200"
                  autocomplete="off"
                  @keyup.enter="saveInlineEdit(m.budgetId)"
                  @keyup.escape="cancelInlineEdit"
                />
              </template>
              <template v-else>
                <span class="font-medium text-base truncate">{{ m.budgetName }}</span>
                <span v-if="m.isDeleted" class="badge badge-error badge-sm shrink-0">
                  {{ t('budgetStructure.selection.deletedBadge') }}
                </span>
              </template>
            </div>

            <!-- Actions -->
            <div class="flex items-center gap-1 shrink-0 ml-3">
              <span class="badge badge-outline capitalize mr-1">{{ m.role }}</span>

              <!-- Inline edit: save / cancel -->
              <template v-if="inlineEditingBudgetId === m.budgetId">
                <button
                  type="button"
                  class="btn btn-xs btn-ghost btn-square text-success"
                  :disabled="!!actionInProgress"
                  :title="t('budgetStructure.common.save')"
                  @click.stop="saveInlineEdit(m.budgetId)"
                >
                  <span v-if="actionInProgress === m.budgetId" class="loading loading-spinner loading-xs" />
                  <Check v-else :size="14" />
                </button>
                <button
                  type="button"
                  class="btn btn-xs btn-ghost btn-square"
                  :title="t('budgetStructure.common.cancel')"
                  @click.stop="cancelInlineEdit"
                >
                  <X :size="14" />
                </button>
              </template>

              <!-- Active budget actions -->
              <template v-else-if="!m.isDeleted">
                <!-- View cycles -->
                <button
                  type="button"
                  class="btn btn-xs btn-ghost btn-square"
                  :title="t('budgetStructure.selection.viewCycles')"
                  @click.stop="selectBudget(m.budgetId, m.budgetName)"
                >
                  <List :size="14" />
                </button>

                <!-- Rename (owner or admin only) -->
                <button
                  v-if="canEdit(m)"
                  type="button"
                  class="btn btn-xs btn-ghost btn-square"
                  :title="t('budgetStructure.selection.renameBudget')"
                  @click.stop="startInlineEdit(m.budgetId, m.budgetName)"
                >
                  <Pencil :size="14" />
                </button>

                <!-- Delete (owner only) -->
                <button
                  v-if="m.role === 'owner'"
                  type="button"
                  class="btn btn-xs btn-ghost btn-square text-error"
                  :disabled="!!actionInProgress"
                  :title="t('budgetStructure.selection.deleteBudget')"
                  :aria-label="t('budgetStructure.selection.deleteBudget')"
                  @click.stop="onDelete(m.budgetId)"
                >
                  <Trash2 :size="14" />
                </button>
              </template>

              <!-- Deleted budget: restore only -->
              <template v-else>
                <button
                  type="button"
                  class="btn btn-success btn-xs"
                  :disabled="actionInProgress === m.budgetId"
                  @click.stop="onRestore(m.budgetId)"
                >
                  <span v-if="actionInProgress === m.budgetId" class="loading loading-spinner loading-xs" />
                  {{ t('budgetStructure.selection.restoreBudget') }}
                </button>
              </template>
            </div>
          </div>
        </div>
      </li>
    </ul>

    <CreateBudgetModal ref="createModal" @created="onBudgetCreated" />

    <!-- Delete confirmation modal -->
    <dialog v-if="pendingDeleteId" class="modal modal-open">
      <div class="modal-box">
        <h3 class="font-bold text-lg mb-2">{{ t('budgetStructure.selection.confirmDeleteTitle') }}</h3>
        <p class="text-base-content/70">{{ t('budgetStructure.selection.confirmDelete') }}</p>
        <div class="modal-action">
          <button type="button" class="btn btn-ghost" :disabled="!!actionInProgress" @click="cancelDelete">
            {{ t('common.cancel') }}
          </button>
          <button
            type="button"
            class="btn btn-error"
            :disabled="!!actionInProgress"
            @click="confirmDelete"
          >
            <span v-if="actionInProgress" class="loading loading-spinner loading-sm" />
            {{ t('budgetStructure.selection.deleteBudget') }}
          </button>
        </div>
      </div>
      <div class="modal-backdrop" @click="cancelDelete" />
    </dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Check, List, Pencil, Trash2, X } from 'lucide-vue-next'
import { useAuthStore } from '@/stores/auth.store'
import { useLayoutStore } from '@/stores/layout.store'
import { deleteBudget, renameBudget, restoreBudget } from '../api/budgets.api'
import CreateBudgetModal from '../components/CreateBudgetModal.vue'
import type { BudgetMembershipDto } from '@/stores/auth.store'

const { t } = useI18n()
const authStore = useAuthStore()
const layoutStore = useLayoutStore()
const router = useRouter()
const route = useRoute()

const showDeleted = ref(false)
const actionInProgress = ref<string | null>(null)
const pendingDeleteId = ref<string | null>(null)
const createModal = ref<InstanceType<typeof CreateBudgetModal>>()

// Inline edit state
const inlineEditingBudgetId = ref<string | null>(null)
const inlineEditName = ref('')

const memberships = computed(() => authStore.user?.memberships ?? [])

const visibleMemberships = computed(() => {
  if (showDeleted.value) return memberships.value
  return memberships.value.filter((m) => !m.isDeleted)
})

const activeCount = computed(() => memberships.value.filter((m) => !m.isDeleted).length)

function canEdit(m: BudgetMembershipDto): boolean {
  return m.role === 'owner' || m.role === 'admin'
}

function selectBudget(budgetId: string, budgetName: string): void {
  layoutStore.setActiveBudget(budgetId, budgetName)
  router.push({ name: 'CycleList', params: { budgetId } })
}

function openCreateModal(): void {
  createModal.value?.open()
}

async function onBudgetCreated(budget: { id: string; name: string }): Promise<void> {
  await authStore.fetchMe()
  selectBudget(budget.id, budget.name)
}

// ── Inline rename ──────────────────────────────────────────────────────────

function startInlineEdit(budgetId: string, currentName: string): void {
  inlineEditingBudgetId.value = budgetId
  inlineEditName.value = currentName
}

function cancelInlineEdit(): void {
  inlineEditingBudgetId.value = null
  inlineEditName.value = ''
}

async function saveInlineEdit(budgetId: string): Promise<void> {
  const trimmed = inlineEditName.value.trim()
  if (!trimmed) return
  actionInProgress.value = budgetId
  try {
    await renameBudget(budgetId, trimmed)
    await authStore.fetchMe()
    // Update navbar if this was the active budget
    if (layoutStore.activeBudgetId === budgetId) {
      layoutStore.setActiveBudget(budgetId, trimmed)
    }
    inlineEditingBudgetId.value = null
  } finally {
    actionInProgress.value = null
  }
}

// ── Delete ─────────────────────────────────────────────────────────────────

function onDelete(budgetId: string): void {
  pendingDeleteId.value = budgetId
}

function cancelDelete(): void {
  if (actionInProgress.value) return
  pendingDeleteId.value = null
}

async function confirmDelete(): Promise<void> {
  if (!pendingDeleteId.value) return
  const budgetId = pendingDeleteId.value
  actionInProgress.value = budgetId
  try {
    await deleteBudget(budgetId)
    await authStore.fetchMe()
    if (layoutStore.activeBudgetId === budgetId) {
      layoutStore.clearActiveBudget()
    }
    pendingDeleteId.value = null
  } finally {
    actionInProgress.value = null
  }
}

// ── Restore ────────────────────────────────────────────────────────────────

async function onRestore(budgetId: string): Promise<void> {
  actionInProgress.value = budgetId
  try {
    await restoreBudget(budgetId)
    await authStore.fetchMe()
  } finally {
    actionInProgress.value = null
  }
}

onMounted(() => {
  // Auto-redirect when the user belongs to exactly one active budget,
  // but NOT when navigating intentionally via ?manage=1 (e.g. from BudgetTabs back-link).
  if (route.query.manage) return
  if (activeCount.value === 1) {
    const m = memberships.value.find((m) => !m.isDeleted)!
    selectBudget(m.budgetId, m.budgetName)
  }
})
</script>

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
          class="card card-border w-full"
          :class="m.isDeleted ? 'opacity-60' : ''"
        >
          <div class="card-body flex-row items-center justify-between py-4">
            <div class="flex items-center gap-2">
              <button
                v-if="!m.isDeleted"
                type="button"
                class="font-medium text-base hover:underline cursor-pointer"
                @click="selectBudget(m.budgetId, m.budgetName)"
              >
                {{ m.budgetName }}
              </button>
              <span v-else class="font-medium text-base">{{ m.budgetName }}</span>
              <span v-if="m.isDeleted" class="badge badge-error badge-sm">
                {{ t('budgetStructure.selection.deletedBadge') }}
              </span>
            </div>

            <div class="flex items-center gap-2">
              <span class="badge badge-outline capitalize">{{ m.role }}</span>

              <!-- Restore button — only on deleted budgets -->
              <button
                v-if="m.isDeleted"
                type="button"
                class="btn btn-success btn-xs"
                :disabled="actionInProgress === m.budgetId"
                @click="onRestore(m.budgetId)"
              >
                <span
                  v-if="actionInProgress === m.budgetId"
                  class="loading loading-spinner loading-xs"
                />
                {{ t('budgetStructure.selection.restoreBudget') }}
              </button>

              <!-- Delete button — only on active budgets owned by the user -->
              <button
                v-if="!m.isDeleted && m.role === 'owner'"
                type="button"
                class="btn btn-error btn-xs btn-outline"
                :disabled="actionInProgress === m.budgetId"
                @click="onDelete(m.budgetId)"
              >
                <span
                  v-if="actionInProgress === m.budgetId"
                  class="loading loading-spinner loading-xs"
                />
                {{ t('budgetStructure.selection.deleteBudget') }}
              </button>
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
import { useAuthStore } from '@/stores/auth.store'
import { useLayoutStore } from '@/stores/layout.store'
import { deleteBudget, restoreBudget } from '../api/budgets.api'
import CreateBudgetModal from '../components/CreateBudgetModal.vue'

const { t } = useI18n()
const authStore = useAuthStore()
const layoutStore = useLayoutStore()
const router = useRouter()
const route = useRoute()

const showDeleted = ref(false)
const actionInProgress = ref<string | null>(null)
const pendingDeleteId = ref<string | null>(null)
const createModal = ref<InstanceType<typeof CreateBudgetModal>>()

const memberships = computed(() => authStore.user?.memberships ?? [])

const visibleMemberships = computed(() => {
  if (showDeleted.value) return memberships.value
  return memberships.value.filter((m) => !m.isDeleted)
})

const activeCount = computed(() => memberships.value.filter((m) => !m.isDeleted).length)

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

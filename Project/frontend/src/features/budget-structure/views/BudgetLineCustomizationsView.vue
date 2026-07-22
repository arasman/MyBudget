<template>
  <div class="container mx-auto px-4 py-6">
    <!-- Breadcrumb -->
    <div class="breadcrumbs text-sm mb-4">
      <ul>
        <li>
          <RouterLink :to="{ name: 'BudgetLines', params: { budgetId } }">
            {{ t('budgetStructure.budgetLines.title') }}
          </RouterLink>
        </li>
        <li>{{ t('budgetStructure.budgetLines.customizations.title') }}</li>
      </ul>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex justify-center py-8">
      <span class="loading loading-spinner loading-md" />
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="alert alert-error mb-4">
      {{ store.error }}
    </div>

    <template v-else>
      <h2 class="text-xl font-bold mb-4">
        {{ t('budgetStructure.budgetLines.customizations.revisions') }}
      </h2>

      <!-- Empty state -->
      <div v-if="store.revisions.length === 0 && !showInlineAdd" class="py-8 text-center text-base-content/60">
        {{ t('budgetStructure.budgetLines.customizations.noRevisions') }}
      </div>

      <!-- Revisions table -->
      <div v-if="store.revisions.length > 0 || showInlineAdd" class="overflow-x-auto">
        <table class="table table-zebra w-full">
          <thead>
            <tr>
              <th>{{ t('budgetStructure.budgetLines.customizations.validFrom') }}</th>
              <th>{{ t('budgetStructure.budgetLines.customizations.validTo') }}</th>
              <th>{{ t('budgetStructure.budgetLines.customizations.amount') }}</th>
              <th>{{ t('budgetStructure.budgetLines.customizations.currency') }}</th>
              <th>{{ t('budgetStructure.budgetLines.customizations.note') }}</th>
              <th>{{ t('budgetStructure.common.actions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="revision in store.revisions" :key="revision.id">
              <td>{{ revision.validFrom }}</td>
              <td>{{ revision.validTo ?? '—' }}</td>
              <td>{{ revision.budgetedAmount }}</td>
              <td>{{ revision.currencyCode ?? revision.currencyId }}</td>
              <td class="text-sm text-base-content/60">{{ revision.note ?? '—' }}</td>
              <td>
                <button
                  v-if="isAdmin"
                  type="button"
                  class="btn btn-xs btn-error btn-ghost"
                  @click="confirmDelete(revision.id)"
                >
                  {{ t('budgetStructure.budgetLines.customizations.deleteRevision') }}
                </button>
              </td>
            </tr>

            <!-- Inline add row -->
            <tr v-if="showInlineAdd" class="bg-base-200">
              <td>
                <input
                  v-model="inlineAddForm.validFrom"
                  type="date"
                  class="input input-xs input-bordered w-full"
                />
              </td>
              <td>—</td>
              <td>
                <input
                  v-model.number="inlineAddForm.amount"
                  type="number"
                  step="0.01"
                  class="input input-xs input-bordered w-24"
                />
              </td>
              <td colspan="2">—</td>
              <td>
                <div class="flex gap-1">
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost btn-square text-success"
                    :title="t('budgetStructure.common.save')"
                    @click="handleInlineAddSave"
                  >
                    <Check :size="14" />
                  </button>
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost btn-square"
                    :title="t('budgetStructure.common.cancel')"
                    @click="showInlineAdd = false"
                  >
                    <X :size="14" />
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>

        <!-- Inline add trigger -->
        <div v-if="isAdmin && !showInlineAdd" class="mt-3">
          <button type="button" class="btn btn-sm btn-ghost" @click="openInlineAdd">
            + {{ t('budgetStructure.budgetLines.customizations.createRevision') }}
          </button>
        </div>
      </div>
    </template>

    <!-- Delete confirmation dialog -->
    <dialog v-if="showDeleteConfirm" class="modal modal-open">
      <div class="modal-box">
        <h3 class="font-bold text-lg mb-4">
          {{ t('budgetStructure.budgetLines.customizations.deleteRevision') }}
        </h3>
        <p>{{ t('budgetStructure.budgetLines.customizations.confirmDeleteRevision') }}</p>
        <div class="modal-action">
          <button type="button" class="btn btn-ghost" @click="showDeleteConfirm = false">
            {{ t('budgetStructure.common.cancel') }}
          </button>
          <button type="button" class="btn btn-error" @click="handleDelete">
            {{ t('budgetStructure.common.confirm') }}
          </button>
        </div>
      </div>
      <div class="modal-backdrop" @click="showDeleteConfirm = false" />
    </dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Check, X } from 'lucide-vue-next'
import { useBudgetStructureStore } from '../store'
import { useLayoutStore } from '@/stores/layout.store'
import { useRoleGate } from '../composables/useRoleGate'

const route = useRoute()
const { t } = useI18n()

const budgetId = route.params.budgetId as string
const lineId = route.params.lineId as string

const store = useBudgetStructureStore()
const layoutStore = useLayoutStore()
const { isAdmin } = useRoleGate(budgetId)

// Delete confirmation state
const showDeleteConfirm = ref(false)
const deletingRevisionId = ref<string | null>(null)

// Inline add state
const showInlineAdd = ref(false)
const inlineAddForm = reactive({
  validFrom: '',
  amount: null as number | null,
})

function openInlineAdd(): void {
  inlineAddForm.validFrom = new Date().toISOString().slice(0, 10)
  inlineAddForm.amount = null
  showInlineAdd.value = true
}

async function handleInlineAddSave(): Promise<void> {
  if (!inlineAddForm.validFrom || !inlineAddForm.amount || inlineAddForm.amount <= 0) return
  try {
    await store.createRevision(budgetId, lineId, {
      validFrom: inlineAddForm.validFrom,
      amount: inlineAddForm.amount,
    })
    showInlineAdd.value = false
  } catch {
    // error handled by store
  }
}

function confirmDelete(revisionId: string): void {
  deletingRevisionId.value = revisionId
  showDeleteConfirm.value = true
}

async function handleDelete(): Promise<void> {
  if (!deletingRevisionId.value) return
  try {
    await store.deleteRevision(budgetId, lineId, deletingRevisionId.value)
    showDeleteConfirm.value = false
    deletingRevisionId.value = null
  } catch {
    // error handled by store
  }
}

onMounted(async () => {
  await store.fetchRevisions(budgetId, lineId)

  if (isAdmin.value) {
    layoutStore.setPageActions([
      {
        key: 'new-revision',
        label: t('budgetStructure.budgetLines.customizations.createRevision'),
        action: openInlineAdd,
        variant: 'primary',
      },
    ])
  }
})

onUnmounted(() => {
  layoutStore.clearPageActions()
})
</script>

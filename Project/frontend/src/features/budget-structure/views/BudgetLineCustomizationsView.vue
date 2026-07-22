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

    <!-- Back link -->
    <RouterLink
      :to="{ name: 'BudgetLines', params: { budgetId } }"
      class="btn btn-ghost btn-sm mb-4"
    >
      {{ t('budgetStructure.budgetLines.customizations.backToLines') }}
    </RouterLink>

    <h2 class="text-xl font-bold mb-4">
      {{ t('budgetStructure.budgetLines.customizations.revisions') }}
    </h2>

    <!-- Loading -->
    <div v-if="store.loading" class="flex justify-center py-8">
      <span class="loading loading-spinner loading-md" />
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="alert alert-error mb-4">
      {{ store.error }}
    </div>

    <!-- Empty state -->
    <div v-else-if="store.revisions.length === 0" class="py-8 text-center text-base-content/60">
      {{ t('budgetStructure.budgetLines.customizations.noRevisions') }}
    </div>

    <!-- Revisions table -->
    <div v-else class="overflow-x-auto">
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
                type="button"
                class="btn btn-xs btn-error btn-ghost"
                @click="confirmDelete(revision.id)"
              >
                {{ t('budgetStructure.budgetLines.customizations.deleteRevision') }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

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
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useBudgetStructureStore } from '../store'

const route = useRoute()
const { t } = useI18n()

const budgetId = route.params.budgetId as string
const lineId = route.params.lineId as string

const store = useBudgetStructureStore()

// Delete confirmation state
const showDeleteConfirm = ref(false)
const deletingRevisionId = ref<string | null>(null)

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
})
</script>

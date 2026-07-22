<template>
  <div class="container mx-auto px-4 py-6">
    <BudgetTabs :budget-id="budgetId" class="mb-6" />

    <!-- Loading -->
    <div v-if="store.loading" class="flex justify-center py-8">
      <span class="loading loading-spinner loading-md" />
    </div>


    <template v-if="!store.loading">
      <h2 class="text-xl font-bold mb-4">
        {{ t('budgetStructure.budgetLines.customizations.title') }}
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
              <td>{{ revision.validTo ?? (currentLine?.endDate ?? '—') }}</td>
              <td>
                <template v-if="editingRevisionId === revision.id">
                  <input
                    v-model.number="editingAmount"
                    type="number"
                    step="0.01"
                    min="0"
                    class="input input-xs input-bordered w-24"
                  />
                </template>
                <template v-else>{{ revision.budgetedAmount }}</template>
              </td>
              <td>{{ revision.currencyCode ?? revision.currencyId }}</td>
              <td class="text-sm text-base-content/60">
                <template v-if="editingRevisionId === revision.id">
                  <input
                    v-model="editingNote"
                    type="text"
                    class="input input-xs input-bordered w-full"
                  />
                </template>
                <template v-else>{{ revision.note ?? '—' }}</template>
              </td>
              <td>
                <template v-if="editingRevisionId === revision.id">
                  <div class="flex gap-1">
                    <button type="button" class="btn btn-xs btn-ghost btn-square text-success"
                      :title="t('budgetStructure.common.save')"
                      @click="handleSaveRevision(revision.id)">
                      <Check :size="14" />
                    </button>
                    <button type="button" class="btn btn-xs btn-ghost btn-square"
                      :title="t('budgetStructure.common.cancel')"
                      @click="editingRevisionId = null">
                      <X :size="14" />
                    </button>
                  </div>
                </template>
                <template v-else>
                  <div class="flex gap-1">
                    <button v-if="isAdmin" type="button"
                      class="btn btn-xs btn-ghost btn-square"
                      :title="t('budgetStructure.budgetLines.customizations.editRevision')"
                      @click="startEditRevision(revision.id, revision.budgetedAmount, revision.note)">
                      <Pencil :size="14" />
                    </button>
                    <button v-if="isAdmin" type="button"
                      class="btn btn-xs btn-ghost btn-square text-error"
                      :title="t('budgetStructure.budgetLines.customizations.deleteRevision')"
                      @click="confirmDelete(revision.id)">
                      <Trash2 :size="14" />
                    </button>
                  </div>
                </template>
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
              <td>
                <input
                  v-model="inlineAddForm.validTo"
                  type="date"
                  class="input input-xs input-bordered w-full"
                />
              </td>
              <td>
                <input
                  v-model.number="inlineAddForm.amount"
                  type="number"
                  step="0.01"
                  min="0"
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
import { ref, reactive, computed, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Check, X, Pencil, Trash2 } from 'lucide-vue-next'
import { useBudgetStructureStore } from '../store'
import { useLayoutStore } from '@/stores/layout.store'
import { useToastStore } from '@/stores/toast.store'
import { useRoleGate } from '../composables/useRoleGate'
import { extractApiErrorCode } from '../utils/apiError'
import BudgetTabs from '../components/BudgetTabs.vue'

const route = useRoute()
const { t } = useI18n()

const budgetId = route.params.budgetId as string
const lineId = route.params.lineId as string

const store = useBudgetStructureStore()
const layoutStore = useLayoutStore()
const toastStore = useToastStore()
const { isAdmin } = useRoleGate(budgetId)

const currentLine = computed(() => store.budgetLines.find(l => l.id === lineId))

// Delete confirmation state
const showDeleteConfirm = ref(false)
const deletingRevisionId = ref<string | null>(null)

// Inline add state
const showInlineAdd = ref(false)
const inlineAddForm = reactive({
  validFrom: '',
  validTo: '',
  amount: null as number | null,
})

// Inline revision edit state
const editingRevisionId = ref<string | null>(null)
const editingAmount = ref<number>(0)
const editingNote = ref<string>('')

function startEditRevision(revisionId: string, currentAmount: number, currentNote: string | null | undefined): void {
  editingRevisionId.value = revisionId
  editingAmount.value = currentAmount
  editingNote.value = currentNote ?? ''
}

async function handleSaveRevision(revisionId: string): Promise<void> {
  try {
    await store.updateRevision(budgetId, lineId, revisionId, {
      amount: editingAmount.value,
      note: editingNote.value || undefined,
    })
    editingRevisionId.value = null
    toastStore.push({ type: 'success', title: t('budgetStructure.budgetLines.customizations.updateSuccess') })
  } catch (err) {
    const code = extractApiErrorCode(err)
    const msg = code
      ? t(`budgetStructure.budgetLines.customizations.errors.${_camelCase(code)}`, t('common.errors.serverError'))
      : t('common.errors.serverError')
    toastStore.push({ type: 'error', title: msg })
  }
}

function openInlineAdd(): void {
  inlineAddForm.validFrom = new Date().toISOString().slice(0, 10)
  inlineAddForm.validTo = ''
  inlineAddForm.amount = null
  showInlineAdd.value = true
}

async function handleInlineAddSave(): Promise<void> {
  if (!inlineAddForm.validFrom || inlineAddForm.amount === null || inlineAddForm.amount < 0) return
  try {
    await store.createRevision(budgetId, lineId, {
      validFrom: inlineAddForm.validFrom,
      validTo: inlineAddForm.validTo || undefined,
      amount: inlineAddForm.amount,
    })
    showInlineAdd.value = false
    toastStore.push({ type: 'success', title: t('budgetStructure.budgetLines.customizations.createSuccess') })
  } catch (err) {
    const code = extractApiErrorCode(err)
    const msg = code
      ? t(`budgetStructure.budgetLines.customizations.errors.${_camelCase(code)}`, t('common.errors.serverError'))
      : t('common.errors.serverError')
    toastStore.push({ type: 'error', title: msg })
  }
}

function confirmDelete(revisionId: string): void {
  deletingRevisionId.value = revisionId
  showDeleteConfirm.value = true
}

async function handleDelete(): Promise<void> {
  if (!deletingRevisionId.value) return
  const id = deletingRevisionId.value
  showDeleteConfirm.value = false
  deletingRevisionId.value = null
  try {
    await store.deleteRevision(budgetId, lineId, id)
    toastStore.push({ type: 'success', title: t('budgetStructure.budgetLines.customizations.deleteSuccess') })
  } catch (err) {
    const code = extractApiErrorCode(err)
    let msg: string
    if (code === 'CANNOT_DELETE_ORIGINAL_REVISION') {
      msg = t('budgetStructure.budgetLines.customizations.errors.cannotDeleteOriginal')
    } else if (code === 'REVISION_HAS_ACTIVE_EXECUTIONS') {
      msg = t('budgetStructure.budgetLines.customizations.errors.hasActiveExecutions')
    } else {
      msg = t('common.errors.serverError')
    }
    toastStore.push({ type: 'error', title: msg })
  }
}

/** Converts SCREAMING_SNAKE_CASE to camelCase for i18n key lookup. */
function _camelCase(code: string): string {
  return code
    .toLowerCase()
    .replace(/_([a-z])/g, (_, c: string) => c.toUpperCase())
}

onMounted(async () => {
  await Promise.all([
    store.fetchRevisions(budgetId, lineId),
    store.loadLines(budgetId),
  ])

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

<template>
  <div class="container mx-auto px-4 py-6">
    <BudgetTabs :budget-id="budgetId" class="mb-6" />

    <!-- Show-deleted toggle -->
    <div class="flex items-center gap-2 mb-4">
      <input
        id="show-deleted-groups"
        v-model="store.showDeletedCategoryGroups"
        type="checkbox"
        class="checkbox checkbox-sm"
      />
      <label for="show-deleted-groups" class="label-text cursor-pointer">
        {{ t('budgetStructure.categoryGroups.showDeleted') }}
      </label>
    </div>

    <!-- Loading indicator -->
    <div v-if="store.loading" class="flex justify-center py-8">
      <span class="loading loading-spinner loading-md" />
    </div>

    <!-- Empty state -->
    <EmptyState
      v-else-if="store.categoryGroups.length === 0"
      :title="t('budgetStructure.categoryGroups.empty.title')"
      :description="t('budgetStructure.categoryGroups.empty.description')"
      :action-label="isAdmin ? t('budgetStructure.categoryGroups.empty.action') : undefined"
      :action="isAdmin ? openCreateGroupModal : undefined"
    />

    <!-- Groups list -->
    <div v-else>
      <!-- Admin: draggable groups -->
      <VueDraggable
        v-if="isAdmin"
        v-model="draggableGroups"
        handle=".group-drag-handle"
        :animation="150"
        @end="onGroupsReordered"
      >
        <div
          v-for="group in draggableGroups"
          :key="group.id"
          class="card bg-base-200 mb-4 shadow-sm"
          :class="{ 'opacity-60': !!group.deletedAt }"
        >
          <div class="card-body p-4">
            <!-- Group header -->
            <div class="flex items-center gap-2 mb-3">
              <span
                class="group-drag-handle cursor-grab text-base-content/40 hover:text-base-content"
                :class="{ 'pointer-events-none': !!group.deletedAt }"
              >
                &#8597;
              </span>
              <template v-if="inlineEditingGroupId === group.id">
                <input
                  v-model="inlineGroupName"
                  type="text"
                  class="input input-xs input-bordered flex-1"
                  @keyup.enter="handleGroupInlineSave(group.id)"
                  @keyup.escape="inlineEditingGroupId = null"
                />
              </template>
              <h3 v-else class="font-semibold text-base flex-1 cursor-pointer select-none" @dblclick="isAdmin && !group.deletedAt ? startGroupEdit(group) : undefined">
                {{ group.name }}
                <span v-if="group.deletedAt" class="badge badge-error badge-sm ml-2">{{ t('budgetStructure.common.deleted') }}</span>
              </h3>
              <template v-if="inlineEditingGroupId === group.id">
                <button
                  type="button"
                  class="btn btn-xs btn-ghost btn-square text-success"
                  :title="t('budgetStructure.common.save')"
                  @click="handleGroupInlineSave(group.id)"
                >
                  <Check :size="14" />
                </button>
                <button
                  type="button"
                  class="btn btn-xs btn-ghost btn-square"
                  :title="t('budgetStructure.common.cancel')"
                  @click="inlineEditingGroupId = null"
                >
                  <X :size="14" />
                </button>
              </template>
              <!-- Deleted group: restore only -->
              <template v-else-if="group.deletedAt">
                <button
                  type="button"
                  class="btn btn-success btn-xs"
                  @click="handleRestoreGroup(group.id)"
                >
                  <RotateCcw :size="14" />
                  {{ t('budgetStructure.common.restore') }}
                </button>
              </template>
              <!-- Active group: edit + delete -->
              <template v-else>
                <button
                  type="button"
                  class="btn btn-xs btn-ghost btn-square"
                  :title="t('budgetStructure.categoryGroups.edit')"
                  @click="openEditGroupModal(group)"
                >
                  <Pencil :size="14" />
                </button>
                <button
                  type="button"
                  class="btn btn-xs btn-ghost btn-square text-error"
                  :title="t('budgetStructure.categoryGroups.delete')"
                  @click="confirmDeleteGroup(group.id)"
                >
                  <Trash2 :size="14" />
                </button>
              </template>
            </div>

            <!-- Categories list (draggable) — skip drag for deleted groups -->
            <VueDraggable
              v-model="group.categories"
              handle=".cat-drag-handle"
              :animation="150"
              :disabled="!!group.deletedAt"
              @end="() => onCategoriesReordered(group.id, group.categories)"
            >
              <div
                v-for="category in group.categories"
                :key="category.id"
                class="flex items-center gap-2 py-1.5 px-2 rounded hover:bg-base-300"
                :class="{ 'opacity-60': !!category.deletedAt }"
              >
                <span class="cat-drag-handle cursor-grab text-base-content/40 hover:text-base-content text-sm">
                  &#8597;
                </span>
                <template v-if="inlineEditingCategoryId === category.id">
                  <input
                    v-model="inlineCategoryName"
                    type="text"
                    class="input input-xs input-bordered flex-1"
                    @keyup.enter="handleCategoryInlineSave(group.id, category.id)"
                    @keyup.escape="inlineEditingCategoryId = null"
                  />
                </template>
                <span v-else class="flex-1 text-sm cursor-pointer select-none" @dblclick="isAdmin && !category.deletedAt ? startCategoryEdit(category) : undefined">
                  {{ category.name }}
                  <span v-if="category.deletedAt" class="badge badge-error badge-xs ml-1">{{ t('budgetStructure.common.deleted') }}</span>
                </span>
                <template v-if="inlineEditingCategoryId === category.id">
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost btn-square text-success"
                    :title="t('budgetStructure.common.save')"
                    @click="handleCategoryInlineSave(group.id, category.id)"
                  >
                    <Check :size="14" />
                  </button>
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost btn-square"
                    :title="t('budgetStructure.common.cancel')"
                    @click="inlineEditingCategoryId = null"
                  >
                    <X :size="14" />
                  </button>
                </template>
                <!-- Deleted category: restore only -->
                <template v-else-if="category.deletedAt">
                  <button
                    type="button"
                    class="btn btn-success btn-xs"
                    @click="handleRestoreCategory(group.id, category.id)"
                  >
                    <RotateCcw :size="14" />
                    {{ t('budgetStructure.common.restore') }}
                  </button>
                </template>
                <!-- Active category: edit + delete -->
                <template v-else>
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost btn-square"
                    :title="t('budgetStructure.categories.edit')"
                    @click="openEditCategoryModal(group.id, category)"
                  >
                    <Pencil :size="14" />
                  </button>
                  <button
                    type="button"
                    class="btn btn-xs btn-ghost btn-square text-error"
                    :title="t('budgetStructure.categories.delete')"
                    @click="confirmDeleteCategory(group.id, category.id)"
                  >
                    <Trash2 :size="14" />
                  </button>
                </template>
              </div>
            </VueDraggable>

            <!-- Add category button — only for non-deleted groups -->
            <div v-if="!group.deletedAt" class="mt-2">
              <button
                type="button"
                class="btn btn-xs btn-ghost text-primary"
                @click="openCreateCategoryModal(group.id)"
              >
                + {{ t('budgetStructure.categories.create') }}
              </button>
            </div>
          </div>
        </div>
      </VueDraggable>

      <!-- Non-admin: static groups list -->
      <div v-else>
        <div
          v-for="group in store.categoryGroups"
          :key="group.id"
          class="card bg-base-200 mb-4 shadow-sm"
        >
          <div class="card-body p-4">
            <h3 class="font-semibold text-base mb-3">{{ group.name }}</h3>
            <div
              v-for="category in group.categories"
              :key="category.id"
              class="py-1.5 px-2 rounded text-sm"
            >
              {{ category.name }}
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- CategoryGroupForm modal -->
    <CategoryGroupForm
      v-if="showGroupForm"
      :model-value="editingGroup"
      @submit="handleGroupFormSubmitWithToast"
      @cancel="closeGroupModal"
    />

    <!-- CategoryForm modal -->
    <CategoryForm
      v-if="showCategoryForm"
      :model-value="editingCategory"
      :group-id="activeCategoryGroupId ?? ''"
      @submit="handleCategoryFormSubmitWithToast"
      @cancel="closeCategoryModal"
    />

    <!-- Delete group confirmation -->
    <dialog v-if="showDeleteGroupConfirm" class="modal modal-open">
      <div class="modal-box">
        <h3 class="font-bold text-lg mb-4">{{ t('budgetStructure.categoryGroups.delete') }}</h3>
        <p>{{ t('budgetStructure.categoryGroups.confirmDelete') }}</p>
        <div class="modal-action">
          <button type="button" class="btn btn-ghost" @click="showDeleteGroupConfirm = false">
            {{ t('budgetStructure.common.cancel') }}
          </button>
          <button type="button" class="btn btn-error" @click="handleDeleteGroup">
            {{ t('budgetStructure.common.confirm') }}
          </button>
        </div>
      </div>
      <div class="modal-backdrop" @click="showDeleteGroupConfirm = false" />
    </dialog>

    <!-- Delete category confirmation -->
    <dialog v-if="showDeleteCategoryConfirm" class="modal modal-open">
      <div class="modal-box">
        <h3 class="font-bold text-lg mb-4">{{ t('budgetStructure.categories.delete') }}</h3>
        <p>{{ t('budgetStructure.categories.confirmDelete') }}</p>
        <div class="modal-action">
          <button type="button" class="btn btn-ghost" @click="showDeleteCategoryConfirm = false">
            {{ t('budgetStructure.common.cancel') }}
          </button>
          <button type="button" class="btn btn-error" @click="handleDeleteCategory">
            {{ t('budgetStructure.common.confirm') }}
          </button>
        </div>
      </div>
      <div class="modal-backdrop" @click="showDeleteCategoryConfirm = false" />
    </dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { VueDraggable } from 'vue-draggable-plus'
import { Check, Pencil, RotateCcw, Trash2, X } from 'lucide-vue-next'
import { useBudgetStructureStore } from '../store'
import { useLayoutStore } from '@/stores/layout.store'
import { useToastStore } from '@/stores/toast.store'
import { useRoleGate } from '../composables/useRoleGate'
import { extractApiErrorCode } from '../utils/apiError'
import BudgetTabs from '../components/BudgetTabs.vue'
import CategoryGroupForm from '../components/CategoryGroupForm.vue'
import CategoryForm from '../components/CategoryForm.vue'
import EmptyState from '../components/EmptyState.vue'
import type { CategoryGroupResponse, CategoryItem } from '../types'

const route = useRoute()
const { t } = useI18n()

const budgetId = route.params.budgetId as string

const store = useBudgetStructureStore()
const layoutStore = useLayoutStore()
const toastStore = useToastStore()
const { isAdmin } = useRoleGate(budgetId)

// Local reactive proxy for draggable (synced from store)
const draggableGroups = computed({
  get: () => store.categoryGroups,
  set: (val) => {
    store.categoryGroups.splice(0, store.categoryGroups.length, ...val)
  },
})

// --- Group inline edit state ---
const inlineEditingGroupId = ref<string | null>(null)
const inlineGroupName = ref('')

function startGroupEdit(group: CategoryGroupResponse): void {
  inlineEditingGroupId.value = group.id
  inlineGroupName.value = group.name
}

function _groupErrorToast(err: unknown): void {
  const code = extractApiErrorCode(err)
  if (code === 'CATEGORY_GROUP_NAME_DUPLICATE') {
    toastStore.push({ type: 'error', title: t('budgetStructure.categoryGroups.errors.nameDuplicate') })
  } else {
    toastStore.push({ type: 'error', title: t('common.errors.serverError') })
  }
}

function _categoryErrorToast(err: unknown): void {
  const code = extractApiErrorCode(err)
  if (code === 'CATEGORY_NAME_DUPLICATE') {
    toastStore.push({ type: 'error', title: t('budgetStructure.categories.errors.nameDuplicate') })
  } else {
    toastStore.push({ type: 'error', title: t('common.errors.serverError') })
  }
}

async function handleGroupInlineSave(groupId: string): Promise<void> {
  if (!inlineGroupName.value.trim()) return
  try {
    await store.updateGroup(budgetId, groupId, { name: inlineGroupName.value })
    inlineEditingGroupId.value = null
    toastStore.push({ type: 'success', title: t('budgetStructure.categoryGroups.updateSuccess') })
  } catch (err) {
    _groupErrorToast(err)
  }
}

// --- Category inline edit state ---
const inlineEditingCategoryId = ref<string | null>(null)
const inlineCategoryName = ref('')

function startCategoryEdit(category: CategoryItem): void {
  inlineEditingCategoryId.value = category.id
  inlineCategoryName.value = category.name
}

async function handleCategoryInlineSave(groupId: string, categoryId: string): Promise<void> {
  if (!inlineCategoryName.value.trim()) return
  try {
    await store.updateCategory(budgetId, groupId, categoryId, { name: inlineCategoryName.value })
    inlineEditingCategoryId.value = null
    toastStore.push({ type: 'success', title: t('budgetStructure.categories.updateSuccess') })
  } catch (err) {
    _categoryErrorToast(err)
  }
}

// --- Group modal state ---
const showGroupForm = ref(false)
const editingGroup = ref<CategoryGroupResponse | null>(null)

function openCreateGroupModal(): void {
  editingGroup.value = null
  showGroupForm.value = true
}

function openEditGroupModal(group: CategoryGroupResponse): void {
  editingGroup.value = group
  showGroupForm.value = true
}

function closeGroupModal(): void {
  showGroupForm.value = false
  editingGroup.value = null
}

async function handleGroupFormSubmit(payload: { name: string }): Promise<void> {
  if (editingGroup.value) {
    await store.updateGroup(budgetId, editingGroup.value.id, payload)
  } else {
    await store.createGroup(budgetId, payload)
  }
  closeGroupModal()
}

// --- Delete group ---
const showDeleteGroupConfirm = ref(false)
const deletingGroupId = ref<string | null>(null)

function confirmDeleteGroup(groupId: string): void {
  deletingGroupId.value = groupId
  showDeleteGroupConfirm.value = true
}

async function handleDeleteGroup(): Promise<void> {
  if (!deletingGroupId.value) return
  await store.deleteGroup(budgetId, deletingGroupId.value)
  showDeleteGroupConfirm.value = false
  deletingGroupId.value = null
  toastStore.push({ type: 'success', title: t('budgetStructure.categoryGroups.deleteSuccess') })
}

async function handleRestoreGroup(groupId: string): Promise<void> {
  await store.restoreGroup(budgetId, groupId, false)
  toastStore.push({ type: 'success', title: t('budgetStructure.categoryGroups.restoreSuccess') })
}

// --- Category modal state ---
const showCategoryForm = ref(false)
const editingCategory = ref<CategoryItem | null>(null)
const activeCategoryGroupId = ref<string | null>(null)

function openCreateCategoryModal(groupId: string): void {
  activeCategoryGroupId.value = groupId
  editingCategory.value = null
  showCategoryForm.value = true
}

function openEditCategoryModal(groupId: string, category: CategoryItem): void {
  activeCategoryGroupId.value = groupId
  editingCategory.value = category
  showCategoryForm.value = true
}

function closeCategoryModal(): void {
  showCategoryForm.value = false
  editingCategory.value = null
  activeCategoryGroupId.value = null
}

async function handleCategoryFormSubmit(payload: { name: string }): Promise<void> {
  if (!activeCategoryGroupId.value) return
  if (editingCategory.value) {
    await store.updateCategory(
      budgetId,
      activeCategoryGroupId.value,
      editingCategory.value.id,
      payload,
    )
  } else {
    await store.createCategory(budgetId, activeCategoryGroupId.value, payload)
  }
  closeCategoryModal()
}

// --- Delete category ---
const showDeleteCategoryConfirm = ref(false)
const deletingCategoryId = ref<string | null>(null)
const deletingCategoryGroupId = ref<string | null>(null)

function confirmDeleteCategory(groupId: string, categoryId: string): void {
  deletingCategoryGroupId.value = groupId
  deletingCategoryId.value = categoryId
  showDeleteCategoryConfirm.value = true
}

async function handleDeleteCategory(): Promise<void> {
  if (!deletingCategoryGroupId.value || !deletingCategoryId.value) return
  await store.deleteCategory(budgetId, deletingCategoryGroupId.value, deletingCategoryId.value)
  showDeleteCategoryConfirm.value = false
  deletingCategoryId.value = null
  deletingCategoryGroupId.value = null
  toastStore.push({ type: 'success', title: t('budgetStructure.categories.deleteSuccess') })
}

async function handleRestoreCategory(groupId: string, categoryId: string): Promise<void> {
  await store.restoreCategory(budgetId, groupId, categoryId, false)
  toastStore.push({ type: 'success', title: t('budgetStructure.categories.restoreSuccess') })
}

// Also toast on group create
async function handleGroupFormSubmitWithToast(payload: { name: string }): Promise<void> {
  const isNew = !editingGroup.value
  try {
    await handleGroupFormSubmit(payload)
    if (isNew) {
      toastStore.push({ type: 'success', title: t('budgetStructure.categoryGroups.createSuccess') })
    } else {
      toastStore.push({ type: 'success', title: t('budgetStructure.categoryGroups.updateSuccess') })
    }
  } catch (err) {
    _groupErrorToast(err)
  }
}

// Also toast on category create
async function handleCategoryFormSubmitWithToast(payload: { name: string }): Promise<void> {
  const isNew = !editingCategory.value
  try {
    await handleCategoryFormSubmit(payload)
    if (isNew) {
      toastStore.push({ type: 'success', title: t('budgetStructure.categories.createSuccess') })
    } else {
      toastStore.push({ type: 'success', title: t('budgetStructure.categories.updateSuccess') })
    }
  } catch (err) {
    _categoryErrorToast(err)
  }
}

// --- Drag-and-drop handlers ---
async function onGroupsReordered(): Promise<void> {
  const ids = store.categoryGroups.map((g) => g.id)
  await store.reorderGroups(budgetId, ids)
}

async function onCategoriesReordered(
  groupId: string,
  categories: CategoryItem[],
): Promise<void> {
  const ids = categories.map((c) => c.id)
  await store.reorderCategories(budgetId, groupId, ids)
}

// --- Lifecycle ---
watch(() => store.showDeletedCategoryGroups, async () => {
  await store.loadGroups(budgetId)
})

onMounted(async () => {
  await store.loadGroups(budgetId)

  if (isAdmin.value) {
    layoutStore.setPageActions([
      {
        key: 'new-group',
        label: t('budgetStructure.categoryGroups.create'),
        action: openCreateGroupModal,
        variant: 'primary',
      },
    ])
  }
})

onUnmounted(() => {
  layoutStore.clearPageActions()
})
</script>

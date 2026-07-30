<template>
  <div class="container mx-auto px-4 py-6">
    <BudgetTabs :budget-id="budgetId" class="mb-6" />

    <div class="flex items-center justify-between mb-4">
      <h2 class="text-xl font-semibold">{{ t('bankAccount.title') }}</h2>
      <button class="btn btn-primary btn-sm" @click="openCreate">
        + {{ t('bankAccount.create') }}
      </button>
    </div>

    <!-- Show deleted toggle -->
    <div class="flex items-center gap-2 mb-4">
      <input
        id="show-deleted-accounts"
        v-model="store.showDeletedAccounts"
        type="checkbox"
        class="checkbox checkbox-sm"
        @change="store.fetchAccounts(budgetId)"
      />
      <label for="show-deleted-accounts" class="label-text cursor-pointer">
        {{ t('bankAccount.showDeleted') }}
      </label>
    </div>

    <div v-if="store.loading" class="text-center py-8">
      <span class="loading loading-spinner loading-md"></span>
    </div>

    <div v-else-if="store.error" class="alert alert-error">
      {{ store.error }}
    </div>

    <div v-else-if="store.accounts.length === 0" class="text-center py-8 text-base-content/50">
      {{ t('bankAccount.empty') }}
    </div>

    <div v-else class="overflow-x-auto select-none">
      <table class="table table-zebra w-full">
        <thead>
          <tr>
            <th>{{ t('bankAccount.columns.alias') }}</th>
            <th>{{ t('bankAccount.columns.currency') }}</th>
            <th>{{ t('bankAccount.columns.type') }}</th>
            <th>{{ t('bankAccount.columns.order') }}</th>
            <th>{{ t('bankAccount.columns.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="account in store.accounts"
            :key="account.id"
            :class="{ 'opacity-60': !!account.deletedAt }"
          >
            <td>
              {{ account.alias }}
              <span v-if="account.deletedAt" class="badge badge-error badge-sm ml-2">
                {{ t('bankAccount.deleted') }}
              </span>
            </td>
            <td>{{ getCurrencyCode(account.currencyId) }}</td>
            <td>
              <span
                class="badge"
                :class="account.isPositive ? 'badge-success' : 'badge-error'"
              >
                {{
                  account.isPositive
                    ? t('bankAccount.positive')
                    : t('bankAccount.negative')
                }}
              </span>
            </td>
            <td>{{ account.displayOrder }}</td>
            <td>
              <div class="flex gap-1">
                <!-- Active account actions -->
                <template v-if="!account.deletedAt">
                  <button
                    class="btn btn-xs btn-ghost btn-square"
                    :title="t('bankAccount.edit')"
                    @click="openEdit(account)"
                  >
                    <Pencil :size="14" />
                  </button>
                  <button
                    class="btn btn-xs btn-ghost btn-square text-error"
                    :title="t('bankAccount.delete')"
                    @click="openDelete(account)"
                  >
                    <Trash2 :size="14" />
                  </button>
                </template>
                <!-- Deleted account actions -->
                <template v-else>
                  <button
                    class="btn btn-success btn-xs"
                    :title="t('bankAccount.restore')"
                    @click="handleRestore(account)"
                  >
                    <RotateCcw :size="14" />
                    {{ t('bankAccount.restore') }}
                  </button>
                </template>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Create / Edit Modal -->
    <dialog v-if="showForm" class="modal modal-open">
      <div class="modal-box">
        <h3 class="font-bold text-lg mb-4">
          {{ editingAccount ? t('bankAccount.editTitle') : t('bankAccount.createTitle') }}
        </h3>
        <BankAccountForm
          :initial-values="formInitialValues"
          :currencies="currencies"
          :is-edit="!!editingAccount"
          @submit="handleFormSubmit"
          @cancel="closeForm"
        />
      </div>
      <form method="dialog" class="modal-backdrop" @click="closeForm">
        <button>close</button>
      </form>
    </dialog>

    <!-- Delete Confirm Modal -->
    <dialog v-if="deletingAccount" class="modal modal-open">
      <div class="modal-box">
        <h3 class="font-bold text-lg mb-2">{{ t('bankAccount.deleteTitle') }}</h3>
        <p class="mb-4">
          {{ t('bankAccount.deleteConfirm', { alias: deletingAccount.alias }) }}
        </p>
        <div class="flex justify-end gap-2">
          <button class="btn btn-ghost" @click="deletingAccount = null">
            {{ t('common.cancel') }}
          </button>
          <button class="btn btn-error" :disabled="deleteLoading" @click="confirmDelete">
            <span v-if="deleteLoading" class="loading loading-spinner loading-xs"></span>
            {{ t('bankAccount.delete') }}
          </button>
        </div>
      </div>
      <form method="dialog" class="modal-backdrop" @click="deletingAccount = null">
        <button>close</button>
      </form>
    </dialog>

    <div v-if="formError" class="alert alert-error mt-2">{{ formError }}</div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Pencil, RotateCcw, Trash2 } from 'lucide-vue-next'
import { useBankAccountStore } from '../store/useBankAccountStore'
import { useToastStore } from '@/stores/toast.store'
import { extractApiErrorCode } from '@/features/budget-structure/utils/apiError'
import BudgetTabs from '@/features/budget-structure/components/BudgetTabs.vue'
import BankAccountForm from '../components/BankAccountForm.vue'
import type { BankAccount, CreateBankAccountDto, UpdateBankAccountDto } from '../types/bankAccount'
import { listCurrencies } from '@/features/budget-structure/api/currencies.api'
import type { CurrencyItem } from '@/features/budget-structure/types'

const route = useRoute()
const { t } = useI18n()
const store = useBankAccountStore()
const toastStore = useToastStore()

const budgetId = computed(() => route.params['budgetId'] as string)

const currencies = ref<CurrencyItem[]>([])
const showForm = ref(false)
const editingAccount = ref<BankAccount | null>(null)
const deletingAccount = ref<BankAccount | null>(null)
const formError = ref<string | null>(null)
const deleteLoading = ref(false)

const formInitialValues = computed(() => {
  if (!editingAccount.value) return undefined
  return {
    alias: editingAccount.value.alias,
    currencyId: editingAccount.value.currencyId,
    isPositive: editingAccount.value.isPositive,
    displayOrder: editingAccount.value.displayOrder,
  }
})

function getCurrencyCode(currencyId: string): string {
  return currencies.value.find((c) => c.id === currencyId)?.code ?? currencyId.slice(0, 8)
}

function openCreate(): void {
  editingAccount.value = null
  formError.value = null
  showForm.value = true
}

function openEdit(account: BankAccount): void {
  editingAccount.value = account
  formError.value = null
  showForm.value = true
}

function openDelete(account: BankAccount): void {
  deletingAccount.value = account
}

function closeForm(): void {
  showForm.value = false
  editingAccount.value = null
  formError.value = null
}

async function handleFormSubmit(
  payload: CreateBankAccountDto | UpdateBankAccountDto,
): Promise<void> {
  formError.value = null
  try {
    if (editingAccount.value) {
      await store.updateAccount(budgetId.value, editingAccount.value.id, payload as UpdateBankAccountDto)
      toastStore.push({ type: 'success', title: t('bankAccount.updateSuccess') })
    } else {
      await store.createAccount(budgetId.value, payload as CreateBankAccountDto)
      toastStore.push({ type: 'success', title: t('bankAccount.createSuccess') })
    }
    closeForm()
  } catch (e) {
    const code = extractApiErrorCode(e)
    if (code === 'ALIAS_DUPLICATE') {
      toastStore.push({ type: 'error', title: t('bankAccount.errors.aliasDuplicate') })
    } else {
      toastStore.push({ type: 'error', title: t('bankAccount.errors.saveFailed') })
    }
  }
}

async function confirmDelete(): Promise<void> {
  if (!deletingAccount.value) return
  deleteLoading.value = true
  try {
    await store.deleteAccount(budgetId.value, deletingAccount.value.id)
    toastStore.push({ type: 'success', title: t('bankAccount.deleteSuccess') })
    deletingAccount.value = null
  } catch (e) {
    formError.value = e instanceof Error ? e.message : t('bankAccount.errors.deleteFailed')
    deletingAccount.value = null
  } finally {
    deleteLoading.value = false
  }
}

async function handleRestore(account: BankAccount): Promise<void> {
  try {
    await store.restoreAccount(budgetId.value, account.id)
    toastStore.push({ type: 'success', title: t('bankAccount.restoreSuccess') })
  } catch (e) {
    formError.value = e instanceof Error ? e.message : t('bankAccount.errors.saveFailed')
  }
}

onMounted(async () => {
  await store.fetchAccounts(budgetId.value)
  try {
    currencies.value = await listCurrencies(budgetId.value)
  } catch {
    // non-fatal: currency codes fall back to partial id
  }
})
</script>

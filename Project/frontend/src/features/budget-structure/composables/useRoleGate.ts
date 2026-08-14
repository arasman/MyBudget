import { computed, type ComputedRef, isRef, type Ref } from 'vue'
import { useAuthStore } from '@/stores/auth.store'

export interface RoleGate {
  /** True when the user has role `owner` or `admin` for the given budget. */
  isAdmin: ComputedRef<boolean>
  /** True when the user has role `operator`, `owner`, or `admin`. */
  isOperator: ComputedRef<boolean>
  /** True when the user may modify budget structure (cycles, groups, categories). Same as isAdmin. */
  canWriteStructure: ComputedRef<boolean>
  /** True when the user may write budget lines. Same as isOperator. */
  canWriteLines: ComputedRef<boolean>
  /** True only when the resolved role is exactly `owner`. */
  isOwner: ComputedRef<boolean>
}

/**
 * Returns computed role-gate flags for the given budget.
 * Reads the active user's memberships from `authStore`.
 *
 * @param budgetId - the budget UUID to evaluate, as a plain string or a Ref<string>
 */
export function useRoleGate(budgetId: Ref<string> | string): RoleGate {
  const authStore = useAuthStore()

  const role = computed<string | undefined>(() => {
    const id = isRef(budgetId) ? budgetId.value : budgetId
    return authStore.user?.memberships.find((m) => m.budgetId === id)?.role
  })

  const isAdmin = computed(() => role.value === 'owner' || role.value === 'admin')
  const isOperator = computed(() => role.value === 'operator' || isAdmin.value)
  const canWriteStructure = computed(() => isAdmin.value)
  const canWriteLines = computed(() => isOperator.value)
  const isOwner = computed(() => role.value === 'owner')

  return { isAdmin, isOperator, canWriteStructure, canWriteLines, isOwner }
}

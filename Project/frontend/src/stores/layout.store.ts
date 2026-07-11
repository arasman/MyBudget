import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface PageAction {
  key: string
  label: string
  icon?: string
  action: () => void
  variant?: 'primary' | 'ghost' | 'error'
  disabled?: boolean
  requiresRole?: 'admin' | 'operator'
}

interface LayoutState {
  activeBudgetId: string | null
  activeBudgetName: string | null
  pageActions: PageAction[]
}

export const useLayoutStore = defineStore('layout', () => {
  // State
  const activeBudgetId = ref<LayoutState['activeBudgetId']>(null)
  const activeBudgetName = ref<LayoutState['activeBudgetName']>(null)
  const pageActions = ref<PageAction[]>([])

  function setPageActions(actions: PageAction[]): void {
    pageActions.value = actions
  }

  function clearPageActions(): void {
    pageActions.value = []
  }

  function setActiveBudget(id: string, name: string): void {
    activeBudgetId.value = id
    activeBudgetName.value = name
  }

  function clearActiveBudget(): void {
    activeBudgetId.value = null
    activeBudgetName.value = null
  }

  return {
    activeBudgetId,
    activeBudgetName,
    pageActions,
    setPageActions,
    clearPageActions,
    setActiveBudget,
    clearActiveBudget,
  }
})

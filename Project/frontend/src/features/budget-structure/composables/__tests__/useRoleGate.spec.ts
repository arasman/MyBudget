import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useRoleGate } from '../useRoleGate'

// Mock the auth store module
vi.mock('@/stores/auth.store', () => ({
  useAuthStore: vi.fn(),
}))

import { useAuthStore } from '@/stores/auth.store'

const BUDGET_ID = 'budget-1'

function setUserRole(role: string | undefined) {
  const mockedStore = vi.mocked(useAuthStore)
  mockedStore.mockReturnValue({
    user: role
      ? {
          id: 'user-1',
          email: 'test@example.com',
          firstName: 'Test',
          lastName: 'User',
          preferredLocale: 'en',
          memberships: [
            { budgetId: BUDGET_ID, budgetName: 'My Budget', role },
          ],
        }
      : {
          id: 'user-1',
          email: 'test@example.com',
          firstName: 'Test',
          lastName: 'User',
          preferredLocale: 'en',
          memberships: [],
        },
  } as ReturnType<typeof useAuthStore>)
}

function setNullUser() {
  vi.mocked(useAuthStore).mockReturnValue({
    user: null,
  } as unknown as ReturnType<typeof useAuthStore>)
}

describe('useRoleGate', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  describe('owner role', () => {
    it('isAdmin = true', () => {
      setUserRole('owner')
      const { isAdmin } = useRoleGate(BUDGET_ID)
      expect(isAdmin.value).toBe(true)
    })

    it('isOperator = true', () => {
      setUserRole('owner')
      const { isOperator } = useRoleGate(BUDGET_ID)
      expect(isOperator.value).toBe(true)
    })

    it('canWriteStructure = true', () => {
      setUserRole('owner')
      const { canWriteStructure } = useRoleGate(BUDGET_ID)
      expect(canWriteStructure.value).toBe(true)
    })

    it('canWriteLines = true', () => {
      setUserRole('owner')
      const { canWriteLines } = useRoleGate(BUDGET_ID)
      expect(canWriteLines.value).toBe(true)
    })
  })

  describe('admin role', () => {
    it('isAdmin = true', () => {
      setUserRole('admin')
      const { isAdmin } = useRoleGate(BUDGET_ID)
      expect(isAdmin.value).toBe(true)
    })

    it('isOperator = true', () => {
      setUserRole('admin')
      const { isOperator } = useRoleGate(BUDGET_ID)
      expect(isOperator.value).toBe(true)
    })

    it('canWriteStructure = true', () => {
      setUserRole('admin')
      const { canWriteStructure } = useRoleGate(BUDGET_ID)
      expect(canWriteStructure.value).toBe(true)
    })

    it('canWriteLines = true', () => {
      setUserRole('admin')
      const { canWriteLines } = useRoleGate(BUDGET_ID)
      expect(canWriteLines.value).toBe(true)
    })
  })

  describe('operator role', () => {
    it('isAdmin = false', () => {
      setUserRole('operator')
      const { isAdmin } = useRoleGate(BUDGET_ID)
      expect(isAdmin.value).toBe(false)
    })

    it('isOperator = true', () => {
      setUserRole('operator')
      const { isOperator } = useRoleGate(BUDGET_ID)
      expect(isOperator.value).toBe(true)
    })

    it('canWriteStructure = false', () => {
      setUserRole('operator')
      const { canWriteStructure } = useRoleGate(BUDGET_ID)
      expect(canWriteStructure.value).toBe(false)
    })

    it('canWriteLines = true', () => {
      setUserRole('operator')
      const { canWriteLines } = useRoleGate(BUDGET_ID)
      expect(canWriteLines.value).toBe(true)
    })
  })

  describe('read-only role (no matching membership)', () => {
    it('isAdmin = false when no membership for budgetId', () => {
      setUserRole(undefined)
      const { isAdmin } = useRoleGate(BUDGET_ID)
      expect(isAdmin.value).toBe(false)
    })

    it('isOperator = false when no membership for budgetId', () => {
      setUserRole(undefined)
      const { isOperator } = useRoleGate(BUDGET_ID)
      expect(isOperator.value).toBe(false)
    })

    it('canWriteStructure = false', () => {
      setUserRole(undefined)
      const { canWriteStructure } = useRoleGate(BUDGET_ID)
      expect(canWriteStructure.value).toBe(false)
    })

    it('canWriteLines = false', () => {
      setUserRole(undefined)
      const { canWriteLines } = useRoleGate(BUDGET_ID)
      expect(canWriteLines.value).toBe(false)
    })
  })

  describe('null user (unauthenticated)', () => {
    it('all flags are false', () => {
      setNullUser()
      const { isAdmin, isOperator, canWriteStructure, canWriteLines } = useRoleGate(BUDGET_ID)
      expect(isAdmin.value).toBe(false)
      expect(isOperator.value).toBe(false)
      expect(canWriteStructure.value).toBe(false)
      expect(canWriteLines.value).toBe(false)
    })
  })

  describe('unknown budgetId', () => {
    it('all flags are false when budgetId does not match any membership', () => {
      setUserRole('admin')
      const { isAdmin, isOperator, canWriteStructure, canWriteLines } = useRoleGate('unknown-budget')
      expect(isAdmin.value).toBe(false)
      expect(isOperator.value).toBe(false)
      expect(canWriteStructure.value).toBe(false)
      expect(canWriteLines.value).toBe(false)
    })
  })
})

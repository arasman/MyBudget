// budget-member-administration WU1 frontend (PR2b), MEMBERS-UI-1 (WU1 scenarios only).
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent, within } from '@testing-library/vue'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import { computed } from 'vue'

import BudgetMembersView from '../BudgetMembersView.vue'
import type { MemberDto } from '../../types'

// --- Mocks ---

const { mockListMembers, mockUpdateMemberRole, mockRemoveMember, mockRestoreMember } = vi.hoisted(() => ({
  mockListMembers: vi.fn(),
  mockUpdateMemberRole: vi.fn(),
  mockRemoveMember: vi.fn(),
  mockRestoreMember: vi.fn(),
}))

vi.mock('../../api/budgetMembers.api', () => ({
  listMembers: mockListMembers,
  updateMemberRole: mockUpdateMemberRole,
  removeMember: mockRemoveMember,
  restoreMember: mockRestoreMember,
}))

vi.mock('../../composables/useRoleGate', () => ({
  useRoleGate: vi.fn(),
}))

const { mockToastPush } = vi.hoisted(() => ({ mockToastPush: vi.fn() }))

vi.mock('@/stores/toast.store', () => ({
  useToastStore: () => ({ push: mockToastPush }),
}))

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: vi.fn(),
}))

vi.mock('../../components/BudgetTabs.vue', () => ({
  default: { template: '<div data-testid="budget-tabs" />' },
}))

import { useRoleGate } from '../../composables/useRoleGate'
import { useAuthStore } from '@/stores/auth.store'

const BUDGET_ID = 'budget-1'
const OWNER = 'owner-user'
const CALLER = 'caller-user'
const OTHER_ADMIN = 'other-admin-user'
const OPERATOR = 'operator-user'

function member(overrides: Partial<MemberDto>): MemberDto {
  return {
    userId: 'u-x',
    email: 'x@example.com',
    firstName: 'First',
    lastName: 'Last',
    role: 'operator',
    joinedAt: '2026-01-01T00:00:00Z',
    isDeleted: false,
    ...overrides,
  }
}

function setupRoleGate({ isAdmin = true, isOwner = false } = {}): void {
  vi.mocked(useRoleGate).mockReturnValue({
    isAdmin: computed(() => isAdmin),
    isOperator: computed(() => true),
    canWriteStructure: computed(() => isAdmin),
    canWriteLines: computed(() => true),
    isOwner: computed(() => isOwner),
  })
}

function setupAuth(userId: string): void {
  vi.mocked(useAuthStore).mockReturnValue({
    user: { id: userId, email: 'caller@example.com', firstName: 'Caller', lastName: 'User', preferredLocale: 'en', memberships: [] },
  } as unknown as ReturnType<typeof useAuthStore>)
}

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        common: {
          cancel: 'Cancel',
          confirm: 'Confirm',
        },
        budgetStructure: {
          members: {
            title: 'Members',
            tabTitle: 'Members',
            columns: { name: 'Name', email: 'Email', role: 'Role', joinedAt: 'Joined' },
            actions: { changeRole: 'Change role', remove: 'Remove', restore: 'Restore' },
            showDeleted: 'Show deleted',
            removeConfirmTitle: 'Remove Member',
            removeConfirm: 'Are you sure you want to remove this member?',
            confirmations: {
              roleChangeSuccess: 'Member role updated successfully',
              roleChangeError: 'Could not update member role',
              removeSuccess: 'Member removed successfully',
              restoreSuccess: 'Member restored successfully',
            },
          },
        },
        enums: {
          role: { admin: 'Admin', operator: 'Operator', readOnly: 'Read Only', owner: 'Owner' },
        },
      },
    },
  })
}

function makeRouter() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/budgets/:budgetId/members', name: 'BudgetMembers', component: BudgetMembersView },
    ],
  })
  return router
}

async function renderView(): Promise<ReturnType<typeof render>> {
  const router = makeRouter()
  await router.push(`/budgets/${BUDGET_ID}/members`)
  await router.isReady()
  return render(BudgetMembersView, {
    global: { plugins: [router, makeI18n()] },
  })
}

describe('BudgetMembersView — row gating (MEMBERS-UI-1, WU1)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('excludes the Owner row entirely — no role select, no control rendered', async () => {
    setupRoleGate({ isAdmin: true, isOwner: true })
    setupAuth(CALLER)
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OWNER, firstName: 'Own', lastName: 'Er', role: 'owner' }),
      member({ userId: OTHER_ADMIN, firstName: 'Other', lastName: 'Admin', role: 'admin' }),
    ])

    await renderView()

    await waitFor(() => expect(screen.getByText('Other Admin')).toBeTruthy())
    expect(screen.queryByText('Own Er')).toBeNull()
    // No select bound to the owner's role anywhere
    expect(screen.queryAllByRole('combobox')).toHaveLength(1)
  })

  it('renders neither a role select nor a remove button on the caller\'s own row', async () => {
    setupRoleGate({ isAdmin: true, isOwner: false })
    setupAuth(CALLER)
    mockListMembers.mockResolvedValueOnce([
      member({ userId: CALLER, firstName: 'Self', lastName: 'Row', role: 'admin' }),
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'operator' }),
    ])

    await renderView()

    await waitFor(() => expect(screen.getByText('Self Row')).toBeTruthy())
    // Only the operator row gets a select (caller's own admin row must not)
    expect(screen.queryAllByRole('combobox')).toHaveLength(1)

    // WU2: the operator row now legitimately gets a Remove button (canActOn is true for it) —
    // scope the "no remove button" assertion to the caller's own row specifically.
    const selfRow = screen.getByText('Self Row').closest('tr')!
    expect(within(selfRow).queryByRole('button', { name: /remove/i })).toBeNull()
  })

  it('Admin caller sees no controls on another Admin\'s row', async () => {
    setupRoleGate({ isAdmin: true, isOwner: false })
    setupAuth(CALLER)
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OTHER_ADMIN, firstName: 'Other', lastName: 'Admin', role: 'admin' }),
    ])

    await renderView()

    await waitFor(() => expect(screen.getByText('Other Admin')).toBeTruthy())
    expect(screen.queryAllByRole('combobox')).toHaveLength(0)
  })

  it('canActOn truth table: Owner caller CAN act on another Admin row', async () => {
    setupRoleGate({ isAdmin: true, isOwner: true })
    setupAuth(CALLER)
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OTHER_ADMIN, firstName: 'Other', lastName: 'Admin', role: 'admin' }),
    ])

    await renderView()

    await waitFor(() => expect(screen.getByText('Other Admin')).toBeTruthy())
    expect(screen.queryAllByRole('combobox')).toHaveLength(1)
  })

  it('canActOn truth table: non-admin caller sees no controls at all', async () => {
    setupRoleGate({ isAdmin: false, isOwner: false })
    setupAuth(CALLER)
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'operator' }),
    ])

    await renderView()

    await waitFor(() => expect(screen.getByText('Op Erator')).toBeTruthy())
    expect(screen.queryAllByRole('combobox')).toHaveLength(0)
  })

  it('role select reads and writes "read-only" (not "readonly")', async () => {
    setupRoleGate({ isAdmin: true, isOwner: true })
    setupAuth(CALLER)
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'read-only' }),
    ])
    mockUpdateMemberRole.mockResolvedValueOnce({ userId: OPERATOR, role: 'operator' })
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'operator' }),
    ])

    await renderView()

    const select = await screen.findByRole<HTMLSelectElement>('combobox')
    expect(select.value).toBe('read-only')

    await fireEvent.update(select, 'operator')

    await waitFor(() => {
      expect(mockUpdateMemberRole).toHaveBeenCalledWith(BUDGET_ID, OPERATOR, 'operator')
    })
  })
})

// budget-member-administration WU2 (PR3): show-deleted toggle, remove/restore actions
describe('BudgetMembersView — show-deleted toggle and remove/restore actions (MEMBERS-UI-1, WU2)', () => {
  beforeEach(() => {
    // Full reset (not just clearAllMocks) — queued mockResolvedValueOnce/mockReturnValueOnce
    // implementations from a prior test must never leak into the next one.
    mockListMembers.mockReset()
    mockUpdateMemberRole.mockReset()
    mockRemoveMember.mockReset()
    mockRestoreMember.mockReset()
    mockToastPush.mockReset()
  })

  it('show-deleted toggle is visible and functional for Admin (not Owner-only)', async () => {
    setupRoleGate({ isAdmin: true, isOwner: false })
    setupAuth(CALLER)
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'operator' }),
    ])
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'operator' }),
    ])

    await renderView()
    await waitFor(() => expect(screen.getByText('Op Erator')).toBeTruthy())

    const toggle = screen.getByLabelText('Show deleted')
    await fireEvent.click(toggle)

    await waitFor(() => {
      expect(mockListMembers).toHaveBeenCalledWith(BUDGET_ID, { includeDeleted: true })
    })
  })

  it('toggling ON renders soft-deleted rows dimmed with a Restore button; toggling OFF hides them again', async () => {
    setupRoleGate({ isAdmin: true, isOwner: true })
    setupAuth(CALLER)
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'operator' }),
    ])
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'operator' }),
      member({ userId: OTHER_ADMIN, firstName: 'Deleted', lastName: 'Member', role: 'operator', isDeleted: true }),
    ])
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'operator' }),
    ])

    await renderView()
    await waitFor(() => expect(screen.getByText('Op Erator')).toBeTruthy())
    expect(screen.queryByText('Deleted Member')).toBeNull()

    const toggle = screen.getByLabelText('Show deleted')
    await fireEvent.click(toggle)

    await waitFor(() => expect(screen.getByText('Deleted Member')).toBeTruthy())
    expect(screen.getByRole('button', { name: 'Restore' })).toBeTruthy()

    await fireEvent.click(toggle)
    await waitFor(() => expect(screen.queryByText('Deleted Member')).toBeNull())
  })

  it('clicking Remove opens a confirm dialog; confirming calls removeMember then refetches', async () => {
    setupRoleGate({ isAdmin: true, isOwner: true })
    setupAuth(CALLER)
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'operator' }),
    ])
    mockRemoveMember.mockResolvedValueOnce(undefined)
    mockListMembers.mockResolvedValueOnce([])

    await renderView()
    await waitFor(() => expect(screen.getByText('Op Erator')).toBeTruthy())

    await fireEvent.click(screen.getByRole('button', { name: 'Remove' }))
    await waitFor(() => expect(screen.getByText('Are you sure you want to remove this member?')).toBeTruthy())

    await fireEvent.click(screen.getByRole('button', { name: 'Confirm' }))

    await waitFor(() => {
      expect(mockRemoveMember).toHaveBeenCalledWith(BUDGET_ID, OPERATOR)
      expect(mockListMembers).toHaveBeenCalledTimes(2)
    })
  })

  it('clicking Restore on a soft-deleted row calls restoreMember then refetches', async () => {
    setupRoleGate({ isAdmin: true, isOwner: true })
    setupAuth(CALLER)
    mockListMembers.mockResolvedValueOnce([])
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'operator', isDeleted: true }),
    ])
    mockRestoreMember.mockResolvedValueOnce({ userId: OPERATOR, role: 'operator' })
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'operator' }),
    ])

    await renderView()
    await fireEvent.click(screen.getByLabelText('Show deleted'))
    await waitFor(() => expect(screen.getByText('Op Erator')).toBeTruthy())

    await fireEvent.click(screen.getByRole('button', { name: 'Restore' }))

    await waitFor(() => {
      expect(mockRestoreMember).toHaveBeenCalledWith(BUDGET_ID, OPERATOR)
      expect(mockListMembers).toHaveBeenCalledTimes(3)
    })
  })

  it('actionInProgress disables the acted-on row Remove button during the in-flight call', async () => {
    setupRoleGate({ isAdmin: true, isOwner: true })
    setupAuth(CALLER)
    mockListMembers.mockResolvedValueOnce([
      member({ userId: OPERATOR, firstName: 'Op', lastName: 'Erator', role: 'operator' }),
    ])
    let resolveRemove!: () => void
    mockRemoveMember.mockReturnValueOnce(
      new Promise<void>((resolve) => {
        resolveRemove = resolve
      }),
    )
    mockListMembers.mockResolvedValueOnce([])

    await renderView()
    await waitFor(() => expect(screen.getByText('Op Erator')).toBeTruthy())

    await fireEvent.click(screen.getByRole('button', { name: 'Remove' }))
    const confirmButton = await screen.findByRole('button', { name: 'Confirm' })
    await fireEvent.click(confirmButton)

    await waitFor(() => {
      expect((screen.getByRole('button', { name: 'Remove' }) as HTMLButtonElement).disabled).toBe(true)
    })

    resolveRemove()
  })
})

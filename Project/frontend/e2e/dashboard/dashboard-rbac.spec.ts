import { test, expect } from '@playwright/test'
import { seedDashboardBudget, upsertCutRecord, inviteMemberWithRole, registerNonMember } from './helpers'

/**
 * E2E: Dashboard role matrix — all 4 budget roles (owner, admin, operator,
 * read-only) can read every dashboard endpoint; a user with no membership on
 * the budget is denied.
 * Spec: DASH-8.
 *
 * Real HTTP round trip through the actual invitation + accept flow (Mailpit),
 * mirroring e2e/auth/invite-accept.spec.ts — this is the E2E-layer
 * confirmation of the role matrix that MyBudget.Integration.Tests already
 * covers at the WebApplicationFactory level for all 3 endpoints.
 */
test.describe('Dashboard — role matrix (DASH-8)', () => {
  test('owner, admin, operator, and read-only can all read every dashboard endpoint; a non-member is denied', async ({
    request,
  }) => {
    const fixture = await seedDashboardBudget(request, 'dash-rbac', 1)
    await upsertCutRecord(request, fixture, '2026-02-01', 1000)

    const endpoints = [
      `/api/budgets/${fixture.budgetId}/dashboard/cut-totals-series`,
      `/api/budgets/${fixture.budgetId}/dashboard/cut-totals-band`,
      `/api/budgets/${fixture.budgetId}/dashboard/line-series`,
    ]

    // Owner (the seeding user) — MUST see 200 on every endpoint.
    for (const url of endpoints) {
      const resp = await request.get(url, { headers: fixture.headers })
      expect(resp.status(), `owner on ${url}`).toBe(200)
    }

    // Admin, operator, read-only — each invited + accepted via the real
    // HTTP flow, MUST see 200 on every endpoint.
    const roles = ['admin', 'operator', 'read-only'] as const
    for (const role of roles) {
      const member = await inviteMemberWithRole(request, fixture.headers, fixture.budgetId, role, `dash-rbac-${role}`)
      const memberHeaders = { Authorization: `Bearer ${member.accessToken}` }
      for (const url of endpoints) {
        const resp = await request.get(url, { headers: memberHeaders })
        expect(resp.status(), `${role} on ${url}`).toBe(200)
      }
    }

    // No membership on this budget — MUST be denied on every endpoint.
    const nonMember = await registerNonMember(request, 'dash-rbac-none')
    const nonMemberHeaders = { Authorization: `Bearer ${nonMember.accessToken}` }
    for (const url of endpoints) {
      const resp = await request.get(url, { headers: nonMemberHeaders })
      expect(resp.status(), `non-member on ${url}`).toBe(403)
    }
  })
})

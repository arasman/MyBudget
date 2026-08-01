import type { APIRequestContext, Page } from '@playwright/test'
import { expect } from '@playwright/test'
import { loginWithToken as _loginWithToken } from '../helpers/auth'

export const PASSWORD = 'Password1!'
export const GTQ_CURRENCY_ID = '11111111-1111-1111-1111-111111111111'
export const USD_CURRENCY_ID = '22222222-2222-2222-2222-222222222222'
const MAILPIT_URL = process.env['MAILPIT_URL'] ?? 'http://localhost:8025'

export interface DashboardFixture {
  budgetId: string
  cycleId: string
  periodIds: string[]
  lineId: string
  accessToken: string
  headers: Record<string, string>
}

/**
 * Seeds an owner + one Cycle (GTQ default / USD alternate) + N periods,
 * a bank account, a category group + BudgetLine, and returns everything an
 * E2E dashboard test needs. No cut records or executions are seeded here —
 * callers add exactly what their scenario needs (DASH-1/2/3 vs DASH-4/5/6/12
 * scenarios need different data shapes).
 */
export async function seedDashboardBudget(
  request: APIRequestContext,
  prefix: string,
  periodCount = 2,
): Promise<DashboardFixture> {
  const email = `e2e-${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}@example.com`

  const regResp = await request.post('/api/auth/register', {
    data: { email, password: PASSWORD, firstName: 'E2E', lastName: 'Dashboard', preferredLocale: 'en' },
  })
  expect(regResp.status()).toBe(201)
  const { accessToken } = await regResp.json()
  const headers = { Authorization: `Bearer ${accessToken}` }

  const meResp = await request.get('/api/auth/me', { headers })
  const me = await meResp.json()
  const budgetId: string = me.memberships[0].budgetId

  const cycleResp = await request.post(`/api/budgets/${budgetId}/cycles`, {
    headers,
    data: {
      name: 'E2E Dashboard Cycle',
      startDate: '2026-01-01',
      endDate: '2026-12-31',
      defaultCurrencyId: GTQ_CURRENCY_ID,
      alternateCurrencyId: USD_CURRENCY_ID,
      exchangeRate: 7.8,
    },
  })
  expect(cycleResp.status()).toBe(201)
  const { id: cycleId } = await cycleResp.json()

  const periodDefs = [
    { name: 'P1', periodNumber: 1, startDate: '2026-01-01', endDate: '2026-06-30' },
    { name: 'P2', periodNumber: 2, startDate: '2026-07-01', endDate: '2026-12-31' },
  ].slice(0, periodCount)

  const periodIds: string[] = []
  for (const def of periodDefs) {
    const resp = await request.post(`/api/budgets/${budgetId}/cycles/${cycleId}/periods`, { headers, data: def })
    expect(resp.status()).toBe(201)
    const { id } = await resp.json()
    periodIds.push(id)
  }

  const accountResp = await request.post(`/api/budgets/${budgetId}/bank-accounts`, {
    headers,
    data: { alias: 'E2E Dashboard Account', currencyId: GTQ_CURRENCY_ID, isPositive: true, displayOrder: 0 },
  })
  expect(accountResp.status()).toBe(201)

  const groupResp = await request.post(`/api/budgets/${budgetId}/category-groups`, {
    headers,
    data: { name: 'E2E Dashboard Group', displayOrder: 1 },
  })
  expect(groupResp.status()).toBe(201)
  const { id: groupId } = await groupResp.json()

  const lineResp = await request.post(`/api/budgets/${budgetId}/lines`, {
    headers,
    data: {
      name: 'E2E Dashboard Line',
      lineType: 'Expense',
      categoryGroupId: groupId,
      startDate: '2026-01-01',
      endDate: null,
      initialAmount: 500,
      currencyId: GTQ_CURRENCY_ID,
    },
  })
  expect(lineResp.status()).toBe(201)
  const { id: lineId } = await lineResp.json()

  return { budgetId, cycleId, periodIds, lineId, accessToken, headers }
}

/** Creates a cut record for the given date (drives lifetime + band widgets, DASH-1/2/3). */
export async function upsertCutRecord(
  request: APIRequestContext,
  fixture: DashboardFixture,
  date: string,
  balance = 1000,
  exchangeRate = 7.8,
): Promise<void> {
  // A bank account already exists on the budget; balances upsert against the cut date.
  const accountsResp = await request.get(`/api/budgets/${fixture.budgetId}/bank-accounts`, {
    headers: fixture.headers,
  })
  const accounts = await accountsResp.json()
  const accountId: string = accounts[0].id

  const resp = await request.put(`/api/budgets/${fixture.budgetId}/cut-records/${date}`, {
    headers: fixture.headers,
    data: { exchangeRate, accounts: [{ bankAccountId: accountId, balance }] },
  })
  expect(resp.status()).toBe(200)
}

/** Creates an ExecutionRecord for the fixture's BudgetLine in the given period (DASH-4/5/6). */
export async function createExecution(
  request: APIRequestContext,
  fixture: DashboardFixture,
  periodId: string,
  amount = 200,
  operationDate = '2026-01-15',
): Promise<void> {
  const resp = await request.post(
    `/api/budgets/${fixture.budgetId}/periods/${periodId}/budget-lines/${fixture.lineId}/executions`,
    {
      headers: fixture.headers,
      data: {
        entryType: 1,
        amount,
        note: 'E2E dashboard execution',
        operationDate,
        currencyId: GTQ_CURRENCY_ID,
        exchangeRate: null,
        exchangeRateTo: null,
        accountId: null,
        paymentMethodId: null,
      },
    },
  )
  expect(resp.status()).toBe(201)
}

export async function loginWithToken(page: Page, accessToken: string, budgetId: string): Promise<void> {
  return _loginWithToken(page, { accessToken, activeBudgetId: budgetId })
}

export async function goToDashboard(page: Page, budgetId: string): Promise<void> {
  await page.goto(`/budgets/${budgetId}/dashboard`)
  await page.waitForLoadState('networkidle')
}

// ── DASH-8 role matrix helpers (mirrors e2e/auth/invite-accept.spec.ts) ────────

interface MailpitMessage {
  ID: string
  Subject: string
  To: { Address: string }[]
}
interface MailpitMessages {
  messages: MailpitMessage[]
}

async function waitForInviteEmail(
  request: APIRequestContext,
  to: string,
  timeoutMs = 15_000,
): Promise<string> {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const resp = await request.get(`${MAILPIT_URL}/api/v1/messages`)
    if (resp.ok()) {
      const data: MailpitMessages = await resp.json()
      const msg = data.messages?.find((m) => m.To.some((t) => t.Address === to) && m.Subject.includes('invited'))
      if (msg) {
        const bodyResp = await request.get(`${MAILPIT_URL}/api/v1/message/${msg.ID}`)
        const body = await bodyResp.json()
        return (body.Text || body.HTML) ?? ''
      }
    }
    await new Promise((r) => setTimeout(r, 500))
  }
  throw new Error(`Timeout waiting for invite email to ${to}`)
}

function extractInviteToken(emailBody: string): string {
  const match = emailBody.match(/\/invitations\/accept\?token=([^\s"'<>]+)/)
  if (!match?.[1]) throw new Error('Could not find invitation token in email body')
  return decodeURIComponent(match[1])
}

/**
 * Registers a new user, invites them to `budgetId` with `role`, accepts the
 * invite via the real HTTP flow (Mailpit round trip), and returns their
 * access token — now a real member of the budget with that role.
 */
export async function inviteMemberWithRole(
  request: APIRequestContext,
  ownerHeaders: Record<string, string>,
  budgetId: string,
  role: 'admin' | 'operator' | 'read-only',
  prefix: string,
): Promise<{ accessToken: string }> {
  const email = `e2e-${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}@example.com`

  const regResp = await request.post('/api/auth/register', {
    data: { email, password: PASSWORD, firstName: 'E2E', lastName: 'Member', preferredLocale: 'en' },
  })
  expect(regResp.status()).toBe(201)

  const inviteResp = await request.post(`/api/budgets/${budgetId}/invitations`, {
    headers: ownerHeaders,
    data: { email, role },
  })
  expect(inviteResp.status()).toBe(201)

  const emailBody = await waitForInviteEmail(request, email)
  const rawToken = extractInviteToken(emailBody)

  const loginResp = await request.post('/api/auth/login', { data: { email, password: PASSWORD } })
  expect(loginResp.status()).toBe(200)
  const { accessToken } = await loginResp.json()

  const acceptResp = await request.post('/api/auth/invitations/accept', {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: { token: rawToken },
  })
  expect(acceptResp.status()).toBe(200)

  return { accessToken }
}

/** Registers a fresh user with no membership on any shared budget. */
export async function registerNonMember(request: APIRequestContext, prefix: string): Promise<{ accessToken: string }> {
  const email = `e2e-${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}@example.com`
  const resp = await request.post('/api/auth/register', {
    data: { email, password: PASSWORD, firstName: 'E2E', lastName: 'NonMember', preferredLocale: 'en' },
  })
  expect(resp.status()).toBe(201)
  const { accessToken } = await resp.json()
  return { accessToken }
}

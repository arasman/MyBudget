using System.Net;
using System.Net.Http.Json;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetExecution;

/// <summary>
/// Integration tests for CreateExecutionRecord endpoint.
/// Covers REQ-EXEC-CREATE-1, REQ-EXEC-3, REQ-EXEC-4, REQ-EXEC-CLOSED-1.
/// </summary>
public sealed class CreateExecutionRecordIntegrationTests : BudgetExecutionTestBase
{
    public CreateExecutionRecordIntegrationTests(IntegrationTestFactory factory) : base(factory) { }

    private sealed record ErrorResponse(string Error);
    private sealed record IdResponse(Guid Id);

    // ── REQ-EXEC-CREATE-1: Happy path ────────────────────────────────────────

    [Fact]
    public async Task CreateExecution_HappyPath_Returns201WithId()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-create1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions",
            new
            {
                entryType       = 1,      // Expense
                amount          = 250m,
                note            = "Test execution note",
                operationDate   = new DateOnly(2025, 1, 15),
                currencyId      = GtqId,
                exchangeRate    = (decimal?)null,
                exchangeRateTo  = (decimal?)null,
                accountId       = (Guid?)null,
                paymentMethodId = (Guid?)null,
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        body!.Id.ShouldNotBe(Guid.Empty);
    }

    // ── REQ-EXEC-CLOSED-1: Period closed guard ───────────────────────────────

    [Fact]
    public async Task CreateExecution_ClosedPeriod_Returns409WithCode()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-create2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        await ClosePeriodAsync(budgetId, cycleId, periodId);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions",
            new
            {
                entryType       = 1,
                amount          = 100m,
                note            = "Test execution note",
                operationDate   = new DateOnly(2025, 1, 15),
                currencyId      = GtqId,
                exchangeRate    = (decimal?)null,
                exchangeRateTo  = (decimal?)null,
                accountId       = (Guid?)null,
                paymentMethodId = (Guid?)null,
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("PERIOD_CLOSED");
    }

    // ── REQ-EXEC-3: Amount must be positive ──────────────────────────────────

    [Fact]
    public async Task CreateExecution_ZeroAmount_Returns400()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-create3@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions",
            new
            {
                entryType      = 1,
                amount         = 0m,
                note           = (string?)null,
                currencyId     = GtqId,
                exchangeRate   = (decimal?)null,
                exchangeRateTo = (decimal?)null,
                accountId      = (Guid?)null,
                paymentMethodId = (Guid?)null,
            });

        // FluentValidation failures return 422 via ValidationBehaviour (not 400)
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // ── REQ-EXEC-4: Note required for CreditNote ──────────────────────────────

    [Fact]
    public async Task CreateExecution_CreditNoteWithoutNote_Returns400()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-create4@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions",
            new
            {
                entryType      = 2,     // CreditNote
                amount         = 50m,
                note           = (string?)null,  // missing — should fail
                currencyId     = GtqId,
                exchangeRate   = (decimal?)null,
                exchangeRateTo = (decimal?)null,
                accountId      = (Guid?)null,
                paymentMethodId = (Guid?)null,
            });

        // FluentValidation failures return 422 via ValidationBehaviour (not 400)
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // ── REQ-EXEC-DATE-RANGE-1: OperationDate must be within BudgetLine date range ──

    [Fact]
    public async Task CreateExecution_DateBeforeLineStart_Rejected()
    {
        // REQ-EXEC-DATE-RANGE-1: operationDate < MAX(Period.StartDate, BudgetLine.StartDate) → 422
        var (_, budgetId) = await SetupOwnerAsync("exec-daterange1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        // Line starts Jan 10 — before Jan 10 is out of range
        var lineId = await CreateBudgetLineAsync(budgetId, periodId, groupId,
            name: "RangeLine", startDate: new DateOnly(2025, 1, 10));

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions",
            new { entryType = 1, amount = 100m, note = "date range test",
                  operationDate = new DateOnly(2025, 1, 5), // before line starts (Jan 10)
                  currencyId = GtqId, exchangeRate = (decimal?)null,
                  exchangeRateTo = (decimal?)null, accountId = (Guid?)null,
                  paymentMethodId = (Guid?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("BUDGET_LINE_NOT_IN_PERIOD");
    }

    [Fact]
    public async Task CreateExecution_DateAfterLineEnd_Rejected()
    {
        // REQ-EXEC-DATE-RANGE-1: operationDate > MIN(Period.EndDate, BudgetLine.EndDate) → 422
        var (_, budgetId) = await SetupOwnerAsync("exec-daterange2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        // Line ends Jan 20 — after Jan 20 is out of range
        var lineId = await CreateBudgetLineAsync(budgetId, periodId, groupId,
            name: "ExpiredLine", startDate: new DateOnly(2025, 1, 1),
            endDate: new DateOnly(2025, 1, 20));

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions",
            new { entryType = 1, amount = 100m, note = "date range test",
                  operationDate = new DateOnly(2025, 1, 25), // after line ends (Jan 20)
                  currencyId = GtqId, exchangeRate = (decimal?)null,
                  exchangeRateTo = (decimal?)null, accountId = (Guid?)null,
                  paymentMethodId = (Guid?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("BUDGET_LINE_NOT_IN_PERIOD");
    }

    [Fact]
    public async Task CreateExecution_DateWithinBothPeriodAndLine_Accepted()
    {
        // REQ-EXEC-DATE-RANGE-1: operationDate within the intersection of Period and BudgetLine ranges → 201
        var (_, budgetId) = await SetupOwnerAsync("exec-daterange3@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        // Line: Jan 10–Jan 25; Period: Jan 1–Jan 31; intersection: Jan 10–Jan 25
        var lineId = await CreateBudgetLineAsync(budgetId, periodId, groupId,
            name: "IntersectLine", startDate: new DateOnly(2025, 1, 10),
            endDate: new DateOnly(2025, 1, 25));

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions",
            new { entryType = 1, amount = 200m, note = "date range test",
                  operationDate = new DateOnly(2025, 1, 15), // within Jan 10–25
                  currencyId = GtqId, exchangeRate = (decimal?)null,
                  exchangeRateTo = (decimal?)null, accountId = (Guid?)null,
                  paymentMethodId = (Guid?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        body!.Id.ShouldNotBe(Guid.Empty);
    }

    // ── RBAC: budget:read cannot write ───────────────────────────────────────

    [Fact]
    public async Task CreateExecution_ReadOnlyUser_Returns403Or404()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-create5-owner@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        // Switch to a user with no membership (simulates 403 → 404 via RBAC middleware)
        var viewerToken = await SetupViewerAsync(budgetId, "exec-create5-viewer@example.com");
        AuthorizeClient(viewerToken);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions",
            new
            {
                entryType      = 1,
                amount         = 100m,
                note           = (string?)null,
                currencyId     = GtqId,
                exchangeRate   = (decimal?)null,
                exchangeRateTo = (decimal?)null,
                accountId      = (Guid?)null,
                paymentMethodId = (Guid?)null,
            });

        // Read-only members cannot write; returns 403 (or 404 via RBAC resource guard)
        ((int)response.StatusCode).ShouldBeOneOf(403, 404);
    }
}

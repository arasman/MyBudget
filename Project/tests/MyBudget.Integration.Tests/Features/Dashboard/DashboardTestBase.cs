using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Features.BudgetStructure;
using MyBudget.Integration.Tests.Infrastructure;

namespace MyBudget.Integration.Tests.Features.Dashboard;

/// <summary>
/// Shared helpers for Dashboard integration tests.
/// Extends BudgetStructureTestBase to reuse budget/role setup (SetupOwnerAsync, SetupViewerAsync).
/// Adds Operator/Admin role setup and direct CutRecord seeding (dashboard reads persisted
/// CutRecord totals directly — no HTTP flow needed to set them up for these read-only tests).
/// </summary>
public abstract class DashboardTestBase : BudgetStructureTestBase
{
    protected DashboardTestBase(IntegrationTestFactory factory) : base(factory) { }

    // ── Auth helpers (DASH-8 role matrix) ─────────────────────────────────────

    /// <summary>Registers a user with Operator role on the given budget and returns their token.</summary>
    protected async Task<string> SetupOperatorAsync(Guid budgetId, string email)
    {
        var login = await RegisterUserAsync(email);

        using var scope = Factory.Services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.BudgetMemberships.Add(BudgetMembership.Create(budgetId, login.User.Id, BudgetRole.Operator));
        await db.SaveChangesAsync();

        return login.AccessToken;
    }

    /// <summary>Registers a user with Admin role on the given budget and returns their token.</summary>
    protected async Task<string> SetupAdminAsync(Guid budgetId, string email)
    {
        var login = await RegisterUserAsync(email);

        using var scope = Factory.Services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.BudgetMemberships.Add(BudgetMembership.Create(budgetId, login.User.Id, BudgetRole.Admin));
        await db.SaveChangesAsync();

        return login.AccessToken;
    }

    // ── CutRecord seeding (direct EF — dashboard slice is read-only over persisted data) ──

    /// <summary>
    /// Inserts a CutRecord directly via EF with the given CutDate/ExchangeRate and all
    /// 16 totals set to the same marker value (sufficient to prove series shape/ordering
    /// without needing the full CurrentSituation upsert HTTP flow).
    /// </summary>
    protected async Task<Guid> CreateCutRecordAsync(
        Guid     budgetId,
        DateOnly cutDate,
        decimal  exchangeRate = 7.8m,
        decimal  marker       = 100m)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var totals = new CutTotals(
            marker, marker,
            marker, marker,
            marker, marker,
            marker, marker,
            marker, marker,
            marker, marker,
            marker, marker,
            marker, marker);

        var cutRecord = CutRecord.Create(budgetId, cutDate, exchangeRate, totals);
        db.CutRecords.Add(cutRecord);
        await db.SaveChangesAsync();

        return cutRecord.Id;
    }
}

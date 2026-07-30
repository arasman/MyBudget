using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.CurrentSituation.Shared;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.CurrentSituation.UpsertCutRecord;

/// <summary>
/// Full-replace upsert for a cut record:
///   (a) verify an active period covers CutDate (CS-1);
///   (b) resolve accounts, compute BalanceInPrimary (CS-5), and fail ACCOUNT_NOT_FOUND
///       — all before any SaveChanges (design.md Decision 4);
///   (c) compute the execution summary (shared query) and the 16 totals (shared calculator, CS-6);
///   (d) in one transaction: create/replace the CutRecord header with totals, delete existing
///       CutBankAccount rows, insert the new ones.
/// Request body total fields (if any) are never read — totals are always server-computed.
/// </summary>
public sealed class UpsertCutRecordHandler : IRequestHandler<UpsertCutRecordCommand, Result<bool>>
{
    private readonly AppDbContext    _db;
    private readonly ConnectionFactory _factory;

    public UpsertCutRecordHandler(AppDbContext db, ConnectionFactory factory)
    {
        _db      = db;
        _factory = factory;
    }

    public async ValueTask<Result<bool>> Handle(UpsertCutRecordCommand cmd, CancellationToken ct)
    {
        // (a) Check that an active period covers the cut date
        using var conn = _factory.CreateConnection();

        const string activePeriodSql = """
            SELECT COUNT(1)
            FROM "Periods" p
            JOIN "Cycles" cy ON cy."Id" = p."CycleId"
            WHERE cy."BudgetId"  = @BudgetId
              AND cy."DeletedAt" IS NULL
              AND p."DeletedAt"  IS NULL
              AND p."IsClosed"   = false
              AND p."StartDate"  <= @CutDate
              AND p."EndDate"    >= @CutDate
            """;

        var coveringPeriods = await conn.ExecuteScalarAsync<int>(
            activePeriodSql,
            new { cmd.BudgetId, CutDate = cmd.CutDate.ToDateTime(TimeOnly.MinValue) });

        if (coveringPeriods == 0)
            return Result<bool>.Failure("NO_ACTIVE_PERIOD_FOR_CUT_DATE");

        // Load the active cycle to determine primary/alternate currency
        // Find the cycle whose date range contains the cut date (no IsActive filter —
        // IsActive is a UI display flag, not a data integrity guard for cut records).
        var cycle = await _db.Cycles
            .FirstOrDefaultAsync(
                c => c.BudgetId == cmd.BudgetId
                     && c.StartDate <= cmd.CutDate
                     && c.EndDate   >= cmd.CutDate,
                ct);

        if (cycle is null)
            return Result<bool>.Failure("NO_ACTIVE_PERIOD_FOR_CUT_DATE");

        // (b) Resolve accounts + compute BalanceInPrimary (CS-5) + fail ACCOUNT_NOT_FOUND
        //     — before any SaveChanges (design.md Decision 4).
        var accountIds = cmd.Accounts.Select(a => a.BankAccountId).ToList();

        var accounts = await _db.BankAccounts
            .IgnoreQueryFilters()
            .Where(a => accountIds.Contains(a.Id) && a.BudgetId == cmd.BudgetId)
            .ToListAsync(ct);

        var accountLookup = accounts.ToDictionary(a => a.Id);

        var resolvedItems = new List<(BankAccount BankAccount, decimal Balance, decimal BalanceInPrimary)>();

        foreach (var item in cmd.Accounts)
        {
            if (!accountLookup.TryGetValue(item.BankAccountId, out var bankAccount))
                return Result<bool>.Failure("ACCOUNT_NOT_FOUND");

            var balanceInPrimary = bankAccount.CurrencyId == cycle.DefaultCurrencyId
                ? item.Balance
                : item.Balance * cmd.ExchangeRate;

            resolvedItems.Add((bankAccount, item.Balance, balanceInPrimary));
        }

        // (c) Execution summary (shared query) + 16 totals (shared calculator, CS-6)
        var executionSummary = await BudgetExecutionSummaryQuery.ExecuteAsync(conn, cmd.BudgetId, cmd.CutDate);

        var totals = CutTotalsCalculator.Compute(
            resolvedItems.Select(r => (r.BankAccount.IsPositive, r.BalanceInPrimary)),
            executionSummary,
            cmd.ExchangeRate);

        // (d) One transaction: header (create/replace with totals) + CutBankAccount rows.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var cutRecord = await _db.CutRecords
            .Include(cr => cr.CutBankAccounts)
            .FirstOrDefaultAsync(
                cr => cr.BudgetId == cmd.BudgetId && cr.CutDate == cmd.CutDate,
                ct);

        if (cutRecord is null)
        {
            cutRecord = CutRecord.Create(
                cmd.BudgetId,
                cmd.CutDate,
                cmd.ExchangeRate,
                totals,
                cmd.ProjectionsJson);
            _db.CutRecords.Add(cutRecord);
        }
        else
        {
            _db.CutBankAccounts.RemoveRange(cutRecord.CutBankAccounts);
            cutRecord.Update(cmd.ExchangeRate, totals, cmd.ProjectionsJson);
        }

        // Save to get the CutRecord Id before inserting CutBankAccounts
        await _db.SaveChangesAsync(ct);

        foreach (var (bankAccount, balance, balanceInPrimary) in resolvedItems)
        {
            var snapshot = CutBankAccount.Create(
                cutRecord.Id,
                bankAccount.Id,
                bankAccount.Alias,
                bankAccount.CurrencyId,
                bankAccount.IsPositive,
                bankAccount.DisplayOrder,
                balance,
                balanceInPrimary);

            _db.CutBankAccounts.Add(snapshot);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<bool>.Success(true);
    }
}

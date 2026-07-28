using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.CurrentSituation.UpsertCutRecord;

/// <summary>
/// Full-replace upsert for a cut record:
///   (a) verify an active period covers CutDate (CS-1);
///   (b) load or create the CutRecord header;
///   (c) delete all existing CutBankAccount rows;
///   (d) for each account: compute BalanceInPrimary (CS-5);
///   (e) insert new CutBankAccount rows;
///   (f) SaveChanges.
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
              AND cy."IsActive"  = true
              AND cy."DeletedAt" IS NULL
              AND p."DeletedAt"  IS NULL
              AND p."IsClosed"   = false
              AND p."StartDate"  <= @CutDate
              AND p."EndDate"    >= @CutDate
            """;

        var coveringPeriods = await conn.ExecuteScalarAsync<int>(
            activePeriodSql,
            new { cmd.BudgetId, CutDate = cmd.CutDate });

        if (coveringPeriods == 0)
            return Result<bool>.Failure("NO_ACTIVE_PERIOD_FOR_CUT_DATE");

        // Load the active cycle to determine primary/alternate currency
        var cycle = await _db.Cycles
            .FirstOrDefaultAsync(c => c.BudgetId == cmd.BudgetId && c.IsActive, ct);

        if (cycle is null)
            return Result<bool>.Failure("NO_ACTIVE_PERIOD_FOR_CUT_DATE");

        // (b) Load or create CutRecord header
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
                cmd.ProjectionsJson);
            _db.CutRecords.Add(cutRecord);
        }
        else
        {
            // (c) Delete existing snapshot rows
            _db.CutBankAccounts.RemoveRange(cutRecord.CutBankAccounts);
            cutRecord.Update(cmd.ExchangeRate, cmd.ProjectionsJson);
        }

        // Save to get the CutRecord Id before inserting CutBankAccounts
        await _db.SaveChangesAsync(ct);

        // (d) & (e) Insert new CutBankAccount rows
        var accountIds = cmd.Accounts.Select(a => a.BankAccountId).ToList();

        var accounts = await _db.BankAccounts
            .IgnoreQueryFilters()
            .Where(a => accountIds.Contains(a.Id) && a.BudgetId == cmd.BudgetId)
            .ToListAsync(ct);

        var accountLookup = accounts.ToDictionary(a => a.Id);

        foreach (var item in cmd.Accounts)
        {
            if (!accountLookup.TryGetValue(item.BankAccountId, out var bankAccount))
                return Result<bool>.Failure("ACCOUNT_NOT_FOUND");

            // CS-5: BalanceInPrimary computation
            var balanceInPrimary = bankAccount.CurrencyId == cycle.DefaultCurrencyId
                ? item.Balance
                : item.Balance * cmd.ExchangeRate;

            var snapshot = CutBankAccount.Create(
                cutRecord.Id,
                bankAccount.Id,
                bankAccount.Alias,
                bankAccount.CurrencyId,
                bankAccount.IsPositive,
                bankAccount.DisplayOrder,
                item.Balance,
                balanceInPrimary);

            _db.CutBankAccounts.Add(snapshot);
        }

        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

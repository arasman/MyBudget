namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Marker interface for entities whose mutations should be recorded in AuditLog.
/// Whitelisted types: Budget, Cycle, Period, CategoryGroup, Category, BudgetLine, BudgetLineRevision.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// Returns the BudgetId to denormalize on the AuditLog entry.
    /// Budget returns its own Id; Cycle/CategoryGroup return BudgetId.
    /// Entities without a direct BudgetId FK (Period, Category, BudgetLine, BudgetLineRevision)
    /// return null — the BudgetId is resolved via a Dapper fallback at audit time.
    /// </summary>
    Guid? ResolveBudgetId();
}

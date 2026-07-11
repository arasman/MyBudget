namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Standalone audit record for whitelisted entity mutations.
/// Does NOT extend BaseEntity — it has its own Guid PK and no UpdatedAt.
/// </summary>
public sealed class AuditLog
{
    public Guid            Id         { get; private set; }
    public string          EntityName { get; private set; } = string.Empty;
    public Guid            EntityId   { get; private set; }
    public string          Action     { get; private set; } = string.Empty;
    public Guid?           UserId     { get; private set; }
    public DateTimeOffset  Timestamp  { get; private set; }
    public string?         BeforeJson { get; private set; }
    public string?         AfterJson  { get; private set; }
    public Guid?           BudgetId   { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        string          entityName,
        Guid            entityId,
        string          action,
        Guid?           userId,
        string?         beforeJson,
        string?         afterJson,
        Guid?           budgetId)
    {
        return new AuditLog
        {
            Id         = Guid.NewGuid(),
            EntityName = entityName,
            EntityId   = entityId,
            Action     = action,
            UserId     = userId,
            Timestamp  = DateTimeOffset.UtcNow,
            BeforeJson = beforeJson,
            AfterJson  = afterJson,
            BudgetId   = budgetId,
        };
    }
}

namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Immutable currency catalog entity. No BaseEntity — no timestamps, no soft delete.
/// Rows are seeded once and never modified via the application.
/// </summary>
public sealed class Currency
{
    public Guid   Id     { get; private set; }
    public string Code   { get; private set; } = string.Empty;
    public string Name   { get; private set; } = string.Empty;
    public string Symbol { get; private set; } = string.Empty;

    // Private parameterless constructor for EF Core
    private Currency() { }

    internal static Currency Create(Guid id, string code, string name, string symbol)
        => new() { Id = id, Code = code, Name = name, Symbol = symbol };
}

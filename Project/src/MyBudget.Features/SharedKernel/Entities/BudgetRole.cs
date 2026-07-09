namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Budget membership roles ordered by privilege level.
/// Integer values enable >= comparisons without a lookup table.
/// </summary>
public enum BudgetRole
{
    ReadOnly = 10,
    Operator = 20,
    Admin    = 30,
    Owner    = 40,
}

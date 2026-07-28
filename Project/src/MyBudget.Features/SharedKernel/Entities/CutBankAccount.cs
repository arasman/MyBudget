namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Point-in-time snapshot of a bank account's balance within a cut record.
/// Alias/CurrencyId/IsPositive/DisplayOrder are denormalized from BankAccount at cut time.
/// BalanceInPrimary is computed at write time: Balance × ExchangeRate for alternate currency,
/// or Balance for primary currency (CS-5).
/// </summary>
public sealed class CutBankAccount : BaseEntity
{
    public Guid    CutRecordId    { get; private set; }
    public Guid    BankAccountId  { get; private set; }
    public string  Alias          { get; private set; } = string.Empty;
    public Guid    CurrencyId     { get; private set; }
    public bool    IsPositive     { get; private set; }
    public int     DisplayOrder   { get; private set; }
    public decimal Balance        { get; private set; }
    public decimal BalanceInPrimary { get; private set; }

    // Navigation
    public CutRecord?    CutRecord   { get; private set; }
    public BankAccount?  BankAccount { get; private set; }

    private CutBankAccount() { }

    public static CutBankAccount Create(
        Guid    cutRecordId,
        Guid    bankAccountId,
        string  alias,
        Guid    currencyId,
        bool    isPositive,
        int     displayOrder,
        decimal balance,
        decimal balanceInPrimary)
    {
        return new CutBankAccount
        {
            CutRecordId     = cutRecordId,
            BankAccountId   = bankAccountId,
            Alias           = alias,
            CurrencyId      = currencyId,
            IsPositive      = isPositive,
            DisplayOrder    = displayOrder,
            Balance         = balance,
            BalanceInPrimary = balanceInPrimary,
        };
    }
}

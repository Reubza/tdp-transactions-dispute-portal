using DisputePortal.Domain.Accounts;
using DisputePortal.Domain.Common;

namespace DisputePortal.Domain.Transactions;

/// <summary>
/// A transaction on a customer's account. Transactions are owned by the core banking ledger;
/// this portal treats them as read-only reference data.
/// </summary>
public sealed class Transaction
{
    private Transaction()
    {
    }

    public Transaction(
        Guid id,
        Guid accountId,
        decimal amount,
        string currency,
        TransactionDirection direction,
        TransactionStatus status,
        string merchant,
        string reference,
        string? description,
        DateTimeOffset postedAt)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive; use Direction for sign.");
        }

        Id = id;
        AccountId = accountId;
        Amount = amount;
        Currency = Guard.NotBlank(currency, nameof(currency), 3).ToUpperInvariant();
        Direction = direction;
        Status = status;
        Merchant = Guard.NotBlank(merchant, nameof(merchant), 200);
        Reference = Guard.NotBlank(reference, nameof(reference), 64);
        Description = Guard.Optional(description, nameof(description), 500);
        PostedAt = postedAt;
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    public Account Account { get; private set; } = null!;

    /// <summary>Absolute amount in <see cref="Currency"/>; see <see cref="Direction"/> for sign.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public TransactionDirection Direction { get; private set; }

    public TransactionStatus Status { get; private set; }

    public string Merchant { get; private set; } = string.Empty;

    public string Reference { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTimeOffset PostedAt { get; private set; }

    /// <summary>
    /// Returns <c>null</c> if the transaction may be disputed at <paramref name="now"/>,
    /// otherwise the reason it may not.
    /// </summary>
    public DomainException? CheckDisputable(DateTimeOffset now, TimeSpan disputeWindow)
    {
        if (Direction != TransactionDirection.Debit)
        {
            return new DomainException(DisputeErrorCodes.NotADebit, "Only debit transactions can be disputed.");
        }

        if (Status != TransactionStatus.Posted)
        {
            return new DomainException(DisputeErrorCodes.NotPosted, "Only posted transactions can be disputed.");
        }

        if (now - PostedAt > disputeWindow)
        {
            return new DomainException(
                DisputeErrorCodes.WindowExpired,
                $"Transactions can only be disputed within {disputeWindow.TotalDays:0} days of being posted.");
        }

        return null;
    }
}

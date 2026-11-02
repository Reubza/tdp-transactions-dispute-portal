using DisputePortal.Domain.Common;

namespace DisputePortal.Domain.Accounts;

public sealed class Account
{
    private Account()
    {
    }

    public Account(Guid id, Guid customerId, string accountNumber, string name, AccountType type, string currency)
    {
        Id = id;
        CustomerId = customerId;
        AccountNumber = Guard.NotBlank(accountNumber, nameof(accountNumber), 34);
        Name = Guard.NotBlank(name, nameof(name), 100);
        Type = type;
        Currency = Guard.NotBlank(currency, nameof(currency), 3).ToUpperInvariant();
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string AccountNumber { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public AccountType Type { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    /// <summary>Account number with all but the last four digits hidden, safe to display.</summary>
    public string MaskedAccountNumber => Mask(AccountNumber);

    public static string Mask(string accountNumber)
    {
        ArgumentNullException.ThrowIfNull(accountNumber);
        return accountNumber.Length <= 4 ? accountNumber : $"****{accountNumber[^4..]}";
    }
}

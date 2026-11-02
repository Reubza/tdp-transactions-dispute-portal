using DisputePortal.Domain.Accounts;
using DisputePortal.Domain.Transactions;
using DisputePortal.Domain.Users;

namespace DisputePortal.UnitTests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    public static readonly TimeSpan Window = TimeSpan.FromDays(120);

    public static User Customer(string name = "Test Customer") =>
        new(Guid.NewGuid(), name, $"{Guid.NewGuid():N}@example.com", UserRole.Customer);

    public static Account AccountFor(User customer) =>
        new(Guid.NewGuid(), customer.Id, Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture), "Cheque", AccountType.Cheque, "ZAR");

    public static Transaction Transaction(
        Guid accountId,
        TransactionDirection direction = TransactionDirection.Debit,
        TransactionStatus status = TransactionStatus.Posted,
        TimeSpan? age = null,
        string merchant = "Test Merchant") =>
        new(
            Guid.NewGuid(),
            accountId,
            123.45m,
            "ZAR",
            direction,
            status,
            merchant,
            $"REF{Guid.NewGuid():N}"[..12],
            null,
            Now - (age ?? TimeSpan.FromDays(3)));
}

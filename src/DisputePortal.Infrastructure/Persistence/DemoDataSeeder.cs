using System.Globalization;
using DisputePortal.Application.Abstractions;
using DisputePortal.Domain.Accounts;
using DisputePortal.Domain.Disputes;
using DisputePortal.Domain.Transactions;
using DisputePortal.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DisputePortal.Infrastructure.Persistence;

/// <summary>
/// Seeds an empty database with demo users, accounts, ~6 months of transactions and a handful of disputes
/// in different lifecycle states. Data is deterministic (fixed random seed) relative to the current date.
/// </summary>
public sealed class DemoDataSeeder(AppDbContext db, IPasswordHasher passwordHasher, TimeProvider clock, ILogger<DemoDataSeeder> logger)
{
    public const string CustomerEmail = "thandi@example.com";
    public const string SecondCustomerEmail = "james@example.com";
    public const string AgentEmail = "agent@example.com";

    private static readonly (string Merchant, decimal Min, decimal Max)[] Merchants =
    [
        ("Checkers Sixty60", 150m, 1800m),
        ("Woolworths Food", 80m, 1200m),
        ("Takealot.com", 199m, 6500m),
        ("Uber Trip", 45m, 320m),
        ("Netflix", 199m, 199m),
        ("Spotify Premium", 69.99m, 69.99m),
        ("Shell Garage Rivonia", 400m, 1300m),
        ("Dis-Chem Pharmacy", 60m, 900m),
        ("Vodacom Airtime", 29m, 499m),
        ("Mr Price Home", 120m, 2200m),
        ("Steers Sandton", 65m, 260m),
        ("Virgin Active", 649m, 649m),
    ];

    public async Task SeedAsync(SeedOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (await db.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Database already contains data; skipping demo seed");
            return;
        }

        if (string.IsNullOrWhiteSpace(options.DemoPassword))
        {
            throw new InvalidOperationException("Database:Seed:DemoPassword must be set when seeding is enabled.");
        }

        var now = clock.GetUtcNow();
        var random = new Random(20261102);
        var passwordHash = passwordHasher.Hash(options.DemoPassword);

        var thandi = CreateUser("Thandi Nkosi", CustomerEmail, UserRole.Customer, passwordHash);
        var james = CreateUser("James Smith", SecondCustomerEmail, UserRole.Customer, passwordHash);
        var agent = CreateUser("Lerato Mokoena", AgentEmail, UserRole.Agent, passwordHash);
        db.Users.AddRange(thandi, james, agent);

        var thandiCheque = new Account(Guid.CreateVersion7(), thandi.Id, "62012345678", "Everyday Cheque", AccountType.Cheque, "ZAR");
        var thandiCard = new Account(Guid.CreateVersion7(), thandi.Id, "4000123412349876", "Platinum Credit Card", AccountType.CreditCard, "ZAR");
        var jamesCheque = new Account(Guid.CreateVersion7(), james.Id, "62098765432", "Everyday Cheque", AccountType.Cheque, "ZAR");
        db.Accounts.AddRange(thandiCheque, thandiCard, jamesCheque);

        var thandiChequeTx = GenerateTransactions(thandiCheque, now, random, count: 45, withSalary: true);
        var thandiCardTx = GenerateTransactions(thandiCard, now, random, count: 30, withSalary: false);
        var jamesTx = GenerateTransactions(jamesCheque, now, random, count: 35, withSalary: true);
        db.Transactions.AddRange(thandiChequeTx.Concat(thandiCardTx).Concat(jamesTx));

        var disputeWindow = TimeSpan.FromDays(3650); // seeded disputes are back-dated; the window is enforced at runtime
        var disputable = thandiChequeTx.Concat(thandiCardTx)
            .Where(t => t.Direction == TransactionDirection.Debit && t.Status == TransactionStatus.Posted)
            .OrderBy(t => t.PostedAt)
            .ToList();

        var upheld = RaiseAt(disputable[5], thandi, DisputeReason.Duplicate, "I was charged twice for the same order.", disputeWindow);
        upheld.StartReview(agent.Id, "Checking with the merchant.", upheld.CreatedAt.AddDays(1));
        upheld.Resolve(agent.Id, upheld: true, "Duplicate charge confirmed; refund issued.", upheld.CreatedAt.AddDays(4));

        var rejected = RaiseAt(disputable[12], thandi, DisputeReason.Unauthorised, "I don't recognise this transaction.", disputeWindow);
        rejected.StartReview(agent.Id, null, rejected.CreatedAt.AddDays(1));
        rejected.Resolve(agent.Id, upheld: false, "Transaction was authorised with your card and PIN at the merchant.", rejected.CreatedAt.AddDays(6));

        var withdrawn = RaiseAt(disputable[20], thandi, DisputeReason.IncorrectAmount, "The amount charged was higher than the till slip.", disputeWindow);
        withdrawn.Withdraw(thandi.Id, "Merchant refunded the difference directly.", withdrawn.CreatedAt.AddDays(2));

        var inReview = RaiseAt(disputable[^4], thandi, DisputeReason.GoodsOrServicesNotReceived, "Order never arrived and the merchant is not responding.", disputeWindow);
        inReview.StartReview(agent.Id, "Requested proof of delivery from merchant.", inReview.CreatedAt.AddHours(5));

        var jamesDisputable = jamesTx.Where(t => t.Direction == TransactionDirection.Debit && t.Status == TransactionStatus.Posted)
            .OrderBy(t => t.PostedAt).ToList();
        var submitted = RaiseAt(jamesDisputable[^3], james, DisputeReason.CancelledSubscription, "I cancelled this subscription last month but was still billed.", disputeWindow);

        db.Disputes.AddRange(upheld, rejected, withdrawn, inReview, submitted);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded demo data: {Users} users, {Transactions} transactions, {Disputes} disputes",
            3, thandiChequeTx.Count + thandiCardTx.Count + jamesTx.Count, 5);
    }

    private static User CreateUser(string name, string email, UserRole role, string passwordHash)
    {
        var user = new User(Guid.CreateVersion7(), name, email, role);
        user.SetPasswordHash(passwordHash);
        return user;
    }

    private static Dispute RaiseAt(Transaction tx, User customer, DisputeReason reason, string description, TimeSpan window) =>
        Dispute.Raise(tx, customer.Id, reason, description, null, tx.PostedAt.AddDays(2), window);

    private static List<Transaction> GenerateTransactions(
        Account account, DateTimeOffset now, Random random, int count, bool withSalary)
    {
        var transactions = new List<Transaction>();
        var start = now.AddDays(-180);

        for (var i = 0; i < count; i++)
        {
            var (merchant, min, max) = Merchants[random.Next(Merchants.Length)];
            var amount = Math.Round(min + ((max - min) * (decimal)random.NextDouble()), 2);
            var postedAt = start.AddMinutes(random.Next(0, 180 * 24 * 60));
            var status = now - postedAt < TimeSpan.FromDays(2) ? TransactionStatus.Pending : TransactionStatus.Posted;
            transactions.Add(NewTransaction(account, amount, TransactionDirection.Debit, status, merchant, postedAt, null));
        }

        // A couple of guaranteed-recent posted debits so the demo always has disputable transactions.
        transactions.Add(NewTransaction(account, 1249.00m, TransactionDirection.Debit, TransactionStatus.Posted, "Takealot.com", now.AddDays(-5), "Order #TK-88213"));
        transactions.Add(NewTransaction(account, 349.50m, TransactionDirection.Debit, TransactionStatus.Posted, "Uber Eats", now.AddDays(-3), null));
        transactions.Add(NewTransaction(account, 89.99m, TransactionDirection.Debit, TransactionStatus.Pending, "Apple.com/bill", now.AddHours(-6), null));

        if (withSalary)
        {
            for (var month = 0; month < 6; month++)
            {
                var payday = new DateTimeOffset(now.Year, now.Month, 25, 8, 0, 0, TimeSpan.Zero).AddMonths(-month);
                if (payday < now)
                {
                    transactions.Add(NewTransaction(account, 32500m, TransactionDirection.Credit, TransactionStatus.Posted, "ACME Corp Salary", payday, "Monthly salary"));
                }
            }
        }

        return transactions;
    }

    private static Transaction NewTransaction(
        Account account,
        decimal amount,
        TransactionDirection direction,
        TransactionStatus status,
        string merchant,
        DateTimeOffset postedAt,
        string? description)
    {
        var reference = string.Create(
            CultureInfo.InvariantCulture, $"TXN{postedAt:yyyyMMdd}{Random.Shared.Next(100000, 999999)}");
        return new Transaction(
            Guid.CreateVersion7(postedAt),
            account.Id,
            amount,
            account.Currency,
            direction,
            status,
            merchant,
            reference,
            description,
            postedAt);
    }
}

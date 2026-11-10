using System.Globalization;
using Application.Abstractions;
using Domain.Accounts;
using Domain.Disputes;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Seeds an empty database with demo users, accounts, ~6 months of transactions and a handful of disputes
/// in different lifecycle states. Data is deterministic (fixed random seed) relative to the current date.
/// </summary>
public sealed class DemoDataSeeder(AppDbContext db, IPasswordHasher passwordHasher, TimeProvider clock, ILogger<DemoDataSeeder> logger)
{
    public const string CustomerEmail = "thandi@example.com";
    public const string SecondCustomerEmail = "james@example.com";
    public const string AgentEmail = "agent@example.com";

    // Card merchants with plausible spend ranges (ZAR).
    private static readonly (string Merchant, decimal Min, decimal Max)[] CardMerchants =
    [
        ("Pick n Pay", 120m, 1650m),
        ("Checkers Sixty60", 150m, 1800m),
        ("Woolworths Food", 80m, 1200m),
        ("Shoprite", 90m, 950m),
        ("Engen", 400m, 1300m),
        ("Shell", 400m, 1300m),
        ("Uber", 45m, 320m),
        ("Bolt", 40m, 260m),
        ("Mr D Food", 95m, 420m),
        ("Uber Eats", 85m, 390m),
        ("Takealot.com", 199m, 6500m),
        ("Clicks", 60m, 780m),
        ("Dis-Chem", 60m, 900m),
        ("Vodacom", 29m, 499m),
        ("MTN", 29m, 399m),
        ("Netflix", 199m, 199m),
        ("Spotify", 69.99m, 69.99m),
        ("Showmax", 99m, 99m),
        ("Mr Price", 120m, 1400m),
        ("Pep", 60m, 650m),
    ];

    // Monthly debit orders on cheque accounts: (merchant, amount, day of month, description).
    private static readonly (string Merchant, decimal Amount, int Day, string Description)[] DebitOrders =
    [
        ("Discovery Life", 1145.60m, 1, "Debit order - Discovery Life policy"),
        ("DStv", 929.00m, 3, "Debit order - DStv Premium"),
        ("Outsurance", 1386.25m, 5, "Debit order - Outsurance car and home"),
        ("Planet Fitness", 749.00m, 7, "Debit order - Planet Fitness membership"),
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

        var thandiChequeTx = GenerateTransactions(thandiCheque, now, random, count: 45, withSalary: true, withDebitOrders: true);
        var thandiCardTx = GenerateTransactions(thandiCard, now, random, count: 30, withSalary: false, withDebitOrders: false);
        var jamesTx = GenerateTransactions(jamesCheque, now, random, count: 35, withSalary: true, withDebitOrders: true);

        // Live-demo scenario: the same Takealot order charged twice, three minutes apart.
        var duplicateAt = new DateTimeOffset(now.AddDays(-4).Date, TimeSpan.Zero).AddHours(12).AddMinutes(2);
        thandiChequeTx.Add(NewTransaction(thandiCheque, random, 2499.00m, TransactionDirection.Debit, TransactionStatus.Posted, "Takealot.com", duplicateAt, "Order #TK-104732"));
        thandiChequeTx.Add(NewTransaction(thandiCheque, random, 2499.00m, TransactionDirection.Debit, TransactionStatus.Posted, "Takealot.com", duplicateAt.AddMinutes(3), "Order #TK-104732"));

        // An online order that never arrived, currently under investigation.
        var undelivered = NewTransaction(thandiCard, random, 1899.00m, TransactionDirection.Debit, TransactionStatus.Posted, "Superbalist.com", now.AddDays(-12), "Order #SB-559102");
        thandiCardTx.Add(undelivered);

        db.Transactions.AddRange(thandiChequeTx.Concat(thandiCardTx).Concat(jamesTx));

        var disputeWindow = TimeSpan.FromDays(3650); // seeded disputes are back-dated; the window is enforced at runtime
        Dispute RaiseAt(Transaction tx, User customer, DisputeReason reason, string description, TimeSpan window) =>
            Dispute.Raise(tx, customer.Id, reason, description, null, Min(tx.PostedAt.AddDays(2), now), window);
        var olderCardSpend = thandiChequeTx.Concat(thandiCardTx)
            .Where(t => t.Direction == TransactionDirection.Debit && t.Status == TransactionStatus.Posted && t.Description is null)
            .OrderBy(t => t.PostedAt)
            .ToList();

        var upheld = RaiseAt(olderCardSpend[5], thandi, DisputeReason.Duplicate,
            "I was charged twice for the same purchase on the same day. Only one of these charges is correct.", disputeWindow);
        upheld.StartReview(agent.Id, "Thank you. We have asked the merchant to confirm both charges.", upheld.CreatedAt.AddDays(1));
        upheld.Resolve(agent.Id, upheld: true,
            "The merchant confirmed a duplicate charge. The amount has been refunded to your account.", upheld.CreatedAt.AddDays(4));

        var rejected = RaiseAt(olderCardSpend[12], thandi, DisputeReason.Unauthorised,
            "I don't recognise this transaction and did not authorise it.", disputeWindow);
        rejected.StartReview(agent.Id, null, rejected.CreatedAt.AddDays(1));
        rejected.Resolve(agent.Id, upheld: false,
            "Our investigation shows this purchase was made with your physical card and PIN at the merchant's terminal, so we are unable to reverse it. Please contact us if you believe your card has been compromised.",
            rejected.CreatedAt.AddDays(6));

        var withdrawn = RaiseAt(olderCardSpend[20], thandi, DisputeReason.IncorrectAmount,
            "The amount charged is higher than the amount on my till slip.", disputeWindow);
        withdrawn.Withdraw(thandi.Id, "The store refunded the difference at the counter.", withdrawn.CreatedAt.AddDays(2));

        var inReview = RaiseAt(undelivered, thandi, DisputeReason.GoodsOrServicesNotReceived,
            "My order was never delivered and the merchant has not responded to my emails.", disputeWindow);
        inReview.StartReview(agent.Id,
            "We have requested proof of delivery from the merchant and will update you within 5 business days.",
            inReview.CreatedAt.AddHours(5));

        var cancelledGym = jamesTx
            .Where(t => t.Merchant == "Planet Fitness" && t.Status == TransactionStatus.Posted)
            .OrderByDescending(t => t.PostedAt)
            .First();
        var submitted = RaiseAt(cancelledGym, james, DisputeReason.CancelledSubscription,
            "I cancelled my Planet Fitness membership last month, but the debit order was still collected.", disputeWindow);

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

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static List<Transaction> GenerateTransactions(
        Account account, DateTimeOffset now, Random random, int count, bool withSalary, bool withDebitOrders)
    {
        var transactions = new List<Transaction>();
        var start = now.AddDays(-180);

        for (var i = 0; i < count; i++)
        {
            var (merchant, min, max) = CardMerchants[random.Next(CardMerchants.Length)];
            var amount = Math.Round(min + ((max - min) * (decimal)random.NextDouble()), 2);
            var postedAt = start.AddMinutes(random.Next(0, 180 * 24 * 60));
            var status = now - postedAt < TimeSpan.FromDays(2) ? TransactionStatus.Pending : TransactionStatus.Posted;
            transactions.Add(NewTransaction(account, random, amount, TransactionDirection.Debit, status, merchant, postedAt, null));
        }

        // Guaranteed recent posted debits so the demo always has disputable transactions, plus one still pending.
        // Varied per account so the accounts don't show identical rows side by side.
        string[] recentMerchants = account.Type == AccountType.CreditCard
            ? ["Uber Eats", "Clicks", "Woolworths Food"]
            : ["Engen", "Mr D Food", "Pick n Pay"];
        decimal Jitter(decimal amount) => Math.Round(amount * (0.8m + (0.4m * (decimal)random.NextDouble())), 2);
        TimeSpan Minutes() => TimeSpan.FromMinutes(random.Next(0, 600));
        transactions.Add(NewTransaction(account, random, Jitter(850.00m), TransactionDirection.Debit, TransactionStatus.Posted, recentMerchants[0], now.AddDays(-6) - Minutes(), null));
        transactions.Add(NewTransaction(account, random, Jitter(289.40m), TransactionDirection.Debit, TransactionStatus.Posted, recentMerchants[1], now.AddDays(-3) - Minutes(), null));
        transactions.Add(NewTransaction(account, random, Jitter(412.37m), TransactionDirection.Debit, TransactionStatus.Pending, recentMerchants[2], now.AddHours(-6) - Minutes(), null));

        for (var month = 0; month < 6; month++)
        {
            var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-month);

            var payday = monthStart.AddDays(24).AddHours(8);
            if (withSalary && payday < now)
            {
                transactions.Add(NewTransaction(account, random, 32500m, TransactionDirection.Credit, TransactionStatus.Posted, "ACME Corp Salary", payday, "Monthly salary"));
            }

            if (!withDebitOrders)
            {
                continue;
            }

            foreach (var (merchant, amount, day, description) in DebitOrders)
            {
                var collectedAt = monthStart.AddDays(day - 1).AddHours(4);
                if (collectedAt < now)
                {
                    transactions.Add(NewTransaction(account, random, amount, TransactionDirection.Debit, TransactionStatus.Posted, merchant, collectedAt, description));
                }
            }
        }

        return transactions;
    }

    private static Transaction NewTransaction(
        Account account,
        Random random,
        decimal amount,
        TransactionDirection direction,
        TransactionStatus status,
        string merchant,
        DateTimeOffset postedAt,
        string? description)
    {
        var reference = string.Create(
            CultureInfo.InvariantCulture, $"TXN{postedAt:yyyyMMdd}{random.Next(100000, 999999)}");
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

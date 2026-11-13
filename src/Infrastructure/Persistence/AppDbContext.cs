using Application.Abstractions;
using Application.Common;
using Domain.Accounts;
using Domain.Common;
using Domain.Disputes;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Dispute> Disputes => Set<Dispute>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException(
                "concurrency_conflict",
                "The resource was modified by someone else. Refresh and try again.",
                ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        } pg)
        {
            throw pg.ConstraintName switch
            {
                DisputeConfiguration.OpenDisputePerTransactionIndex => new ConflictException(
                    DisputeErrorCodes.AlreadyDisputed, "This transaction already has an open dispute.", ex),
                DisputeConfiguration.OneAppealPerDisputeIndex => new ConflictException(
                    DisputeErrorCodes.AppealExhausted, "This dispute has already been appealed.", ex),
                DisputeConfiguration.IdempotencyKeyIndex => new ConflictException(
                    "dispute.idempotency_key_reused", "This Idempotency-Key has already been used.", ex),
                _ => new ConflictException("unique_violation", "The resource already exists.", ex),
            };
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Persist enums as readable strings rather than opaque integers.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}

using Domain.Accounts;
using Domain.Disputes;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions;

/// <summary>
/// Unit of work over the portal's data. Implementations must translate store-specific
/// uniqueness and concurrency failures into <see cref="Common.ConflictException"/>.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<Account> Accounts { get; }

    DbSet<Transaction> Transactions { get; }

    DbSet<Dispute> Disputes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

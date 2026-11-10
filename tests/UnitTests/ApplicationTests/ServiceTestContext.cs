using Application;
using Application.Abstractions;
using Application.Disputes;
using Application.Transactions;
using Domain.Users;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace UnitTests.ApplicationTests;

/// <summary>
/// Wires application services against an in-memory EF Core store. Database-level constraints (filtered unique
/// indexes, xmin concurrency) are covered by the PostgreSQL-backed integration tests instead.
/// </summary>
internal sealed class ServiceTestContext : IDisposable
{
    private readonly ServiceProvider _provider;

    public ServiceTestContext()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<ICurrentUser>(CurrentUser);
        services.AddSingleton(Options.Create(new DisputeOptions { DisputeWindowDays = 120 }));
        services.AddApplication();
        _provider = services.BuildServiceProvider(validateScopes: true);
    }

    public FakeTimeProvider Clock { get; } = new(TestData.Now);

    public MutableCurrentUser CurrentUser { get; } = new();

    public T Get<T>()
        where T : notnull
    {
        var scope = _provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>Runs <paramref name="seed"/> against a fresh context so services see persisted, untracked data.</summary>
    public async Task SeedAsync(Action<AppDbContext> seed)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    public TransactionService Transactions() => Get<TransactionService>();

    public DisputeService Disputes() => Get<DisputeService>();

    public AgentDisputeService AgentDisputes() => Get<AgentDisputeService>();

    public void Dispose() => _provider.Dispose();

    internal sealed class MutableCurrentUser : ICurrentUser
    {
        public Guid UserId { get; set; }

        public UserRole Role { get; set; } = UserRole.Customer;

        public void SignInAs(User user)
        {
            UserId = user.Id;
            Role = user.Role;
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending EF Core migrations at startup. Disable when migrations run as a deployment step.</summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>
    /// How long startup waits for PostgreSQL to accept connections before giving up with a clear error.
    /// Only used when migrating or seeding at startup.
    /// </summary>
    [Range(0, 600)]
    public int StartupConnectTimeoutSeconds { get; set; } = 30;

    public SeedOptions Seed { get; set; } = new();
}

public sealed class SeedOptions
{
    /// <summary>Populate an empty database with demo customers, transactions and disputes.</summary>
    public bool Enabled { get; set; }

    /// <summary>Password given to every seeded demo user. Required when seeding is enabled.</summary>
    public string? DemoPassword { get; set; }
}

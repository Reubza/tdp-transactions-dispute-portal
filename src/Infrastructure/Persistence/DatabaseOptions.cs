namespace Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending EF Core migrations at startup. Disable when migrations run as a deployment step.</summary>
    public bool MigrateOnStartup { get; set; }

    public SeedOptions Seed { get; set; } = new();
}

public sealed class SeedOptions
{
    /// <summary>Populate an empty database with demo customers, transactions and disputes.</summary>
    public bool Enabled { get; set; }

    /// <summary>Password given to every seeded demo user. Required when seeding is enabled.</summary>
    public string? DemoPassword { get; set; }
}

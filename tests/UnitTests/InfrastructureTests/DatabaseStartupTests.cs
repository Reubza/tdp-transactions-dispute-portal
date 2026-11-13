using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UnitTests.InfrastructureTests;

public class DatabaseStartupTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_connection_string_fails_startup_validation_with_actionable_message(string? connectionString)
    {
        using var provider = BuildServices(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DisputePortal"] = connectionString,
        });

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<DatabaseOptions>>().Value);

        Assert.Contains("ConnectionStrings__DisputePortal", ex.Message, StringComparison.Ordinal);
        Assert.Contains("docker compose up", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unreachable_database_fails_with_actionable_message_that_hides_the_password()
    {
        // Port 1 on loopback refuses connections immediately; a zero-second budget means a single attempt.
        using var provider = BuildServices(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DisputePortal"] =
                "Host=127.0.0.1;Port=1;Database=dispute_portal;Username=dispute_portal;Password=super-secret-pw;Timeout=2",
            ["Database:MigrateOnStartup"] = "true",
            ["Database:StartupConnectTimeoutSeconds"] = "0",
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.InitialiseDatabaseAsync());

        Assert.Contains("Cannot connect to PostgreSQL at 127.0.0.1:1", ex.Message, StringComparison.Ordinal);
        Assert.Contains("docker compose up", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-pw", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nothing_touches_the_database_when_migrations_and_seeding_are_disabled()
    {
        using var provider = BuildServices(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DisputePortal"] = "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x",
        });

        await provider.InitialiseDatabaseAsync();
    }

    private static ServiceProvider BuildServices(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(TimeProvider.System)
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
    }
}

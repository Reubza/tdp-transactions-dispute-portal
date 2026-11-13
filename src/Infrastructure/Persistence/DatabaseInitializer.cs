using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Infrastructure.Persistence;

public static class DatabaseInitializer
{
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>Applies migrations and seeds demo data according to <see cref="DatabaseOptions"/>.</summary>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (!options.MigrateOnStartup && !options.Seed.Enabled)
        {
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        await WaitForDatabaseServerAsync(
            db.Database.GetConnectionString()!, TimeSpan.FromSeconds(options.StartupConnectTimeoutSeconds), logger, cancellationToken);

        if (options.MigrateOnStartup)
        {
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync(cancellationToken);
        }

        if (options.Seed.Enabled)
        {
            var seeder = ActivatorUtilities.CreateInstance<DemoDataSeeder>(scope.ServiceProvider);
            await seeder.SeedAsync(options.Seed, cancellationToken);
        }
    }

    /// <summary>
    /// Waits until the PostgreSQL server accepts connections, retrying with backoff, so a database that is still
    /// starting doesn't crash the app, and a missing one fails with a message that says what to do.
    /// A missing database on a reachable server counts as success: migrations create it.
    /// </summary>
    private static async Task WaitForDatabaseServerAsync(
        string connectionString, TimeSpan timeout, ILogger logger, CancellationToken cancellationToken)
    {
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        var stopwatch = Stopwatch.StartNew();
        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                return;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
            {
                return;
            }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.InvalidPassword
                                                   or PostgresErrorCodes.InvalidAuthorizationSpecification)
            {
                throw new InvalidOperationException(
                    $"PostgreSQL at {target.Host}:{target.Port} rejected the credentials for user '{target.Username}'. " +
                    "Check the username and password in ConnectionStrings__DisputePortal.", ex);
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException && !cancellationToken.IsCancellationRequested)
            {
                if (stopwatch.Elapsed + delay > timeout)
                {
                    throw new InvalidOperationException(
                        $"Cannot connect to PostgreSQL at {target.Host}:{target.Port} (database '{target.Database}') " +
                        $"after {attempt} attempt(s) over {stopwatch.Elapsed.TotalSeconds:0} s. " +
                        "Run the whole system with 'docker compose up --build', start just the database with " +
                        "'docker compose up -d db', or point ConnectionStrings__DisputePortal at your own server.", ex);
                }

                logger.LogWarning(
                    "PostgreSQL at {Host}:{Port} is not reachable yet ({Reason}); retrying in {Delay} s",
                    target.Host, target.Port, ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
            }
        }
    }
}

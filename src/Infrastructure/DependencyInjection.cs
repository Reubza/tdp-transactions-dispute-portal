using Application.Abstractions;
using Infrastructure.Auth;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DisputePortal";

    private const string MissingConnectionStringMessage =
        $"Connection string '{ConnectionStringName}' is not configured. Set the ConnectionStrings__{ConnectionStringName} " +
        "environment variable to a PostgreSQL connection string, or run the whole system with 'docker compose up --build'.";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Resolved lazily so configuration added late (e.g. by test hosts) is honoured.
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(MissingConnectionStringMessage);
            }

            ConfigureDbContext(options, connectionString);
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate<IConfiguration>(
                (_, config) => !string.IsNullOrWhiteSpace(config.GetConnectionString(ConnectionStringName)),
                MissingConnectionStringMessage)
            .ValidateOnStart();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        return services;
    }

    internal static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
            .UseSnakeCaseNamingConvention();
}

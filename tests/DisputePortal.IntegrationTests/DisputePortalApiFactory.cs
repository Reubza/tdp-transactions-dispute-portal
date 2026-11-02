using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DisputePortal.Application.Auth;
using DisputePortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DisputePortal.IntegrationTests;

/// <summary>
/// Hosts the real API against a real PostgreSQL database with migrations and demo seed data applied.
/// Uses a throwaway Testcontainers PostgreSQL by default; set <c>DISPUTEPORTAL_TEST_POSTGRES</c> to a connection
/// string to use an existing server instead (a uniquely named database is created and dropped).
/// </summary>
public sealed class DisputePortalApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Integration@Test1";
    public const string ExternalServerVariable = "DISPUTEPORTAL_TEST_POSTGRES";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private PostgreSqlContainer? _container;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(ExternalServerVariable);
        if (!string.IsNullOrWhiteSpace(external))
        {
            _connectionString = new NpgsqlConnectionStringBuilder(external)
            {
                Database = $"dispute_portal_test_{Guid.NewGuid():N}",
            }.ConnectionString;
        }
        else
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        // Force host creation so migrations and seeding run before the first test.
        _ = Server;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_container is null)
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }

        await DisposeAsync();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task<HttpClient> CreateClientForAsync(string email)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, DemoPassword), Json);
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DisputePortal", _connectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:Seed:Enabled", "true");
        builder.UseSetting("Database:Seed:DemoPassword", DemoPassword);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<DisputePortalApiFactory>
{
    public const string Name = "api";
}

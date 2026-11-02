using System.Globalization;
using DisputePortal.Api.Infrastructure;
using DisputePortal.Application;
using DisputePortal.Infrastructure;
using DisputePortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: CultureInfo.InvariantCulture).CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services
        .AddOptions<DisputeOptions>()
        .Bind(builder.Configuration.GetSection(DisputeOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddApi();

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseMiddleware<RequestContextMiddleware>();
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

    if (app.Configuration.GetValue("Api:EnableDocs", app.Environment.IsDevelopment()))
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(o => o.WithTitle("Transactions Dispute Portal API")).AllowAnonymous();
    }

    // Unknown API routes are a 404 problem, everything else falls through to the Angular SPA.
    app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound)).AllowAnonymous();
    app.MapFallbackToFile("index.html").AllowAnonymous();

    await app.Services.InitialiseDatabaseAsync();
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Entry point; exposed for <c>WebApplicationFactory</c> in integration tests.</summary>
public partial class Program;

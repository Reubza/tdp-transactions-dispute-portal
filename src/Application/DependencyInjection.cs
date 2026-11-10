using Application.Auth;
using Application.Disputes;
using Application.Transactions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(includeInternalTypes: true);
        services.AddScoped<AuthService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<DisputeService>();
        services.AddScoped<AgentDisputeService>();
        return services;
    }
}

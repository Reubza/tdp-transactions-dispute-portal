using System.IdentityModel.Tokens.Jwt;
using DisputePortal.Application.Abstractions;
using DisputePortal.Domain.Users;
using DisputePortal.Infrastructure.Auth;

namespace DisputePortal.Api.Infrastructure;

/// <summary>Resolves the caller from the validated JWT on the current HTTP request.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId =>
        Guid.TryParse(Principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new InvalidOperationException("The current user has no valid subject claim.");

    public UserRole Role =>
        Enum.TryParse<UserRole>(Principal.FindFirst(JwtTokenIssuer.RoleClaimType)?.Value, out var role)
            ? role
            : throw new InvalidOperationException("The current user has no valid role claim.");

    private System.Security.Claims.ClaimsPrincipal Principal =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? user
            : throw new InvalidOperationException("No authenticated user on the current request.");
}

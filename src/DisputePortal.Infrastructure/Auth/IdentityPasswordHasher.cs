using DisputePortal.Domain.Users;
using Microsoft.AspNetCore.Identity;
using IPasswordHasher = DisputePortal.Application.Abstractions.IPasswordHasher;

namespace DisputePortal.Infrastructure.Auth;

/// <summary>PBKDF2 (HMAC-SHA512, 100k iterations) via ASP.NET Core Identity's hasher.</summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        _inner.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}

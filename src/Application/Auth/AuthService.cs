using Application.Abstractions;
using Application.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Application.Auth;

public sealed class AuthService(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    IValidator<LoginRequest> validator)
{
    // Verified against when the user does not exist so that response timing does not reveal valid e-mails.
    private readonly Lazy<string> _dummyHash = new(() => passwordHasher.Hash(Guid.NewGuid().ToString()));

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var email = request.Email.Trim().ToUpperInvariant();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        var passwordValid = passwordHasher.Verify(user?.PasswordHash ?? _dummyHash.Value, request.Password);
        if (user is null || !passwordValid)
        {
            throw new AuthenticationFailedException();
        }

        var token = tokenIssuer.Issue(user);
        return new LoginResponse(
            token.AccessToken,
            token.ExpiresAt,
            new UserProfileDto(user.Id, user.FullName, user.Email.ToLowerInvariant(), user.Role));
    }
}

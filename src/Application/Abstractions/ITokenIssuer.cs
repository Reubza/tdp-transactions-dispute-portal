using Domain.Users;

namespace Application.Abstractions;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);

public interface ITokenIssuer
{
    IssuedToken Issue(User user);
}

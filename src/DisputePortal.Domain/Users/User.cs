using DisputePortal.Domain.Common;

namespace DisputePortal.Domain.Users;

/// <summary>A person who can sign in to the portal: a bank customer or a back-office dispute agent.</summary>
public sealed class User
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 320;

    private User()
    {
    }

    public User(Guid id, string fullName, string email, UserRole role)
    {
        Id = id;
        FullName = Guard.NotBlank(fullName, nameof(fullName), NameMaxLength);
        Email = Guard.NotBlank(email, nameof(email), EmailMaxLength).ToUpperInvariant();
        Role = role;
    }

    public Guid Id { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Normalised (upper-case) e-mail address used as the login name.</summary>
    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public void SetPasswordHash(string passwordHash) =>
        PasswordHash = Guard.NotBlank(passwordHash, nameof(passwordHash), 500);
}

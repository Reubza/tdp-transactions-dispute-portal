using DisputePortal.Domain.Users;

namespace DisputePortal.Application.Abstractions;

/// <summary>The authenticated caller of the current request.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }

    UserRole Role { get; }
}

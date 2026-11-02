using DisputePortal.Domain.Users;

namespace DisputePortal.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record UserProfileDto(Guid Id, string FullName, string Email, UserRole Role);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserProfileDto User);

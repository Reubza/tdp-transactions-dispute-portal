namespace DisputePortal.Application.Common;

/// <summary>The requested resource does not exist or is not visible to the caller.</summary>
public sealed class NotFoundException(string resource, Guid id)
    : Exception($"{resource} '{id}' was not found.")
{
    public string Resource { get; } = resource;
}

/// <summary>The request conflicts with the current state of a resource.</summary>
public sealed class ConflictException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

/// <summary>The supplied credentials were not valid.</summary>
public sealed class AuthenticationFailedException()
    : Exception("Invalid e-mail address or password.");

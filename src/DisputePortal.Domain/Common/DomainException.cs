namespace DisputePortal.Domain.Common;

/// <summary>
/// Raised when an operation would violate a business rule. <see cref="Code"/> is a stable,
/// machine-readable identifier that clients can rely on (it is surfaced in API problem details).
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

namespace Domain.Common;

/// <summary>Stable error codes returned to API clients when a dispute business rule is violated.</summary>
public static class DisputeErrorCodes
{
    public const string NotADebit = "dispute.not_a_debit";
    public const string NotPosted = "dispute.not_posted";
    public const string WindowExpired = "dispute.window_expired";
    public const string AlreadyDisputed = "dispute.already_open";
    public const string AlreadyResolved = "dispute.already_resolved";
    public const string AppealExhausted = "dispute.appeal_exhausted";
    public const string AppealWindowExpired = "dispute.appeal_window_expired";
    public const string InvalidTransition = "dispute.invalid_transition";
}

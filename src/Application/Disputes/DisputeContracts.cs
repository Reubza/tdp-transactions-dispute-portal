using Application.Common;
using Domain.Disputes;
using Domain.Users;

namespace Application.Disputes;

public sealed record CreateDisputeRequest(Guid TransactionId, DisputeReason Reason, string Description);

public sealed record WithdrawDisputeRequest(string? Note);

public enum AgentDisputeAction
{
    StartReview = 0,
    Uphold = 1,
    Reject = 2,
}

public sealed record UpdateDisputeStatusRequest(AgentDisputeAction Action, string? Note);

/// <summary>Actions the current caller may perform on a dispute, as returned to clients.</summary>
public static class DisputeActions
{
    public const string Withdraw = "Withdraw";
    public const string StartReview = nameof(AgentDisputeAction.StartReview);
    public const string Uphold = nameof(AgentDisputeAction.Uphold);
    public const string Reject = nameof(AgentDisputeAction.Reject);
}

public sealed record DisputeQuery : PagedQuery
{
    public DisputeStatus? Status { get; init; }

    /// <summary><c>true</c>: Submitted/UnderReview only; <c>false</c>: closed (resolved or withdrawn) only.</summary>
    public bool? Open { get; init; }

    /// <summary>Case-insensitive match on merchant, transaction reference or customer name.</summary>
    public string? Search { get; init; }
}

public sealed record DisputeSummaryDto(
    Guid Id,
    Guid TransactionId,
    string CustomerName,
    string Merchant,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateTimeOffset TransactionPostedAt,
    DisputeReason Reason,
    DisputeStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record DisputeHistoryEntryDto(
    DisputeStatus? FromStatus,
    DisputeStatus ToStatus,
    UserRole ActorRole,
    string? Note,
    DateTimeOffset ChangedAt);

public sealed record DisputeDetailDto(
    DisputeSummaryDto Summary,
    string Description,
    string? ResolutionNote,
    IReadOnlyList<string> AvailableActions,
    IReadOnlyList<DisputeHistoryEntryDto> History);

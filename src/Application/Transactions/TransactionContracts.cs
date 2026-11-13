using Application.Common;
using Domain.Disputes;
using Domain.Transactions;

namespace Application.Transactions;

public sealed record TransactionQuery : PagedQuery
{
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    /// <summary>Case-insensitive match on merchant, reference or description.</summary>
    public string? Search { get; init; }

    public Guid? AccountId { get; init; }

    /// <summary><c>true</c>: only transactions that have ever been disputed; <c>false</c>: never disputed.</summary>
    public bool? Disputed { get; init; }
}

public sealed record TransactionDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string MaskedAccountNumber,
    decimal Amount,
    string Currency,
    TransactionDirection Direction,
    TransactionStatus Status,
    string Merchant,
    string Reference,
    string? Description,
    DateTimeOffset PostedAt,
    bool CanDispute,
    string? NotDisputableReason,
    bool DisputeIsAppeal,
    Guid? LatestDisputeId,
    DisputeStatus? LatestDisputeStatus);

public sealed record AccountDto(Guid Id, string Name, string MaskedAccountNumber, string Type, string Currency);

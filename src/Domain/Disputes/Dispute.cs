using Domain.Common;
using Domain.Transactions;
using Domain.Users;

namespace Domain.Disputes;

/// <summary>
/// A customer's dispute of a transaction. The aggregate enforces the status lifecycle and records every
/// transition in <see cref="History"/>, which is the source of the customer's historic view.
/// </summary>
/// <remarks>
/// Lifecycle: Submitted → UnderReview → Upheld | Rejected; Submitted | UnderReview → Withdrawn (customer only).
/// </remarks>
public sealed class Dispute
{
    public const int DescriptionMaxLength = 2000;
    public const int NoteMaxLength = 1000;

    private static readonly Dictionary<DisputeStatus, DisputeStatus[]> AllowedTransitions = new()
    {
        [DisputeStatus.Submitted] = [DisputeStatus.UnderReview, DisputeStatus.Withdrawn],
        [DisputeStatus.UnderReview] = [DisputeStatus.Upheld, DisputeStatus.Rejected, DisputeStatus.Withdrawn],
        [DisputeStatus.Upheld] = [],
        [DisputeStatus.Rejected] = [],
        [DisputeStatus.Withdrawn] = [],
    };

    private readonly List<DisputeStatusChange> _history = [];

    private Dispute()
    {
    }

    public Guid Id { get; private set; }

    public Guid TransactionId { get; private set; }

    public Transaction Transaction { get; private set; } = null!;

    /// <summary>The rejected dispute this one appeals, or <c>null</c> for a first dispute.</summary>
    public Guid? AppealOfDisputeId { get; private set; }

    public bool IsAppeal => AppealOfDisputeId is not null;

    public Guid CustomerId { get; private set; }

    public DisputeReason Reason { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public DisputeStatus Status { get; private set; }

    /// <summary>Client-supplied key used to make dispute creation safe to retry.</summary>
    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? ResolutionNote { get; private set; }

    /// <summary>Optimistic concurrency token (mapped to PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; private set; }

    public IReadOnlyList<DisputeStatusChange> History => _history;

    public bool IsOpen => IsOpenStatus(Status);

    public static bool IsOpenStatus(DisputeStatus status) =>
        status is DisputeStatus.Submitted or DisputeStatus.UnderReview;

    /// <summary>
    /// Raises a dispute, or an appeal of a rejected one, if <see cref="DisputeEligibility"/> allows it given
    /// <paramref name="priorDisputes"/> (every earlier dispute of the same transaction).
    /// </summary>
    public static Dispute Raise(
        Transaction transaction,
        IEnumerable<PriorDispute> priorDisputes,
        Guid customerId,
        DisputeReason reason,
        string description,
        string? idempotencyKey,
        DateTimeOffset now,
        DisputeRules rules)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        var eligibility = DisputeEligibility.Check(transaction, priorDisputes, now, rules);
        if (eligibility.Error is { } error)
        {
            throw error;
        }

        var dispute = new Dispute
        {
            Id = Guid.CreateVersion7(now),
            TransactionId = transaction.Id,
            AppealOfDisputeId = eligibility.AppealOfDisputeId,
            CustomerId = customerId,
            Reason = reason,
            Description = Guard.NotBlank(description, nameof(description), DescriptionMaxLength),
            Status = DisputeStatus.Submitted,
            IdempotencyKey = Guard.Optional(idempotencyKey, nameof(idempotencyKey), 100),
            CreatedAt = now,
            UpdatedAt = now,
        };
        dispute._history.Add(new DisputeStatusChange(
            dispute.Id, null, DisputeStatus.Submitted, customerId, UserRole.Customer, null, now));
        return dispute;
    }

    public void Withdraw(Guid customerId, string? note, DateTimeOffset now)
    {
        if (customerId != CustomerId)
        {
            throw new InvalidOperationException("Only the customer who raised a dispute can withdraw it.");
        }

        TransitionTo(DisputeStatus.Withdrawn, customerId, UserRole.Customer, note, now);
    }

    public void StartReview(Guid agentId, string? note, DateTimeOffset now) =>
        TransitionTo(DisputeStatus.UnderReview, agentId, UserRole.Agent, note, now);

    public void Resolve(Guid agentId, bool upheld, string note, DateTimeOffset now)
    {
        var resolutionNote = Guard.NotBlank(note, nameof(note), NoteMaxLength);
        TransitionTo(upheld ? DisputeStatus.Upheld : DisputeStatus.Rejected, agentId, UserRole.Agent, resolutionNote, now);
        ResolvedAt = now;
        ResolutionNote = resolutionNote;
    }

    public bool CanTransitionTo(DisputeStatus target) => AllowedTransitions[Status].Contains(target);

    private void TransitionTo(DisputeStatus target, Guid actorId, UserRole actorRole, string? note, DateTimeOffset now)
    {
        if (!CanTransitionTo(target))
        {
            throw new DomainException(
                DisputeErrorCodes.InvalidTransition,
                $"A dispute that is {Status} cannot be moved to {target}.");
        }

        var change = new DisputeStatusChange(
            Id, Status, target, actorId, actorRole, Guard.Optional(note, nameof(note), NoteMaxLength), now);
        _history.Add(change);
        Status = target;
        UpdatedAt = now;
    }
}

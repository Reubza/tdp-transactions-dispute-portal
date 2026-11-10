using Domain.Users;

namespace Domain.Disputes;

/// <summary>Append-only audit record of a dispute changing status.</summary>
public sealed class DisputeStatusChange
{
    private DisputeStatusChange()
    {
    }

    internal DisputeStatusChange(
        Guid disputeId,
        DisputeStatus? fromStatus,
        DisputeStatus toStatus,
        Guid changedByUserId,
        UserRole changedByRole,
        string? note,
        DateTimeOffset changedAt)
    {
        Id = Guid.CreateVersion7(changedAt);
        DisputeId = disputeId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedByUserId = changedByUserId;
        ChangedByRole = changedByRole;
        Note = note;
        ChangedAt = changedAt;
    }

    public Guid Id { get; private set; }

    public Guid DisputeId { get; private set; }

    /// <summary><c>null</c> for the initial "submitted" entry.</summary>
    public DisputeStatus? FromStatus { get; private set; }

    public DisputeStatus ToStatus { get; private set; }

    public Guid ChangedByUserId { get; private set; }

    public UserRole ChangedByRole { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }
}

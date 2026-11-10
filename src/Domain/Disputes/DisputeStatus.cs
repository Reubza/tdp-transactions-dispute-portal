namespace Domain.Disputes;

public enum DisputeStatus
{
    Submitted = 0,
    UnderReview = 1,

    /// <summary>Resolved in the customer's favour.</summary>
    Upheld = 2,

    /// <summary>Resolved against the customer.</summary>
    Rejected = 3,

    /// <summary>Withdrawn by the customer before resolution.</summary>
    Withdrawn = 4,
}

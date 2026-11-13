namespace Domain.Disputes;

/// <param name="DisputeWindow">How long after posting a transaction may be disputed for the first time.</param>
/// <param name="AppealWindow">How long after a dispute is rejected the customer may appeal it (once).</param>
public sealed record DisputeRules(TimeSpan DisputeWindow, TimeSpan AppealWindow);

/// <summary>What eligibility needs to know about an earlier dispute of the same transaction.</summary>
public sealed record PriorDispute(Guid Id, DisputeStatus Status, DateTimeOffset? ResolvedAt, Guid? AppealOfDisputeId);

using Domain.Common;
using Domain.Transactions;

namespace Domain.Disputes;

/// <summary>
/// Decides whether a transaction may be disputed, given every earlier dispute of it.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Only posted debits can be disputed.</item>
/// <item>Withdrawn disputes are ignored: nothing was decided, so the customer may raise the dispute again.</item>
/// <item>At most one open dispute at a time.</item>
/// <item>Never after an <see cref="DisputeStatus.Upheld"/> dispute: the customer has been refunded, and a second
/// dispute would risk a double refund.</item>
/// <item>After a <see cref="DisputeStatus.Rejected"/> dispute the customer may appeal <b>once</b>, within
/// <see cref="DisputeRules.AppealWindow"/> of the decision (the original posting window no longer applies).
/// A rejected appeal is final.</item>
/// <item>Otherwise, a first dispute must be raised within <see cref="DisputeRules.DisputeWindow"/> of posting.</item>
/// </list>
/// </remarks>
public static class DisputeEligibility
{
    public static EligibilityResult Check(
        Transaction transaction, IEnumerable<PriorDispute> priorDisputes, DateTimeOffset now, DisputeRules rules)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(priorDisputes);
        ArgumentNullException.ThrowIfNull(rules);

        if (transaction.CheckDisputableType() is { } typeError)
        {
            return EligibilityResult.No(typeError);
        }

        var decided = priorDisputes.Where(d => d.Status != DisputeStatus.Withdrawn).ToList();

        if (decided.Any(d => Dispute.IsOpenStatus(d.Status)))
        {
            return EligibilityResult.No(new DomainException(
                DisputeErrorCodes.AlreadyDisputed, "This transaction already has an open dispute."));
        }

        if (decided.Any(d => d.Status == DisputeStatus.Upheld))
        {
            return EligibilityResult.No(new DomainException(
                DisputeErrorCodes.AlreadyResolved,
                "This transaction was already refunded after a successful dispute, so it can't be disputed again."));
        }

        var rejected = decided.Where(d => d.Status == DisputeStatus.Rejected).ToList();
        if (rejected.Count == 0)
        {
            return transaction.CheckWithinWindow(now, rules.DisputeWindow) is { } windowError
                ? EligibilityResult.No(windowError)
                : EligibilityResult.FirstDispute;
        }

        if (rejected.Count > 1 || rejected.Any(d => d.AppealOfDisputeId is not null))
        {
            return EligibilityResult.No(new DomainException(
                DisputeErrorCodes.AppealExhausted,
                "This dispute was declined and the appeal was also declined. Please contact us if you need further help."));
        }

        var original = rejected[0];
        var decidedAt = original.ResolvedAt ?? now;
        if (now - decidedAt > rules.AppealWindow)
        {
            return EligibilityResult.No(new DomainException(
                DisputeErrorCodes.AppealWindowExpired,
                $"A declined dispute can only be appealed within {rules.AppealWindow.TotalDays:0} days of the decision."));
        }

        return EligibilityResult.Appeal(original.Id);
    }
}

/// <summary>Outcome of <see cref="DisputeEligibility.Check"/>.</summary>
public sealed record EligibilityResult
{
    public static readonly EligibilityResult FirstDispute = new();

    private EligibilityResult()
    {
    }

    /// <summary>Why the transaction can't be disputed, or <c>null</c> if it can.</summary>
    public DomainException? Error { get; private init; }

    /// <summary>The rejected dispute a new dispute would appeal, if any.</summary>
    public Guid? AppealOfDisputeId { get; private init; }

    public bool IsEligible => Error is null;

    public bool IsAppeal => AppealOfDisputeId is not null;

    public static EligibilityResult No(DomainException error) => new() { Error = error };

    public static EligibilityResult Appeal(Guid rejectedDisputeId) => new() { AppealOfDisputeId = rejectedDisputeId };
}

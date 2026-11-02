using DisputePortal.Application.Abstractions;
using DisputePortal.Application.Common;
using DisputePortal.Domain.Disputes;
using DisputePortal.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace DisputePortal.Application.Disputes;

/// <summary>Query/projection logic shared by the customer and agent dispute services.</summary>
internal static class DisputeReadModel
{
    public static async Task<PagedResult<DisputeSummaryDto>> ListAsync(
        IApplicationDbContext db, IQueryable<Dispute> disputes, DisputeQuery query, CancellationToken cancellationToken)
    {
        if (query.Status is { } status)
        {
            disputes = disputes.Where(d => d.Status == status);
        }

        if (query.Open is { } open)
        {
            disputes = open
                ? disputes.Where(d => d.Status == DisputeStatus.Submitted || d.Status == DisputeStatus.UnderReview)
                : disputes.Where(d => d.Status != DisputeStatus.Submitted && d.Status != DisputeStatus.UnderReview);
        }

        var rows =
            from d in disputes
            join u in db.Users on d.CustomerId equals u.Id
            select new { Dispute = d, Customer = u.FullName };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            rows = rows.Where(r =>
                r.Dispute.Transaction.Merchant.ToLower().Contains(term) ||
                r.Dispute.Transaction.Reference.ToLower().Contains(term) ||
                r.Customer.ToLower().Contains(term));
        }

        var (items, total) = await rows
            .OrderByDescending(r => r.Dispute.CreatedAt)
            .ThenBy(r => r.Dispute.Id)
            .Select(r => new DisputeSummaryDto(
                r.Dispute.Id,
                r.Dispute.TransactionId,
                r.Customer,
                r.Dispute.Transaction.Merchant,
                r.Dispute.Transaction.Reference,
                r.Dispute.Transaction.Amount,
                r.Dispute.Transaction.Currency,
                r.Dispute.Transaction.PostedAt,
                r.Dispute.Reason,
                r.Dispute.Status,
                r.Dispute.CreatedAt,
                r.Dispute.UpdatedAt,
                r.Dispute.ResolvedAt))
            .ToPageAsync(query, cancellationToken);

        return new PagedResult<DisputeSummaryDto>(items, query.Page, query.PageSize, total);
    }

    /// <summary>Loads a dispute with its transaction and history through the supplied visibility filter.</summary>
    public static async Task<Dispute> LoadAsync(IQueryable<Dispute> disputes, Guid id, CancellationToken cancellationToken) =>
        await disputes
            .Include(d => d.Transaction)
            .Include(d => d.History)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
        ?? throw new NotFoundException("Dispute", id);

    public static async Task<DisputeDetailDto> ToDetailAsync(
        IApplicationDbContext db, Dispute dispute, UserRole viewerRole, CancellationToken cancellationToken)
    {
        var customerName = await db.Users
            .Where(u => u.Id == dispute.CustomerId)
            .Select(u => u.FullName)
            .SingleAsync(cancellationToken);

        var t = dispute.Transaction;
        var summary = new DisputeSummaryDto(
            dispute.Id,
            dispute.TransactionId,
            customerName,
            t.Merchant,
            t.Reference,
            t.Amount,
            t.Currency,
            t.PostedAt,
            dispute.Reason,
            dispute.Status,
            dispute.CreatedAt,
            dispute.UpdatedAt,
            dispute.ResolvedAt);

        var history = dispute.History
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .Select(h => new DisputeHistoryEntryDto(h.FromStatus, h.ToStatus, h.ChangedByRole, h.Note, h.ChangedAt))
            .ToList();

        return new DisputeDetailDto(
            summary, dispute.Description, dispute.ResolutionNote, AvailableActions(dispute, viewerRole), history);
    }

    private static List<string> AvailableActions(Dispute dispute, UserRole role)
    {
        var actions = new List<string>();
        if (role == UserRole.Customer)
        {
            if (dispute.CanTransitionTo(DisputeStatus.Withdrawn))
            {
                actions.Add(DisputeActions.Withdraw);
            }

            return actions;
        }

        if (dispute.CanTransitionTo(DisputeStatus.UnderReview))
        {
            actions.Add(DisputeActions.StartReview);
        }

        if (dispute.CanTransitionTo(DisputeStatus.Upheld))
        {
            actions.Add(DisputeActions.Uphold);
            actions.Add(DisputeActions.Reject);
        }

        return actions;
    }
}

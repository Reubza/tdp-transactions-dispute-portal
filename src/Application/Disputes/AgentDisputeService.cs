using Application.Abstractions;
using Application.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Application.Disputes;

/// <summary>Back-office operations used by dispute agents to work the dispute queue.</summary>
public sealed class AgentDisputeService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TimeProvider clock,
    IValidator<UpdateDisputeStatusRequest> updateValidator,
    IValidator<DisputeQuery> queryValidator)
{
    public async Task<PagedResult<DisputeSummaryDto>> ListAsync(DisputeQuery query, CancellationToken cancellationToken)
    {
        await queryValidator.ValidateAndThrowAsync(query, cancellationToken);
        return await DisputeReadModel.ListAsync(db, db.Disputes.AsNoTracking(), query, cancellationToken);
    }

    public async Task<DisputeDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var dispute = await DisputeReadModel.LoadAsync(db.Disputes.AsNoTracking(), id, cancellationToken);
        return await DisputeReadModel.ToDetailAsync(db, dispute, currentUser.Role, cancellationToken);
    }

    public async Task<DisputeDetailDto> UpdateStatusAsync(
        Guid id, UpdateDisputeStatusRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var dispute = await DisputeReadModel.LoadAsync(db.Disputes, id, cancellationToken);
        var now = clock.GetUtcNow();
        switch (request.Action)
        {
            case AgentDisputeAction.StartReview:
                dispute.StartReview(currentUser.UserId, request.Note, now);
                break;
            case AgentDisputeAction.Uphold:
                dispute.Resolve(currentUser.UserId, upheld: true, request.Note!, now);
                break;
            case AgentDisputeAction.Reject:
                dispute.Resolve(currentUser.UserId, upheld: false, request.Note!, now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Action, "Unknown action.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return await DisputeReadModel.ToDetailAsync(db, dispute, currentUser.Role, cancellationToken);
    }
}

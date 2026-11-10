using Application.Abstractions;
using Application.Common;
using Domain.Common;
using Domain.Disputes;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Application.Disputes;

/// <summary>Customer-facing dispute operations. Every query is scoped to the signed-in customer.</summary>
public sealed class DisputeService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TimeProvider clock,
    IOptions<DisputeOptions> options,
    IValidator<CreateDisputeRequest> createValidator,
    IValidator<WithdrawDisputeRequest> withdrawValidator,
    IValidator<DisputeQuery> queryValidator)
{
    public const string IdempotencyKeyReusedCode = "dispute.idempotency_key_reused";
    public const int IdempotencyKeyMaxLength = 100;

    /// <returns>The dispute, and <c>false</c> if it already existed for <paramref name="idempotencyKey"/>.</returns>
    public async Task<(DisputeDetailDto Dispute, bool Created)> CreateAsync(
        CreateDisputeRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (key is { Length: > IdempotencyKeyMaxLength })
        {
            throw new ValidationException(
                [new FluentValidation.Results.ValidationFailure("Idempotency-Key", "Idempotency-Key is too long.")]);
        }

        if (key is not null)
        {
            var existing = await OwnDisputes()
                .Where(d => d.IdempotencyKey == key)
                .Select(d => new { d.Id, d.TransactionId })
                .SingleOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                if (existing.TransactionId != request.TransactionId)
                {
                    throw new ConflictException(
                        IdempotencyKeyReusedCode, "This Idempotency-Key was already used for a different request.");
                }

                return (await GetAsync(existing.Id, cancellationToken), false);
            }
        }

        var transaction = await db.Transactions
            .Where(t => t.Id == request.TransactionId && t.Account.CustomerId == currentUser.UserId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Transaction", request.TransactionId);

        var hasOpenDispute = await db.Disputes.AnyAsync(
            d => d.TransactionId == transaction.Id &&
                 (d.Status == DisputeStatus.Submitted || d.Status == DisputeStatus.UnderReview),
            cancellationToken);
        if (hasOpenDispute)
        {
            throw new ConflictException(
                DisputeErrorCodes.AlreadyDisputed, "This transaction already has an open dispute.");
        }

        var dispute = Dispute.Raise(
            transaction,
            currentUser.UserId,
            request.Reason,
            request.Description,
            key,
            clock.GetUtcNow(),
            options.Value.DisputeWindow);

        db.Disputes.Add(dispute);
        await db.SaveChangesAsync(cancellationToken);

        return (await DisputeReadModel.ToDetailAsync(db, dispute, currentUser.Role, cancellationToken), true);
    }

    public async Task<PagedResult<DisputeSummaryDto>> ListAsync(DisputeQuery query, CancellationToken cancellationToken)
    {
        await queryValidator.ValidateAndThrowAsync(query, cancellationToken);
        return await DisputeReadModel.ListAsync(db, OwnDisputes().AsNoTracking(), query, cancellationToken);
    }

    public async Task<DisputeDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var dispute = await DisputeReadModel.LoadAsync(OwnDisputes().AsNoTracking(), id, cancellationToken);
        return await DisputeReadModel.ToDetailAsync(db, dispute, currentUser.Role, cancellationToken);
    }

    public async Task<DisputeDetailDto> WithdrawAsync(
        Guid id, WithdrawDisputeRequest request, CancellationToken cancellationToken)
    {
        await withdrawValidator.ValidateAndThrowAsync(request, cancellationToken);

        var dispute = await DisputeReadModel.LoadAsync(OwnDisputes(), id, cancellationToken);
        dispute.Withdraw(currentUser.UserId, request.Note, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return await DisputeReadModel.ToDetailAsync(db, dispute, currentUser.Role, cancellationToken);
    }

    private IQueryable<Dispute> OwnDisputes() => db.Disputes.Where(d => d.CustomerId == currentUser.UserId);
}

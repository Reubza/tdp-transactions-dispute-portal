using Application.Abstractions;
using Application.Common;
using Domain.Accounts;
using Domain.Disputes;
using Domain.Transactions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Application.Transactions;

/// <summary>Read-side queries over the signed-in customer's transactions.</summary>
public sealed class TransactionService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TimeProvider clock,
    IOptions<DisputeOptions> options,
    IValidator<TransactionQuery> validator)
{
    public async Task<IReadOnlyList<AccountDto>> ListAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts.AsNoTracking()
            .Where(a => a.CustomerId == currentUser.UserId)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
        return accounts
            .Select(a => new AccountDto(a.Id, a.Name, a.MaskedAccountNumber, a.Type.ToString(), a.Currency))
            .ToList();
    }

    public async Task<PagedResult<TransactionDto>> ListAsync(TransactionQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);

        var transactions = OwnTransactions();

        if (query.AccountId is { } accountId)
        {
            transactions = transactions.Where(t => t.AccountId == accountId);
        }

        if (query.From is { } from)
        {
            var fromUtc = from.ToUniversalTime();
            transactions = transactions.Where(t => t.PostedAt >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = to.ToUniversalTime();
            transactions = transactions.Where(t => t.PostedAt <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            transactions = transactions.Where(t =>
                t.Merchant.ToLower().Contains(term) ||
                t.Reference.ToLower().Contains(term) ||
                (t.Description != null && t.Description.ToLower().Contains(term)));
        }

        if (query.Disputed is { } disputed)
        {
            transactions = transactions.Where(t => db.Disputes.Any(d => d.TransactionId == t.Id) == disputed);
        }

        var (rows, total) = await Project(transactions.OrderByDescending(t => t.PostedAt).ThenBy(t => t.Id))
            .ToPageAsync(query, cancellationToken);

        return new PagedResult<TransactionDto>(rows.Select(ToDto).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<TransactionDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await Project(OwnTransactions().Where(t => t.Id == id)).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Transaction", id);
        return ToDto(row);
    }

    private IQueryable<Transaction> OwnTransactions() =>
        db.Transactions.AsNoTracking().Where(t => t.Account.CustomerId == currentUser.UserId);

    private IQueryable<TransactionRow> Project(IQueryable<Transaction> transactions) =>
        transactions.Select(t => new TransactionRow(
            t,
            t.Account.Name,
            t.Account.AccountNumber,
            db.Disputes
                .Where(d => d.TransactionId == t.Id)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new LatestDispute(d.Id, d.Status))
                .FirstOrDefault(),
            db.Disputes
                .Where(d => d.TransactionId == t.Id)
                .Select(d => new PriorDispute(d.Id, d.Status, d.ResolvedAt, d.AppealOfDisputeId))
                .ToList()));

    private TransactionDto ToDto(TransactionRow row)
    {
        var t = row.Transaction;
        var eligibility = DisputeEligibility.Check(t, row.PriorDisputes, clock.GetUtcNow(), options.Value.Rules);

        return new TransactionDto(
            t.Id,
            t.AccountId,
            row.AccountName,
            Account.Mask(row.AccountNumber),
            t.Amount,
            t.Currency,
            t.Direction,
            t.Status,
            t.Merchant,
            t.Reference,
            t.Description,
            t.PostedAt,
            eligibility.IsEligible,
            eligibility.Error?.Message,
            eligibility.IsAppeal,
            row.LatestDispute?.Id,
            row.LatestDispute?.Status);
    }

    private sealed record LatestDispute(Guid Id, DisputeStatus Status);

    private sealed record TransactionRow(
        Transaction Transaction,
        string AccountName,
        string AccountNumber,
        LatestDispute? LatestDispute,
        List<PriorDispute> PriorDisputes);
}

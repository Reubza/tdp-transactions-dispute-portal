using Application.Common;
using FluentValidation;

namespace Application.Transactions;

public sealed class TransactionQueryValidator : AbstractValidator<TransactionQuery>
{
    public TransactionQueryValidator()
    {
        this.ApplyPagingRules();
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .When(q => q.From.HasValue && q.To.HasValue)
            .WithMessage("'To' must be on or after 'From'.");
    }
}

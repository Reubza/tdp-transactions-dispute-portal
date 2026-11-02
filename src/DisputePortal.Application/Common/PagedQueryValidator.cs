using FluentValidation;

namespace DisputePortal.Application.Common;

internal static class PagedQueryValidator
{
    public static void ApplyPagingRules<T>(this AbstractValidator<T> validator)
        where T : PagedQuery
    {
        validator.RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        validator.RuleFor(q => q.PageSize).InclusiveBetween(1, PagedQuery.MaxPageSize);
    }
}

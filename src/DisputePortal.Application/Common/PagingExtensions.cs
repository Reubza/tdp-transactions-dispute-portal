using Microsoft.EntityFrameworkCore;

namespace DisputePortal.Application.Common;

internal static class PagingExtensions
{
    public static async Task<(List<T> Items, int Total)> ToPageAsync<T>(
        this IQueryable<T> query, PagedQuery paging, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence;

/// <summary>Orders persisted instants without moving scoped queries or paging into memory.</summary>
public static class UtcQueryExtensions
{
    public static IOrderedQueryable<TEntity> OrderByUtc<TEntity>(
        this IQueryable<TEntity> query,
        DbContext db,
        Expression<Func<TEntity, DateTimeOffset>> timestamp,
        bool descending = false) where TEntity : class
    {
        if (!db.Database.IsSqlite())
        {
            return descending ? query.OrderByDescending(timestamp) : query.OrderBy(timestamp);
        }

        if (timestamp.Body is not MemberExpression { Expression: ParameterExpression } member)
        {
            throw new ArgumentException("A mapped timestamp property is required.", nameof(timestamp));
        }

        var column = member.Member.Name + "SortTicks";
        return descending
            ? query.OrderByDescending(entity => EF.Property<long>(entity, column))
            : query.OrderBy(entity => EF.Property<long>(entity, column));
    }
}

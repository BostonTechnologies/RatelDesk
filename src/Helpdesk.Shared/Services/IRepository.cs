using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Helpdesk.Shared.Services;

/// <summary>
/// Generic repository abstraction for CRUD operations.
/// </summary>
/// <typeparam name="T">Entity type.</typeparam>
public interface IRepository<T>
{
    /// <summary>
    /// Retrieves all entities of type <typeparamref name="T"/>.
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Counts entities matching a predicate without materializing them.
    /// </summary>
    /// <param name="predicate">The database-translatable filter to apply.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single entity by identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    Task<T?> GetAsync(string id);

    /// <summary>
    /// Creates a new entity instance.
    /// </summary>
    Task<T> CreateAsync(T entity);

    /// <summary>
    /// Persists updates to an entity.
    /// </summary>
    Task<T?> UpdateAsync(T entity);

    /// <summary>
    /// Deletes an entity by identifier.
    /// </summary>
    Task<bool> DeleteAsync(string id);

    /// <summary>
    /// Returns a no-tracking <see cref="IQueryable{T}"/> for composing database-side queries.
    /// Consumers should finish with ToListAsync/FirstOrDefaultAsync/etc.
    /// </summary>
    IQueryable<T> Query();
}

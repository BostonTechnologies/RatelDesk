using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using Dodo.Primitives;

namespace Helpdesk.Shared.Services;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IRepository{T}"/>.
/// </summary>
/// <typeparam name="T">Entity type with an <c>Id</c> property of type <see cref="string"/>.</typeparam>
public class InMemoryRepository<T> : IRepository<T> where T : class
{
    private readonly ConcurrentDictionary<string, T> _store = new();

    /// <inheritdoc />
    public Task<T> CreateAsync(T entity)
    {
        var idProp = typeof(T).GetProperty("Id")
            ?? throw new InvalidOperationException("Entity must have Id property");

        var idValue = idProp.GetValue(entity);
        if (idValue is not string id || string.IsNullOrWhiteSpace(id))
        {
            id = Uuid.CreateVersion7().ToString();
            idProp.SetValue(entity, id);
        }

        _store[id] = entity;
        return Task.FromResult(entity);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string id)
        => Task.FromResult(_store.TryRemove(id, out _));

    /// <inheritdoc />
    public Task<T?> GetAsync(string id)
    {
        _store.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    /// <inheritdoc />
    public Task<IEnumerable<T>> GetAllAsync()
        => Task.FromResult(_store.Values.AsEnumerable());

    /// <inheritdoc />
    public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Query().Count(predicate));
    }

    /// <inheritdoc />
    public Task<T?> UpdateAsync(T entity)
    {
        var idProp = typeof(T).GetProperty("Id")
            ?? throw new InvalidOperationException("Entity must have Id property");

        if (idProp.GetValue(entity) is not string id)
            throw new InvalidOperationException("Entity Id property must be string");

        if (!_store.ContainsKey(id))
            return Task.FromResult<T?>(null);

        _store[id] = entity;
        return Task.FromResult<T?>(entity);
    }

    /// <inheritdoc />
    public IQueryable<T> Query()
    {
        // Return a stable snapshot to avoid enumeration issues during concurrent writes
        return _store.Values.ToArray().AsQueryable();
    }
}

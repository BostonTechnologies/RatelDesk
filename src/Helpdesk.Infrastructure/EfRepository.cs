using System.Linq.Expressions;
using Helpdesk.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence;

public class EfRepository<T>(HelpdeskDbContext context) : IRepository<T> where T : class
{
    private readonly HelpdeskDbContext _context = context;
    public IQueryable<T> Query() => _context.Set<T>().AsNoTracking();

    public async Task<T> CreateAsync(T entity)
    {
        _context.Set<T>().Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var entity = await GetAsync(id);
        if (entity is null)
        {
            return false;
        }
        _context.Set<T>().Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _context.Set<T>().AsNoTracking().ToListAsync();
    }

    public async Task<T?> GetAsync(string id)
    {
        var prop = typeof(T).GetProperty("Id");
        if (prop is null)
            return null;

        var keyType = prop.PropertyType;
        var normalizedId = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();

        if (keyType == typeof(string))
        {
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                return null;
            }

            return await QueryByStringKeyAsync(prop.Name, BuildStringIdCandidates(normalizedId));
        }

        object? typedId = id;
        if (keyType == typeof(Guid) && Guid.TryParse(id, out var guid))
            typedId = guid;
        else if (keyType != typeof(string))
            typedId = Convert.ChangeType(id, keyType);

        return await _context.Set<T>().FindAsync(typedId);
    }

    private async Task<T?> QueryByStringKeyAsync(string propertyName, IReadOnlyList<string> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var entity = Expression.Parameter(typeof(T), "entity");
        var property = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [typeof(string)],
            entity,
            Expression.Constant(propertyName));

        var containsMethod = typeof(Enumerable)
            .GetMethods()
            .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(string));

        var candidatesConstant = Expression.Constant(candidates);
        var body = Expression.Call(containsMethod, candidatesConstant, property);
        var predicate = Expression.Lambda<Func<T, bool>>(body, entity);

        return await _context.Set<T>().FirstOrDefaultAsync(predicate);
    }

    private static IReadOnlyList<string> BuildStringIdCandidates(string id)
    {
        var candidates = new List<string> { id };

        if (Guid.TryParse(id, out var parsedGuid))
        {
            AddCandidate(candidates, parsedGuid.ToString("N"));
            AddCandidate(candidates, parsedGuid.ToString("D"));
        }

        return candidates;
    }

    private static void AddCandidate(List<string> candidates, string value)
    {
        if (!candidates.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(value);
        }
    }

    public async Task<T?> UpdateAsync(T entity)
    {
        // This is the standard, correct, and type-safe way to update
        // an entity in EF Core. It attaches the entity to the context
        // and marks its state as "Modified".
        _context.Set<T>().Update(entity);

        // SaveChangesAsync will now generate the correct SQL UPDATE statement
        // for the record matching the entity's primary key.
        await _context.SaveChangesAsync();

        return entity;
    }
}

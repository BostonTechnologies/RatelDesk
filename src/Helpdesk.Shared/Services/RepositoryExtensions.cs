namespace Helpdesk.Shared.Services;

public static class RepositoryExtensions
{
    /// <summary>
    /// Convenience alias for GetAsync to match common naming.
    /// </summary>
    public static Task<T?> GetByIdAsync<T>(this IRepository<T> repo, string id) where T : class
        => repo.GetAsync(id);
}

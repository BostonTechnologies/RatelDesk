namespace HelpDesk.NewWeb.Services.Search;

public sealed record GlobalSearchGroup(
    string Key,
    string Title,
    string Icon,
    string Href,
    IReadOnlyList<GlobalSearchResult> Results,
    string? ContinueHref = null,
    int TotalCount = 0,
    bool IsLoading = false,
    bool TimedOut = false);

public sealed record GlobalSearchResult(
    string Section,
    string Title,
    string? Subtitle,
    string? Meta,
    string Href,
    string Icon);

public interface IGlobalSearchService
{
    Task<IReadOnlyList<GlobalSearchGroup>> SearchAsync(string? query, CancellationToken cancellationToken = default);

    IAsyncEnumerable<GlobalSearchGroup> SearchIncrementalAsync(string? query, CancellationToken cancellationToken = default);
}

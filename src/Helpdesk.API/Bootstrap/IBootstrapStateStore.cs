namespace Helpdesk.API.Bootstrap;

public interface IBootstrapStateStore
{
    Task<BootstrapDescriptor> LoadOrCreateAsync(CancellationToken cancellationToken = default);

    Task<BootstrapDescriptor> UpdateAsync(
        Func<BootstrapDescriptor, BootstrapDescriptor> update,
        CancellationToken cancellationToken = default);
}

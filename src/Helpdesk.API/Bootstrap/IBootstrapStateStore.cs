namespace Helpdesk.API.Bootstrap;

public interface IBootstrapStateStore
{
    Task<BootstrapDescriptor> LoadOrCreateAsync(CancellationToken cancellationToken = default);

    Task<BootstrapDescriptor> UpdateAsync(
        Func<BootstrapDescriptor, BootstrapDescriptor> update,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the operator setup code while the instance has not completed setup.
    /// Returns <see langword="null"/> once setup is complete or recovery is required.
    /// </summary>
    Task<string?> RotateSetupCodeAsync(CancellationToken cancellationToken = default);
}

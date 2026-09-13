namespace Helpdesk.API.Bootstrap;

/// <summary>Serializes selection and initialization, including across API processes.</summary>
public static class BootstrapOperationLease
{
    public static async Task<FileStream> AcquireAsync(string directory, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "operation.lock");
        var started = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (started.Elapsed < TimeSpan.FromSeconds(30))
            {
                await Task.Delay(100, token);
            }
        }
    }
}

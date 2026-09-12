using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using Hangfire.Storage.SQLite;

namespace Helpdesk.Tests.Api;

public sealed class SqliteSchedulerStorageTests
{
    [Fact]
    public void Recurring_job_registration_survives_reopening_the_sqlite_scheduler_store()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rateldesk-scheduler-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(directory, "rateldesk.hangfire.db");
        Directory.CreateDirectory(directory);

        try
        {
            using (var storage = new SQLiteStorage(databasePath))
            {
                var recurringJobs = new RecurringJobManager(storage);
                recurringJobs.AddOrUpdate(
                    "durable-scheduler-test",
                    Job.FromExpression(() => Console.WriteLine("scheduler persistence probe")),
                    "*/15 * * * *");
            }

            using var reopenedStorage = new SQLiteStorage(databasePath);
            using var connection = reopenedStorage.GetConnection();

            Assert.Contains(connection.GetRecurringJobs(), job => job.Id == "durable-scheduler-test" && !job.Removed);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

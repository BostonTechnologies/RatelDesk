using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Infrastructure.Persistence.Entities;
using Helpdesk.Shared.DTOs.Notification;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Helpdesk.Tests.Persistence;

public sealed class NotificationRepositoryTests
{
    [Fact]
    public async Task GetNotificationsForUserAsync_FiltersByCreatedRange()
    {
        await using var ctx = CreateContext();
        await SeedNotificationAsync(ctx, "Before", "user-1", new DateTime(2026, 5, 20, 7, 59, 0, DateTimeKind.Utc));
        await SeedNotificationAsync(ctx, "Inside", "user-1", new DateTime(2026, 5, 20, 8, 30, 0, DateTimeKind.Utc));
        await SeedNotificationAsync(ctx, "After", "user-1", new DateTime(2026, 5, 20, 10, 1, 0, DateTimeKind.Utc));
        var repo = new NotificationRepository(ctx);

        var from = new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 20, 10, 0, 0, TimeSpan.Zero);

        var result = await repo.GetNotificationsForUserAsync(
            "user-1",
            skip: 0,
            take: 10,
            searchTerm: null,
            severity: null,
            source: null,
            category: null,
            from: from,
            to: to,
            sortBy: "created",
            sortDir: "asc",
            CancellationToken.None);
        var count = await repo.CountNotificationsForUserAsync("user-1", null, null, null, null, from, to, CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal("Inside", item.Title);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetNotificationsForUserAsync_SupportsOpenEndedCreatedFrom()
    {
        await using var ctx = CreateContext();
        await SeedNotificationAsync(ctx, "Before", "user-1", new DateTime(2026, 5, 20, 7, 59, 0, DateTimeKind.Utc));
        await SeedNotificationAsync(ctx, "Inside", "user-1", new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc));
        await SeedNotificationAsync(ctx, "After", "user-1", new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc));
        var repo = new NotificationRepository(ctx);

        var from = new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero);

        var result = await repo.GetNotificationsForUserAsync(
            "user-1",
            skip: 0,
            take: 10,
            searchTerm: null,
            severity: null,
            source: null,
            category: null,
            from: from,
            to: null,
            sortBy: "created",
            sortDir: "asc",
            CancellationToken.None);

        Assert.Equal(["Inside", "After"], result.Select(x => x.Title));
    }

    [Fact]
    public async Task GetNotificationsForUserAsync_SupportsOpenEndedCreatedTo()
    {
        await using var ctx = CreateContext();
        await SeedNotificationAsync(ctx, "Before", "user-1", new DateTime(2026, 5, 20, 7, 59, 0, DateTimeKind.Utc));
        await SeedNotificationAsync(ctx, "Inside", "user-1", new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc));
        await SeedNotificationAsync(ctx, "After", "user-1", new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc));
        var repo = new NotificationRepository(ctx);

        var to = new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero);

        var result = await repo.GetNotificationsForUserAsync(
            "user-1",
            skip: 0,
            take: 10,
            searchTerm: null,
            severity: null,
            source: null,
            category: null,
            from: null,
            to: to,
            sortBy: "created",
            sortDir: "asc",
            CancellationToken.None);

        Assert.Equal(["Before", "Inside"], result.Select(x => x.Title));
    }

    private static HelpdeskDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HelpdeskDbContext(options, Substitute.For<ITenantContext>(), new HttpContextAccessor());
    }

    private static async Task SeedNotificationAsync(HelpdeskDbContext ctx, string title, string userId, DateTime createdUtc)
    {
        ctx.Notifications.Add(new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = title,
            Severity = NotificationSeverity.Info,
            CreatedUtc = createdUtc,
            Source = "Application",
            Category = "System"
        });
        await ctx.SaveChangesAsync();
    }
}

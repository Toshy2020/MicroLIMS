using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// The header polls notifications every minute. With a throttle, only one poll
// per user per window recomputes (and saves/emails); the rest read the saved list.
public class NotificationRecomputeThrottleTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<(User Reviewer, CauseOfTesting Cause)> SeedReviewerAsync(MicroLimsDbContext db)
    {
        var role = new Role { Type = RoleType.Reviewer, Name = "Reviewer" };
        db.Roles.Add(role);
        var cause = new CauseOfTesting { Name = "Routine" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var reviewer = new User { FullName = "Rita Viewer", Username = "reviewer", RoleId = role.Id, PasswordHash = "not-used" };
        db.Users.Add(reviewer);
        await db.SaveChangesAsync();
        return (reviewer, cause);
    }

    private static async Task AddSampleUnderReviewAsync(MicroLimsDbContext db, CauseOfTesting cause, string reference)
    {
        db.Samples.Add(new Sample { ReferenceNumber = reference, Status = SampleStatus.UnderReview, CauseOfTesting = cause });
        await db.SaveChangesAsync();
    }

    private static DashboardNotificationService Service(MicroLimsDbContext db, NotificationRecomputeThrottle? throttle) =>
        new(db, new NoOpNotificationService(), new NoOpEmailSender(), throttle);

    [Fact]
    public void IsDue_UntilComputed_ThenAgainOnlyAfterTheWindow()
    {
        var throttle = new NotificationRecomputeThrottle(TimeSpan.FromMinutes(5));
        var start = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(throttle.IsDue(1, start));

        throttle.MarkComputed(1, start);
        Assert.False(throttle.IsDue(1, start.AddMinutes(4).AddSeconds(59)));
        Assert.True(throttle.IsDue(1, start.AddMinutes(5)));

        // Tracked per user.
        Assert.True(throttle.IsDue(2, start.AddMinutes(1)));
    }

    [Fact]
    public async Task WithThrottle_SecondPollInsideWindow_ReturnsSavedListWithoutRecomputing()
    {
        await using var db = NewDb();
        var (reviewer, cause) = await SeedReviewerAsync(db);
        await AddSampleUnderReviewAsync(db, cause, "FP-R1");
        var throttle = new NotificationRecomputeThrottle(TimeSpan.FromMinutes(5));

        var first = await Service(db, throttle).GetNotificationsAsync(RoleType.Reviewer, reviewer.Id);
        Assert.Contains(first, n => n.Message == "1 sample(s) awaiting review.");

        // A second sample enters review, but the next poll is inside the window.
        await AddSampleUnderReviewAsync(db, cause, "FP-R2");
        var second = await Service(db, throttle).GetNotificationsAsync(RoleType.Reviewer, reviewer.Id);

        Assert.DoesNotContain(second, n => n.Message == "2 sample(s) awaiting review.");
        Assert.Equal(first.Select(n => n.Id), second.Select(n => n.Id));
        Assert.Equal(1, await db.NotificationLogs.CountAsync(n => n.UserId == reviewer.Id));
    }

    [Fact]
    public async Task WithoutThrottle_EveryPollRecomputes()
    {
        await using var db = NewDb();
        var (reviewer, cause) = await SeedReviewerAsync(db);
        await AddSampleUnderReviewAsync(db, cause, "FP-R1");

        await Service(db, null).GetNotificationsAsync(RoleType.Reviewer, reviewer.Id);
        await AddSampleUnderReviewAsync(db, cause, "FP-R2");
        var second = await Service(db, null).GetNotificationsAsync(RoleType.Reviewer, reviewer.Id);

        Assert.Contains(second, n => n.Message == "2 sample(s) awaiting review.");
    }

    [Fact]
    public async Task WithThrottle_MarkingReadIsVisibleOnTheNextPollInsideTheWindow()
    {
        await using var db = NewDb();
        var (reviewer, cause) = await SeedReviewerAsync(db);
        await AddSampleUnderReviewAsync(db, cause, "FP-R1");
        var throttle = new NotificationRecomputeThrottle(TimeSpan.FromMinutes(5));

        var first = await Service(db, throttle).GetNotificationsAsync(RoleType.Reviewer, reviewer.Id);
        var notification = Assert.Single(first);
        Assert.False(notification.IsRead);

        await Service(db, throttle).MarkAsReadAsync(notification.Id!.Value, reviewer.Id);
        var second = await Service(db, throttle).GetNotificationsAsync(RoleType.Reviewer, reviewer.Id);

        Assert.True(Assert.Single(second).IsRead);
    }
}

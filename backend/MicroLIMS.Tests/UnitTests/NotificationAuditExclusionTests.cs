using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Notifications are derived reminders, not GxP records - the action they
// point to is audited on its own entity - so creating one or marking it
// read must not add entries to the audit trail.
public class NotificationAuditExclusionTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task SaveChanges_NotificationCreatedAndRead_WritesNoAuditLog()
    {
        await using var db = NewDb();

        var log = new NotificationLog { UserId = 1, Type = "ReviewWaiting", Message = "1 sample(s) awaiting review." };
        db.NotificationLogs.Add(log);
        await db.SaveChangesAsync();

        log.IsRead = true;
        await db.SaveChangesAsync();

        Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityName == nameof(NotificationLog)));
    }

    [Fact]
    public async Task SaveChanges_GxpEntityAlongsideNotification_StillAuditsTheGxpEntity()
    {
        await using var db = NewDb();

        db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine" });
        db.NotificationLogs.Add(new NotificationLog { UserId = 1, Type = "MediaExpiry", Message = "Lot expires." });
        await db.SaveChangesAsync();

        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityName == nameof(CauseOfTesting)));
        Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityName == nameof(NotificationLog)));
    }
}

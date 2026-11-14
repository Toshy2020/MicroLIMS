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

    [Fact]
    public async Task SaveChanges_EmailOutboxAndEscalationEvent_WriteNoAuditLog_WhileConfigEntitiesAreAudited()
    {
        await using var db = NewDb();
        var now = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

        var rule = new EscalationRule
        {
            SectionId = 1,
            Trigger = MicroLIMS.Domain.Enums.EscalationTrigger.ReviewWaiting,
            ThresholdWorkingHours = 8m,
            NotifyRoleType = MicroLIMS.Domain.Enums.RoleType.SectionHead,
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = now
        };
        db.EscalationRules.Add(rule);
        await db.SaveChangesAsync();

        db.EmailOutbox.Add(new EmailOutbox
        {
            UserId = 1,
            ToAddress = "user@example.com",
            Subject = "Alert",
            Body = "Body",
            Kind = MicroLIMS.Domain.Enums.EmailOutboxKind.Immediate,
            CreatedAt = now,
            NextAttemptAt = now
        });
        db.EscalationEvents.Add(new EscalationEvent
        {
            EscalationRuleId = rule.Id,
            RecordType = "Sample",
            RecordId = 42,
            FiredAt = now,
            RecipientUserIds = "[1]",
            NoRecipient = false
        });
        db.LabWorkingHours.Add(new LabWorkingHours
        {
            DayOfWeek = DayOfWeek.Sunday,
            IsWorkingDay = true,
            StartMinute = 8 * 60,
            EndMinute = 16 * 60
        });
        db.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            UserId = 1,
            Type = "MediaExpiry",
            EmailMode = MicroLIMS.Domain.Enums.EmailMode.Digest
        });
        db.MandatoryNotificationTypes.Add(new MandatoryNotificationType
        {
            SectionId = 1,
            Type = "Escalation",
            SetByUserId = 1,
            SetAt = now
        });
        await db.SaveChangesAsync();

        Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityName == nameof(EmailOutbox)));
        Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityName == nameof(EscalationEvent)));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityName == nameof(EscalationRule)));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityName == nameof(LabWorkingHours)));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityName == nameof(UserNotificationPreference)));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityName == nameof(MandatoryNotificationType)));
    }
}

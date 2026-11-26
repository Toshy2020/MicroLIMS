using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Competency;
using MicroLIMS.Application.Services.MyWork;
using MicroLIMS.Application.Services.MyWork.Sources;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MyWorkCompetencySourcePostgresTests(PostgresTestFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2037, 1, 30, 22, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly LabToday = new(2037, 1, 31);
    private readonly FixedTimeProvider _clock = new(Now);

    private sealed class StubSettings : ISystemSettingsReader
    {
        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(7);
        public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class NoopErrorCapture : IErrorCaptureService
    {
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private LabClock Clock => new(_clock, LabClock.ResolveTimeZone("Africa/Cairo"));

    private MyWorkService BuildService(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db), Clock, new StubSettings(), new NoopErrorCapture(),
            new IMyWorkSource[] { new CompetencyMyWorkSource(new CompetencyReminderService(db, Clock)) });

    [PostgresFact]
    public async Task MyWork_includes_own_expiring_and_expired_qualifications_and_excludes_others_and_withdrawn()
    {
        int userA, userB;
        int sectionId;
        int testExpiringId, testExpiredId, testWithdrawnId;
        string codeExpiring = "EXPG-" + Guid.NewGuid().ToString("N")[..6];
        string codeExpired = "EXPD-" + Guid.NewGuid().ToString("N")[..6];
        string codeWithdrawn = "WDRW-" + Guid.NewGuid().ToString("N")[..6];

        await using (var db = fixture.CreateDbContext())
        {
            var key = Guid.NewGuid().ToString("N")[..8];
            var section = new DocumentSection
            {
                Code = key, Name = $"Lab {key}", DepartmentId = fixture.SeededDepartmentId, IsActive = true
            };
            db.DocumentSections.Add(section);

            var analystRoleId = await db.Roles.AsNoTracking()
                .Where(r => r.Type == RoleType.Analyst).Select(r => r.Id).FirstAsync();
            var a = new User { RoleId = analystRoleId, FullName = "Analyst A", Username = $"a-{key}", IsActive = true };
            var b = new User { RoleId = analystRoleId, FullName = "Analyst B", Username = $"b-{key}", IsActive = true };
            db.Users.AddRange(a, b);
            await db.SaveChangesAsync();
            sectionId = section.Id;
            userA = a.Id;
            userB = b.Id;

            db.UserOrgMemberships.AddRange(
                new UserOrgMembership { UserId = userA, DepartmentId = fixture.SeededDepartmentId, SectionId = sectionId },
                new UserOrgMembership { UserId = userB, DepartmentId = fixture.SeededDepartmentId, SectionId = sectionId });

            db.CompetencySectionSettings.Add(new CompetencySectionSetting
            {
                SectionId = sectionId, GateEnabled = false, ReminderDaysBeforeExpiry = 14,
                ChangedAt = Now.UtcDateTime, ChangedByUserId = fixture.SeededControllerUserId
            });

            var t1 = new TestDefinition { Code = codeExpiring, DisplayName = "Expiring Test", SectionId = sectionId };
            var t2 = new TestDefinition { Code = codeExpired, DisplayName = "Expired Test", SectionId = sectionId };
            var t3 = new TestDefinition { Code = codeWithdrawn, DisplayName = "Withdrawn Test", SectionId = sectionId };
            db.TestDefinitions.AddRange(t1, t2, t3);
            await db.SaveChangesAsync();
            testExpiringId = t1.Id;
            testExpiredId = t2.Id;
            testWithdrawnId = t3.Id;

            await AddQualificationAsync(db, userA, testExpiringId, LabToday.AddDays(3));
            await AddQualificationAsync(db, userA, testExpiredId, LabToday.AddDays(-2));
            await AddQualificationAsync(db, userA, testWithdrawnId, LabToday.AddDays(3), withdrawn: true);
            await AddQualificationAsync(db, userB, testExpiringId, LabToday.AddDays(3));
        }

        await using var act = fixture.CreateDbContext(_clock);
        var response = await BuildService(act).GetAsync(userA, null);

        var group = Assert.Single(response.Groups, g => g.Key == "competency");
        Assert.Equal(2, group.Count);
        var expiredItem = Assert.Single(group.Items, i => i.Identifier == codeExpired);
        Assert.Equal(MyWorkUrgencyLevel.Overdue, expiredItem.Urgency);
        Assert.Equal("competency-qualification", expiredItem.Target.Type);

        var expiringItem = Assert.Single(group.Items, i => i.Identifier == codeExpiring);
        Assert.Equal(MyWorkUrgencyLevel.Later, expiringItem.Urgency);

        Assert.DoesNotContain(group.Items, i => i.Identifier == codeWithdrawn);

        var summary = await BuildService(act).GetSummaryAsync(userA);
        Assert.True(summary.Complete);
        Assert.Equal(2, summary.Total);
        Assert.Equal(1, summary.Overdue);
    }

    private async Task AddQualificationAsync(
        MicroLimsDbContext db, int userId, int testDefinitionId, DateOnly expiresOn, bool withdrawn = false)
    {
        var id = await fixture.CreateSequenceHelper(db).ReserveIdAsync<AnalystQualification>();
        var q = new AnalystQualification
        {
            Id = id,
            UserId = userId,
            TestDefinitionId = testDefinitionId,
            Basis = QualificationBasis.ObservedPractice,
            Evidence = "Observed",
            QualifiedOn = LabToday.AddMonths(-1),
            ExpiresOn = expiresOn,
            SignedByUserId = fixture.SeededControllerUserId,
            Signature = new ElectronicSignature
            {
                UserId = fixture.SeededControllerUserId, EntityType = nameof(AnalystQualification), EntityId = id,
                MeaningOfSignature = SignatureMeaning.AnalystQualified, SignedAt = Now.UtcDateTime.AddDays(-1)
            }
        };
        db.AnalystQualifications.Add(q);
        await db.SaveChangesAsync();
        if (withdrawn)
        {
            q.WithdrawnOn = Now.UtcDateTime;
            q.WithdrawnByUserId = fixture.SeededControllerUserId;
            q.WithdrawnReason = "Withdrawn";
            q.WithdrawnSignature = new ElectronicSignature
            {
                UserId = fixture.SeededControllerUserId, EntityType = nameof(AnalystQualification), EntityId = id,
                MeaningOfSignature = SignatureMeaning.AnalystQualificationWithdrawn, SignedAt = Now.UtcDateTime,
                Comment = "Withdrawn"
            };
            await db.SaveChangesAsync();
        }
    }
}

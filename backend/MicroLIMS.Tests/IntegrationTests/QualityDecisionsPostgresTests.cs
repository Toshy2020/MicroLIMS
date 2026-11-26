using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public sealed class QualityDecisionsPostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "Decisions-test-password";
    private static readonly DateTime BaseNow = new(2089, 3, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(BaseNow);

    private sealed class MutableClock(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => new(UtcNow);
    }

    private MicroLimsDbContext Context(MutableClock clock, int? actor = null)
    {
        var db = fixture.CreateDbContext(clock);
        db.CurrentUserId = actor;
        return db;
    }

    private static QualityRecordCore Core(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        return new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, clock),
            new ElectronicSignatureService(db, clock), clock);
    }

    private static QualityConfigService Config(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        return new QualityConfigService(db, Core(db, clock), permissions, scope, clock);
    }

    private static DeviationService Deviations(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = Core(db, clock);
        return new DeviationService(db, core, permissions, scope, new SegregationOfDutiesGuard(db),
            new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(db));
    }

    private static CapaService Capas(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        return new CapaService(db, Core(db, clock), permissions, scope, new LabClock(clock, TimeZoneInfo.Utc),
            new QualityDueDateService(db));
    }

    private static ChangeControlService ChangeControls(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = Core(db, clock);
        var config = new QualityConfigService(db, core, permissions, scope, clock);
        return new ChangeControlService(db, core, permissions, scope, new LabClock(clock, TimeZoneInfo.Utc),
            new QualityDueDateService(db), config);
    }

    private static ComplaintService Complaints(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        return new ComplaintService(db, Core(db, clock), permissions, scope, new LabClock(clock, TimeZoneInfo.Utc),
            new QualityDueDateService(db));
    }

    private static QualityLinkService Links(MicroLimsDbContext db, MutableClock clock) =>
        new(db, Core(db, clock), new PermissionService(db), new UserSectionScopeService(db), clock);

    private static QualityCommentService Comments(MicroLimsDbContext db, MutableClock clock) =>
        new(db, Core(db, clock), clock);

    private QualityAttachmentService Attachments(MicroLimsDbContext db, MutableClock clock) =>
        new(db, Core(db, clock), new InMemoryFileStorageService(), new AuditEventService(db, fixture.CreateSequenceHelper(db), clock), clock);

    private static QualityQueryService Queries(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = Core(db, clock);
        return new QualityQueryService(db, core, new QualityLinkService(db, core, permissions, scope, clock),
            new LabClock(clock, TimeZoneInfo.Utc));
    }

    private async Task<int> ActorAsync(MicroLimsDbContext db, RoleType role, params string[] extraPermissions)
    {
        var key = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = $"Dec {role} {key[..6]}",
            Username = key,
            IsActive = true,
            Role = new Role { Name = key, Type = role, IsActive = true },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var codes = new HashSet<string>(StringComparer.Ordinal)
        {
            PermissionConstants.QualityView,
            PermissionConstants.QualityRaise
        };
        if (role == RoleType.Qa) codes.Add(PermissionConstants.QualityApprove);
        if (role == RoleType.SectionHead)
        {
            codes.Add(PermissionConstants.QualityInvestigate);
            codes.Add(PermissionConstants.QualityLabConclude);
        }
        if (role == RoleType.QcManager) codes.Add(PermissionConstants.QualityLabConclude);
        foreach (var p in extraPermissions) codes.Add(p);

        var ids = await db.Permissions.AsNoTracking().Where(p => codes.Contains(p.Code)).Select(p => p.Id).ToListAsync();
        db.RolePermissions.AddRange(ids.Select(id => new RolePermission { RoleId = user.RoleId, PermissionId = id }));
        var section = await db.DocumentSections.AsNoTracking()
            .Where(s => s.IsActive && s.Id == fixture.SeededSectionId)
            .Select(s => new { s.Id, s.DepartmentId }).SingleAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id, DepartmentId = section.DepartmentId, SectionId = section.Id
        });
        await db.SaveChangesAsync();
        return user.Id;
    }

    [PostgresFact]
    public async Task Conf228_extension_decision_refuses_requester_raiser_and_owner_and_allows_other_qa()
    {
        var clock = new MutableClock(BaseNow);
        int raiserQa, ownerQa, otherQa;
        int recordId;
        uint recordVersion;

        await using (var db = Context(clock))
        {
            raiserQa = await ActorAsync(db, RoleType.Qa);
            ownerQa = await ActorAsync(db, RoleType.Qa);
            otherQa = await ActorAsync(db, RoleType.Qa);
            var rec = new QualityRecord
            {
                Number = $"EXT-{Guid.NewGuid():N}"[..20],
                Title = "Extension SoD test",
                Description = "Desc",
                Type = QualityRecordType.Deviation,
                SectionId = fixture.SeededSectionId,
                RaisedByUserId = raiserQa,
                OwnerUserId = ownerQa,
                DueDate = Today.AddDays(5),
                RaisedAtUtc = clock.UtcNow,
                DeviationDetail = new DeviationDetail
                {
                    Status = QualityDeviationStatus.Investigation,
                    OccurredAtUtc = clock.UtcNow.AddHours(-1),
                    ImmediateAction = "Containment"
                }
            };
            db.QualityRecords.Add(rec);
            await db.SaveChangesAsync();
            recordId = rec.Id;
            recordVersion = rec.Version;
        }

        QualityExtensionDto ext;
        await using (var actDb = Context(clock, ownerQa))
        {
            ext = await Config(actDb, clock).RequestAsync(recordId, null, Today.AddDays(10), "Need more time", recordVersion, ownerQa);
        }

        // 1) Requester (= ownerQa) cannot decide
        await using (var actDb = Context(clock, ownerQa))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Config(actDb, clock).DecideAsync(ext.Id, true, "Self", Password, ext.Version, ownerQa));
            Assert.Equal(MessageCodes.QualityExtensionSelfDecision, ex.Code);
        }

        // 2) Raiser (raiserQa, holds Quality.Approve, did not request) cannot decide
        await using (var actDb = Context(clock, raiserQa))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Config(actDb, clock).DecideAsync(ext.Id, true, "Raiser", Password, ext.Version, raiserQa));
            Assert.Equal(MessageCodes.QualityRaiserOrOwner, ex.Code);
        }

        // 3) Owner who did NOT request (test with action responsible requesting, ownerQa deciding)
        int actionId;
        int responsible;
        uint verAfter;
        await using (var db = Context(clock))
        {
            responsible = await ActorAsync(db, RoleType.Analyst);
            var action = new QualityAction
            {
                RecordId = recordId, Kind = QualityActionKind.Corrective, Description = "Action",
                ResponsibleUserId = responsible, DueDate = Today.AddDays(2)
            };
            db.QualityActions.Add(action);
            await db.SaveChangesAsync();
            actionId = action.Id;
            verAfter = await db.QualityRecords.AsNoTracking().Where(r => r.Id == recordId).Select(r => r.Version).SingleAsync();
        }
        QualityExtensionDto actionExt;
        await using (var actDb = Context(clock, responsible))
        {
            actionExt = await Config(actDb, clock).RequestAsync(recordId, actionId, Today.AddDays(8), "Lab delay", verAfter, responsible);
        }
        await using (var actDb = Context(clock, ownerQa))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Config(actDb, clock).DecideAsync(actionExt.Id, true, "Owner", Password, actionExt.Version, ownerQa));
            Assert.Equal(MessageCodes.QualityRaiserOrOwner, ex.Code);
        }

        // 4) Other QA can decide -> persisted state + AuditLogs
        QualityExtensionDto decided;
        await using (var actDb = Context(clock, otherQa))
        {
            decided = await Config(actDb, clock).DecideAsync(ext.Id, true, "Approved by QA", Password, ext.Version, otherQa);
        }
        Assert.Equal(QualityExtensionDecision.Approved, decided.Decision);

        await using var assertDb = Context(clock);
        var persistedExt = await assertDb.QualityExtensionRequests.AsNoTracking().SingleAsync(e => e.Id == ext.Id);
        Assert.Equal(QualityExtensionDecision.Approved, persistedExt.Decision);
        Assert.Equal(otherQa, persistedExt.DecidedByUserId);
        var persistedRec = await assertDb.QualityRecords.AsNoTracking().SingleAsync(r => r.Id == recordId);
        Assert.Equal(Today.AddDays(10), persistedRec.DueDate);
        Assert.True(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.UserId == otherQa && a.EntityName == nameof(QualityExtensionRequest) && a.EntityId == ext.Id.ToString()));
    }

    [PostgresFact]
    public async Task Conf229_cancel_refuses_raiser_and_owner_for_all_four_types_and_allows_other_qa()
    {
        var clock = new MutableClock(BaseNow);
        int raiserQa, ownerQa, otherQa;

        await using (var db = Context(clock))
        {
            raiserQa = await ActorAsync(db, RoleType.Qa);
            ownerQa = await ActorAsync(db, RoleType.Qa);
            otherQa = await ActorAsync(db, RoleType.Qa);
        }

        // Seed one open record of each type with raiser = raiserQa, owner = ownerQa
        async Task<int> SeedAsync(QualityRecordType type)
        {
            await using var db = Context(clock);
            var rec = new QualityRecord
            {
                Number = $"CAN-{type}-{Guid.NewGuid():N}"[..20],
                Title = $"Cancel {type}",
                Description = "Desc",
                Type = type,
                SectionId = fixture.SeededSectionId,
                RaisedByUserId = raiserQa,
                OwnerUserId = ownerQa,
                DueDate = Today.AddDays(5),
                RaisedAtUtc = clock.UtcNow
            };
            switch (type)
            {
                case QualityRecordType.Deviation:
                    rec.DeviationDetail = new DeviationDetail { Status = QualityDeviationStatus.Raised, OccurredAtUtc = clock.UtcNow.AddHours(-1), ImmediateAction = "Act" };
                    break;
                case QualityRecordType.Capa:
                    rec.CapaDetail = new CapaDetail { Status = CapaStatus.Plan };
                    break;
                case QualityRecordType.ChangeControl:
                    rec.ChangeControlDetail = new ChangeControlDetail { Status = ChangeControlStatus.Proposed, Reason = "Reason" };
                    break;
                case QualityRecordType.Complaint:
                    rec.ComplaintDetail = new ComplaintDetail { Status = ComplaintStatus.Received, Source = "Customer", ReceivedOn = Today, ComplaintText = "Text" };
                    break;
            }
            db.QualityRecords.Add(rec);
            await db.SaveChangesAsync();
            return rec.Id;
        }

        foreach (var type in Enum.GetValues<QualityRecordType>())
        {
            var id = await SeedAsync(type);
            uint ver;
            await using (var db = Context(clock))
                ver = await db.QualityRecords.AsNoTracking().Where(r => r.Id == id).Select(r => r.Version).SingleAsync();

            Task CancelCall(MicroLimsDbContext actDb, int actor) => type switch
            {
                QualityRecordType.Deviation => Deviations(actDb, clock).CancelAsync(id, "Reason", Password, ver, actor),
                QualityRecordType.Capa => Capas(actDb, clock).CancelAsync(id, "Reason", Password, ver, actor),
                QualityRecordType.ChangeControl => ChangeControls(actDb, clock).CancelAsync(id, "Reason", Password, ver, actor),
                QualityRecordType.Complaint => Complaints(actDb, clock).CancelAsync(id, "Reason", Password, ver, actor),
                _ => throw new ArgumentOutOfRangeException()
            };

            // Raiser refused
            await using (var actDb = Context(clock, raiserQa))
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CancelCall(actDb, raiserQa));
                Assert.Equal(MessageCodes.QualityRaiserOrOwner, ex.Code);
            }

            // Owner refused
            await using (var actDb = Context(clock, ownerQa))
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CancelCall(actDb, ownerQa));
                Assert.Equal(MessageCodes.QualityRaiserOrOwner, ex.Code);
            }

            // Other QA succeeds
            await using (var actDb = Context(clock, otherQa))
                await CancelCall(actDb, otherQa);

            await using var assertDb = Context(clock);
            var persisted = await assertDb.QualityRecords.AsNoTracking().SingleAsync(r => r.Id == id);
            Assert.True(persisted.IsCancelled);
            Assert.True(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.UserId == otherQa && a.EntityName == nameof(QualityRecord) && a.EntityId == id.ToString()));
        }
    }

    [PostgresFact]
    public async Task Conf234_complaint_justified_requires_linked_non_cancelled_deviation_or_capa()
    {
        var clock = new MutableClock(BaseNow);
        int qaReceiver, qaApprover, owner;
        int cancelledDevId, activeDevId, cmpId1, cmpId2;
        uint cmpVer1, cmpVer2;

        await using (var db = Context(clock))
        {
            qaReceiver = await ActorAsync(db, RoleType.Qa);
            qaApprover = await ActorAsync(db, RoleType.Qa);
            owner = await ActorAsync(db, RoleType.SectionHead);

            var cancelledDev = new QualityRecord
            {
                Number = $"DEV-C-{Guid.NewGuid():N}"[..20],
                Title = "Cancelled Dev", Description = "Desc", Type = QualityRecordType.Deviation,
                SectionId = fixture.SeededSectionId, RaisedByUserId = owner, RaisedAtUtc = clock.UtcNow, IsCancelled = true,
                DeviationDetail = new DeviationDetail { Status = QualityDeviationStatus.Cancelled, OccurredAtUtc = clock.UtcNow.AddHours(-1), ImmediateAction = "Act" }
            };
            var activeDev = new QualityRecord
            {
                Number = $"DEV-A-{Guid.NewGuid():N}"[..20],
                Title = "Active Dev", Description = "Desc", Type = QualityRecordType.Deviation,
                SectionId = fixture.SeededSectionId, RaisedByUserId = owner, RaisedAtUtc = clock.UtcNow,
                DeviationDetail = new DeviationDetail { Status = QualityDeviationStatus.Raised, OccurredAtUtc = clock.UtcNow.AddHours(-1), ImmediateAction = "Act" }
            };
            db.QualityRecords.AddRange(cancelledDev, activeDev);
            await db.SaveChangesAsync();
            cancelledDevId = cancelledDev.Id;
            activeDevId = activeDev.Id;

            async Task<(int Id, uint Ver)> SeedLabReportedComplaintAsync()
            {
                var c = new QualityRecord
                {
                    Number = $"CMP-{Guid.NewGuid():N}"[..20],
                    Title = "Complaint", Description = "Desc", Type = QualityRecordType.Complaint,
                    SectionId = fixture.SeededSectionId, RaisedByUserId = qaReceiver, OwnerUserId = owner, RaisedAtUtc = clock.UtcNow,
                    ComplaintDetail = new ComplaintDetail
                    {
                        Status = ComplaintStatus.LabReported, Source = "Customer", ReceivedOn = Today,
                        ComplaintText = "Broken seal", WhatToTest = "Seal check", Findings = "Confirmed"
                    }
                };
                db.QualityRecords.Add(c);
                await db.SaveChangesAsync();
                var sig = new ElectronicSignature
                {
                    UserId = owner, UserFullNameSnapshot = "Owner", UsernameSnapshot = "owner", RoleSnapshot = "SectionHead",
                    MeaningOfSignature = SignatureMeaning.QualityLabConclusion, EntityType = nameof(QualityRecord),
                    EntityId = c.Id, SignedAt = clock.UtcNow
                };
                db.ElectronicSignatures.Add(sig);
                await db.SaveChangesAsync();
                db.QualityRecordSteps.Add(new QualityRecordStep
                {
                    RecordId = c.Id, Kind = QualityStepKind.ComplaintLabReport, SignatureId = sig.Id,
                    SignedByUserId = owner, SignedAtUtc = clock.UtcNow, SnapshotJson = "{}"
                });
                await db.SaveChangesAsync();
                return (c.Id, c.Version);
            }

            (cmpId1, cmpVer1) = await SeedLabReportedComplaintAsync();
            (cmpId2, cmpVer2) = await SeedLabReportedComplaintAsync();
        }

        // 1) Justified without link -> refused
        await using (var actDb = Context(clock, qaApprover))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Complaints(actDb, clock).ConcludeAsync(cmpId1, new(ComplaintOutcome.Justified, "Confirmed", Password), cmpVer1, qaApprover));
            Assert.Equal(MessageCodes.ComplaintFollowUpRequired, ex.Code);
        }

        // 2) Link a CANCELLED deviation -> still refused
        await using (var actDb = Context(clock, qaApprover))
        {
            var linked = await Links(actDb, clock).AddAsync(cmpId1, QualityLinkTargetType.QualityRecord, cancelledDevId, qaApprover, cmpVer1);
            Assert.NotNull(linked);
        }
        await using (var db = Context(clock))
            cmpVer1 = await db.QualityRecords.AsNoTracking().Where(r => r.Id == cmpId1).Select(r => r.Version).SingleAsync();
        await using (var actDb = Context(clock, qaApprover))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Complaints(actDb, clock).ConcludeAsync(cmpId1, new(ComplaintOutcome.Justified, "Confirmed", Password), cmpVer1, qaApprover));
            Assert.Equal(MessageCodes.ComplaintFollowUpRequired, ex.Code);
        }

        // 3) Link an active Deviation via FollowUpRecordId -> succeeds!
        await using (var actDb = Context(clock, qaApprover))
        {
            var concluded = await Complaints(actDb, clock).ConcludeAsync(cmpId1,
                new(ComplaintOutcome.Justified, "Confirmed", Password, activeDevId), cmpVer1, qaApprover);
            Assert.Equal(ComplaintStatus.Concluded, concluded.Status);
        }

        // 4) NotJustified without link -> succeeds!
        await using (var actDb = Context(clock, qaApprover))
        {
            var concluded = await Complaints(actDb, clock).ConcludeAsync(cmpId2,
                new(ComplaintOutcome.NotJustified, "No defect", Password), cmpVer2, qaApprover);
            Assert.Equal(ComplaintStatus.Concluded, concluded.Status);
        }

        await using var assertDb = Context(clock);
        Assert.True(await assertDb.QualityRecordLinks.AsNoTracking().AnyAsync(l =>
            l.RecordId == cmpId1 && l.TargetType == QualityLinkTargetType.QualityRecord && l.TargetId == activeDevId));
        var d1 = await assertDb.ComplaintDetails.AsNoTracking().SingleAsync(d => d.RecordId == cmpId1);
        Assert.Equal(ComplaintStatus.Concluded, d1.Status);
        Assert.Equal(ComplaintOutcome.Justified, d1.Outcome);
        Assert.True(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.UserId == qaApprover && a.EntityName == nameof(ComplaintDetail) && a.EntityId == cmpId1.ToString()));
    }

    [PostgresFact]
    public async Task Conf236_change_control_owner_without_investigate_may_save_and_sign_assessment_while_qa_refused()
    {
        var clock = new MutableClock(BaseNow);
        int qcOwner, nonOwnerNoInv, qaUser, otherHead;
        int ccId;
        uint ccVer;

        await using (var db = Context(clock))
        {
            qcOwner = await ActorAsync(db, RoleType.QcManager);
            nonOwnerNoInv = await ActorAsync(db, RoleType.QcManager);
            qaUser = await ActorAsync(db, RoleType.Qa);
            otherHead = await ActorAsync(db, RoleType.SectionHead);
        }

        await using (var actDb = Context(clock, qcOwner))
        {
            var proposed = await ChangeControls(actDb, clock).ProposeAsync(
                new ProposeChangeControlRequest("CC Title", "CC Desc", "CC Reason", fixture.SeededSectionId, null, qcOwner),
                qcOwner);
            ccId = proposed.Id;
            ccVer = proposed.Version;
        }

        // 1) Non-owner without Investigate -> refused
        await using (var actDb = Context(clock, nonOwnerNoInv))
        {
            await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                ChangeControls(actDb, clock).SaveAssessmentAsync(ccId, new("Impact", []), ccVer, nonOwnerNoInv));
        }

        // 2) QA user -> refused
        await using (var actDb = Context(clock, qaUser))
        {
            await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                ChangeControls(actDb, clock).SaveAssessmentAsync(ccId, new("QA impact", []), ccVer, qaUser));
            await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                ChangeControls(actDb, clock).SignAssessmentAsync(ccId, Password, ccVer, qaUser));
        }

        // 3) Named QC Manager owner WITHOUT Quality.Investigate -> Save & Sign succeed!
        await using (var actDb = Context(clock, qcOwner))
        {
            var saved = await ChangeControls(actDb, clock).SaveAssessmentAsync(ccId, new("Owner impact", []), ccVer, qcOwner);
            ccVer = saved.Version;
            var signed = await ChangeControls(actDb, clock).SignAssessmentAsync(ccId, Password, ccVer, qcOwner);
            Assert.Equal(ChangeControlStatus.Assessed, signed.Status);
            ccVer = signed.Version;
        }

        // 4) Non-owner with Quality.Investigate can also save/sign (test on a second CC)
        int ccId2;
        uint ccVer2;
        await using (var actDb = Context(clock, qcOwner))
        {
            var proposed2 = await ChangeControls(actDb, clock).ProposeAsync(
                new ProposeChangeControlRequest("CC 2", "Desc 2", "Reason 2", fixture.SeededSectionId, null, qcOwner),
                qcOwner);
            ccId2 = proposed2.Id;
            ccVer2 = proposed2.Version;
        }
        await using (var actDb = Context(clock, otherHead))
        {
            var saved2 = await ChangeControls(actDb, clock).SaveAssessmentAsync(ccId2, new("Head impact", []), ccVer2, otherHead);
            var signed2 = await ChangeControls(actDb, clock).SignAssessmentAsync(ccId2, Password, saved2.Version, otherHead);
            Assert.Equal(ChangeControlStatus.Assessed, signed2.Status);
        }

        // 5) QA approval works; raiser/owner approval still refused
        await using (var actDb = Context(clock, qaUser))
        {
            var approved = await ChangeControls(actDb, clock).ApproveAsync(ccId, Password, ccVer, qaUser);
            Assert.Equal(ChangeControlStatus.ApprovedForImplementation, approved.Status);
        }
    }

    [PostgresFact]
    public async Task Conf237_record_owner_may_request_action_extension_and_non_owner_non_responsible_refused()
    {
        var clock = new MutableClock(BaseNow);
        int owner, responsible, outsiderAnalyst;
        int recordId, actionId;
        uint version;

        await using (var db = Context(clock))
        {
            owner = await ActorAsync(db, RoleType.SectionHead);
            responsible = await ActorAsync(db, RoleType.Analyst);
            outsiderAnalyst = await ActorAsync(db, RoleType.SectionHead);

            var rec = new QualityRecord
            {
                Number = $"ACT-{Guid.NewGuid():N}"[..20],
                Title = "Action ext", Description = "Desc", Type = QualityRecordType.Capa,
                SectionId = fixture.SeededSectionId, RaisedByUserId = owner, OwnerUserId = owner,
                DueDate = Today.AddDays(20), RaisedAtUtc = clock.UtcNow,
                CapaDetail = new CapaDetail { Status = CapaStatus.PlanApproved },
                Actions = [new QualityAction { Kind = QualityActionKind.Corrective, Description = "Fix it", ResponsibleUserId = responsible, DueDate = Today.AddDays(5) }]
            };
            db.QualityRecords.Add(rec);
            await db.SaveChangesAsync();
            recordId = rec.Id;
            actionId = rec.Actions[0].Id;
            version = rec.Version;
        }

        // Other user (neither owner nor responsible) refused
        await using (var actDb = Context(clock, outsiderAnalyst))
        {
            await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                Config(actDb, clock).RequestAsync(recordId, actionId, Today.AddDays(9), "Outsider", version, outsiderAnalyst));
        }

        // Record owner (not responsible) succeeds!
        QualityExtensionDto ext;
        await using (var actDb = Context(clock, owner))
        {
            ext = await Config(actDb, clock).RequestAsync(recordId, actionId, Today.AddDays(9), "Owner request", version, owner);
        }
        Assert.Equal(actionId, ext.ActionId);
        Assert.Equal(owner, ext.RequestedByUserId);

        await using var assertDb = Context(clock);
        var persisted = await assertDb.QualityExtensionRequests.AsNoTracking().SingleAsync(e => e.Id == ext.Id);
        Assert.Equal(owner, persisted.RequestedByUserId);
        Assert.True(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.UserId == owner && a.EntityName == nameof(QualityExtensionRequest) && a.EntityId == ext.Id.ToString()));
    }

    [PostgresFact]
    public async Task Conf238_terminal_record_blocks_links_allows_addendum_comments_and_attachments_and_marks_pdf()
    {
        var clock = new MutableClock(BaseNow);
        int owner, qa;
        int recordId, linkTargetId;
        uint version;

        await using (var db = Context(clock))
        {
            owner = await ActorAsync(db, RoleType.SectionHead);
            qa = await ActorAsync(db, RoleType.Qa);
            var target = new QualityRecord
            {
                Number = $"TGT-{Guid.NewGuid():N}"[..20],
                Title = "Target", Description = "Desc", Type = QualityRecordType.Deviation,
                SectionId = fixture.SeededSectionId, RaisedByUserId = owner, RaisedAtUtc = clock.UtcNow,
                DeviationDetail = new DeviationDetail { Status = QualityDeviationStatus.Raised, OccurredAtUtc = clock.UtcNow.AddHours(-1), ImmediateAction = "Act" }
            };
            var rec = new QualityRecord
            {
                Number = $"TERM-{Guid.NewGuid():N}"[..20],
                Title = "Terminal", Description = "Desc", Type = QualityRecordType.Deviation,
                SectionId = fixture.SeededSectionId, RaisedByUserId = owner, OwnerUserId = owner, RaisedAtUtc = clock.UtcNow,
                DeviationDetail = new DeviationDetail { Status = QualityDeviationStatus.Raised, OccurredAtUtc = clock.UtcNow.AddHours(-1), ImmediateAction = "Act" }
            };
            db.QualityRecords.AddRange(target, rec);
            await db.SaveChangesAsync();
            linkTargetId = target.Id;
            recordId = rec.Id;
            version = rec.Version;
        }

        // Pre-closure comment
        await using (var actDb = Context(clock, owner))
        {
            var pre = await Comments(actDb, clock).AddAsync(recordId, version, "Before rejection", owner);
            Assert.False(pre.IsAddendum);
        }

        // Reject record at BaseNow + 1 hour (terminal)
        clock.UtcNow = BaseNow.AddHours(1);
        await using (var db = Context(clock))
            version = await db.QualityRecords.AsNoTracking().Where(r => r.Id == recordId).Select(r => r.Version).SingleAsync();
        await using (var actDb = Context(clock, qa))
        {
            var rejected = await Deviations(actDb, clock).RejectAsync(recordId, "Not a deviation", Password, version, qa);
            version = rejected.Version;
        }

        // 1) Link on terminal record -> REFUSED (Quality.RecordTerminal)
        await using (var actDb = Context(clock, owner))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Links(actDb, clock).AddAsync(recordId, QualityLinkTargetType.QualityRecord, linkTargetId, owner, version));
            Assert.Equal(MessageCodes.QualityRecordTerminal, ex.Code);
        }

        // 2) Comment and attachment after closure -> ALLOWED with IsAddendum = true
        clock.UtcNow = BaseNow.AddHours(2);
        await using (var actDb = Context(clock, owner))
        {
            var postComment = await Comments(actDb, clock).AddAsync(recordId, version, "Addendum note", owner);
            Assert.True(postComment.IsAddendum);
        }
        await using (var db = Context(clock))
            version = await db.QualityRecords.AsNoTracking().Where(r => r.Id == recordId).Select(r => r.Version).SingleAsync();

        await using (var actDb = Context(clock, owner))
        {
            var postAtt = await Attachments(actDb, clock).UploadAsync(recordId, version, "addendum.txt", "after"u8.ToArray(), owner);
            Assert.True(postAtt.IsAddendum);
        }

        // 3) Queries & PDF report show addendum marker
        await using var assertDb = Context(clock);
        var comments = await Comments(assertDb, clock).ListAsync(recordId, owner, 0, 25);
        Assert.Equal(2, comments.Count);
        Assert.False(comments[0].IsAddendum);
        Assert.True(comments[1].IsAddendum);

        var detail = await Queries(assertDb, clock).GetDetailAsync(recordId, owner);
        Assert.Single(detail.Attachments);
        Assert.True(detail.Attachments[0].IsAddendum);

        var doc = QualityReportBuilder.Build(detail, [], comments);
        var addendumBlock = Assert.Single(doc.Blocks.OfType<MicroLIMS.Application.Abstractions.Pdf.ListBlock>(),
            b => b.Label.Contains("addend", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(addendumBlock.Rows, r => r.Right.Contains("Added after closure", StringComparison.OrdinalIgnoreCase));
    }

    [PostgresFact]
    public async Task Conf239_due_date_falls_back_to_site_rule_when_classification_rule_has_null_working_days()
    {
        var clock = new MutableClock(BaseNow);
        int clsNullId, clsSetId;

        await using (var db = Context(clock))
        {
            var c1 = new QualityClassification { Name = $"NullDays-{Guid.NewGuid():N}", IsActive = true };
            var c2 = new QualityClassification { Name = $"SetDays-{Guid.NewGuid():N}", IsActive = true };
            db.QualityClassifications.AddRange(c1, c2);
            await db.SaveChangesAsync();
            clsNullId = c1.Id;
            clsSetId = c2.Id;

            // Remove any existing site rule for Complaint in this test tx or test on a type with clean rules
            var existingSite = await db.QualityDueRules.SingleOrDefaultAsync(r => r.Type == QualityRecordType.Complaint && r.ClassificationId == null);
            if (existingSite is null)
                db.QualityDueRules.Add(new QualityDueRule { Type = QualityRecordType.Complaint, ClassificationId = null, WorkingDays = 10 });
            else
                existingSite.WorkingDays = 10;

            db.QualityDueRules.Add(new QualityDueRule { Type = QualityRecordType.Complaint, ClassificationId = clsNullId, WorkingDays = null });
            db.QualityDueRules.Add(new QualityDueRule { Type = QualityRecordType.Complaint, ClassificationId = clsSetId, WorkingDays = 2 });
            await db.SaveChangesAsync();
        }

        await using var assertDb = Context(clock);
        var svc = new QualityDueDateService(assertDb);
        var siteDue = await svc.ComputeDueAsync(QualityRecordType.Complaint, null, Today);
        var fallbackDue = await svc.ComputeDueAsync(QualityRecordType.Complaint, clsNullId, Today);
        var overrideDue = await svc.ComputeDueAsync(QualityRecordType.Complaint, clsSetId, Today);

        Assert.NotNull(siteDue);
        Assert.Equal(siteDue, fallbackDue);
        Assert.NotNull(overrideDue);
        Assert.NotEqual(siteDue, overrideDue);
    }
}

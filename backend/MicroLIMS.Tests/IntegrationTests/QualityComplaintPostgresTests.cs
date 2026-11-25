using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class QualityComplaintPostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "Complaint-test-password";
    private static readonly DateTime BaseNow = new(2086, 9, 15, 12, 0, 0, DateTimeKind.Utc);
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

    private static ComplaintService Service(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, clock),
            new ElectronicSignatureService(db, clock), clock);
        return new(db, core, permissions, scope, new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(db));
    }

    private static QualityLinkService Links(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, clock),
            new ElectronicSignatureService(db, clock), clock);
        return new(db, core, permissions, scope, clock);
    }

    private static ChangeControlService ChangeControls(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, clock),
            new ElectronicSignatureService(db, clock), clock);
        var config = new QualityConfigService(db, core, permissions, scope, clock);
        return new(db, core, permissions, scope, new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(db), config);
    }

    private async Task<int> ActorAsync(MicroLimsDbContext db, RoleType role, bool member = true)
    {
        var key = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = $"Complaint {role} {key[..6]}",
            Username = key,
            IsActive = true,
            Role = new Role { Name = key, Type = role, IsActive = true },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var codes = new List<string> { PermissionConstants.QualityView, PermissionConstants.QualityRaise };
        if (role == RoleType.Qa) codes.Add(PermissionConstants.QualityApprove);
        if (role == RoleType.SectionHead)
            codes.AddRange([PermissionConstants.QualityInvestigate, PermissionConstants.QualityLabConclude]);

        var ids = await db.Permissions.AsNoTracking().Where(p => codes.Contains(p.Code)).Select(p => p.Id).ToListAsync();
        db.RolePermissions.AddRange(ids.Select(id => new RolePermission { RoleId = user.RoleId, PermissionId = id }));
        var membership = await db.DocumentSections.AsNoTracking()
            .Where(s => s.IsActive && (member ? s.Id == fixture.SeededSectionId : s.Id != fixture.SeededSectionId))
            .OrderBy(s => s.Id).Select(s => new { s.Id, s.DepartmentId }).FirstAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id, DepartmentId = membership.DepartmentId, SectionId = membership.Id
        });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private sealed record World(int QaReceiver, int QaApprover, int Owner, int Head, int Analyst, int Outside,
        int ClassificationId, int ItemId, int SampleId, string SampleNumber, int OtherSectionSampleId,
        string OtherSectionSampleNumber);

    private async Task<World> ArrangeAsync(MutableClock clock)
    {
        await using var db = Context(clock);
        var qaReceiver = await ActorAsync(db, RoleType.Qa);
        var qaApprover = await ActorAsync(db, RoleType.Qa);
        var owner = await ActorAsync(db, RoleType.SectionHead);
        var head = await ActorAsync(db, RoleType.SectionHead);
        var analyst = await ActorAsync(db, RoleType.Analyst);
        var outside = await ActorAsync(db, RoleType.SectionHead, member: false);

        var classification = new QualityClassification { Name = $"Cls-{Guid.NewGuid():N}", IsActive = true };
        db.QualityClassifications.Add(classification);

        var item = new Item { Code = $"ITM-{Guid.NewGuid():N}"[..16], Name = "Complaint Product", Category = SampleCategory.FinishedProduct, IsActive = true };
        db.Items.Add(item);

        var cause = await db.CausesOfTesting.AsNoTracking().FirstOrDefaultAsync(c => c.Name == DbSeeder.ComplaintCause)
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = DbSeeder.ComplaintCause, IsActive = true }).Entity;
        await db.SaveChangesAsync();

        var otherSectionId = await db.DocumentSections.AsNoTracking()
            .Where(s => s.IsActive && s.Id != fixture.SeededSectionId)
            .OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

        var sampleNumber = $"SMP-{Guid.NewGuid():N}"[..16];
        var sample = new Sample
        {
            ReferenceNumber = sampleNumber,
            Category = SampleCategory.FinishedProduct,
            ItemId = item.Id,
            CauseOfTestingId = cause.Id,
            ControlNumber = "LOT-CMP-1",
            SampledBy = "QA",
            ReceivedByUserId = qaReceiver,
            ReceivedAt = clock.UtcNow,
            Status = SampleStatus.Received,
            LabSectionId = fixture.SeededSectionId
        };
        var otherSampleNumber = $"SMP-{Guid.NewGuid():N}"[..16];
        var otherSample = new Sample
        {
            ReferenceNumber = otherSampleNumber,
            Category = SampleCategory.FinishedProduct,
            ItemId = item.Id,
            CauseOfTestingId = cause.Id,
            ControlNumber = "LOT-CMP-2",
            SampledBy = "QA",
            ReceivedByUserId = qaReceiver,
            ReceivedAt = clock.UtcNow,
            Status = SampleStatus.Received,
            LabSectionId = otherSectionId
        };
        db.Samples.AddRange(sample, otherSample);
        await db.SaveChangesAsync();

        return new(qaReceiver, qaApprover, owner, head, analyst, outside, classification.Id, item.Id,
            sample.Id, sampleNumber, otherSample.Id, otherSampleNumber);
    }

    private async Task<ComplaintDto> ActAsync(MutableClock clock, int actor, Func<ComplaintService, Task<ComplaintDto>> action)
    {
        await using var db = Context(clock, actor);
        return await action(Service(db, clock));
    }

    private Task<ComplaintDto> ReceiveAsync(MutableClock clock, World w, int? actor = null, int? sectionId = null) =>
        ActAsync(clock, actor ?? w.QaReceiver, s => s.ReceiveAsync(
            new("Customer complaint on tablet color", "Customer", Today.AddDays(-1), "EXT-2086-01",
                w.ItemId, "LOT-CMP-1", "Customer reported discoloration in two blisters.",
                sectionId ?? fixture.SeededSectionId),
            actor ?? w.QaReceiver));

    private Task<ComplaintDto> RequestInvestigationAsync(MutableClock clock, World w, ComplaintDto c, int? actor = null,
        int? ownerUserId = null, DateOnly? dueDate = null) =>
        ActAsync(clock, actor ?? w.QaReceiver, s => s.RequestInvestigationAsync(
            c.Id,
            new(w.ClassificationId, ownerUserId ?? w.Owner, dueDate ?? Today.AddDays(5),
                "Test retained sample for appearance and assay."),
            c.Version, actor ?? w.QaReceiver));

    private Task<ComplaintDto> SaveInvestigationAsync(MutableClock clock, World w, ComplaintDto c, int? actor = null,
        IReadOnlyList<int>? sampleIds = null) =>
        ActAsync(clock, actor ?? w.Owner, s => s.SaveInvestigationAsync(
            c.Id,
            new("Retained sample tested; all results within specification.", sampleIds ?? [w.SampleId]),
            c.Version, actor ?? w.Owner));

    private Task<ComplaintDto> SignLabReportAsync(MutableClock clock, World w, ComplaintDto c, int? actor = null,
        string? password = null) =>
        ActAsync(clock, actor ?? w.Head, s => s.SignLabReportAsync(
            c.Id, password ?? Password, c.Version, actor ?? w.Head));

    private Task<ComplaintDto> ConcludeAsync(MutableClock clock, World w, ComplaintDto c, int? actor = null,
        string? password = null, ComplaintOutcome outcome = ComplaintOutcome.NotJustified) =>
        ActAsync(clock, actor ?? w.QaApprover, s => s.ConcludeAsync(
            c.Id, new(outcome, "Confirmed batch meets all release and retained specifications.", password ?? Password),
            c.Version, actor ?? w.QaApprover));

    private Task<ComplaintDto> CloseAsync(MutableClock clock, World w, ComplaintDto c, int? actor = null,
        string? password = null) =>
        ActAsync(clock, actor ?? w.QaApprover, s => s.CloseAsync(
            c.Id, password ?? Password, c.Version, actor ?? w.QaApprover));

    private static async Task AssertAuditAsync(MicroLimsDbContext db, string entity, int recordId, int actor,
        string? action = null)
    {
        var idText = recordId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var logs = await db.AuditLogs.AsNoTracking()
            .Where(l => l.EntityName == entity && l.EntityId == idText && l.UserId == actor &&
                (action == null || l.Action == action))
            .OrderByDescending(l => l.Id)
            .ToListAsync();
        Assert.NotEmpty(logs);
        Assert.All(logs, l => Assert.False(string.IsNullOrWhiteSpace(l.NewValue)));
    }

    [PostgresFact]
    public async Task Full_lifecycle_persists_state_signatures_and_audit_triad()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);

        // 1. Receive (QA)
        var received = await ReceiveAsync(clock, w);
        Assert.StartsWith($"CMP-{BaseNow:yyyy}-", received.Record.Number);
        Assert.Equal(ComplaintStatus.Received, received.Status);
        Assert.Equal("Customer", received.Source);
        Assert.Equal("EXT-2086-01", received.ExternalReference);
        Assert.Equal(w.ItemId, received.ItemId);
        Assert.Equal("LOT-CMP-1", received.LotText);
        Assert.Equal(w.QaReceiver, received.Record.RaisedByUserId);

        // 2. Request investigation (QA)
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var requested = await RequestInvestigationAsync(clock, w, received);
        Assert.Equal(ComplaintStatus.Investigation, requested.Status);
        Assert.Equal(w.ClassificationId, requested.ClassificationId);
        Assert.Equal(w.Owner, requested.Record.OwnerUserId);
        Assert.Equal(Today.AddDays(5), requested.DueDate);
        Assert.Equal("Test retained sample for appearance and assay.", requested.WhatToTest);

        // 3. Save investigation + sample link (Owner)
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var investigated = await SaveInvestigationAsync(clock, w, requested);
        Assert.Equal("Retained sample tested; all results within specification.", investigated.Findings);

        // 4. Sign lab report (Section Head != owner)
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var reported = await SignLabReportAsync(clock, w, investigated);
        Assert.Equal(ComplaintStatus.LabReported, reported.Status);

        await using (var assertDb = Context(clock))
        {
            var detail = await assertDb.ComplaintDetails.AsNoTracking().SingleAsync(d => d.RecordId == reported.Id);
            Assert.Equal(ComplaintStatus.LabReported, detail.Status);

            var step = await assertDb.QualityRecordSteps.AsNoTracking()
                .SingleAsync(s => s.RecordId == reported.Id && s.Kind == QualityStepKind.ComplaintLabReport);
            Assert.Equal(w.Head, step.SignedByUserId);
            var sig = await assertDb.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == step.SignatureId);
            Assert.Equal(SignatureMeaning.QualityLabConclusion, sig.MeaningOfSignature);

            var snapshot = JsonDocument.Parse(step.SnapshotJson).RootElement;
            Assert.Equal("Retained sample tested; all results within specification.", snapshot.GetProperty("Data").GetProperty("Findings").GetString());
            Assert.Single(snapshot.GetProperty("Data").GetProperty("Links").EnumerateArray());

            await AssertAuditAsync(assertDb, nameof(ComplaintDetail), reported.Id, w.Head, "Update");
        }

        // 5. Conclude (different QA)
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var concluded = await ConcludeAsync(clock, w, reported);
        Assert.Equal(ComplaintStatus.Concluded, concluded.Status);
        Assert.Equal(ComplaintOutcome.NotJustified, concluded.Outcome);

        await using (var assertDb = Context(clock))
        {
            var step = await assertDb.QualityRecordSteps.AsNoTracking()
                .SingleAsync(s => s.RecordId == concluded.Id && s.Kind == QualityStepKind.ComplaintConcluded);
            Assert.Equal(w.QaApprover, step.SignedByUserId);
            var sig = await assertDb.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == step.SignatureId);
            Assert.Equal(SignatureMeaning.ComplaintConcluded, sig.MeaningOfSignature);
            await AssertAuditAsync(assertDb, nameof(ComplaintDetail), concluded.Id, w.QaApprover, "Update");
        }

        // 6. Close (different QA)
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var closed = await CloseAsync(clock, w, concluded);
        Assert.Equal(ComplaintStatus.Closed, closed.Status);

        await using (var assertDb = Context(clock))
        {
            var detail = await assertDb.ComplaintDetails.AsNoTracking().SingleAsync(d => d.RecordId == closed.Id);
            Assert.Equal(ComplaintStatus.Closed, detail.Status);

            var step = await assertDb.QualityRecordSteps.AsNoTracking()
                .SingleAsync(s => s.RecordId == closed.Id && s.Kind == QualityStepKind.ComplaintClosed);
            Assert.Equal(w.QaApprover, step.SignedByUserId);
            var sig = await assertDb.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == step.SignatureId);
            Assert.Equal(SignatureMeaning.QualityClosed, sig.MeaningOfSignature);

            await AssertAuditAsync(assertDb, nameof(ComplaintDetail), closed.Id, w.QaApprover, "Update");
        }
    }

    [PostgresFact]
    public async Task Laboratory_user_cannot_receive_complaint()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);

        await Assert.ThrowsAsync<CodedAccessDeniedException>(() => ReceiveAsync(clock, w, actor: w.Head));
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() => ReceiveAsync(clock, w, actor: w.Analyst));
    }

    [PostgresFact]
    public async Task Owner_cannot_sign_lab_report()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var received = await ReceiveAsync(clock, w);
        var requested = await RequestInvestigationAsync(clock, w, received);
        var investigated = await SaveInvestigationAsync(clock, w, requested);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SignLabReportAsync(clock, w, investigated, actor: w.Owner));
        Assert.Equal(MessageCodes.ComplaintOwnerLabReport, ex.Code);
        Assert.Equal("The owner cannot sign the laboratory conclusion.", ex.Message);

        await using var assertDb = Context(clock);
        Assert.Equal(ComplaintStatus.Investigation,
            (await assertDb.ComplaintDetails.AsNoTracking().SingleAsync(d => d.RecordId == investigated.Id)).Status);
    }

    [PostgresFact]
    public async Task Conclusion_without_lab_report_is_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var received = await ReceiveAsync(clock, w);
        var requested = await RequestInvestigationAsync(clock, w, received);
        var investigated = await SaveInvestigationAsync(clock, w, requested);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => ConcludeAsync(clock, w, investigated));
        Assert.Equal(MessageCodes.ComplaintWrongState, ex.Code);
    }

    [PostgresFact]
    public async Task Linked_sample_not_readable_by_viewer_is_shown_as_number_only()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);

        // Site-wide complaint (SectionId = null) so both QA and Outside section user can read the complaint itself,
        // while Outside user cannot read a sample belonging to SeededSectionId.
        var received = await ActAsync(clock, w.QaReceiver, s => s.ReceiveAsync(
            new("Site-wide complaint", "Distributor", Today, null, w.ItemId, "LOT-CMP-1",
                "Packaging tear reported.", null), w.QaReceiver));

        await using (var linkDb = Context(clock, w.QaReceiver))
        {
            await Links(linkDb, clock).AddAsync(received.Id, QualityLinkTargetType.Sample, w.SampleId,
                w.QaReceiver, received.Version);
        }

        await using var viewDb = Context(clock);
        var linksForOutside = await Links(viewDb, clock).ResolveAsync(received.Id, w.Outside);
        var restricted = Assert.Single(linksForOutside);
        Assert.True(restricted.IsRestricted);
        Assert.Equal(w.SampleNumber, restricted.Number);
        Assert.Null(restricted.Label);
        Assert.Null(restricted.Route);

        var linksForMember = await Links(viewDb, clock).ResolveAsync(received.Id, w.Head);
        var readable = Assert.Single(linksForMember);
        Assert.False(readable.IsRestricted);
        Assert.Equal(w.SampleNumber, readable.Number);
        Assert.NotNull(readable.Label);
        Assert.NotNull(readable.Route);
    }

    [PostgresFact]
    public async Task Raiser_and_owner_cannot_conclude_or_close()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var received = await ReceiveAsync(clock, w);
        var requested = await RequestInvestigationAsync(clock, w, received);
        var investigated = await SaveInvestigationAsync(clock, w, requested);
        var reported = await SignLabReportAsync(clock, w, investigated);

        var exConclude = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ConcludeAsync(clock, w, reported, actor: w.QaReceiver));
        Assert.Equal(MessageCodes.QualityRaiserOrOwner, exConclude.Code);

        var concluded = await ConcludeAsync(clock, w, reported);
        var exClose = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CloseAsync(clock, w, concluded, actor: w.QaReceiver));
        Assert.Equal(MessageCodes.QualityRaiserOrOwner, exClose.Code);
    }

    [PostgresFact]
    public async Task Wrong_password_and_stale_version_and_other_section_are_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var received = await ReceiveAsync(clock, w);
        var requested = await RequestInvestigationAsync(clock, w, received);
        var investigated = await SaveInvestigationAsync(clock, w, requested);

        // Other-section user refused on investigation save & lab report
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SaveInvestigationAsync(clock, w, investigated, actor: w.Outside));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SignLabReportAsync(clock, w, investigated, actor: w.Outside));

        // Stale version refused
        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
            SignLabReportAsync(clock, w, requested));

        // Wrong password changes nothing
        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            SignLabReportAsync(clock, w, investigated, password: "wrong-password"));

        await using var assertDb = Context(clock);
        var persisted = await assertDb.ComplaintDetails.AsNoTracking().SingleAsync(d => d.RecordId == investigated.Id);
        Assert.Equal(ComplaintStatus.Investigation, persisted.Status);
        Assert.Empty(await assertDb.QualityRecordSteps.AsNoTracking().Where(s => s.RecordId == investigated.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task Cancel_is_allowed_only_while_open()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);

        var received = await ReceiveAsync(clock, w);
        var cancelled = await ActAsync(clock, w.QaApprover, s =>
            s.CancelAsync(received.Id, "Duplicate report", Password, received.Version, w.QaApprover));
        Assert.Equal(ComplaintStatus.Cancelled, cancelled.Status);
        Assert.True(cancelled.Record.Id == received.Id);

        await using (var assertDb = Context(clock))
        {
            var r = await assertDb.QualityRecords.AsNoTracking().SingleAsync(x => x.Id == received.Id);
            Assert.True(r.IsCancelled);
        }

        // Cannot cancel an already cancelled complaint
        var exDoubleCancel = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.QaApprover, s =>
            s.CancelAsync(cancelled.Id, "Again", Password, cancelled.Version, w.QaApprover)));
        Assert.Equal(MessageCodes.ComplaintWrongState, exDoubleCancel.Code);

        // Cannot cancel a closed complaint
        var open = await ReceiveAsync(clock, w);
        var req = await RequestInvestigationAsync(clock, w, open);
        var inv = await SaveInvestigationAsync(clock, w, req);
        var rep = await SignLabReportAsync(clock, w, inv);
        var con = await ConcludeAsync(clock, w, rep);
        var closed = await CloseAsync(clock, w, con);

        var exClosed = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.QaApprover, s =>
            s.CancelAsync(closed.Id, "Too late", Password, closed.Version, w.QaApprover)));
        Assert.Equal(MessageCodes.ComplaintFinalCancellation, exClosed.Code);
    }

    [PostgresFact]
    public async Task Complaint_cause_of_testing_is_present_after_migration_and_seed_once()
    {
        var admin = PostgresTestConfiguration.AdminConnectionString!;
        var name = PostgresTestConfiguration.NewTestDatabaseName();
        var connectionString = PostgresTestConfiguration.BuildTestConnectionString(admin, name);

        await ExecuteSqlAsync(admin, $"CREATE DATABASE \"{name}\";");
        try
        {
            await using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
                .UseNpgsql(connectionString).Options);
            await db.Database.MigrateAsync();
            Assert.Equal(1, await db.CausesOfTesting.CountAsync(c => c.Name == DbSeeder.ComplaintCause));

            DbSeeder.Seed(db);
            DbSeeder.Seed(db);
            Assert.Equal(1, await db.CausesOfTesting.CountAsync(c => c.Name == DbSeeder.ComplaintCause));
            var cause = await db.CausesOfTesting.AsNoTracking().SingleAsync(c => c.Name == DbSeeder.ComplaintCause);
            Assert.True(cause.IsActive);
            Assert.False(cause.RequiresTestSelection);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            PostgresTestConfiguration.EnsureSafeToDrop(name, admin);
            await ExecuteSqlAsync(admin, $"DROP DATABASE IF EXISTS \"{name}\";");
        }
    }

    [PostgresFact]
    public async Task Change_control_task_fields_are_protected_by_database_trigger_after_approval_or_cancellation()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);

        int ccId;
        int otherCcId;
        int taskId;
        int cancelledTaskId;

        await using (var ccDb = Context(clock, w.Head))
        {
            var svc = ChangeControls(ccDb, clock);
            var proposed = await svc.ProposeAsync(new("CC for trigger test", "Desc", "Reason",
                fixture.SeededSectionId, null, w.Owner), w.Head);
            var saved = await svc.SaveAssessmentAsync(proposed.Id,
                new("Impact", [new("Execute task", w.Analyst, Today.AddDays(5))], null),
                proposed.Version, w.Head);
            var assessed = await svc.SignAssessmentAsync(saved.Id, Password, saved.Version, w.Head);

            await using var qaDb = Context(clock, w.QaApprover);
            var approved = await ChangeControls(qaDb, clock).ApproveAsync(assessed.Id, Password, assessed.Version, w.QaApprover);
            ccId = approved.Id;
            taskId = Assert.Single(approved.Tasks).Id;

            var other = await svc.ProposeAsync(new("Other CC", "Desc", "Reason",
                fixture.SeededSectionId, null, w.Owner), w.Head);
            otherCcId = other.Id;

            var ccCancel = await svc.ProposeAsync(new("Cancelled CC", "Desc", "Reason",
                fixture.SeededSectionId, null, w.Owner), w.Head);
            var ccCancelSaved = await svc.SaveAssessmentAsync(ccCancel.Id,
                new("Impact", [new("Cancel task", w.Analyst, Today.AddDays(5))], null),
                ccCancel.Version, w.Head);
            cancelledTaskId = Assert.Single(ccCancelSaved.Tasks).Id;
            await ChangeControls(qaDb, clock).CancelAsync(ccCancelSaved.Id, "No longer needed", Password,
                ccCancelSaved.Version, w.QaApprover);
        }

        // Planned fields cannot be modified once ApprovedForImplementation
        await using (var afterDb = Context(clock))
        {
            foreach (var sql in new FormattableString[]
            {
                $"UPDATE \"QualityActions\" SET \"RecordId\" = {otherCcId} WHERE \"Id\" = {taskId}",
                $"UPDATE \"QualityActions\" SET \"Kind\" = 0 WHERE \"Id\" = {taskId}",
                $"UPDATE \"QualityActions\" SET \"Description\" = 'Tampered' WHERE \"Id\" = {taskId}",
                $"UPDATE \"QualityActions\" SET \"ResponsibleUserId\" = {w.Owner} WHERE \"Id\" = {taskId}",
                $"UPDATE \"QualityActions\" SET \"DueDate\" = {Today.AddDays(30)} WHERE \"Id\" = {taskId}"
            })
            {
                var ex = await Assert.ThrowsAsync<PostgresException>(() => afterDb.Database.ExecuteSqlInterpolatedAsync(sql));
                Assert.Equal("P0001", ex.SqlState);
                Assert.Contains("Change control task", ex.Message);
                Assert.Contains("planned fields cannot be modified", ex.Message);
            }

            // Cancelled change control also locks planned fields
            var cancelEx = await Assert.ThrowsAsync<PostgresException>(() => afterDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"QualityActions\" SET \"Description\" = 'Tampered cancelled' WHERE \"Id\" = {cancelledTaskId}"));
            Assert.Equal("P0001", cancelEx.SqlState);
            Assert.Contains("Change control task", cancelEx.Message);
            Assert.Contains("planned fields cannot be modified", cancelEx.Message);
        }

        // Completion NULL -> value once is allowed
        await using (var completeDb = Context(clock, w.Analyst))
        {
            var cc = await completeDb.QualityRecords.AsNoTracking().SingleAsync(r => r.Id == ccId);
            await ChangeControls(completeDb, clock).CompleteTaskAsync(taskId, new("Completed evidence"), cc.Version, w.Analyst);
        }

        // Completion fields cannot be modified or cleared once recorded
        await using (var tamperDb = Context(clock))
        {
            var modEx = await Assert.ThrowsAsync<PostgresException>(() => tamperDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"QualityActions\" SET \"Evidence\" = 'Tampered evidence' WHERE \"Id\" = {taskId}"));
            Assert.Equal("P0001", modEx.SqlState);
            Assert.Contains("Change control task", modEx.Message);
            Assert.Contains("completion fields cannot be modified or cleared", modEx.Message);

            var clearEx = await Assert.ThrowsAsync<PostgresException>(() => tamperDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"QualityActions\" SET \"DoneAtUtc\" = NULL WHERE \"Id\" = {taskId}"));
            Assert.Equal("P0001", clearEx.SqlState);
            Assert.Contains("Change control task", clearEx.Message);
            Assert.Contains("completion fields cannot be modified or cleared", clearEx.Message);
        }
    }

    [PostgresFact]
    public async Task Unauthorized_receive_with_invalid_input_returns_access_denied()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);

        await Assert.ThrowsAsync<CodedAccessDeniedException>(() => ActAsync(clock, w.Analyst, s => s.ReceiveAsync(
            new("", "", Today.AddDays(10), null, -999, null, "", fixture.SeededSectionId),
            w.Analyst)));
    }

    [PostgresFact]
    public async Task Complaint_cause_migration_is_idempotent_when_row_exists_and_down_preserves_referenced_cause()
    {
        var admin = PostgresTestConfiguration.AdminConnectionString!;
        var name = PostgresTestConfiguration.NewTestDatabaseName();
        var connectionString = PostgresTestConfiguration.BuildTestConnectionString(admin, name);

        await ExecuteSqlAsync(admin, $"CREATE DATABASE \"{name}\";");
        try
        {
            await using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
                .UseNpgsql(connectionString).Options);

            // Migrate to the migration immediately before AddComplaintCauseOfTesting,
            // pre-insert "Complaint" manually, then apply AddComplaintCauseOfTesting.
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261125191721_ProtectApprovedCapaActions");
            db.CausesOfTesting.Add(new CauseOfTesting { Name = DbSeeder.ComplaintCause, IsActive = true });
            await db.SaveChangesAsync();

            await db.Database.MigrateAsync();
            Assert.Equal(1, await db.CausesOfTesting.CountAsync(c => c.Name == DbSeeder.ComplaintCause));

            // Down() cleanup must not delete the cause row when referenced by a Sample.
            DbSeeder.Seed(db, "Init-Admin-123!");
            var cause = await db.CausesOfTesting.AsNoTracking().SingleAsync(c => c.Name == DbSeeder.ComplaintCause);
            var section = await db.DocumentSections.AsNoTracking().FirstAsync();
            var user = await db.Users.AsNoTracking().FirstAsync();
            db.Samples.Add(new Sample
            {
                ReferenceNumber = $"SMP-{Guid.NewGuid():N}"[..16],
                Category = SampleCategory.FinishedProduct,
                CauseOfTestingId = cause.Id,
                ControlNumber = "LOT-DOWN-1",
                SampledBy = "QA",
                ReceivedByUserId = user.Id,
                ReceivedAt = BaseNow,
                Status = SampleStatus.Received,
                LabSectionId = section.Id
            });
            await db.SaveChangesAsync();

            await db.Database.ExecuteSqlRawAsync(AddComplaintCauseOfTesting.DeleteComplaintCauseSql);
            Assert.Equal(1, await db.CausesOfTesting.CountAsync(c => c.Name == DbSeeder.ComplaintCause));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            PostgresTestConfiguration.EnsureSafeToDrop(name, admin);
            await ExecuteSqlAsync(admin, $"DROP DATABASE IF EXISTS \"{name}\";");
        }
    }

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}

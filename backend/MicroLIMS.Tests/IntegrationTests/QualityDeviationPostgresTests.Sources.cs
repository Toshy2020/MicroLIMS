using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

public partial class QualityDeviationPostgresTests
{
    private async Task<OosDeviationRequest> OosRequestAsync(World w)
    {
        DocumentSection section;
        await using (var db = Context())
            section = await db.DocumentSections.AsNoTracking().SingleAsync(s => s.Id == fixture.SeededSectionId);
        var sample = await new ActionGuardScenarios(fixture).UnderApprovalAsync(section, w.Head, analystId: w.Raiser);
        await using var arrange = Context(w.Qa);
        var investigation = new OosInvestigation
        {
            Number = $"OOS-{Guid.NewGuid():N}"[..20], SampleId = sample.Sample.Id, SectionId = section.Id,
            Status = OosInvestigationStatus.PhaseOne, OpenedAtUtc = Now, OpenedByUserId = w.Head, TemplateSnapshotJson = "{}"
        };
        arrange.OosInvestigations.Add(investigation);
        await arrange.SaveChangesAsync();
        var request = new OosDeviationRequest
        {
            InvestigationId = investigation.Id, Reason = "Investigate the failing result", RequestedByUserId = w.Qa,
            RequestedAtUtc = Now, Status = OosDeviationRequestStatus.Open
        };
        arrange.OosDeviationRequests.Add(request);
        arrange.OosInvestigationResults.Add(new OosInvestigationResult
        {
            InvestigationId = investigation.Id, TestOrderId = Assert.Single(sample.Orders).Id,
            ResultSummaryAtOpening = "Out of specification", AddedAtUtc = Now
        });
        await arrange.SaveChangesAsync();
        return request;
    }

    [PostgresFact]
    public async Task Deviation_from_a_result_cannot_be_owned_by_the_analyst()
    {
        var w = await ArrangeAsync();
        var request = await OosRequestAsync(w);
        var raised = await ActAsync(w.Qa, s => s.RaiseFromOosRequestAsync(request.Id, request.Version, w.Qa));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => AssessAsync(w, raised, w.Raiser));
        Assert.Equal("The analyst who produced the result cannot own this deviation.", error.Message);
        await using var db = Context();
        Assert.Null(await db.QualityRecords.AsNoTracking().Where(r => r.Id == raised.Id).Select(r => r.OwnerUserId).SingleAsync());
    }

    [PostgresFact]
    public async Task OOS_request_and_deviation_commit_and_roll_back_together()
    {
        var w = await ArrangeAsync();
        var request = await OosRequestAsync(w);
        await using (var act = fixture.CreateDbContext(new FailWhenAddressing()))
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() =>
                Service(act).RaiseFromOosRequestAsync(request.Id, request.Version, w.Qa));
            Assert.Contains(FailWhenAddressing.Message, error.ToString());
        }
        await using (var check = Context())
        {
            var source = await check.OosDeviationRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id);
            Assert.Equal(OosDeviationRequestStatus.Open, source.Status);
            Assert.Equal(request.Version, source.Version);
            Assert.False(await check.QualityRecordLinks.AsNoTracking().AnyAsync(l =>
                l.TargetType == QualityLinkTargetType.OosInvestigation && l.TargetId == request.InvestigationId));
            Assert.False(await check.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == w.Qa
                && a.EntityName == nameof(QualityRecord) && a.Action == "Create"));
            Assert.False(await check.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == w.Qa
                && a.EntityName == nameof(OosDeviationRequest) && a.Action == "Update"));
        }
        var raised = await ActAsync(w.Qa, s => s.RaiseFromOosRequestAsync(request.Id, request.Version, w.Qa));
        var stale = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ActAsync(w.Qa, s => s.RaiseFromOosRequestAsync(request.Id, request.Version, w.Qa)));
        Assert.Equal(MicroLIMS.Shared.Messages.MessageCodes.OosVersionConflict, stale.Code);
        await using var db = Context();
        var addressed = await db.OosDeviationRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id);
        Assert.Equal(OosDeviationRequestStatus.Addressed, addressed.Status);
        Assert.Equal(w.Qa, addressed.AddressedByUserId);
        Assert.Equal(Now, addressed.AddressedAtUtc);
        Assert.Equal(raised.Record.Number, addressed.AddressedComment);
        Assert.NotEqual(request.Version, addressed.Version);
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(OosDeviationRequest)
            && a.EntityId == request.Id.ToString() && a.Action == "Update");
        Assert.Equal(w.Qa, audit.UserId);
        Assert.Contains("\"Status\":0", audit.PreviousValue);
        Assert.Contains("\"Status\":1", audit.NewValue);
        var link = await db.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.TargetType == QualityLinkTargetType.OosInvestigation
            && l.TargetId == request.InvestigationId);
        Assert.Equal(raised.Id, link.RecordId);
        var record = await db.QualityRecords.AsNoTracking().SingleAsync(r => r.Id == raised.Id);
        Assert.Equal(request.Reason, record.Description);
        Assert.Equal(w.Qa, record.RaisedByUserId);
        var created = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(QualityRecord)
            && a.EntityId == record.Id.ToString() && a.Action == "Create");
        Assert.Null(created.PreviousValue);
        Assert.Equal(w.Qa, created.UserId);
        Assert.Contains(request.Reason, created.NewValue);
    }

    [PostgresFact]
    public async Task Audit_finding_is_linked_without_changing_its_status()
    {
        var w = await ArrangeAsync();
        int findingId;
        await using (var arrange = Context(w.Qa))
        {
            var auditId = await arrange.AuditLogs.AsNoTracking().OrderBy(a => a.Id).Select(a => a.Id).FirstAsync();
            var finding = new AuditTrailReviewFinding
            {
                AuditLogId = auditId, Note = "Investigate this audit finding",
                RecordedAtUtc = Now, RecordedByUserId = w.Qa,
                Review = new AuditTrailReview
                {
                    Title = "Deviation source", PeriodFromUtc = Now.AddDays(-1), PeriodToUtc = Now,
                    CutoffAuditLogId = auditId, StartedAtUtc = Now, StartedByUserId = w.Qa
                }
            };
            arrange.AuditTrailReviewFindings.Add(finding);
            await arrange.SaveChangesAsync();
            findingId = finding.Id;
        }

        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() => ActAsync(w.Qa,
            s => s.RaiseFromAuditFindingAsync(findingId, (uint)AuditFindingStatus.Closed, w.Qa)));
        var raised = await ActAsync(w.Qa, s => s.RaiseFromAuditFindingAsync(findingId, (uint)AuditFindingStatus.Open, w.Qa));
        await using var verify = Context();
        var findingStatus = await verify.AuditTrailReviewFindings.AsNoTracking().Where(f => f.Id == findingId)
            .Select(f => f.Status).SingleAsync();
        Assert.Equal(AuditFindingStatus.Open, findingStatus);
        var link = await verify.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.RecordId == raised.Id
            && l.TargetType == QualityLinkTargetType.AuditTrailReviewFinding);
        Assert.Equal(findingId, link.TargetId);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(QualityRecordLink)
            && a.EntityId == link.Id.ToString() && a.Action == "Create");
        Assert.Equal(w.Qa, audit.UserId);
        Assert.Null(audit.PreviousValue);
        Assert.Contains($"\"TargetId\":{findingId}", audit.NewValue);
    }

    private sealed class FailWhenAddressing : DbCommandInterceptor
    {
        internal const string Message = "Injected failure while addressing the OOS request.";

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE \"OosDeviationRequests\"", StringComparison.Ordinal))
                throw new InvalidOperationException(Message);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [PostgresFact]
    public async Task Raising_from_OOS_waits_for_the_investigation_row_lock()
    {
        var w = await ArrangeAsync();
        var request = await OosRequestAsync(w);
        await using (var blocker = Context())
        await using (var held = await blocker.Database.BeginTransactionAsync())
        {
            await blocker.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT "Id" FROM "OosInvestigations" WHERE "Id" = {request.InvestigationId} FOR UPDATE""");

            await using var act = Context(w.Qa);
            await using var attempt = await act.Database.BeginTransactionAsync();
            await act.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '300ms'");
            var error = await Assert.ThrowsAsync<PostgresException>(() =>
                Service(act).RaiseFromOosRequestAsync(request.Id, request.Version, w.Qa));
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, error.SqlState);
        }
        await using var check = Context();
        Assert.Equal(OosDeviationRequestStatus.Open, await check.OosDeviationRequests.AsNoTracking().Where(r => r.Id == request.Id)
            .Select(r => r.Status).SingleAsync());
        Assert.False(await check.QualityRecordLinks.AsNoTracking().AnyAsync(l =>
            l.TargetType == QualityLinkTargetType.OosInvestigation && l.TargetId == request.InvestigationId));
    }

    [PostgresFact]
    public async Task Monitoring_escalation_links_without_changing_the_source()
    {
        var w = await ArrangeAsync();
        var today = DateOnly.FromDateTime(Now);
        var key = Guid.NewGuid().ToString("N");
        var source = new MonitoringDeviation
        {
            Kind = DeviationKind.MissedOccurrence, Status = DeviationStatus.Open, Number = $"MD-{key}"[..20],
            Description = "Missed window", OpenedAt = Now, DueDate = today.AddDays(1),
            MonitoringOccurrence = new MonitoringOccurrence
            {
                Category = MonitoringCategory.EnvironmentalMonitoring, TargetType = MonitoringTargetType.Room,
                CreatedByRevision = new MonitoringPlanRevision
                {
                    Year = 2086, RevisionNumber = 1, Status = PlanRevisionStatus.Approved, EffectiveFrom = today,
                    GeneratedAt = Now, GeneratedByUserId = w.Head
                },
                PeriodKey = key, WindowStart = today.AddDays(-1), WindowEnd = today, OriginalWindowEnd = today
            }
        };
        await using (var arrange = Context())
        {
            var room = new Room { Department = new Department { Name = key }, Name = key };
            arrange.Rooms.Add(room);
            await arrange.SaveChangesAsync();
            Assert.NotNull(source.MonitoringOccurrence);
            source.MonitoringOccurrence.RoomId = room.Id;
            arrange.MonitoringDeviations.Add(source);
            await arrange.SaveChangesAsync();
        }
        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
            ActAsync(w.Qa, s => s.EscalateMonitoringDeviationAsync(source.Id, source.Version + 1, w.Qa)));
        var raised = await ActAsync(w.Qa, s => s.EscalateMonitoringDeviationAsync(source.Id, source.Version, w.Qa));
        await using var db = Context();
        var persisted = await db.MonitoringDeviations.AsNoTracking().SingleAsync(d => d.Id == source.Id);
        Assert.Equal(source.Version, persisted.Version);
        Assert.Equal(source.Status, persisted.Status);
        Assert.Equal(source.Description, persisted.Description);
        Assert.Equal(source.DueDate, persisted.DueDate);
        Assert.Null(persisted.ClosedAt);
        var link = await db.QualityRecordLinks.AsNoTracking().SingleAsync(l =>
            l.TargetType == QualityLinkTargetType.MonitoringDeviation && l.TargetId == source.Id);
        Assert.Equal(raised.Id, link.RecordId);
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(QualityRecordLink) && a.EntityId == link.Id.ToString() && a.Action == "Create");
        Assert.Equal(w.Qa, audit.UserId);
        Assert.Null(audit.PreviousValue);
        Assert.Contains($"\"TargetId\":{source.Id}", audit.NewValue);
        Assert.False(await db.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(MonitoringDeviation) && a.EntityId == source.Id.ToString() && a.Action == "Update"));
    }
}

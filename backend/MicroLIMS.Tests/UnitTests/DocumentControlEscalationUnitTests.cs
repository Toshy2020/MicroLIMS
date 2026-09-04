using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlEscalationUnitTests
{
    private class TestDbSequenceHelper : IDatabaseSequenceHelper
    {
        private long _current = 7000;
        public Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Interlocked.Increment(ref _current));
        }
    }

    private async Task<(
        MicroLimsDbContext db,
        User admin,
        User analyst1,
        User analyst2,
        DocumentMaster master,
        DocumentRevision rev1,
        DocumentTrainingAssignment assignment1,
        DocumentEscalationService service
    )> CreateTestEnvironmentAsync(
        int daysBeforeDue = 3,
        int daysAfterDue = 1,
        bool requiresReading = true)
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new MicroLimsDbContext(options);

        var analystRole = new Role { Type = RoleType.Analyst, Name = "QC Analyst", IsActive = true };
        var adminRole = new Role { Type = RoleType.SystemAdministrator, Name = "System Admin", IsActive = true };
        db.Roles.AddRange(analystRole, adminRole);
        await db.SaveChangesAsync();

        var admin = new User { FullName = "Admin User", Username = "admin1", RoleId = adminRole.Id, IsActive = true, Role = adminRole };
        var analyst1 = new User { FullName = "Jane Doe", Username = "jdoe", RoleId = analystRole.Id, IsActive = true, Role = analystRole };
        var analyst2 = new User { FullName = "John Smith", Username = "jsmith", RoleId = analystRole.Id, IsActive = true, Role = analystRole };
        db.Users.AddRange(admin, analyst1, analyst2);
        await db.SaveChangesAsync();

        var docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure" };
        var docDept = new DocumentDepartment { Code = "QC", Name = "Quality Control" };
        db.DocumentTypes.Add(docType);
        db.DocumentDepartments.Add(docDept);
        await db.SaveChangesAsync();

        var master = new DocumentMaster
        {
            CompanyDocumentCode = "SOP-QC-001",
            Title = "Purified Water Testing",
            DocumentTypeId = docType.Id,
            DepartmentId = docDept.Id,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var rev1 = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow.AddDays(-10),
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        db.DocumentRevisions.Add(rev1);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = rev1.Id;
        await db.SaveChangesAsync();

        var config = new DocumentTrainingConfiguration
        {
            DocumentMasterId = master.Id,
            RequiresReading = requiresReading,
            RequiresRetrainingOnRevision = true,
            DefaultGracePeriodDays = 14,
            EscalationDaysBeforeDue = daysBeforeDue,
            EscalationDaysAfterDue = daysAfterDue,
            ModifiedByUserId = admin.Id,
            ModifiedAtUtc = DateTime.UtcNow.AddDays(-30)
        };
        db.DocumentTrainingConfigurations.Add(config);
        await db.SaveChangesAsync();

        // Baseline assignment: Due in 14 days from 10 days ago -> Due in 4 days from now
        var assignment1 = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev1.Id,
            AssignedUserId = analyst1.Id,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = DateTime.UtcNow.AddDays(-10),
            DueDateUtc = DateTime.UtcNow.AddDays(4),
            CreatedByUserId = admin.Id,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
        };
        db.DocumentTrainingAssignments.Add(assignment1);
        await db.SaveChangesAsync();

        var audit = new AuditEventService(db, new TestDbSequenceHelper());
        var service = new DocumentEscalationService(db, audit, NullLogger<DocumentEscalationService>.Instance);

        return (db, admin, analyst1, analyst2, master, rev1, assignment1, service);
    }

    [Fact]
    public async Task NotDueAssignment_DoesNotEscalate()
    {
        // Assignment due in 10 days; T-3 is in 7 days
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync();
        assignment.DueDateUtc = DateTime.UtcNow.AddDays(10);
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(DateTime.UtcNow);

        Assert.Equal(0, result.TotalEscalationsCreated);
        Assert.Equal(0, await db.DocumentEscalationRecords.CountAsync());
    }

    [Fact]
    public async Task TMinus3Days_ReachesApproachingDueEscalation()
    {
        // Due in 2 days (within T-3 window)
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync(daysBeforeDue: 3);
        assignment.DueDateUtc = DateTime.UtcNow.AddDays(2);
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(DateTime.UtcNow);

        Assert.Equal(1, result.TotalEscalationsCreated);
        Assert.Equal(1, result.ApproachingDueCreated);

        var escalation = await db.DocumentEscalationRecords.FirstAsync();
        Assert.Equal(DocumentEscalationLevel.ApproachingDue, escalation.EscalationLevel);
        Assert.Equal("AssignedUser", escalation.RecipientRoleOrTarget);
        Assert.Equal(DocumentEscalationStatus.Raised, escalation.Status);
    }

    [Fact]
    public async Task DueDateReached_ReachesDueEscalation_AndUpdatesAssignmentStatusToOverdue()
    {
        // Due right now / 1 minute ago
        var now = DateTime.UtcNow;
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync();
        assignment.DueDateUtc = now.AddMinutes(-5);
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now);

        Assert.Equal(1, result.DueCreated);

        var updatedAssignment = await db.DocumentTrainingAssignments.FindAsync(assignment.Id);
        Assert.Equal(TrainingAssignmentStatus.Overdue, updatedAssignment!.Status);

        var record = await db.DocumentEscalationRecords.FirstAsync(e => e.EscalationLevel == DocumentEscalationLevel.Due);
        Assert.NotNull(record);
        Assert.Equal(DocumentEscalationLevel.Due, record.EscalationLevel);
    }

    [Fact]
    public async Task TPlus1Day_ReachesOverdueEscalation_TargetingManagersAndQA()
    {
        var now = DateTime.UtcNow;
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync(daysAfterDue: 1);
        assignment.DueDateUtc = now.AddDays(-2); // 2 days past due (> T+1)
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now);

        // Should generate Due and Overdue escalations (both triggers passed)
        Assert.Equal(2, result.TotalEscalationsCreated);
        Assert.Equal(1, result.DueCreated);
        Assert.Equal(1, result.OverdueCreated);

        var overdueRecord = await db.DocumentEscalationRecords.FirstAsync(e => e.EscalationLevel == DocumentEscalationLevel.Overdue);
        Assert.Equal("Manager;QACompliance", overdueRecord.RecipientRoleOrTarget);
    }

    [Fact]
    public async Task ConfigurableEscalationIntervals_RespectedFromConfiguration()
    {
        // Configuration: T-5 days before, T+2 days after
        var now = DateTime.UtcNow;
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync(daysBeforeDue: 5, daysAfterDue: 2);
        assignment.DueDateUtc = now.AddDays(4); // 4 days until due: within T-5 window
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now);

        Assert.Equal(1, result.ApproachingDueCreated);
        var record = await db.DocumentEscalationRecords.FirstAsync();
        Assert.Equal(DocumentEscalationLevel.ApproachingDue, record.EscalationLevel);
    }

    [Fact]
    public async Task DocumentMasterSpecificConfiguration_OverridesDocumentTypeSettings()
    {
        var now = DateTime.UtcNow;
        var (db, admin, _, _, master, _, assignment, service) = await CreateTestEnvironmentAsync();

        // Add doc-type config with 1 day before due
        var typeConfig = new DocumentTrainingConfiguration
        {
            DocumentTypeId = master.DocumentTypeId,
            EscalationDaysBeforeDue = 1,
            ModifiedByUserId = admin.Id
        };
        db.DocumentTrainingConfigurations.Add(typeConfig);

        // Master config has 7 days before due
        var masterConfig = await db.DocumentTrainingConfigurations.FirstAsync(c => c.DocumentMasterId == master.Id);
        masterConfig.EscalationDaysBeforeDue = 7;
        await db.SaveChangesAsync();

        assignment.DueDateUtc = now.AddDays(5); // 5 days left: eligible under master (7d), not type (1d)
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now);
        Assert.Equal(1, result.ApproachingDueCreated);
    }

    [Fact]
    public async Task DisabledTrainingInConfiguration_ExemptsFromEscalation()
    {
        var now = DateTime.UtcNow;
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync(requiresReading: false);
        assignment.DueDateUtc = now.AddDays(-2); // Past due
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now);
        Assert.Equal(0, result.TotalEscalationsCreated);
        Assert.Equal(1, result.SkippedExemptOrTerminalCount);
    }

    [Fact]
    public async Task DuplicatePrevention_AndIdempotency_ProducesZeroDuplicatesOnRepeatedRuns()
    {
        var now = DateTime.UtcNow;
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync();
        assignment.DueDateUtc = now.AddDays(-2);
        await db.SaveChangesAsync();

        // Run 1: Generates Due and Overdue
        var result1 = await service.ProcessDueEscalationsAsync(now);
        Assert.Equal(2, result1.TotalEscalationsCreated);

        // Run 2: Exact same conditions
        var result2 = await service.ProcessDueEscalationsAsync(now);
        Assert.Equal(0, result2.TotalEscalationsCreated);
        Assert.Equal(2, result2.SkippedAlreadyEscalatedCount);

        // Database still contains exactly 2 records
        Assert.Equal(2, await db.DocumentEscalationRecords.CountAsync());
    }

    [Fact]
    public async Task DowntimeCatchUp_MissedEscalationsExecutedOnceUponRestart()
    {
        // System was down, missed both ApproachingDue and Due triggers
        var now = DateTime.UtcNow;
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync();
        assignment.DueDateUtc = now.AddHours(-1); // Missed T-3 and Due
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now);

        // Generates Due level upon restart catch-up
        Assert.True(result.TotalEscalationsCreated >= 1);

        // Rerunning produces 0
        var rerunResult = await service.ProcessDueEscalationsAsync(now);
        Assert.Equal(0, rerunResult.TotalEscalationsCreated);
    }

    [Fact]
    public async Task MultipleAssignments_AndMultipleDocuments_EscalateIndependently()
    {
        var now = DateTime.UtcNow;
        var (db, admin, analyst1, analyst2, master, rev1, assignment1, service) = await CreateTestEnvironmentAsync();

        assignment1.DueDateUtc = now.AddDays(1); // Approaching due (T-3)

        // Second assignment for analyst 2: Overdue
        var assignment2 = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev1.Id,
            AssignedUserId = analyst2.Id,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = now.AddDays(-20),
            DueDateUtc = now.AddDays(-2),
            CreatedByUserId = admin.Id
        };
        db.DocumentTrainingAssignments.Add(assignment2);
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now);

        Assert.Equal(2, result.TotalEvaluatedCount);
        Assert.Equal(3, result.TotalEscalationsCreated); // 1 approaching for assign1, 2 (Due+Overdue) for assign2
    }

    [Fact]
    public async Task AcknowledgedAssignment_IsExcludedFromEscalation()
    {
        var now = DateTime.UtcNow;
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync();
        assignment.DueDateUtc = now.AddDays(-5);
        assignment.Status = TrainingAssignmentStatus.Acknowledged;
        assignment.AcknowledgedAtUtc = now.AddDays(-6);
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now);

        Assert.Equal(0, result.TotalEscalationsCreated);
        Assert.Equal(0, await db.DocumentEscalationRecords.CountAsync());
    }

    [Fact]
    public async Task CompletedPassedAssignment_IsExcludedFromEscalation()
    {
        var now = DateTime.UtcNow;
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync();
        assignment.DueDateUtc = now.AddDays(-5);
        assignment.Status = TrainingAssignmentStatus.CompletedPassed;
        assignment.CompletedAtUtc = now.AddDays(-6);
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now);

        Assert.Equal(0, result.TotalEscalationsCreated);
    }

    [Fact]
    public async Task CancelledAssignment_IsExcludedFromEscalation()
    {
        var now = DateTime.UtcNow;
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync();
        assignment.DueDateUtc = now.AddDays(-5);
        assignment.Status = TrainingAssignmentStatus.Cancelled;
        assignment.ClosedReason = "Document Obsoleted";
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now);

        Assert.Equal(0, result.TotalEscalationsCreated);
    }

    [Fact]
    public async Task SupersededIncompleteAssignment_IsExcludedFromEscalation()
    {
        var now = DateTime.UtcNow;
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync();
        assignment.DueDateUtc = now.AddDays(-5);
        assignment.Status = TrainingAssignmentStatus.SupersededIncomplete;
        assignment.ClosedReason = "Superseded by Rev 02";
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now);

        Assert.Equal(0, result.TotalEscalationsCreated);
    }

    [Fact]
    public async Task SystemAttribution_AndAuditLogging_RecordedCorrectly()
    {
        var now = DateTime.UtcNow;
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync();
        assignment.DueDateUtc = now.AddMinutes(-5);
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now, processName: "DocumentEffectiveDateWorker");

        var auditLog = await db.AuditLogs
            .FirstOrDefaultAsync(l => l.ActionCode == "TrainingEscalationRaised_Due");

        Assert.NotNull(auditLog);
        Assert.Equal(ActorType.System, auditLog.ActorType);
        Assert.Equal("DocumentEffectiveDateWorker", auditLog.SystemProcessName);
        Assert.Null(auditLog.UserId);
        Assert.Equal(AuditActionCategory.Training, auditLog.ActionCategory);
    }

    [Fact]
    public async Task FailureIsolation_OneFailingAssignmentDoesNotCorruptOthers()
    {
        var now = DateTime.UtcNow;
        var (db, admin, analyst1, analyst2, master, rev1, assignment1, service) = await CreateTestEnvironmentAsync();

        assignment1.DueDateUtc = now.AddDays(-2);

        // Second valid assignment
        var assignment2 = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev1.Id,
            AssignedUserId = analyst2.Id,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            DueDateUtc = now.AddDays(-2),
            CreatedByUserId = admin.Id
        };
        db.DocumentTrainingAssignments.Add(assignment2);
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(now);

        // Both assignments evaluated successfully
        Assert.Equal(2, result.TotalEvaluatedCount);
        Assert.Equal(4, result.TotalEscalationsCreated);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task ServerUtcCalculation_StandardizedAuthoritativeTime()
    {
        var nowUtc = new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync();

        // Due exactly at 12:00:00 UTC
        assignment.DueDateUtc = nowUtc;
        await db.SaveChangesAsync();

        var result = await service.ProcessDueEscalationsAsync(nowUtc);

        Assert.Equal(1, result.DueCreated);
        var record = await db.DocumentEscalationRecords.FirstAsync(e => e.EscalationLevel == DocumentEscalationLevel.Due);
        Assert.Equal(nowUtc, record.ExecutedAtUtc);
    }

    [Fact]
    public async Task ResolveEscalation_RequiresReason_AndUpdatesStatusWithAttribution()
    {
        var now = DateTime.UtcNow;
        var (db, admin, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync();
        assignment.DueDateUtc = now.AddDays(-2);
        await db.SaveChangesAsync();

        await service.ProcessDueEscalationsAsync(now);

        var record = await db.DocumentEscalationRecords.FirstAsync(e => e.EscalationLevel == DocumentEscalationLevel.Due);

        // 1. Missing reason throws
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResolveEscalationAsync(new ResolveEscalationRequest(record.Id, ""), admin.Id));

        // 2. Valid resolution succeeds
        var resolved = await service.ResolveEscalationAsync(
            new ResolveEscalationRequest(record.Id, "Analyst was on approved sick leave; manager verified extension."),
            admin.Id,
            now);

        Assert.Equal(DocumentEscalationStatus.Resolved, resolved.Status);
        Assert.Equal(admin.Id, resolved.ResolvedByUserId);
        Assert.NotNull(resolved.ResolvedAtUtc);
        Assert.Contains("approved sick leave", resolved.ResolutionReason);
    }

    [Fact]
    public async Task GetOverdueTrainingSummary_ReturnsAccurateMetrics()
    {
        var now = DateTime.UtcNow;
        var (db, _, _, _, _, _, assignment, service) = await CreateTestEnvironmentAsync();
        assignment.DueDateUtc = now.AddDays(-3);
        await db.SaveChangesAsync();

        await service.ProcessDueEscalationsAsync(now);

        var summary = await service.GetOverdueTrainingSummaryAsync(now);

        Assert.Equal(1, summary.OverdueAssignmentsCount);
        Assert.Equal(1, summary.EscalatedCount);
        Assert.Single(summary.OverdueItems);
        Assert.Equal(3.0, summary.OverdueItems[0].DaysOverdue);
        Assert.Equal(DocumentEscalationLevel.Overdue, summary.OverdueItems[0].CurrentEscalationLevel);
    }
}

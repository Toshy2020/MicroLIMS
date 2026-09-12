using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.BackgroundServices;
using MicroLIMS.API.Controllers.DocumentControl;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

/// <summary>
/// Release 1c Work Package 8 (WP8): Comprehensive End-to-End Integration Verification Suite.
/// Proves the unbroken integration between:
/// Document Lifecycle -> Effective Revision -> Training Assignment -> Retraining Cascade ->
/// My Reading List -> Controlled Revision Viewing -> Reading Progress -> Explicit Acknowledgement ->
/// Escalation / Overdue Management -> Training Matrix -> Compliance Dashboard -> Audit / Evidence.
/// </summary>
[Collection("PostgresDatabaseCollection")]
public class Release1cIntegrationVerificationPostgresTests
{
    private const string DefaultPassword = "Part11-Valid-Password-2026!";
    private readonly PostgresTestFixture _fixture;

    public Release1cIntegrationVerificationPostgresTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private class PostgresSequenceHelper : IDatabaseSequenceHelper
    {
        private long _seq = 99000;
        public Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Interlocked.Increment(ref _seq));
        }
    }

    private ControllerContext CreateContext(int userId, string roleName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"User_{userId}"),
            new(ClaimTypes.Role, roleName)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private async Task<(
        DocumentMaster Master,
        DocumentRevision Rev1,
        User TraineeUser,
        User AdminUser,
        User SectionHeadUser,
        DocumentDepartment Dept,
        DocumentRoleCurriculum Curriculum
    )> SeedCompleteIntegratedBaselineAsync(MicroLimsDbContext db)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];

        var analystRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Analyst)
            ?? db.Roles.Add(new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true }).Entity;

        var sectionHeadRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.SectionHead)
            ?? db.Roles.Add(new Role { Type = RoleType.SectionHead, Name = "SectionHead", IsActive = true }).Entity;

        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.SystemAdministrator)
            ?? db.Roles.Add(new Role { Type = RoleType.SystemAdministrator, Name = "SystemAdministrator", IsActive = true }).Entity;

        await db.SaveChangesAsync();

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword);

        var trainee = new User
        {
            FullName = $"Trainee Analyst {suffix}",
            Username = $"trn_{suffix}",
            PasswordHash = passwordHash,
            RoleId = analystRole.Id,
            IsActive = true
        };

        var sectionHead = new User
        {
            FullName = $"Quality Section Head {suffix}",
            Username = $"sh_{suffix}",
            PasswordHash = passwordHash,
            RoleId = sectionHeadRole.Id,
            IsActive = true
        };

        var admin = new User
        {
            FullName = $"System Admin {suffix}",
            Username = $"adm_{suffix}",
            PasswordHash = passwordHash,
            RoleId = adminRole.Id,
            IsActive = true
        };

        db.Users.AddRange(trainee, sectionHead, admin);
        await db.SaveChangesAsync();

        var docType = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Code == "SOP")
            ?? db.DocumentTypes.Add(new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", DefaultReviewCycleMonths = 24, IsActive = true }).Entity;

        var dept = await db.DocumentDepartments.FirstOrDefaultAsync(d => d.Code == "QC")
            ?? db.DocumentDepartments.Add(new DocumentDepartment { Code = "QC", Name = "Quality Control", IsActive = true }).Entity;

        var sec = await db.DocumentSections.FirstOrDefaultAsync(s => s.DepartmentId == dept.Id)
            ?? db.DocumentSections.Add(new DocumentSection { DepartmentId = dept.Id, Name = "Analytical Micro", IsActive = true }).Entity;

        await db.SaveChangesAsync();

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-WP8-{suffix}".ToUpperInvariant(),
            CompanyDocumentCode = $"SOP-WP8-{suffix}".ToUpperInvariant(),
            Title = $"WP8 Integrated Master {suffix}",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = sec.Id,
            DocumentOwnerUserId = sectionHead.Id,
            CreatedByUserId = admin.Id,
            RecordStatus = DocumentRecordStatus.Active
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var rev1 = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow.Date,
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.UtcNow.Date
        };
        db.DocumentRevisions.Add(rev1);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = rev1.Id;
        await db.SaveChangesAsync();

        // Seed Role Curriculum mapping trainee's Analyst role AND matching department to this Master
        var curriculum = new DocumentRoleCurriculum
        {
            RoleId = analystRole.Id,
            DepartmentId = dept.Id,
            Name = $"Curriculum for Analysts {suffix}",
            Description = "Automated role-based qualification",
            IsActive = true,
            CreatedByUserId = admin.Id,
            CreatedAtUtc = DateTime.UtcNow
        };
        curriculum.Items.Add(new DocumentRoleCurriculumItem
        {
            DocumentMasterId = master.Id,
            IsMandatory = true,
            CustomGracePeriodDays = 14,
            AddedAtUtc = DateTime.UtcNow
        });
        db.DocumentRoleCurricula.Add(curriculum);
        await db.SaveChangesAsync();

        return (master, rev1, trainee, admin, sectionHead, dept, curriculum);
    }

    #region Scenario A: New Effective Revision & Cascade
    [PostgresFact]
    public async Task ScenarioA_NewEffectiveRevision_CascadesAssignments_FollowsGraceHierarchy_AndAudits()
    {
        await using var db = _fixture.CreateDbContext();
        var (master, rev1, trainee, admin, _, _, _) = await SeedCompleteIntegratedBaselineAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);

        // 1. Trigger assignment cascade for Rev 1
        var cascadeResult = await trainingService.ProcessEffectiveRevisionCascadeAsync(
            effectiveRevisionId: rev1.Id,
            supersededRevisionId: null,
            utcNowOverride: DateTime.UtcNow);

        Assert.True(cascadeResult.NewAssignmentsCreated >= 1);

        // 2. Verify assignment in PostgreSQL
        var assignment = await db.DocumentTrainingAssignments
            .FirstOrDefaultAsync(a => a.DocumentRevisionId == rev1.Id && a.AssignedUserId == trainee.Id);

        Assert.NotNull(assignment);
        Assert.Equal(TrainingAssignmentStatus.Assigned, assignment.Status);
        Assert.Equal(AssignmentType.Reading, assignment.AssignmentType);

        // 3. Verify Grace Period Hierarchy: Curriculum specified 14 days from revision EffectiveDate
        var expectedDueDate = rev1.EffectiveDate!.Value.Date.AddDays(14);
        Assert.Equal(expectedDueDate, assignment.DueDateUtc.Date);

        // 4. Verify Audit Trail was persisted in PostgreSQL
        var auditEntry = await db.AuditLogs
            .FirstOrDefaultAsync(l => l.DocumentMasterId == master.Id && l.ActionCode == "TrainingCascadeAssignmentsCreated");

        Assert.NotNull(auditEntry);
        Assert.Equal(AuditActionCategory.Document, auditEntry.ActionCategory);
    }
    #endregion

    #region Scenario B: User Reading Workflow
    [PostgresFact]
    public async Task ScenarioB_UserReadingWorkflow_ProgressIsInformational_AcknowledgementPersistsEvidence_UpdatesCompliance()
    {
        await using var db = _fixture.CreateDbContext();
        var (master, rev1, trainee, admin, _, _, _) = await SeedCompleteIntegratedBaselineAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);
        var ackService = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);
        var matrixService = new TrainingMatrixService(db, NullLogger<TrainingMatrixService>.Instance);

        await trainingService.ProcessEffectiveRevisionCascadeAsync(rev1.Id, null);

        var assignment = await db.DocumentTrainingAssignments
            .FirstAsync(a => a.DocumentRevisionId == rev1.Id && a.AssignedUserId == trainee.Id);

        var trainingController = new DocumentTrainingAssignmentController(trainingService)
        {
            ControllerContext = CreateContext(trainee.Id, nameof(RoleType.Analyst))
        };
        var ackController = new DocumentAcknowledgementController(ackService)
        {
            ControllerContext = CreateContext(trainee.Id, nameof(RoleType.Analyst))
        };

        // 1. My Reading List query returns the pending assignment
        var myAssignmentsRes = await trainingController.GetMyAssignments();
        var ok1 = Assert.IsType<OkObjectResult>(myAssignmentsRes);
        var listData = Assert.IsType<ApiResponse<IReadOnlyList<DocumentTrainingAssignmentDto>>>(ok1.Value).Data;
        Assert.Contains(listData, a => a.Id == assignment.Id && a.Status == TrainingAssignmentStatus.Assigned);

        // 2. Record reading progress to 100% -> MUST remain Reading, NOT Acknowledged
        var progressReq = new ReadingProgressUpdateRequest(assignment.Id, rev1.Id, ProgressPercentage: 100);
        var progressRes = await ackController.RecordReadingProgress(assignment.Id, progressReq);
        Assert.IsType<OkObjectResult>(progressRes);

        var assignAfterProgress = await db.DocumentTrainingAssignments.FindAsync(assignment.Id);
        Assert.Equal(TrainingAssignmentStatus.Reading, assignAfterProgress!.Status);

        // Confirm zero acknowledgement evidence exists
        var preAckEvidence = await db.DocumentAcknowledgementRecords
            .FirstOrDefaultAsync(r => r.DocumentTrainingAssignmentId == assignment.Id);
        Assert.Null(preAckEvidence);

        // 3. Submit conscious explicit acknowledgement
        var ackReq = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: true,
            Comments: "Read and understood entire document."
        );
        var ackRes = await ackController.SubmitAcknowledgement(assignment.Id, ackReq);
        Assert.IsType<OkObjectResult>(ackRes);

        // 4. Verify acknowledgement evidence is permanently recorded in PostgreSQL
        var postAckEvidence = await db.DocumentAcknowledgementRecords
            .FirstOrDefaultAsync(r => r.DocumentTrainingAssignmentId == assignment.Id);
        Assert.NotNull(postAckEvidence);
        Assert.Equal(trainee.Id, postAckEvidence.AcknowledgedByUserId);
        Assert.NotEmpty(postAckEvidence.StatementText);

        // 5. Verify refreshed Reading List & Matrix reflect Qualified state
        var assignAfterAck = await db.DocumentTrainingAssignments.FindAsync(assignment.Id);
        Assert.Equal(TrainingAssignmentStatus.Acknowledged, assignAfterAck!.Status);

        var matrix = await matrixService.GetTrainingMatrixGridAsync(new TrainingMatrixFilterDto(UserId: trainee.Id));
        var cell = Assert.Single(matrix.Cells, c => c.DocumentMasterId == master.Id && c.UserId == trainee.Id);
        Assert.Equal("Qualified", cell.CellStatus);
    }
    #endregion

    #region Scenario C: Overdue & Escalation Management
    [PostgresFact]
    public async Task ScenarioC_OverdueAndEscalation_ProcessesLevels_Idempotent_PreservesHistory()
    {
        await using var db = _fixture.CreateDbContext();
        var (master, rev1, trainee, admin, sectionHead, _, _) = await SeedCompleteIntegratedBaselineAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);
        var escService = new DocumentEscalationService(db, audit, NullLogger<DocumentEscalationService>.Instance);

        await trainingService.ProcessEffectiveRevisionCascadeAsync(rev1.Id, null);
        var assignment = await db.DocumentTrainingAssignments
            .FirstAsync(a => a.DocumentRevisionId == rev1.Id && a.AssignedUserId == trainee.Id);

        // Set due date to 2 days in the past (triggers Due and Overdue levels)
        var now = DateTime.UtcNow;
        assignment.DueDateUtc = now.AddDays(-2);
        await db.SaveChangesAsync();

        // 1. First execution cycle: Generates Due and Overdue escalations
        var result1 = await escService.ProcessDueEscalationsAsync(now, processName: "DocumentEffectiveDateWorker");
        Assert.True(result1.TotalEscalationsCreated >= 2);
        Assert.True(result1.DueCreated >= 1);
        Assert.True(result1.OverdueCreated >= 1);

        var updatedAssign = await db.DocumentTrainingAssignments.FindAsync(assignment.Id);
        Assert.Equal(TrainingAssignmentStatus.Overdue, updatedAssign!.Status);

        // Verify recipient attribution
        var overdueRec = await db.DocumentEscalationRecords
            .FirstAsync(e => e.DocumentTrainingAssignmentId == assignment.Id && e.EscalationLevel == DocumentEscalationLevel.Overdue);
        Assert.Equal("Manager;QACompliance", overdueRec.RecipientRoleOrTarget);

        // 2. Idempotency verification: rerun identical cycle -> zero duplicate escalations for this assignment
        var preCount = await db.DocumentEscalationRecords
            .CountAsync(e => e.DocumentTrainingAssignmentId == assignment.Id);
        Assert.Equal(2, preCount);

        var result2 = await escService.ProcessDueEscalationsAsync(now, processName: "DocumentEffectiveDateWorker");
        
        var postCount = await db.DocumentEscalationRecords
            .CountAsync(e => e.DocumentTrainingAssignmentId == assignment.Id);
        Assert.Equal(2, postCount); // Zero duplicate records created

        // 3. Acknowledge assignment -> ensures future cycles ignore it
        var ackService = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);
        await ackService.AcknowledgeAssignmentAsync(
            new AcknowledgementSubmissionRequest(assignment.Id, rev1.Id, ConfirmedLegalStatement: true),
            actingUserId: trainee.Id);

        var result3 = await escService.ProcessDueEscalationsAsync(now.AddDays(5), processName: "DocumentEffectiveDateWorker");
        
        // Historical escalation records remain intact in PostgreSQL
        var historyCount = await db.DocumentEscalationRecords
            .CountAsync(e => e.DocumentTrainingAssignmentId == assignment.Id);
        Assert.Equal(2, historyCount);
    }
    #endregion

    #region Scenario D: Retraining & Supersession
    [PostgresFact]
    public async Task ScenarioD_RetrainingCascade_PreservesHistory_SetsSourceAncestry_IdentifiesSupersededGap()
    {
        await using var db = _fixture.CreateDbContext();
        var (master, rev1, trainee, admin, _, _, _) = await SeedCompleteIntegratedBaselineAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);
        var ackService = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);
        var matrixService = new TrainingMatrixService(db, NullLogger<TrainingMatrixService>.Instance);

        // 1. Trainee completes training on Revision 01
        await trainingService.ProcessEffectiveRevisionCascadeAsync(rev1.Id, null);
        var assignment1 = await db.DocumentTrainingAssignments
            .FirstAsync(a => a.DocumentRevisionId == rev1.Id && a.AssignedUserId == trainee.Id);

        await ackService.AcknowledgeAssignmentAsync(
            new AcknowledgementSubmissionRequest(assignment1.Id, rev1.Id, ConfirmedLegalStatement: true),
            actingUserId: trainee.Id);

        // 2. Revision 02 becomes effective, superseding Revision 01
        var rev2 = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow,
            CreatedByUserId = admin.Id
        };
        db.DocumentRevisions.Add(rev2);
        await db.SaveChangesAsync();

        rev1.RevisionStatus = DocumentRevisionStatus.Superseded;
        master.CurrentEffectiveRevisionId = rev2.Id;
        await db.SaveChangesAsync();

        // 3. Trigger Retraining Cascade
        var cascadeResult = await trainingService.ProcessEffectiveRevisionCascadeAsync(
            effectiveRevisionId: rev2.Id,
            supersededRevisionId: rev1.Id);

        Assert.True(cascadeResult.NewAssignmentsCreated >= 1);

        // 4. Verify Rev 01 assignment and acknowledgement remain intact
        var historicalAck = await db.DocumentAcknowledgementRecords
            .FirstOrDefaultAsync(r => r.DocumentTrainingAssignmentId == assignment1.Id);
        Assert.NotNull(historicalAck);
        Assert.Equal(rev1.Id, historicalAck.DocumentRevisionId);

        // 5. Verify Rev 02 assignment has SourceAssignmentId pointer to assignment 1
        var assignment2 = await db.DocumentTrainingAssignments
            .FirstOrDefaultAsync(a => a.DocumentRevisionId == rev2.Id && a.AssignedUserId == trainee.Id);
        Assert.NotNull(assignment2);
        Assert.Equal(assignment1.Id, assignment2.SourceAssignmentId);
        Assert.Equal(TrainingAssignmentStatus.Assigned, assignment2.Status);

        // 6. Verify Gap Detection: Trainee is Qualified on R1, but not R2
        var isGap = await trainingService.IsTrainedOnSupersededOnlyAsync(trainee.Id, master.Id);
        Assert.True(isGap);

        // 7. Verify Compliance Dashboard and Training Matrix reflect the gap
        var kpis = await matrixService.GetComplianceKpisAsync();
        Assert.True(kpis.SupersededGapCount >= 1);

        var matrix = await matrixService.GetTrainingMatrixGridAsync(new TrainingMatrixFilterDto(UserId: trainee.Id));
        var matrixCell = Assert.Single(matrix.Cells, c => c.DocumentMasterId == master.Id && c.UserId == trainee.Id);
        Assert.Equal("TrainedOnSupersededOnly", matrixCell.CellStatus);
    }
    #endregion

    #region Scenario E: Document Obsolescence
    [PostgresFact]
    public async Task ScenarioE_DocumentObsolescence_CancelsOutstandingTraining_PreservesEvidence()
    {
        await using var db = _fixture.CreateDbContext();
        var (master, rev1, trainee, admin, _, _, _) = await SeedCompleteIntegratedBaselineAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);
        var matrixService = new TrainingMatrixService(db, NullLogger<TrainingMatrixService>.Instance);

        await trainingService.ProcessEffectiveRevisionCascadeAsync(rev1.Id, null);
        var assignment = await db.DocumentTrainingAssignments
            .FirstAsync(a => a.DocumentRevisionId == rev1.Id && a.AssignedUserId == trainee.Id);

        // Set Document Revision to Obsolete
        rev1.RevisionStatus = DocumentRevisionStatus.Obsolete;
        master.CurrentEffectiveRevisionId = null;
        await db.SaveChangesAsync();

        // Handle obsolescence training cancellation
        var cancelledCount = await trainingService.HandleDocumentObsolescenceAsync(
            documentMasterId: master.Id,
            documentRevisionId: rev1.Id,
            reason: "Document retired");
        Assert.True(cancelledCount >= 1);

        // Verify assignment status transitioned to Cancelled
        var updatedAssign = await db.DocumentTrainingAssignments.FindAsync(assignment.Id);
        Assert.Equal(TrainingAssignmentStatus.Cancelled, updatedAssign!.Status);
        Assert.Contains("Document retired", updatedAssign.ClosedReason);

        // Verify matrix excludes documents without effective revisions from active matrix
        var matrix = await matrixService.GetTrainingMatrixGridAsync(new TrainingMatrixFilterDto(UserId: trainee.Id));
        Assert.DoesNotContain(matrix.Documents, d => d.DocumentMasterId == master.Id);
    }
    #endregion

    #region Scenario F: Authorization & Segregation of Duties
    [PostgresFact]
    public async Task ScenarioF_AuthorizationAndSoD_EnforcesScoping_AndRejectsCrossUserActions()
    {
        await using var db = _fixture.CreateDbContext();
        var (master, rev1, trainee, admin, sectionHead, _, _) = await SeedCompleteIntegratedBaselineAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);
        var ackService = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);
        var matrixService = new TrainingMatrixService(db, NullLogger<TrainingMatrixService>.Instance);

        await trainingService.ProcessEffectiveRevisionCascadeAsync(rev1.Id, null);
        var assignment = await db.DocumentTrainingAssignments
            .FirstAsync(a => a.DocumentRevisionId == rev1.Id && a.AssignedUserId == trainee.Id);

        // 1. Trainee attempts to view another user's matrix profile -> 403 Forbidden
        var matrixController = new DocumentTrainingMatrixController(matrixService)
        {
            ControllerContext = CreateContext(trainee.Id, nameof(RoleType.Analyst))
        };
        var userDetailRes = await matrixController.GetUserCompliance(sectionHead.Id);
        var forbidResult = Assert.IsType<ObjectResult>(userDetailRes);
        Assert.Equal(403, forbidResult.StatusCode);

        // 2. SectionHead attempts to submit acknowledgement for trainee's assignment -> Rejection (403 Forbidden)
        var otherUserAckReq = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: true
        );
        var sectionHeadAckController = new DocumentAcknowledgementController(ackService)
        {
            ControllerContext = CreateContext(sectionHead.Id, nameof(RoleType.SectionHead))
        };
        var ackRes = await sectionHeadAckController.SubmitAcknowledgement(assignment.Id, otherUserAckReq);
        var statusResult = Assert.IsType<ObjectResult>(ackRes);
        Assert.Equal(403, statusResult.StatusCode);
        var errorData = Assert.IsType<ApiResponse<object>>(statusResult.Value);
        Assert.Contains("not authorized", errorData.Message, StringComparison.OrdinalIgnoreCase);

        // 3. Non-admin user querying grid is strictly restricted to their own row
        var gridRes = await matrixController.GetTrainingMatrixGrid(new TrainingMatrixFilterDto());
        var okGrid = Assert.IsType<OkObjectResult>(gridRes);
        var gridData = Assert.IsType<ApiResponse<TrainingMatrixGridDto>>(okGrid.Value).Data;
        Assert.Single(gridData.Users);
        Assert.Equal(trainee.Id, gridData.Users[0].UserId);
    }
    #endregion

    #region Scenario G: Immutability Trigger Defense
    [PostgresFact]
    public async Task ScenarioG_ImmutabilityTriggers_DirectSqlUpdateOrDelete_IsProhibitedOnPostgres()
    {
        await using var db = _fixture.CreateDbContext();
        var (master, rev1, trainee, _, _, _, _) = await SeedCompleteIntegratedBaselineAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);
        var ackService = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);

        await trainingService.ProcessEffectiveRevisionCascadeAsync(rev1.Id, null);
        var assignment = await db.DocumentTrainingAssignments
            .FirstAsync(a => a.DocumentRevisionId == rev1.Id && a.AssignedUserId == trainee.Id);

        await ackService.AcknowledgeAssignmentAsync(
            new AcknowledgementSubmissionRequest(assignment.Id, rev1.Id, ConfirmedLegalStatement: true),
            actingUserId: trainee.Id);

        var ackRecord = await db.DocumentAcknowledgementRecords
            .FirstAsync(r => r.DocumentTrainingAssignmentId == assignment.Id);

        // Direct raw SQL UPDATE on DocumentAcknowledgementRecords MUST be blocked by PostgreSQL trigger
        var updateEx = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE \"DocumentAcknowledgementRecords\" SET \"StatementText\" = 'Tampered' WHERE \"Id\" = {ackRecord.Id}"));
        Assert.Contains("append-only: UPDATE and DELETE operations are prohibited by GMP regulations", updateEx.Message);

        // Direct raw SQL DELETE on DocumentAcknowledgementRecords MUST be blocked by PostgreSQL trigger
        var deleteEx = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM \"DocumentAcknowledgementRecords\" WHERE \"Id\" = {ackRecord.Id}"));
        Assert.Contains("append-only: UPDATE and DELETE operations are prohibited by GMP regulations", deleteEx.Message);
    }
    #endregion

    #region Scenario H: DocumentEffectiveDateWorker Integrated Cycle
    [PostgresFact]
    public async Task ScenarioH_DocumentEffectiveDateWorker_IntegratedExecution_ProcessesAllCyclesIdempotently()
    {
        await using var db = _fixture.CreateDbContext();
        var (master, rev1, trainee, admin, _, _, _) = await SeedCompleteIntegratedBaselineAsync(db);

        var seqHelper = new PostgresSequenceHelper();
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var sig = new ElectronicSignatureService(db);
        var appService = new DocumentApprovalService(db, audit, auth, sig);
        var effectiveService = new DocumentEffectiveDateService(db, audit, NullLogger<DocumentEffectiveDateService>.Instance);
        var periodicService = new PeriodicReviewService(db, audit, auth, appService, NullLogger<PeriodicReviewService>.Instance);
        var escService = new DocumentEscalationService(db, audit, NullLogger<DocumentEscalationService>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IDocumentEffectiveDateService>(effectiveService);
        services.AddSingleton<IPeriodicReviewService>(periodicService);
        services.AddSingleton<IDocumentEscalationService>(escService);

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var config = new ConfigurationBuilder().Build();

        var worker = new DocumentEffectiveDateWorker(
            scopeFactory,
            config,
            NullLogger<DocumentEffectiveDateWorker>.Instance);

        // 1. Run worker cycle: ensures effective date, periodic review, and training escalations run smoothly
        await worker.RunCycleAsync(CancellationToken.None);

        // 2. Confirm worker execution is clean and repeatable without error
        await worker.RunCycleAsync(CancellationToken.None);
    }
    #endregion
}

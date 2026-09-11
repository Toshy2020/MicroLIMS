using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.Controllers.DocumentControl;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentControlRestApiPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentControlRestApiPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private class PostgresSequenceHelper : IDatabaseSequenceHelper
    {
        private long _seq = 105000;
        public Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Interlocked.Increment(ref _seq));
        }
    }

    private static ControllerContext CreateContext(int userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"user{userId}"),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "PostgresTestAuth");
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
                Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
            }
        };
    }

    private async Task<(int MasterId, int RevId, int AssignmentId)> SeedScenarioAsync(MicroLimsDbContext db)
    {
        var docType = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Code == "SOP");
        if (docType == null)
        {
            docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", DefaultReviewCycleMonths = 24, IsActive = true };
            db.DocumentTypes.Add(docType);
            await db.SaveChangesAsync();
        }

        var dept = await db.DocumentDepartments.FirstOrDefaultAsync(d => d.Code == "QC");
        if (dept == null)
        {
            dept = new DocumentDepartment { Code = "QC", Name = "Quality Control", IsActive = true };
            db.DocumentDepartments.Add(dept);
            await db.SaveChangesAsync();
        }

        var sec = await db.DocumentSections.FirstOrDefaultAsync(s => s.DepartmentId == dept.Id);
        if (sec == null)
        {
            sec = new DocumentSection { DepartmentId = dept.Id, Name = "Analytical", IsActive = true };
            db.DocumentSections.Add(sec);
            await db.SaveChangesAsync();
        }

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-API-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            CompanyDocumentCode = $"SOP-API-{Guid.NewGuid():N}".Substring(0, 15),
            Title = "API Integration Test Master",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = sec.Id,
            DocumentOwnerUserId = _fixture.SeededUserId,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var rev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow.AddDays(-20),
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        db.DocumentRevisions.Add(rev);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = rev.Id;
        await db.SaveChangesAsync();

        var assignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev.Id,
            AssignedUserId = _fixture.SeededUserId,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = DateTime.UtcNow.AddDays(-10),
            DueDateUtc = DateTime.UtcNow.AddDays(4),
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
        };
        db.DocumentTrainingAssignments.Add(assignment);
        await db.SaveChangesAsync();

        return (master.Id, rev.Id, assignment.Id);
    }

    [PostgresFact]
    public async Task Postgres_GetAssignments_And_GetMyAssignments_QueryRealDatabase()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, revId, assignmentId) = await SeedScenarioAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);

        var controller = new DocumentTrainingAssignmentController(trainingService)
        {
            ControllerContext = CreateContext(_fixture.SeededUserId, nameof(RoleType.Analyst))
        };

        var myAssignmentsRes = await controller.GetMyAssignments();
        var okResult = Assert.IsType<OkObjectResult>(myAssignmentsRes);
        var apiRes = Assert.IsType<ApiResponse<IReadOnlyList<DocumentTrainingAssignmentDto>>>(okResult.Value);

        Assert.Contains(apiRes.Data, a => a.Id == assignmentId);
    }

    [PostgresFact]
    public async Task Postgres_SubmitAcknowledgement_PersistsEvidentiaryRecord_ThroughApiController()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, revId, assignmentId) = await SeedScenarioAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var ackService = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);

        var controller = new DocumentAcknowledgementController(ackService)
        {
            ControllerContext = CreateContext(_fixture.SeededUserId, nameof(RoleType.Analyst))
        };

        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignmentId,
            DocumentRevisionId: revId,
            ConfirmedLegalStatement: true,
            Comments: "Postgres API Integration Ack"
        );

        var submitRes = await controller.SubmitAcknowledgement(assignmentId, request);
        var okResult = Assert.IsType<OkObjectResult>(submitRes);
        var apiRes = Assert.IsType<ApiResponse<AcknowledgementResultDto>>(okResult.Value);

        Assert.Equal(assignmentId, apiRes.Data.AssignmentId);
        Assert.Equal(TrainingAssignmentStatus.Acknowledged, apiRes.Data.Status);

        // Verify with fresh DbContext against PostgreSQL
        await using var verifyDb = _fixture.CreateDbContext();
        var record = await verifyDb.DocumentAcknowledgementRecords
            .FirstOrDefaultAsync(r => r.DocumentTrainingAssignmentId == assignmentId);

        Assert.NotNull(record);
        Assert.Equal(_fixture.SeededUserId, record.AcknowledgedByUserId);
        Assert.Contains("Postgres API Integration Ack", record.Comments);
    }

    [PostgresFact]
    public async Task Postgres_ProcessDueEscalations_And_ResolveEscalation_ThroughApiController()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, revId, assignmentId) = await SeedScenarioAsync(db);

        // Make assignment overdue
        var assignment = await db.DocumentTrainingAssignments.FindAsync(assignmentId);
        assignment!.DueDateUtc = DateTime.UtcNow.AddDays(-2);
        await db.SaveChangesAsync();

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var escService = new DocumentEscalationService(db, audit, NullLogger<DocumentEscalationService>.Instance);

        var controller = new DocumentEscalationController(escService)
        {
            ControllerContext = CreateContext(_fixture.SeededUserId, nameof(RoleType.SystemAdministrator))
        };

        // 1. Trigger process-due
        var processRes = await controller.ProcessDueEscalations();
        var okResult1 = Assert.IsType<OkObjectResult>(processRes);
        var apiRes1 = Assert.IsType<ApiResponse<EscalationProcessingResultDto>>(okResult1.Value);
        Assert.True(apiRes1.Data.TotalEscalationsCreated >= 1);

        // 2. Query history
        var historyRes = await controller.GetAssignmentEscalationHistory(assignmentId);
        var okResult2 = Assert.IsType<OkObjectResult>(historyRes);
        var apiRes2 = Assert.IsType<ApiResponse<IReadOnlyList<DocumentEscalationSummaryDto>>>(okResult2.Value);
        Assert.NotEmpty(apiRes2.Data);

        var escalationToResolve = apiRes2.Data[0];

        // 3. Resolve escalation
        var resolveReq = new ResolveEscalationRequest(
            EscalationRecordId: escalationToResolve.Id,
            ResolutionReason: "Manager reviewed and resolved via API"
        );

        var resolveRes = await controller.ResolveEscalation(resolveReq);
        var okResult3 = Assert.IsType<OkObjectResult>(resolveRes);
        var apiRes3 = Assert.IsType<ApiResponse<DocumentEscalationSummaryDto>>(okResult3.Value);

        Assert.Equal(DocumentEscalationStatus.Resolved, apiRes3.Data.Status);
    }

    [PostgresFact]
    public async Task Postgres_EndToEnd_ReadingListWorkflow_ViewRevision_RecordProgress_AndAcknowledge()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, revId, assignmentId) = await SeedScenarioAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);
        var ackService = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);

        var trainingController = new DocumentTrainingAssignmentController(trainingService)
        {
            ControllerContext = CreateContext(_fixture.SeededUserId, nameof(RoleType.Analyst))
        };
        var ackController = new DocumentAcknowledgementController(ackService)
        {
            ControllerContext = CreateContext(_fixture.SeededUserId, nameof(RoleType.Analyst))
        };

        // 1. My Reading List query returns the assignment
        var myAssignmentsRes = await trainingController.GetMyAssignments();
        var ok1 = Assert.IsType<OkObjectResult>(myAssignmentsRes);
        var listData = Assert.IsType<ApiResponse<IReadOnlyList<DocumentTrainingAssignmentDto>>>(ok1.Value).Data;
        var myAssignment = Assert.Single(listData, a => a.Id == assignmentId);
        Assert.Equal(TrainingAssignmentStatus.Assigned, myAssignment.Status);
        Assert.Equal(revId, myAssignment.DocumentRevisionId);

        // 2. Controlled reading progress updates to 50% without acknowledging (informational only)
        var progressReq50 = new ReadingProgressUpdateRequest(
            AssignmentId: assignmentId,
            DocumentRevisionId: revId,
            ProgressPercentage: 50
        );
        var progressRes1 = await ackController.RecordReadingProgress(assignmentId, progressReq50);
        Assert.IsType<OkObjectResult>(progressRes1);

        var assignmentAfterProgress1 = await db.DocumentTrainingAssignments.FindAsync(assignmentId);
        Assert.Equal(TrainingAssignmentStatus.Reading, assignmentAfterProgress1!.Status);

        // 3. Controlled reading progress updates to 100% - still NOT acknowledged
        var progressReq100 = new ReadingProgressUpdateRequest(
            AssignmentId: assignmentId,
            DocumentRevisionId: revId,
            ProgressPercentage: 100
        );
        var progressRes2 = await ackController.RecordReadingProgress(assignmentId, progressReq100);
        Assert.IsType<OkObjectResult>(progressRes2);

        var assignmentAfterProgress2 = await db.DocumentTrainingAssignments.FindAsync(assignmentId);
        Assert.Equal(TrainingAssignmentStatus.Reading, assignmentAfterProgress2!.Status);

        // 4. Retrieve context for conscious acknowledgement dialog
        var contextRes = await ackController.GetAcknowledgementContext(assignmentId);
        var okCtx = Assert.IsType<OkObjectResult>(contextRes);
        var ctxData = Assert.IsType<ApiResponse<AcknowledgementPresentationDto>>(okCtx.Value).Data;
        Assert.True(ctxData.CanAcknowledge);
        Assert.NotEmpty(ctxData.LegalStatementText);

        // 5. Submit conscious acknowledgement with mandatory confirmation
        var ackReq = new AcknowledgementSubmissionRequest(
            AssignmentId: assignmentId,
            DocumentRevisionId: revId,
            ConfirmedLegalStatement: true,
            Comments: "Controlled WP6 End-to-End Reading List Verification"
        );
        var ackRes = await ackController.SubmitAcknowledgement(assignmentId, ackReq);
        var okAck = Assert.IsType<OkObjectResult>(ackRes);
        var ackData = Assert.IsType<ApiResponse<AcknowledgementResultDto>>(okAck.Value).Data;
        Assert.Equal(TrainingAssignmentStatus.Acknowledged, ackData.Status);

        // 6. Refresh My Reading List: verify state transitioned to Acknowledged
        var myAssignmentsRefreshed = await trainingController.GetMyAssignments();
        var ok2 = Assert.IsType<OkObjectResult>(myAssignmentsRefreshed);
        var refreshedData = Assert.IsType<ApiResponse<IReadOnlyList<DocumentTrainingAssignmentDto>>>(ok2.Value).Data;
        var completedAssignment = Assert.Single(refreshedData, a => a.Id == assignmentId);
        Assert.Equal(TrainingAssignmentStatus.Acknowledged, completedAssignment.Status);
        Assert.NotNull(completedAssignment.AcknowledgedAtUtc);
    }

    [PostgresFact]
    public async Task Postgres_TrainingMatrix_And_ComplianceKpis_CalculatesAccurately()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, revId, assignmentId) = await SeedScenarioAsync(db);

        var matrixService = new TrainingMatrixService(db, NullLogger<TrainingMatrixService>.Instance);
        var controller = new DocumentTrainingMatrixController(matrixService)
        {
            ControllerContext = CreateContext(_fixture.SeededUserId, nameof(RoleType.SystemAdministrator))
        };

        // 1. Query Matrix Grid
        var gridRes = await controller.GetTrainingMatrixGrid(new TrainingMatrixFilterDto());
        var okGrid = Assert.IsType<OkObjectResult>(gridRes);
        var gridData = Assert.IsType<ApiResponse<TrainingMatrixGridDto>>(okGrid.Value).Data;

        Assert.True(gridData.TotalUsers >= 1);
        Assert.True(gridData.TotalDocuments >= 1);
        Assert.Contains(gridData.Documents, d => d.DocumentMasterId == masterId);

        // 2. Query KPIs
        var kpisRes = await controller.GetComplianceKpis();
        var okKpis = Assert.IsType<OkObjectResult>(kpisRes);
        var kpisData = Assert.IsType<ApiResponse<ComplianceKpiSummaryDto>>(okKpis.Value).Data;

        Assert.True(kpisData.TotalTrackedUsers >= 1);
        Assert.True(kpisData.TotalEffectiveDocuments >= 1);
        Assert.True(kpisData.OverallComplianceRatePercentage >= 0.0);

        // 3. Query Document Compliance Drilldown
        var docRes = await controller.GetDocumentCompliance(masterId);
        var okDoc = Assert.IsType<OkObjectResult>(docRes);
        var docData = Assert.IsType<ApiResponse<DocumentComplianceDetailDto>>(okDoc.Value).Data;
        Assert.Equal(masterId, docData.DocumentMasterId);

        // 4. Query User Compliance Drilldown
        var userRes = await controller.GetUserCompliance(_fixture.SeededUserId);
        var okUser = Assert.IsType<OkObjectResult>(userRes);
        var userData = Assert.IsType<ApiResponse<UserComplianceDetailDto>>(okUser.Value).Data;
        Assert.Equal(_fixture.SeededUserId, userData.UserId);
    }
}

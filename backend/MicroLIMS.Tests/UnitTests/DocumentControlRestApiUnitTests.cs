using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlRestApiUnitTests
{
    private class TestDbSequenceHelper : IDatabaseSequenceHelper
    {
        private long _seq = 8000;
        public Task<long> GetNextSequenceValueAsync(string sequenceName, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(System.Threading.Interlocked.Increment(ref _seq));
        }
    }

    private static ControllerContext CreateControllerContext(int userId, string roleName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"user{userId}"),
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
        MicroLimsDbContext db,
        User admin,
        User analyst1,
        User analyst2,
        DocumentMaster master,
        DocumentRevision rev1,
        DocumentTrainingAssignment assignment1,
        DocumentTrainingAssignmentController assignmentController,
        DocumentAcknowledgementController ackController,
        DocumentEscalationController escController
    )> SetupTestEnvironmentAsync()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new MicroLimsDbContext(options);

        var adminRole = new Role { Type = RoleType.SystemAdministrator, Name = nameof(RoleType.SystemAdministrator), IsActive = true };
        var analystRole = new Role { Type = RoleType.Analyst, Name = nameof(RoleType.Analyst), IsActive = true };
        db.Roles.AddRange(adminRole, analystRole);
        await db.SaveChangesAsync();

        var admin = new User { FullName = "Admin User", Username = "admin", RoleId = adminRole.Id, IsActive = true, Role = adminRole };
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
            MicroLimsDocumentId = "DOC-API-0001",
            CompanyDocumentCode = "SOP-QC-001",
            Title = "Sample Testing Procedure",
            DocumentTypeId = docType.Id,
            DepartmentId = docDept.Id,
            DocumentOwnerUserId = admin.Id,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedByUserId = admin.Id
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
            CreatedByUserId = admin.Id
        };
        db.DocumentRevisions.Add(rev1);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = rev1.Id;
        await db.SaveChangesAsync();

        var assignment1 = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev1.Id,
            AssignedUserId = analyst1.Id,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = DateTime.UtcNow.AddDays(-5),
            DueDateUtc = DateTime.UtcNow.AddDays(9),
            CreatedByUserId = admin.Id
        };
        db.DocumentTrainingAssignments.Add(assignment1);
        await db.SaveChangesAsync();

        var audit = new AuditEventService(db, new TestDbSequenceHelper());
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);
        var ackService = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);
        var escService = new DocumentEscalationService(db, audit, NullLogger<DocumentEscalationService>.Instance);

        var assignmentController = new DocumentTrainingAssignmentController(trainingService)
        {
            ControllerContext = CreateControllerContext(admin.Id, nameof(RoleType.SystemAdministrator))
        };

        var ackController = new DocumentAcknowledgementController(ackService)
        {
            ControllerContext = CreateControllerContext(analyst1.Id, nameof(RoleType.Analyst))
        };

        var escController = new DocumentEscalationController(escService)
        {
            ControllerContext = CreateControllerContext(admin.Id, nameof(RoleType.SystemAdministrator))
        };

        return (db, admin, analyst1, analyst2, master, rev1, assignment1, assignmentController, ackController, escController);
    }

    [Fact]
    public async Task GetAssignments_ReturnsPagedResults()
    {
        var (_, _, _, _, _, _, _, controller, _, _) = await SetupTestEnvironmentAsync();

        var result = await controller.GetAssignments(new DocumentTrainingAssignmentFilter());
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiRes = Assert.IsType<ApiResponse<PagedResult<DocumentTrainingAssignmentDto>>>(okResult.Value);

        Assert.True(apiRes.Success);
        Assert.Single(apiRes.Data.Items);
        Assert.Equal("SOP-QC-001", apiRes.Data.Items[0].CompanyDocumentCode);
    }

    [Fact]
    public async Task GetAssignmentById_ReturnsDetails_ForAuthorizedUser()
    {
        var (_, _, analyst1, _, _, _, assignment1, controller, _, _) = await SetupTestEnvironmentAsync();

        controller.ControllerContext = CreateControllerContext(analyst1.Id, nameof(RoleType.Analyst));

        var result = await controller.GetAssignmentById(assignment1.Id);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiRes = Assert.IsType<ApiResponse<DocumentTrainingAssignmentDto>>(okResult.Value);

        Assert.Equal(assignment1.Id, apiRes.Data.Id);
        Assert.Equal("DOC-API-0001", apiRes.Data.MicroLimsDocumentId);
    }

    [Fact]
    public async Task GetAssignmentById_RejectsAccess_ForUnauthorizedUser()
    {
        var (_, _, _, analyst2, _, _, assignment1, controller, _, _) = await SetupTestEnvironmentAsync();

        // Analyst 2 attempting to view Analyst 1's assignment
        controller.ControllerContext = CreateControllerContext(analyst2.Id, nameof(RoleType.Analyst));

        var result = await controller.GetAssignmentById(assignment1.Id);
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetMyAssignments_ReturnsOnlyAuthenticatedUserAssignments()
    {
        var (_, _, analyst1, _, _, _, assignment1, controller, _, _) = await SetupTestEnvironmentAsync();

        controller.ControllerContext = CreateControllerContext(analyst1.Id, nameof(RoleType.Analyst));

        var result = await controller.GetMyAssignments();
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiRes = Assert.IsType<ApiResponse<IReadOnlyList<DocumentTrainingAssignmentDto>>>(okResult.Value);

        Assert.Single(apiRes.Data);
        Assert.Equal(analyst1.Id, apiRes.Data[0].AssignedUserId);
    }

    [Fact]
    public async Task ManualAssign_CreatesAssignments_WhenAdmin()
    {
        var (_, admin, _, analyst2, _, rev1, _, controller, _, _) = await SetupTestEnvironmentAsync();

        var request = new ManualTrainingAssignmentRequest(
            DocumentRevisionId: rev1.Id,
            UserIds: new List<int> { analyst2.Id },
            AssignmentReason: "API Unit Test Assignment"
        );

        var result = await controller.AssignToUsers(request);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiRes = Assert.IsType<ApiResponse<TrainingAssignmentGenerationResultDto>>(okResult.Value);

        Assert.Equal(1, apiRes.Data.NewAssignmentsCreated);
    }

    [Fact]
    public async Task ManualAssign_Rejects_WhenNotAdmin()
    {
        var (_, _, analyst1, analyst2, _, rev1, _, controller, _, _) = await SetupTestEnvironmentAsync();

        controller.ControllerContext = CreateControllerContext(analyst1.Id, nameof(RoleType.Analyst));

        var request = new ManualTrainingAssignmentRequest(
            DocumentRevisionId: rev1.Id,
            UserIds: new List<int> { analyst2.Id }
        );

        var result = await controller.AssignToUsers(request);
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task SubmitAcknowledgement_ThroughApi_Succeeds_AndRecordsEvidence()
    {
        var (db, _, analyst1, _, _, rev1, assignment1, _, ackController, _) = await SetupTestEnvironmentAsync();

        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: true,
            Comments: "API test acknowledgement"
        );

        var result = await ackController.SubmitAcknowledgement(assignment1.Id, request);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiRes = Assert.IsType<ApiResponse<AcknowledgementResultDto>>(okResult.Value);

        Assert.Equal(assignment1.Id, apiRes.Data.AssignmentId);
        Assert.Equal(TrainingAssignmentStatus.Acknowledged, apiRes.Data.Status);

        var record = await db.DocumentAcknowledgementRecords.FirstOrDefaultAsync(r => r.DocumentTrainingAssignmentId == assignment1.Id);
        Assert.NotNull(record);
        Assert.Equal(analyst1.Id, record.AcknowledgedByUserId);
    }

    [Fact]
    public async Task SubmitAcknowledgement_RejectsUnconfirmedStatement()
    {
        var (_, _, _, _, _, rev1, assignment1, _, ackController, _) = await SetupTestEnvironmentAsync();

        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: false // Unconfirmed!
        );

        var result = await ackController.SubmitAcknowledgement(assignment1.Id, request);
        var badReq = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badReq.Value);
    }

    [Fact]
    public async Task RecordReadingProgress_UpdatesProgressWithoutAcknowledging()
    {
        var (db, _, _, _, _, rev1, assignment1, _, ackController, _) = await SetupTestEnvironmentAsync();

        var request = new ReadingProgressUpdateRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev1.Id,
            ProgressPercentage: 100
        );

        var result = await ackController.RecordReadingProgress(assignment1.Id, request);
        var okResult = Assert.IsType<OkObjectResult>(result);

        var updated = await db.DocumentTrainingAssignments.FindAsync(assignment1.Id);
        Assert.Equal(TrainingAssignmentStatus.Reading, updated!.Status); // Must remain Reading!
    }

    [Fact]
    public async Task ProcessDueEscalations_ThroughApi_GeneratesEscalations_WhenAdmin()
    {
        var (db, _, _, _, _, _, assignment1, _, _, escController) = await SetupTestEnvironmentAsync();

        assignment1.DueDateUtc = DateTime.UtcNow.AddDays(-2);
        await db.SaveChangesAsync();

        var result = await escController.ProcessDueEscalations();
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiRes = Assert.IsType<ApiResponse<EscalationProcessingResultDto>>(okResult.Value);

        Assert.True(apiRes.Data.TotalEscalationsCreated >= 1);
    }

    [Fact]
    public async Task ResolveEscalation_ThroughApi_UpdatesStatus_WhenAdmin()
    {
        var (db, admin, _, _, _, _, assignment1, _, _, escController) = await SetupTestEnvironmentAsync();

        assignment1.DueDateUtc = DateTime.UtcNow.AddDays(-2);
        await db.SaveChangesAsync();

        await escController.ProcessDueEscalations();

        var escalation = await db.DocumentEscalationRecords.FirstAsync();

        var resolveReq = new ResolveEscalationRequest(
            EscalationRecordId: escalation.Id,
            ResolutionReason: "Manager approved 24hr extension"
        );

        var result = await escController.ResolveEscalation(resolveReq);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiRes = Assert.IsType<ApiResponse<DocumentEscalationSummaryDto>>(okResult.Value);

        Assert.Equal(DocumentEscalationStatus.Resolved, apiRes.Data.Status);
        Assert.Equal(admin.Id, apiRes.Data.ResolvedByUserId);
    }
}

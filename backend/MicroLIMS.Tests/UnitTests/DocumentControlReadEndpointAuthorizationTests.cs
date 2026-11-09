using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Controllers.DocumentControl;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The training-compliance read endpoints must apply the same "everyone's data
// vs. own data" rule as their siblings, and PeriodicReviewController must
// never attribute a request without an identity to a real user (the old
// fallback silently used user 1).
public class DocumentControlReadEndpointAuthorizationTests
{
    private static ControllerContext Context(int? userId, string role)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        claims.AddRange(TestPrincipals.RoleAndPermissionClaims(role));
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };
    }

    // ---- Training matrix: KPIs and document drilldown ----

    [Fact]
    public async Task GetComplianceKpis_CallerWithoutTrainingPermissions_ReturnsForbidden()
    {
        var controller = new DocumentTrainingMatrixController(new TestTrainingMatrixService())
        {
            ControllerContext = Context(10, nameof(RoleType.Analyst))
        };

        var res = await controller.GetComplianceKpis();

        Assert.Equal(403, Assert.IsType<ObjectResult>(res).StatusCode);
    }

    [Theory]
    [InlineData(nameof(RoleType.SectionHead))] // holds Documents.TrainingAssign
    [InlineData(nameof(RoleType.Reviewer))]    // holds Documents.TrainingViewMatrix only
    public async Task GetComplianceKpis_PrivilegedCaller_ReturnsKpis(string role)
    {
        var controller = new DocumentTrainingMatrixController(new TestTrainingMatrixService())
        {
            ControllerContext = Context(1, role)
        };

        var res = await controller.GetComplianceKpis();

        Assert.IsType<OkObjectResult>(res);
    }

    [Fact]
    public async Task GetDocumentCompliance_CallerWithoutTrainingPermissions_ReturnsForbidden()
    {
        var controller = new DocumentTrainingMatrixController(new TestTrainingMatrixService())
        {
            ControllerContext = Context(10, nameof(RoleType.Analyst))
        };

        var res = await controller.GetDocumentCompliance(100);

        Assert.Equal(403, Assert.IsType<ObjectResult>(res).StatusCode);
    }

    [Fact]
    public async Task GetDocumentCompliance_ViewMatrixCaller_ReturnsDetail()
    {
        var controller = new DocumentTrainingMatrixController(new TestTrainingMatrixService())
        {
            ControllerContext = Context(7, nameof(RoleType.Reviewer))
        };

        var res = await controller.GetDocumentCompliance(100);

        Assert.IsType<OkObjectResult>(res);
    }

    // ---- Escalation history ----

    private sealed class StubEscalationService : IDocumentEscalationService
    {
        public List<DocumentEscalationSummaryDto> History { get; } = new();

        public Task<IReadOnlyList<DocumentEscalationSummaryDto>> GetAssignmentEscalationHistoryAsync(int assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DocumentEscalationSummaryDto>>(History);

        public Task<EscalationProcessingResultDto> ProcessDueEscalationsAsync(DateTime? utcNowOverride = null, string processName = "", CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OverdueTrainingSummaryDto> GetOverdueTrainingSummaryAsync(DateTime? utcNowOverride = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DocumentEscalationSummaryDto> ResolveEscalationAsync(ResolveEscalationRequest request, int actingUserId, DateTime? utcNowOverride = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static DocumentEscalationSummaryDto Record(int assignedUserId)
    {
        var when = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new DocumentEscalationSummaryDto(
            Id: 1, DocumentTrainingAssignmentId: 5, DocumentMasterId: 2, CompanyDocumentCode: "SOP-001", DocumentTitle: "SOP",
            DocumentRevisionId: 3, RevisionNumber: "01", AssignedUserId: assignedUserId, AssignedUserName: "Trainee",
            DueDateUtc: when, EscalationLevel: DocumentEscalationLevel.Overdue, Status: default,
            ScheduledTriggerUtc: when, ExecutedAtUtc: when,
            RecipientRoleOrTarget: "User", EscalationReason: "Overdue", ResolvedAtUtc: null, ResolvedByUserId: null,
            ResolvedByUserName: null, ResolutionReason: null, ProcessName: "Test");
    }

    [Fact]
    public async Task GetAssignmentEscalationHistory_OtherUsersAssignment_NonPrivileged_ReturnsForbidden()
    {
        var service = new StubEscalationService();
        service.History.Add(Record(assignedUserId: 99));
        var controller = new DocumentEscalationController(service) { ControllerContext = Context(10, nameof(RoleType.Analyst)) };

        var res = await controller.GetAssignmentEscalationHistory(5);

        Assert.Equal(403, Assert.IsType<ObjectResult>(res).StatusCode);
    }

    [Fact]
    public async Task GetAssignmentEscalationHistory_OwnAssignment_NonPrivileged_ReturnsHistory()
    {
        var service = new StubEscalationService();
        service.History.Add(Record(assignedUserId: 10));
        var controller = new DocumentEscalationController(service) { ControllerContext = Context(10, nameof(RoleType.Analyst)) };

        var res = await controller.GetAssignmentEscalationHistory(5);

        Assert.IsType<OkObjectResult>(res);
    }

    [Fact]
    public async Task GetAssignmentEscalationHistory_AnyAssignment_Privileged_ReturnsHistory()
    {
        var service = new StubEscalationService();
        service.History.Add(Record(assignedUserId: 99));
        var controller = new DocumentEscalationController(service) { ControllerContext = Context(1, nameof(RoleType.SectionHead)) };

        var res = await controller.GetAssignmentEscalationHistory(5);

        Assert.IsType<OkObjectResult>(res);
    }

    // ---- PeriodicReviewController identity ----

    private sealed class StubPeriodicReviewService : IPeriodicReviewService
    {
        public int? LastUserId { get; private set; }

        public Task<PeriodicReviewTaskDto> GetReviewTaskByIdAsync(int taskId, int userId)
        {
            LastUserId = userId;
            return Task.FromResult<PeriodicReviewTaskDto>(null!);
        }

        public Task<PeriodicReviewGenerationResultDto> GenerateDueReviewTasksAsync(DateTime? utcNowOverride = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<PeriodicReviewTaskDto>> GetReviewTasksAsync(int? masterId = null, int? revisionId = null, PeriodicReviewTaskStatus? status = null, int? reviewerUserId = null) => throw new NotSupportedException();
        public Task<PeriodicReviewWorkspaceDto> GetReviewWorkspaceAsync(int taskId, int userId) => throw new NotSupportedException();
        public Task<PeriodicReviewFindingDto> AddFindingAsync(int taskId, CreatePeriodicReviewFindingRequest request, int userId) => throw new NotSupportedException();
        public Task<PeriodicReviewFindingDto> ResolveFindingAsync(int taskId, int findingId, int userId) => throw new NotSupportedException();
        public Task<PeriodicReviewTaskDto> AssignReviewerAsync(int taskId, AssignPeriodicReviewerRequest request, int userId) => throw new NotSupportedException();
        public Task<PeriodicReviewTaskDto> CompleteReviewAsync(int taskId, CompletePeriodicReviewRequest request, int userId) => throw new NotSupportedException();
        public Task<List<PeriodicReviewTaskDto>> GetMasterReviewHistoryAsync(int masterId, int userId) => throw new NotSupportedException();
    }

    [Fact]
    public async Task PeriodicReview_MissingNameIdentifierClaim_IsRejected_NotAttributedToUserOne()
    {
        var service = new StubPeriodicReviewService();
        var controller = new PeriodicReviewController(service) { ControllerContext = Context(null, nameof(RoleType.SectionHead)) };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetReviewTaskById(3));

        Assert.Null(service.LastUserId);
    }

    [Fact]
    public async Task PeriodicReview_WithNameIdentifierClaim_PassesThatUserToService()
    {
        var service = new StubPeriodicReviewService();
        var controller = new PeriodicReviewController(service) { ControllerContext = Context(77, nameof(RoleType.SectionHead)) };

        await controller.GetReviewTaskById(3);

        Assert.Equal(77, service.LastUserId);
    }
}

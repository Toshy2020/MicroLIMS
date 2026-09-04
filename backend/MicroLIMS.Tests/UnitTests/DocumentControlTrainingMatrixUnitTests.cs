using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Controllers.DocumentControl;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class TestTrainingMatrixService : ITrainingMatrixService
{
    public TrainingMatrixFilterDto? LastGridFilter { get; private set; }

    public Task<TrainingMatrixGridDto> GetTrainingMatrixGridAsync(TrainingMatrixFilterDto filter, CancellationToken cancellationToken = default)
    {
        LastGridFilter = filter;
        return Task.FromResult(new TrainingMatrixGridDto(
            Users: new List<TrainingMatrixUserHeaderDto>(),
            Documents: new List<TrainingMatrixDocumentHeaderDto>(),
            Cells: new List<TrainingMatrixCellDto>(),
            TotalUsers: filter.UserId.HasValue ? 1 : 10,
            TotalDocuments: 5,
            TotalCompliantCells: 30,
            TotalPendingCells: 15,
            TotalOverdueCells: 5,
            TotalSupersededGapCells: 0
        ));
    }

    public Task<ComplianceKpiSummaryDto> GetComplianceKpisAsync(int? departmentId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ComplianceKpiSummaryDto(
            TotalTrackedUsers: 25,
            TotalEffectiveDocuments: 12,
            TotalRequiredAssignments: 100,
            CompletedAssignments: 85,
            PendingAssignments: 10,
            OverdueAssignments: 5,
            SupersededGapCount: 2,
            OverallComplianceRatePercentage: 85.0,
            DepartmentCompliance: new List<DepartmentComplianceKpiDto>(),
            TopOverdueDocuments: new List<TopOverdueDocumentKpiDto>()
        ));
    }

    public Task<UserComplianceDetailDto?> GetUserComplianceDetailAsync(int userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<UserComplianceDetailDto?>(new UserComplianceDetailDto(
            UserId: userId,
            FullName: "Test User",
            Username: "testuser",
            RoleName: "Analyst",
            DepartmentName: "QC",
            TotalRequiredDocuments: 10,
            CompletedDocuments: 8,
            PendingDocuments: 2,
            OverdueDocuments: 0,
            SupersededGapDocuments: 0,
            ComplianceRatePercentage: 80.0,
            DocumentStatuses: new List<TrainingMatrixCellDto>()
        ));
    }

    public Task<DocumentComplianceDetailDto?> GetDocumentComplianceDetailAsync(int documentMasterId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<DocumentComplianceDetailDto?>(new DocumentComplianceDetailDto(
            DocumentMasterId: documentMasterId,
            CompanyDocumentCode: "SOP-MIC-001",
            MicroLimsDocumentId: "DOC-2026-0001",
            Title: "Sample Prep SOP",
            CurrentEffectiveRevisionId: 1,
            CurrentEffectiveRevisionNumber: "01",
            AssignedUserCount: 15,
            CompletedUserCount: 12,
            PendingUserCount: 2,
            OverdueUserCount: 1,
            SupersededGapUserCount: 0,
            UserStatuses: new List<TrainingMatrixCellDto>()
        ));
    }
}

public class DocumentControlTrainingMatrixUnitTests
{
    private static ControllerContext CreateContext(int userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    [Fact]
    public async Task GetTrainingMatrixGrid_AdminUser_ReturnsFullGrid()
    {
        var stubService = new TestTrainingMatrixService();
        var controller = new DocumentTrainingMatrixController(stubService)
        {
            ControllerContext = CreateContext(1, nameof(RoleType.SystemAdministrator))
        };

        var res = await controller.GetTrainingMatrixGrid(new TrainingMatrixFilterDto());
        var ok = Assert.IsType<OkObjectResult>(res);
        var data = Assert.IsType<ApiResponse<TrainingMatrixGridDto>>(ok.Value).Data;

        Assert.Equal(10, data.TotalUsers);
        Assert.Equal(5, data.TotalDocuments);
    }

    [Fact]
    public async Task GetTrainingMatrixGrid_NonAdminUser_ScopesToCurrentUserId()
    {
        var stubService = new TestTrainingMatrixService();
        var controller = new DocumentTrainingMatrixController(stubService)
        {
            ControllerContext = CreateContext(42, nameof(RoleType.Analyst))
        };

        var res = await controller.GetTrainingMatrixGrid(new TrainingMatrixFilterDto());
        Assert.IsType<OkObjectResult>(res);
        Assert.NotNull(stubService.LastGridFilter);
        Assert.Equal(42, stubService.LastGridFilter!.UserId);
    }

    [Fact]
    public async Task GetComplianceKpis_ReturnsCalculatedKpis()
    {
        var stubService = new TestTrainingMatrixService();
        var controller = new DocumentTrainingMatrixController(stubService)
        {
            ControllerContext = CreateContext(1, nameof(RoleType.SystemAdministrator))
        };

        var res = await controller.GetComplianceKpis();
        var ok = Assert.IsType<OkObjectResult>(res);
        var data = Assert.IsType<ApiResponse<ComplianceKpiSummaryDto>>(ok.Value).Data;

        Assert.Equal(85.0, data.OverallComplianceRatePercentage);
        Assert.Equal(5, data.OverdueAssignments);
        Assert.Equal(2, data.SupersededGapCount);
    }

    [Fact]
    public async Task GetUserCompliance_NonAdmin_OtherUser_ReturnsForbidden()
    {
        var stubService = new TestTrainingMatrixService();
        var controller = new DocumentTrainingMatrixController(stubService)
        {
            ControllerContext = CreateContext(10, nameof(RoleType.Analyst))
        };

        var res = await controller.GetUserCompliance(99);
        var statusRes = Assert.IsType<ObjectResult>(res);
        Assert.Equal(403, statusRes.StatusCode);
    }

    [Fact]
    public async Task GetDocumentCompliance_AdminUser_ReturnsDocumentDetail()
    {
        var stubService = new TestTrainingMatrixService();
        var controller = new DocumentTrainingMatrixController(stubService)
        {
            ControllerContext = CreateContext(1, nameof(RoleType.SectionHead))
        };

        var res = await controller.GetDocumentCompliance(100);
        var ok = Assert.IsType<OkObjectResult>(res);
        var data = Assert.IsType<ApiResponse<DocumentComplianceDetailDto>>(ok.Value).Data;

        Assert.Equal("SOP-MIC-001", data.CompanyDocumentCode);
        Assert.Equal(15, data.AssignedUserCount);
        Assert.Equal(12, data.CompletedUserCount);
    }
}

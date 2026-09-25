using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// LabScope.Narrow: the workspace page picks one laboratory (?labSectionId=)
// out of a user's accessible sections. See LabScope.cs for the "why".
public class LabScopeTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public void Narrow_NoLabRequested_ReturnsScopeUnchanged()
    {
        IReadOnlyList<int> scope = new[] { 1, 2 };

        var result = LabScope.Narrow(scope, null);

        Assert.Same(scope, result);
    }

    [Fact]
    public void Narrow_LabRequested_ScopeNull_Admin_ReturnsJustThatLab()
    {
        var result = LabScope.Narrow(null, 2);

        Assert.Equal(new[] { 2 }, result);
    }

    [Fact]
    public void Narrow_LabRequested_LabInScope_ReturnsJustThatLab()
    {
        IReadOnlyList<int> scope = new[] { 1, 2 };

        var result = LabScope.Narrow(scope, 2);

        Assert.Equal(new[] { 2 }, result);
    }

    [Fact]
    public void Narrow_LabRequested_LabNotInScope_Throws()
    {
        IReadOnlyList<int> scope = new[] { 1 };

        Assert.Throws<UnauthorizedAccessException>(() => LabScope.Narrow(scope, 2));
    }

    private static async Task<int> SeedUserAsync(MicroLimsDbContext db, int id, RoleType type, int departmentId, int? sectionId)
    {
        var role = new Role { Type = type, Name = type.ToString() + id };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.Users.Add(new User { Id = id, FullName = $"User {id}", Username = $"user{id}", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword("Correct-Horse-1!") });
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = id, DepartmentId = departmentId, SectionId = sectionId });
        await db.SaveChangesAsync();
        return id;
    }

    // A user who belongs to both labs (department-level membership, no
    // SectionId) asks the workspace for FP only: the mixed sample still
    // shows up (it has an FP test order), but its AssignedTests must be
    // narrowed to the FP order alone - not also carry the Micro order.
    [Fact]
    public async Task GetActiveSamplesAsync_UserInBothLabs_FilterByLabSectionId_ReturnsOnlyThatLabsAssignedTests()
    {
        await using var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        await db.SaveChangesAsync();

        // Department-level membership (SectionId null): accessible to both sections.
        var userId = await SeedUserAsync(db, 1, RoleType.Analyst, micro.DepartmentId, null);

        var cause = new CauseOfTesting { Name = "Routine", IsActive = true };
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-MIX-1", Status = SampleStatus.InTesting, CauseOfTesting = cause };
        var microOrder = new TestOrder { SectionId = micro.Id, TestCode = "TAMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running };
        var fpOrder = new TestOrder { SectionId = fp.Id, TestCode = "ASSAY", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running };
        sample.TestOrders.AddRange(new[] { microOrder, fpOrder });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var scope = new UserSectionScopeService(db);
        // Confirm the user really is in both labs before narrowing.
        var fullScope = await scope.GetAccessibleSectionIdsAsync(userId);
        Assert.Equal(new[] { fp.Id, micro.Id }.OrderBy(x => x), fullScope!.OrderBy(x => x));

        var service = TestServiceFactory.TestingWorkspace(db, scope);
        var filter = new TestingWorkspaceFilterDto { LabSectionId = fp.Id };

        var result = await service.GetActiveSamplesAsync(filter, userId);

        var dto = Assert.Single(result.Items);
        Assert.Equal(sample.Id, dto.SampleId);
        var assignedTest = Assert.Single(dto.AssignedTests);
        Assert.Equal(fp.Id, assignedTest.SectionId);
    }

    [Fact]
    public async Task GetActiveSamplesAsync_UserInOnlyMicro_FilterByFp_Throws()
    {
        await using var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        await db.SaveChangesAsync();

        var userId = await SeedUserAsync(db, 1, RoleType.Analyst, micro.DepartmentId, micro.Id);

        var service = TestServiceFactory.TestingWorkspace(db, new UserSectionScopeService(db));
        var filter = new TestingWorkspaceFilterDto { LabSectionId = fp.Id };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetActiveSamplesAsync(filter, userId));
    }

    [Fact]
    public async Task GetWorkloadCountsAsync_UserInBothLabs_FilterByLabSectionId_CountsOnlyThatLab()
    {
        await using var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        await db.SaveChangesAsync();

        var userId = await SeedUserAsync(db, 1, RoleType.Analyst, micro.DepartmentId, null);

        var cause = new CauseOfTesting { Name = "Routine", IsActive = true };
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-MIX-2", Status = SampleStatus.InTesting, CauseOfTesting = cause };
        var microOrder = new TestOrder { SectionId = micro.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = userId };
        var fpOrder = new TestOrder { SectionId = fp.Id, TestCode = "ASSAY", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = userId };
        sample.TestOrders.AddRange(new[] { microOrder, fpOrder });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.TestingWorkspace(db, new UserSectionScopeService(db));

        var fpCounts = await service.GetWorkloadCountsAsync(userId, fp.Id);
        Assert.Equal(1, fpCounts.Mine);

        var microCounts = await service.GetWorkloadCountsAsync(userId, micro.Id);
        Assert.Equal(1, microCounts.Mine);
    }

    // Task 13c (controller ruling R9, from Task 13a's finding #3): a card
    // refreshed inside a lab workspace via GET /testorders/{id} must not
    // leak the caller's other lab's tests onto it - same narrowing as the
    // paged list/counts above, applied to the single-sample fetch.
    [Fact]
    public async Task GetSampleAsync_UserInBothLabs_FilterByLabSectionId_ReturnsOnlyThatLabsAssignedTests()
    {
        await using var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        await db.SaveChangesAsync();

        var userId = await SeedUserAsync(db, 1, RoleType.Analyst, micro.DepartmentId, null);

        var cause = new CauseOfTesting { Name = "Routine", IsActive = true };
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-MIX-3", Status = SampleStatus.InTesting, CauseOfTesting = cause };
        var microOrder = new TestOrder { SectionId = micro.Id, TestCode = "TAMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running };
        var fpOrder = new TestOrder { SectionId = fp.Id, TestCode = "ASSAY", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running };
        sample.TestOrders.AddRange(new[] { microOrder, fpOrder });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.TestingWorkspace(db, new UserSectionScopeService(db));

        var dto = await service.GetSampleAsync(sample.Id, userId, fp.Id);

        Assert.NotNull(dto);
        var assignedTest = Assert.Single(dto!.AssignedTests);
        Assert.Equal(fp.Id, assignedTest.SectionId);
    }

    [Fact]
    public async Task GetSampleAsync_UserInOnlyMicro_FilterByFp_Throws()
    {
        await using var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        await db.SaveChangesAsync();

        var userId = await SeedUserAsync(db, 1, RoleType.Analyst, micro.DepartmentId, micro.Id);

        var cause = new CauseOfTesting { Name = "Routine", IsActive = true };
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-MIX-4", Status = SampleStatus.InTesting, CauseOfTesting = cause };
        var fpOrder = new TestOrder { SectionId = fp.Id, TestCode = "ASSAY", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running };
        sample.TestOrders.Add(fpOrder);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.TestingWorkspace(db, new UserSectionScopeService(db));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetSampleAsync(sample.Id, userId, fp.Id));
    }

    // No labSectionId: behaviour is unchanged from before Task 13c - the
    // caller's full accessible scope decides visibility/narrowing, and a
    // mixed sample's AssignedTests still carries both labs' orders.
    [Fact]
    public async Task GetSampleAsync_NoLabSectionId_ReturnsAllAccessibleAssignedTests()
    {
        await using var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        await db.SaveChangesAsync();

        var userId = await SeedUserAsync(db, 1, RoleType.Analyst, micro.DepartmentId, null);

        var cause = new CauseOfTesting { Name = "Routine", IsActive = true };
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-MIX-5", Status = SampleStatus.InTesting, CauseOfTesting = cause };
        var microOrder = new TestOrder { SectionId = micro.Id, TestCode = "TAMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running };
        var fpOrder = new TestOrder { SectionId = fp.Id, TestCode = "ASSAY", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running };
        sample.TestOrders.AddRange(new[] { microOrder, fpOrder });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.TestingWorkspace(db, new UserSectionScopeService(db));

        var dto = await service.GetSampleAsync(sample.Id, userId);

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.AssignedTests.Count);
    }
}

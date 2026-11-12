using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class TestWorkflowTypeGuardTests
{
    private const string Msg = "The workflow type of this test is set by its test type; change it in the test settings.";

    private static async Task<(MicroLimsDbContext db, TestWorkflowStepMasterDataService svc, int id, int userId)> SeedAsync(
        WorkflowType workflow, EquationType equation)
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { CurrentUserId = 1 };
        var section = TestServiceFactory.EnsureMicroSection(db);
        var test = new TestDefinition
        {
            Code = "T-" + Guid.NewGuid().ToString("N")[..5], DisplayName = "T", SectionId = section.Id,
            WorkflowType = workflow, EquationType = equation
        };
        db.TestDefinitions.Add(test);
        // The acting user is a member of the test's laboratory section.
        var role = new Role { Name = "Section Head", Type = RoleType.SectionHead, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { Username = "head_" + Guid.NewGuid().ToString("N")[..6], FullName = "Head", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return (db, new TestWorkflowStepMasterDataService(db, new UserSectionScopeService(db)), test.Id, user.Id);
    }

    [Fact]
    public async Task UpdateWorkflowType_CountTestToObservation_Works()
    {
        var (db, svc, id, userId) = await SeedAsync(WorkflowType.CountTest, EquationType.None);
        await svc.UpdateWorkflowTypeAsync(userId, id, new UpdateWorkflowTypeRequest(WorkflowType.Observation));
        Assert.Equal(WorkflowType.Observation, db.TestDefinitions.Single(t => t.Id == id).WorkflowType);
    }

    [Fact]
    public async Task UpdateWorkflowType_EquationBoundTest_Throws()
    {
        var (db, svc, id, userId) = await SeedAsync(WorkflowType.HplcMethodAssay, EquationType.HplcMethodAssay);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.UpdateWorkflowTypeAsync(userId, id, new UpdateWorkflowTypeRequest(WorkflowType.ElementalAssay)));
        Assert.Equal(Msg, ex.Message);
        Assert.Equal(WorkflowType.HplcMethodAssay, db.TestDefinitions.Single(t => t.Id == id).WorkflowType);
    }

    [Fact]
    public async Task UpdateWorkflowType_ToNonStepWorkflow_Throws()
    {
        var (db, svc, id, userId) = await SeedAsync(WorkflowType.CountTest, EquationType.None);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.UpdateWorkflowTypeAsync(userId, id, new UpdateWorkflowTypeRequest(WorkflowType.ElementalAssay)));
        Assert.Equal(Msg, ex.Message);
        Assert.Equal(WorkflowType.CountTest, db.TestDefinitions.Single(t => t.Id == id).WorkflowType);
    }
}

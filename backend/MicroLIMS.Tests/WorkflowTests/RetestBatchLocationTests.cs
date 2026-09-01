using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// EM/Water/After Cleaning retest must carry over ONLY the locations that
// actually failed on the original TestOrder - not every location assigned
// to it, and not a free re-pick from the whole department/machine/room
// catalog via the Preparation screen. See SampleApprovalService.
// CloneFailedLocationsAsync.
public class RetestBatchLocationTests
{
    private const string Password = "Correct-Horse-1!";

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<User> SeedUser(MicroLimsDbContext db, int id, RoleType roleType)
    {
        var role = new Role { Type = roleType, Name = roleType.ToString() };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { Id = id, FullName = $"User {id}", Username = $"user{id}", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // Three rooms, one TestCode (TAMC) TestOrder covering all three via
    // SampleLocation rows - two came back non-conforming, one conformed.
    private static async Task<(Sample sample, TestOrder order, SampleLocation failed1, SampleLocation failed2, SampleLocation passed)> SeedEmSampleUnderApprovalAsync(
        MicroLimsDbContext db, int analystId, int reviewerId)
    {
        db.CausesOfTesting.Add(new CauseOfTesting { Name = "Retest" });
        var department = new Department { Name = "Filling Suite", Class = "B", TestingFrequency = "Monthly" };
        db.Departments.Add(department);
        await db.SaveChangesAsync();

        var room1 = new Room { Name = "Room 1", DepartmentId = department.Id, GradeClassification = "B" };
        var room2 = new Room { Name = "Room 2", DepartmentId = department.Id, GradeClassification = "B" };
        var room3 = new Room { Name = "Room 3", DepartmentId = department.Id, GradeClassification = "B" };
        db.Rooms.AddRange(room1, room2, room3);
        await db.SaveChangesAsync();

        var config1 = new RoomTestConfiguration { RoomId = room1.Id, TestType = "PassiveAirSample", TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100", Unit = "CFU/plate/4 hours" };
        var config2 = new RoomTestConfiguration { RoomId = room2.Id, TestType = "PassiveAirSample", TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100", Unit = "CFU/plate/4 hours" };
        var config3 = new RoomTestConfiguration { RoomId = room3.Id, TestType = "PassiveAirSample", TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100", Unit = "CFU/plate/4 hours" };
        db.RoomTestConfigurations.AddRange(config1, config2, config3);
        await db.SaveChangesAsync();

        var cause = await db.CausesOfTesting.FirstAsync(c => c.Name == "Retest");

        var sample = new Sample
        {
            ReferenceNumber = "EM0826001",
            Category = SampleCategory.EnvironmentalMonitoring,
            DepartmentId = department.Id,
            ControlNumber = "CTRL-EM-1",
            SampledBy = "Sampler",
            ReceivedByUserId = analystId,
            ReceivedAt = DateTime.UtcNow,
            CauseOfTestingId = cause.Id,
            Status = SampleStatus.UnderApproval,
            PreparationStatus = SamplePreparationStatus.Ready,
            ReviewedByUserId = reviewerId,
            ReviewedAt = DateTime.UtcNow
        };
        var order = new TestOrder
        {
            TestCode = "TAMC",
            Status = ApprovalStatus.Reviewed,
            CurrentStep = WorkflowStep.Reviewed,
            AssignedAnalystId = analystId
        };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var failed1 = new SampleLocation { SampleId = sample.Id, TestOrderId = order.Id, LocationType = LocationType.Room, RoomTestConfigurationId = config1.Id, Status = "ActionLimitExceeded", CFUResult = 75 };
        var failed2 = new SampleLocation { SampleId = sample.Id, TestOrderId = order.Id, LocationType = LocationType.Room, RoomTestConfigurationId = config2.Id, Status = "OutOfSpecification", CFUResult = 150 };
        var passed = new SampleLocation { SampleId = sample.Id, TestOrderId = order.Id, LocationType = LocationType.Room, RoomTestConfigurationId = config3.Id, Status = "WithinLimits", CFUResult = 2 };
        db.SampleLocations.AddRange(failed1, failed2, passed);
        await db.SaveChangesAsync();

        return (sample, order, failed1, failed2, passed);
    }

    [Fact]
    public async Task RetestRetainedSample_EmBatchOrder_CarriesOnlyFailedLocationsAndSkipsPreparation()
    {
        await using var db = NewDb();
        await SeedUser(db, 1, RoleType.Analyst);
        await SeedUser(db, 2, RoleType.Reviewer);
        await SeedUser(db, 3, RoleType.SectionHead);

        var (origin, order, failed1, failed2, passed) = await SeedEmSampleUnderApprovalAsync(db, analystId: 1, reviewerId: 2);
        var approvalService = TestServiceFactory.SampleApproval(db);

        await approvalService.DecideAsync(origin.Id, sectionHeadUserId: 3, Password, ApprovalDecision.RetestRetainedSample,
            null, null, selectedTestOrderIds: new List<int> { order.Id });

        var retestSample = await db.Samples.SingleAsync(s => s.OriginSampleId == origin.Id);

        // Skips the checkbox Preparation screen entirely - the failed
        // locations are already attached, so re-picking from the room
        // catalog would only risk re-adding the room that already passed.
        Assert.Equal(SamplePreparationStatus.Ready, retestSample.PreparationStatus);

        var retestOrder = await db.TestOrders.SingleAsync(o => o.SampleId == retestSample.Id);
        Assert.Equal("TAMC", retestOrder.TestCode);

        var retestLocations = await db.SampleLocations.Where(l => l.TestOrderId == retestOrder.Id).ToListAsync();

        // Only the 2 failed rooms carried over - not the 3rd, passing one.
        Assert.Equal(2, retestLocations.Count);
        var carriedConfigIds = retestLocations.Select(l => l.RoomTestConfigurationId).ToHashSet();
        Assert.Contains(failed1.RoomTestConfigurationId, carriedConfigIds);
        Assert.Contains(failed2.RoomTestConfigurationId, carriedConfigIds);
        Assert.DoesNotContain(passed.RoomTestConfigurationId, carriedConfigIds);

        // Cloned locations are fresh - no leftover result/status from the
        // original failing reading.
        Assert.All(retestLocations, l =>
        {
            Assert.Null(l.Status);
            Assert.Null(l.CFUResult);
            Assert.Null(l.EnteredAt);
            Assert.Null(l.EnteredByUserId);
        });

        // The original order's locations are untouched (audit trail intact).
        var originalLocations = await db.SampleLocations.Where(l => l.TestOrderId == order.Id).ToListAsync();
        Assert.Equal(3, originalLocations.Count);
    }
}

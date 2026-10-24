using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The live figures on a laboratory section page's cards: each comes from a
// rule that already exists elsewhere, counts only that laboratory, and is
// computed only for cards the caller may open.
public class SectionCountersServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Today = Now.UtcDateTime.Date;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Records which workspace queues were asked for; the queue rules themselves
    // are TestingWorkspaceService's and are tested there.
    private sealed class StubWorkspace : ITestWorkspaceService
    {
        public readonly List<(int? Section, string? Area)> Calls = new();

        public Task<WorkspaceTileCountsDto> GetWorkloadCountsAsync(int? currentUserId = null, int? labSectionId = null, string? area = null)
        {
            Calls.Add((labSectionId, area));
            return Task.FromResult(new WorkspaceTileCountsDto { Overdue = area == "fp" ? 2 : 4, AwaitingReview = 0 });
        }

        public Task<MicroLIMS.Shared.Responses.PagedResult<SampleDto>> GetActiveSamplesAsync(TestingWorkspaceFilterDto filter, int? currentUserId = null) => throw new NotSupportedException();
        public Task<SampleDto?> GetSampleAsync(int id, int? currentUserId = null, int? labSectionId = null, string? area = null) => throw new NotSupportedException();
    }

    private sealed record Fixture(
        MicroLimsDbContext Db, DocumentSection Micro, DocumentSection Fp, User MicroAnalyst, User FpAnalyst, StubWorkspace Workspace)
    {
        public SectionCountersService Service => new(
            Db,
            new DashboardLabScopeService(Db, new UserSectionScopeService(Db)),
            new UserSectionScopeService(Db),
            Workspace,
            new LabClock(new FixedTimeProvider(Now), TimeZoneInfo.Utc),
            new FixedTimeProvider(Now));
    }

    private static readonly SectionCardAccess AllCards = new(Media: true, Cryovials: true, Materials: true, Equipment: true, Solutions: true);

    private static async Task<Fixture> SeedAsync()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var dept = new DocumentDepartment { Name = "Quality Control", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();
        var micro = new DocumentSection { Name = "Microbiology", Code = "MICRO", DepartmentId = dept.Id, IsActive = true };
        var fp = new DocumentSection { Name = "Physicochemical", Code = "FP", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(micro, fp);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var microAnalyst = new User { Username = "micro", FullName = "Micro Analyst", RoleId = role.Id, Role = role, IsActive = true };
        var fpAnalyst = new User { Username = "fp", FullName = "FP Analyst", RoleId = role.Id, Role = role, IsActive = true };
        db.Users.AddRange(microAnalyst, fpAnalyst);
        await db.SaveChangesAsync();
        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = microAnalyst.Id, DepartmentId = dept.Id, SectionId = micro.Id },
            // Finished product only: no RM & PM workspace card.
            new UserOrgMembership { UserId = fpAnalyst.Id, DepartmentId = dept.Id, SectionId = fp.Id, PhyschemArea = PhyschemArea.FinishedProduct });
        await db.SaveChangesAsync();

        return new Fixture(db, micro, fp, microAnalyst, fpAnalyst, new StubWorkspace());
    }

    private static Material Lot(DocumentSection section, string name, DateTime? expiry = null, decimal remaining = 10, decimal? minimum = null,
        MaterialType type = MaterialType.Chemical) => new()
    {
        SectionId = section.Id, MaterialName = name, BatchNumber = name, MaterialType = type,
        ReceivingDate = Today.AddMonths(-1), ExpiryDate = expiry, QuantityReceived = remaining, QuantityRemaining = remaining,
        MinimumStockLevel = minimum
    };

    private static int Figure(IReadOnlyList<SectionCardCountersDto> cards, string card, string label) =>
        cards.Single(c => c.Card == card).Counters.Single(x => x.Label == label).Value;

    [Fact]
    public async Task Microbiology_CountsItsOwnMediaCryovialsStockAndEquipment()
    {
        var f = await SeedAsync();
        var microBase = Lot(f.Micro, "TSA");
        var fpBase = Lot(f.Fp, "FP base");
        f.Db.Materials.AddRange(microBase, fpBase,
            Lot(f.Micro, "expired", expiry: Today.AddDays(-1)),
            Lot(f.Micro, "low", remaining: 2, minimum: 5),
            Lot(f.Micro, "empty", remaining: 0, minimum: 5),          // depleted, not "low"
            Lot(f.Fp, "fp expired", expiry: Today.AddDays(-3)));      // other laboratory
        await f.Db.SaveChangesAsync();

        f.Db.Media.AddRange(
            new Media { MaterialId = microBase.Id, LotNumber = "M1", Status = MediaStatus.Active, ExpiryDate = Today.AddDays(3) },
            new Media { MaterialId = microBase.Id, LotNumber = "M2", Status = MediaStatus.Active, ExpiryDate = Today.AddDays(30) },
            new Media { MaterialId = microBase.Id, LotNumber = "M3", Status = MediaStatus.Prepared, ExpiryDate = Today.AddDays(30) },
            new Media { MaterialId = fpBase.Id, LotNumber = "F1", Status = MediaStatus.Active, ExpiryDate = Today.AddDays(1) });
        f.Db.Cryovials.AddRange(
            new Cryovial { Code = "C1", MaterialId = microBase.Id, ApprovalStatus = ApprovalGateStatus.Approved, ExpiryDate = Today, VialsRemaining = 5 },
            new Cryovial { Code = "C2", MaterialId = microBase.Id, ApprovalStatus = ApprovalGateStatus.Approved, ExpiryDate = Today.AddDays(-1), VialsRemaining = 3 },
            new Cryovial { Code = "C3", MaterialId = microBase.Id, ApprovalStatus = ApprovalGateStatus.Approved, ExpiryDate = Today.AddDays(90), VialsRemaining = 4, IsDestroyed = true },
            new Cryovial { Code = "C4", MaterialId = microBase.Id, ApprovalStatus = ApprovalGateStatus.PendingReview, ExpiryDate = Today.AddDays(90), VialsRemaining = 6 });
        f.Db.EquipmentInventories.AddRange(
            new EquipmentInventory { Code = "E1", SectionId = f.Micro.Id, CalibrationDueDate = Today.AddDays(-2) },
            new EquipmentInventory { Code = "E2", SectionId = f.Micro.Id, CalibrationDueDate = Today.AddDays(-2), Status = EquipmentOperationalStatus.Retired },
            new EquipmentInventory { Code = "E3", SectionId = f.Micro.Id, CalibrationDueDate = Today, Status = EquipmentOperationalStatus.OutOfService },
            new EquipmentInventory { Code = "E4", SectionId = f.Fp.Id, CalibrationDueDate = Today.AddDays(-9) });
        await f.Db.SaveChangesAsync();

        var cards = await f.Service.GetAsync(f.MicroAnalyst.Id, "MICRO", AllCards);

        Assert.Equal(new[] { "workspace", "media", "cryovials", "materials", "equipment" }, cards.Select(c => c.Card));
        Assert.Equal((f.Micro.Id, (string?)null), f.Workspace.Calls.Single());
        Assert.Equal(4, Figure(cards, "workspace", "overdue"));

        Assert.Equal(2, Figure(cards, "media", "active lots"));
        Assert.Equal(1, Figure(cards, "media", "expiring within 7 days"));
        Assert.Equal(1, Figure(cards, "media", "awaiting release"));

        Assert.Equal(5, Figure(cards, "cryovials", "vials available"));   // C1 only: expires today, still usable
        Assert.Equal(1, Figure(cards, "cryovials", "awaiting approval"));

        Assert.Equal(1, Figure(cards, "materials", "expired"));
        Assert.Equal(1, Figure(cards, "materials", "low stock"));

        Assert.Equal(1, Figure(cards, "equipment", "calibration overdue")); // retired E2 and FP E4 excluded
        Assert.Equal(1, Figure(cards, "equipment", "out of service"));
    }

    [Fact]
    public async Task CardsTheCallerCannotOpen_AreNotComputed()
    {
        var f = await SeedAsync();

        var cards = await f.Service.GetAsync(f.MicroAnalyst.Id, "MICRO", new SectionCardAccess());

        Assert.Equal(new[] { "workspace" }, cards.Select(c => c.Card));
    }

    [Fact]
    public async Task Physicochemical_CountsRunsSolutionsAndWorkingStandards_AndOnlyTheUsersAreas()
    {
        var f = await SeedAsync();
        f.Db.Materials.AddRange(
            Lot(f.Fp, "WS expired", expiry: Today.AddDays(-1), type: MaterialType.WorkingStandard),
            Lot(f.Fp, "WS due", expiry: Today.AddDays(30), type: MaterialType.WorkingStandard),
            Lot(f.Fp, "WS valid", expiry: Today.AddDays(31), type: MaterialType.WorkingStandard),
            Lot(f.Fp, "WS used up", expiry: Today.AddDays(-5), remaining: 0, type: MaterialType.WorkingStandard));
        var hplcMethod = new HplcMethod { Name = "Assay", Abbreviation = "AS", Technique = HplcTechnique.Hplc };
        var gcMethod = new HplcMethod { Name = "Residual solvents", Abbreviation = "RS", Technique = HplcTechnique.Gc };
        f.Db.HplcMethods.AddRange(hplcMethod, gcMethod);
        await f.Db.SaveChangesAsync();
        f.Db.HplcRuns.AddRange(
            new HplcRun { Code = "R1", SectionId = f.Fp.Id, HplcMethodId = hplcMethod.Id, Status = HplcRunStatus.Open },
            new HplcRun { Code = "R2", SectionId = f.Fp.Id, HplcMethodId = hplcMethod.Id, Status = HplcRunStatus.Completed },
            new HplcRun { Code = "R3", SectionId = f.Fp.Id, HplcMethodId = gcMethod.Id, Status = HplcRunStatus.Open });
        f.Db.IcpRuns.Add(new IcpRun { Code = "I1", SectionId = f.Fp.Id, Status = IcpRunStatus.Abandoned });
        f.Db.SolutionPreparations.AddRange(
            new SolutionPreparation { SectionId = f.Fp.Id, Status = SolutionPreparationStatus.InProgress },
            new SolutionPreparation { SectionId = f.Fp.Id, Status = SolutionPreparationStatus.Prepared });
        await f.Db.SaveChangesAsync();

        var cards = await f.Service.GetAsync(f.FpAnalyst.Id, "FP", AllCards);

        Assert.Contains(cards, c => c.Card == "fp-workspace");
        Assert.DoesNotContain(cards, c => c.Card == "rmpm-workspace");
        Assert.Equal((f.Fp.Id, (string?)"fp"), f.Workspace.Calls.Single());
        Assert.DoesNotContain(cards, c => c.Card is "media" or "cryovials");

        Assert.Equal(1, Figure(cards, "solutions", "in progress"));
        Assert.Equal(1, Figure(cards, "working-standards", "expired"));
        Assert.Equal(1, Figure(cards, "working-standards", "due within 30 days"));
        Assert.Equal(1, Figure(cards, "hplc", "open run"));
        Assert.Equal(1, Figure(cards, "gc", "open run"));
        Assert.Equal(0, Figure(cards, "icp", "open runs"));
        Assert.Equal("pending", cards.Single(c => c.Card == "icp").Counters.Single().Tone);
    }

    [Fact]
    public async Task ALaboratoryTheUserDoesNotBelongTo_IsRefused()
    {
        var f = await SeedAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetAsync(f.MicroAnalyst.Id, "FP", AllCards));
    }
    [Fact]
    public async Task DocumentControl_CountsTheCallersReadingList_AsTheReadingListTabsDo()
    {
        var f = await SeedAsync();
        DocumentTrainingAssignment Assignment(TrainingAssignmentStatus status, int dueInDays, User? user = null) => new()
        {
            AssignedUserId = (user ?? f.MicroAnalyst).Id, Status = status, DueDateUtc = Now.UtcDateTime.AddDays(dueInDays)
        };
        f.Db.DocumentTrainingAssignments.AddRange(
            Assignment(TrainingAssignmentStatus.Assigned, 5),                 // to read
            Assignment(TrainingAssignmentStatus.Reading, 1),                  // to read
            Assignment(TrainingAssignmentStatus.Reading, -1),                 // overdue: past due, not "to read" as well
            Assignment(TrainingAssignmentStatus.Overdue, 3),                  // overdue by status
            Assignment(TrainingAssignmentStatus.Acknowledged, -10),           // completed
            Assignment(TrainingAssignmentStatus.SupersededIncomplete, -10),   // superseded
            Assignment(TrainingAssignmentStatus.Cancelled, -10),              // neither
            Assignment(TrainingAssignmentStatus.Failed, 5),                   // neither ("Other")
            Assignment(TrainingAssignmentStatus.Assigned, 5, f.FpAnalyst));   // someone else's
        await f.Db.SaveChangesAsync();

        var cards = await f.Service.GetDocumentControlAsync(f.MicroAnalyst.Id);

        Assert.Equal("reading-list", cards.Single().Card);
        Assert.Equal(2, Figure(cards, "reading-list", "to read"));
        Assert.Equal(2, Figure(cards, "reading-list", "overdue"));
        Assert.Equal("detected", cards.Single().Counters.Single(c => c.Label == "overdue").Tone);
    }
}

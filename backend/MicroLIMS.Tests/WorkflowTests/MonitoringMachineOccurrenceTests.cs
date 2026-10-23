using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// After Cleaning is planned for the machine itself: whichever parts are cleaned
// and sampled at a session fulfil it, and not every part has to be cleaned together.
public class MonitoringMachineOccurrenceTests
{
    // Wednesday 21 Oct 2026, 09:00 in Cairo.
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 6, 0, 0, TimeSpan.Zero);

    private sealed class World
    {
        public MicroLimsDbContext Db = null!;
        public MonitoringOccurrenceService Occurrences = null!;
        public MonitoringLinkService Links = null!;
        public User Head = null!;
        public Machine Machine = null!, Other = null!;
        public MachinePartConfiguration Nozzle = null!, Needle = null!;
        public MonitoringPlanRevision Revision = null!;
    }

    private static async Task<World> BuildAsync()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var w = new World { Db = new MicroLimsDbContext(options) };
        var clock = new LabClock(new FixedTimeProvider(Now), TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));

        var role = new Role { Name = "Head", Type = RoleType.SectionHead, IsSystemRole = true, IsActive = true };
        w.Db.Roles.Add(role);
        await w.Db.SaveChangesAsync();
        w.Head = new User { Username = "head", FullName = "head", RoleId = role.Id, PasswordHash = "x" };
        w.Db.Users.Add(w.Head);

        w.Machine = new Machine { Name = "Filler 1" };
        w.Other = new Machine { Name = "Filler 2" };
        w.Db.Machines.AddRange(w.Machine, w.Other);
        await w.Db.SaveChangesAsync();
        var nozzle = new MachinePart { MachineId = w.Machine.Id, Name = "Nozzle" };
        var needle = new MachinePart { MachineId = w.Machine.Id, Name = "Needle" };
        w.Db.MachineParts.AddRange(nozzle, needle);
        await w.Db.SaveChangesAsync();
        w.Nozzle = new MachinePartConfiguration { MachinePartId = nozzle.Id, TestType = "Swab", TestCode = "TAMC" };
        w.Needle = new MachinePartConfiguration { MachinePartId = needle.Id, TestType = "Swab", TestCode = "TAMC" };
        w.Revision = new MonitoringPlanRevision
        {
            Year = 2026, RevisionNumber = 1, Status = PlanRevisionStatus.Approved,
            EffectiveFrom = new DateOnly(2026, 1, 1), GeneratedAt = Now.UtcDateTime, GeneratedByUserId = w.Head.Id
        };
        w.Db.MachinePartConfigurations.AddRange(w.Nozzle, w.Needle);
        w.Db.MonitoringPlanRevisions.Add(w.Revision);
        await w.Db.SaveChangesAsync();

        var status = new OccurrenceStatusService(w.Db, clock);
        w.Occurrences = new MonitoringOccurrenceService(w.Db, clock, status, new ElectronicSignatureService(w.Db));
        w.Links = new MonitoringLinkService(w.Db, clock, status);
        return w;
    }

    private static async Task<MonitoringOccurrence> MachineOccurrenceAsync(World w, params MachinePartConfiguration[] parts)
    {
        var o = new MonitoringOccurrence
        {
            CreatedByRevisionId = w.Revision.Id,
            Category = MonitoringCategory.AfterCleaning,
            TargetType = MonitoringTargetType.Machine,
            MachineId = w.Machine.Id,
            PeriodKey = "manual-20261021",
            FrequencySnapshot = "Manual entry",
            TargetDate = new DateOnly(2026, 10, 21),
            WindowStart = new DateOnly(2026, 10, 21),
            WindowEnd = new DateOnly(2026, 10, 25),
            OriginalWindowEnd = new DateOnly(2026, 10, 25),
            Items = parts.Length == 0
                ? new List<MonitoringOccurrenceItem> { new() }
                : parts.Select(p => new MonitoringOccurrenceItem { MachinePartConfigurationId = p.Id }).ToList()
        };
        w.Db.MonitoringOccurrences.Add(o);
        await w.Db.SaveChangesAsync();
        return o;
    }

    // A saved session: sample, test order and one location for the part.
    private static async Task SessionAsync(
        World w, MonitoringOccurrence o, MachinePartConfiguration part, ApprovalStatus approval, WorkflowStep step,
        ResultStatus result = ResultStatus.WithinLimits)
    {
        var at = new DateTime(2026, 10, 21, 6, 0, 0, DateTimeKind.Utc);
        var sample = new Sample
        {
            ReferenceNumber = $"AC-{part.Id}", Category = SampleCategory.AfterCleaning, MachineId = w.Machine.Id, ReceivedAt = at, SampledAt = at
        };
        w.Db.Samples.Add(sample);
        await w.Db.SaveChangesAsync();
        var order = new TestOrder { SampleId = sample.Id, TestCode = "TAMC", Status = approval, CurrentStep = step };
        w.Db.TestOrders.Add(order);
        await w.Db.SaveChangesAsync();
        w.Db.SampleLocations.Add(new SampleLocation
        {
            SampleId = sample.Id, TestOrderId = order.Id, LocationType = LocationType.MachinePart,
            MachinePartConfigurationId = part.Id, MonitoringOccurrenceId = o.Id, Status = result
        });
        await w.Db.SaveChangesAsync();
    }

    private static Sample FreshSession(World w, params MachinePartConfiguration[] parts) => new()
    {
        ReferenceNumber = "AC-NEW", Category = SampleCategory.AfterCleaning, MachineId = w.Machine.Id,
        Locations = parts.Select(p => new SampleLocation
        {
            LocationType = LocationType.MachinePart, MachinePartConfigurationId = p.Id,
            TestOrder = new TestOrder { TestCode = "TAMC" }
        }).ToList()
    };

    // ---- planning ----

    [Fact]
    public async Task ManualEntry_NamesNoParts_ItIsForTheMachineItself()
    {
        var w = await BuildAsync();

        var created = await w.Occurrences.CreateManualAsync(
            new CreateManualOccurrenceRequest(w.Machine.Id, new DateOnly(2026, 10, 28), null, "Cleaned after batch change"), w.Head.Id);

        var detail = await w.Occurrences.GetDetailAsync(created.Id);
        var item = Assert.Single(detail.Items);
        Assert.Equal(ItemProgress.None, item.Progress);
        Assert.Contains("Machine", item.Label);
        Assert.Equal(1, created.ItemsTotal);
        Assert.Equal("Filler 1", created.Target);
    }

    [Fact]
    public async Task ManualEntry_StillAcceptsNamedParts_ForAnEntryThatMustCoverExactlyThose()
    {
        var w = await BuildAsync();

        var created = await w.Occurrences.CreateManualAsync(
            new CreateManualOccurrenceRequest(w.Machine.Id, new DateOnly(2026, 10, 28), new List<int> { w.Nozzle.Id }, null), w.Head.Id);

        Assert.Equal(1, created.ItemsTotal);
        var item = Assert.Single((await w.Occurrences.GetDetailAsync(created.Id)).Items);
        Assert.Contains("Nozzle", item.Label);
    }

    // ---- status ----

    [Fact]
    public async Task AnyPartSampled_FulfilsTheMachine_AndNotEveryPartHasToBeCleaned()
    {
        var w = await BuildAsync();
        var o = await MachineOccurrenceAsync(w);
        Assert.Equal(OccurrenceStatus.DueToday, (await w.Occurrences.GetDetailAsync(o.Id)).Summary.Status);

        // Only the nozzle was cleaned this time.
        await SessionAsync(w, o, w.Nozzle, ApprovalStatus.Approved, WorkflowStep.Approved);

        var summary = (await w.Occurrences.GetDetailAsync(o.Id)).Summary;
        Assert.Equal(OccurrenceStatus.Performed, summary.Status);
        Assert.Equal(1, summary.ItemsPerformed);
    }

    [Fact]
    public async Task SeveralPartsAtOneSession_TheSlowestDecidesHowFarTheMachineHasGot()
    {
        var w = await BuildAsync();
        var o = await MachineOccurrenceAsync(w);
        await SessionAsync(w, o, w.Nozzle, ApprovalStatus.Approved, WorkflowStep.Approved);
        await SessionAsync(w, o, w.Needle, ApprovalStatus.Pending, WorkflowStep.Incubating);

        var detail = await w.Occurrences.GetDetailAsync(o.Id);

        Assert.Equal(OccurrenceStatus.InTesting, detail.Summary.Status);
        Assert.Contains("AC-", detail.Items.Single().SampleReference);
    }

    [Fact]
    public async Task OnePartOverItsActionLimit_MakesTheMachineAnExcursion()
    {
        var w = await BuildAsync();
        var o = await MachineOccurrenceAsync(w);
        await SessionAsync(w, o, w.Nozzle, ApprovalStatus.Approved, WorkflowStep.Approved);
        await SessionAsync(w, o, w.Needle, ApprovalStatus.Approved, WorkflowStep.Approved, ResultStatus.ActionLimitExceeded);

        Assert.Equal(OccurrenceStatus.PerformedExcursion, (await w.Occurrences.GetDetailAsync(o.Id)).Summary.Status);
    }

    [Fact]
    public async Task AnOccurrenceThatNamesPartsStillNeedsEachOfThem()
    {
        var w = await BuildAsync();
        var o = await MachineOccurrenceAsync(w, w.Nozzle, w.Needle);
        await SessionAsync(w, o, w.Nozzle, ApprovalStatus.Approved, WorkflowStep.Approved);

        var summary = (await w.Occurrences.GetDetailAsync(o.Id)).Summary;

        Assert.Equal(OccurrenceStatus.PartlyPerformed, summary.Status);
        Assert.Equal(1, summary.ItemsPerformed);
        Assert.Equal(2, summary.ItemsTotal);
    }

    // ---- linking ----

    [Fact]
    public async Task LinkingASession_TakesEveryPartSampledAtIt()
    {
        var w = await BuildAsync();
        var o = await MachineOccurrenceAsync(w);
        var sample = FreshSession(w, w.Nozzle, w.Needle);

        await w.Links.LinkAsync(sample, new[] { o.Id }, w.Head.Id);

        Assert.All(sample.Locations, l => Assert.Equal(o.Id, l.MonitoringOccurrenceId));
        var ev = w.Db.ChangeTracker.Entries<MonitoringOccurrenceEvent>().Single().Entity;
        Assert.Equal("AC-NEW: 2 part(s) sampled", ev.NewValue);
    }

    [Fact]
    public async Task LinkingAPartOnlyOccurrence_TakesOnlyItsOwnPart()
    {
        var w = await BuildAsync();
        var o = await MachineOccurrenceAsync(w, w.Nozzle);
        var sample = FreshSession(w, w.Nozzle, w.Needle);

        await w.Links.LinkAsync(sample, new[] { o.Id }, w.Head.Id);

        Assert.Equal(o.Id, sample.Locations.Single(l => l.MachinePartConfigurationId == w.Nozzle.Id).MonitoringOccurrenceId);
        Assert.Null(sample.Locations.Single(l => l.MachinePartConfigurationId == w.Needle.Id).MonitoringOccurrenceId);
    }

    [Fact]
    public async Task ASecondSession_CannotFulfilTheSameMachineOccurrenceAgain()
    {
        var w = await BuildAsync();
        var o = await MachineOccurrenceAsync(w);
        await SessionAsync(w, o, w.Nozzle, ApprovalStatus.Pending, WorkflowStep.Incubating);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Links.LinkAsync(FreshSession(w, w.Needle), new[] { o.Id }, w.Head.Id));

        Assert.Contains("already has this test fulfilled", ex.Message);
    }

    [Fact]
    public async Task ASessionOfAnotherMachine_CannotFulfilIt()
    {
        var w = await BuildAsync();
        var o = await MachineOccurrenceAsync(w);
        var sample = FreshSession(w, w.Nozzle);
        sample.MachineId = w.Other.Id;

        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Links.LinkAsync(sample, new[] { o.Id }, w.Head.Id));
    }
}

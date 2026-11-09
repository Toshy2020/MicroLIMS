using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class WaterFrequencyPerLabTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 6, 0, 0, TimeSpan.Zero);
    private const string Password = "Correct-horse-1";

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed record TestSeed(
        DocumentSection Micro,
        DocumentSection Fp,
        User AdminUser,
        User MicroUser,
        User FpUser,
        WaterDepartment WaterDept,
        TestDefinition MicroTest,
        TestDefinition FpTest);

    private static TestSeed SeedEnvironment(MicroLimsDbContext db)
    {
        var dept = new DocumentDepartment { Name = "Quality Control", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        db.SaveChanges();

        var micro = new DocumentSection { Name = "Microbiology Laboratory", Code = LaboratoryCodes.Microbiology, DepartmentId = dept.Id, IsActive = true };
        var fp = new DocumentSection { Name = "Physicochemical Laboratory", Code = PhyschemAreas.SectionCode, DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(micro, fp);

        var adminRole = new Role { Name = "Admin", Type = RoleType.SystemAdministrator, IsActive = true };
        var headRole = new Role { Name = "Head", Type = RoleType.SectionHead, IsActive = true };
        var analystRole = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.AddRange(adminRole, headRole, analystRole);
        db.SaveChanges();

        var adminUser = new User { Username = "admin", FullName = "Admin", RoleId = adminRole.Id, Role = adminRole, IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) };
        var microUser = new User { Username = "micro_user", FullName = "Micro User", RoleId = headRole.Id, Role = headRole, IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) };
        var fpUser = new User { Username = "fp_user", FullName = "FP User", RoleId = headRole.Id, Role = headRole, IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) };
        db.Users.AddRange(adminUser, microUser, fpUser);
        db.SaveChanges();

        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = microUser.Id, DepartmentId = dept.Id, SectionId = micro.Id },
            new UserOrgMembership { UserId = fpUser.Id, DepartmentId = dept.Id, SectionId = fp.Id, PhyschemArea = PhyschemArea.FinishedProduct });

        var waterDept = new WaterDepartment { Name = "Purified Water System", IsActive = true };
        db.WaterDepartments.Add(waterDept);

        var microTest = new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", SectionId = micro.Id, IsActive = true };
        var fpTest = new TestDefinition { Code = "COND", DisplayName = "Conductivity", SectionId = fp.Id, PhyschemArea = PhyschemArea.RawPackaging, IsActive = true };
        db.TestDefinitions.AddRange(microTest, fpTest);

        db.SaveChanges();

        return new TestSeed(micro, fp, adminUser, microUser, fpUser, waterDept, microTest, fpTest);
    }

    private static LabClock MakeClock() =>
        new(new FixedTimeProvider(Now), TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));

    // B2 + B6: Override CRUD + permission (other lab refused)
    [Fact]
    public async Task OverrideCrud_AndPermission_OtherLabRefused()
    {
        await using var db = NewDb();
        var seed = SeedEnvironment(db);
        var scope = new UserSectionScopeService(db);
        var service = new WaterMasterDataService(db, scope);

        var point = new WaterSamplingPoint
        {
            Code = "WP-01",
            Location = "Loop A",
            TestingFrequency = "Weekly",
            WaterDepartmentId = seed.WaterDept.Id,
            FrequencyType = FrequencyType.Weekly,
            FrequencyInterval = 1,
            SamplingWeekday = DayOfWeek.Sunday,
            AssignedTestCodes = new() { seed.MicroTest.Code, seed.FpTest.Code }
        };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        // 1. FP user tries to configure MICRO override -> Refused!
        var microOverrideReq = new SetWaterPointLabFrequencyRequest(
            FrequencyType: FrequencyType.Weekly,
            FrequencyInterval: 2,
            SamplingWeekday: DayOfWeek.Monday);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SetWaterPointLabFrequencyAsync(seed.FpUser.Id, point.Id, seed.Micro.Id, microOverrideReq));

        // 2. FP user configures FP override -> Succeeds!
        var fpOverrideReq = new SetWaterPointLabFrequencyRequest(
            FrequencyType: FrequencyType.Weekly,
            FrequencyInterval: 1,
            SamplingWeekday: DayOfWeek.Tuesday);

        var setResp = await service.SetWaterPointLabFrequencyAsync(seed.FpUser.Id, point.Id, seed.Fp.Id, fpOverrideReq);
        Assert.Equal(seed.Fp.Id, setResp.SectionId);
        Assert.Equal(seed.Fp.Code, setResp.SectionCode);
        Assert.Equal(FrequencyType.Weekly, setResp.FrequencyType);
        Assert.Equal(DayOfWeek.Tuesday, setResp.SamplingWeekday);
        Assert.Contains("Tuesday", setResp.Description);

        // 3. Get override for FP -> Succeeds!
        var getResp = await service.GetWaterPointLabFrequencyAsync(seed.FpUser.Id, point.Id, seed.Fp.Id);
        Assert.Equal(seed.Fp.Id, getResp.SectionId);
        Assert.Equal(DayOfWeek.Tuesday, getResp.SamplingWeekday);

        // 4. Point response carries labFrequencies
        var points = await service.GetWaterSamplingPointsAsync();
        var pt = Assert.Single(points, p => p.Id == point.Id);
        var labFreq = Assert.Single(pt.LabFrequencies);
        Assert.Equal(seed.Fp.Id, labFreq.SectionId);
        Assert.Equal(seed.Fp.Code, labFreq.SectionCode);
        Assert.Equal(DayOfWeek.Tuesday, labFreq.SamplingWeekday);
        Assert.Contains("Tuesday", labFreq.Description);

        // 5. Delete override -> Cleared
        await service.DeleteWaterPointLabFrequencyAsync(seed.FpUser.Id, point.Id, seed.Fp.Id);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetWaterPointLabFrequencyAsync(seed.FpUser.Id, point.Id, seed.Fp.Id));

        var pointsAfter = await service.GetWaterSamplingPointsAsync();
        Assert.Empty(Assert.Single(pointsAfter, p => p.Id == point.Id).LabFrequencies);
    }

    // B3 + B6: Generation: point with MICRO + FP codes -> two occurrence streams, FP override changes only FP's dates
    [Fact]
    public async Task Generation_PointWithMicroAndFpCodes_GeneratesTwoStreams_FpOverrideChangesOnlyFpDates()
    {
        await using var db = NewDb();
        var seed = SeedEnvironment(db);
        var clock = MakeClock();
        var scope = new UserSectionScopeService(db);
        var signatures = new ElectronicSignatureService(db);
        var planService = new MonitoringPlanService(db, clock, signatures, scope);

        var point = new WaterSamplingPoint
        {
            Code = "WP-01",
            Location = "Loop A",
            TestingFrequency = "Weekly",
            WaterDepartmentId = seed.WaterDept.Id,
            FrequencyType = FrequencyType.Weekly,
            FrequencyInterval = 1,
            SamplingWeekday = DayOfWeek.Sunday,
            AssignedTestCodes = new() { seed.MicroTest.Code, seed.FpTest.Code }
        };
        db.WaterSamplingPoints.Add(point);

        // Override for FP to be Weekly on Tuesday
        var fpOverride = new WaterPointLabFrequency
        {
            WaterSamplingPointId = point.Id,
            SectionId = seed.Fp.Id,
            FrequencyType = FrequencyType.Weekly,
            FrequencyInterval = 1,
            SamplingWeekday = DayOfWeek.Tuesday
        };
        point.LabFrequencies.Add(fpOverride);
        await db.SaveChangesAsync();

        var draft = await planService.GenerateDraftAsync(2026, seed.AdminUser.Id);

        var occurrences = await db.MonitoringOccurrences.Include(o => o.Items).ToListAsync();
        Assert.NotEmpty(occurrences);

        var microStream = occurrences.Where(o => o.LabSectionId == seed.Micro.Id).ToList();
        var fpStream = occurrences.Where(o => o.LabSectionId == seed.Fp.Id).ToList();

        Assert.NotEmpty(microStream);
        Assert.NotEmpty(fpStream);

        // MICRO stream uses Sunday target date and has TAMC test
        var firstMicro = microStream.OrderBy(o => o.WindowStart).First();
        Assert.Equal(DayOfWeek.Sunday, firstMicro.TargetDate!.Value.DayOfWeek);
        Assert.Contains(seed.MicroTest.Code, firstMicro.Items.Select(i => i.TestCode));
        Assert.DoesNotContain(seed.FpTest.Code, firstMicro.Items.Select(i => i.TestCode));
        Assert.Contains("own", firstMicro.FrequencySnapshot);

        // FP stream uses Tuesday target date and has COND test
        var firstFp = fpStream.OrderBy(o => o.WindowStart).First();
        Assert.Equal(DayOfWeek.Tuesday, firstFp.TargetDate!.Value.DayOfWeek);
        Assert.Contains(seed.FpTest.Code, firstFp.Items.Select(i => i.TestCode));
        Assert.DoesNotContain(seed.MicroTest.Code, firstFp.Items.Select(i => i.TestCode));
        Assert.Contains("lab", firstFp.FrequencySnapshot);
    }

    // B3 + B6: Key / no churn for backfilled MICRO rows
    [Fact]
    public async Task Key_NoChurnForBackfilledMicroRows()
    {
        await using var db = NewDb();
        var seed = SeedEnvironment(db);
        var clock = MakeClock();
        var scope = new UserSectionScopeService(db);
        var signatures = new ElectronicSignatureService(db);
        var planService = new MonitoringPlanService(db, clock, signatures, scope);

        var point = new WaterSamplingPoint
        {
            Code = "WP-01",
            Location = "Loop A",
            TestingFrequency = "Weekly",
            WaterDepartmentId = seed.WaterDept.Id,
            FrequencyType = FrequencyType.Weekly,
            FrequencyInterval = 1,
            SamplingWeekday = DayOfWeek.Sunday,
            AssignedTestCodes = new() { seed.MicroTest.Code }
        };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        // Seed approved 2026 revision
        var approvedRevision = new MonitoringPlanRevision
        {
            Year = 2026,
            RevisionNumber = 1,
            Status = PlanRevisionStatus.Approved,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            GeneratedAt = Now.UtcDateTime,
            GeneratedByUserId = seed.AdminUser.Id
        };
        db.MonitoringPlanRevisions.Add(approvedRevision);
        await db.SaveChangesAsync();

        // Simulate an existing water occurrence backfilled with LabSectionId = MICRO.Id
        // Sunday 1 Nov 2026 is week 2026-W44
        var backfilledOccurrence = new MonitoringOccurrence
        {
            CreatedByRevisionId = approvedRevision.Id,
            Category = MonitoringCategory.Water,
            TargetType = MonitoringTargetType.WaterSamplingPoint,
            WaterSamplingPointId = point.Id,
            LabSectionId = seed.Micro.Id,
            PeriodKey = "2026-W44",
            Sequence = 1,
            FrequencySnapshot = "Weekly, Sunday (own)",
            TargetDate = new DateOnly(2026, 11, 1),
            WindowStart = new DateOnly(2026, 11, 1),
            WindowEnd = new DateOnly(2026, 11, 3),
            OriginalTargetDate = new DateOnly(2026, 11, 1),
            OriginalWindowEnd = new DateOnly(2026, 11, 3),
            Items = new List<MonitoringOccurrenceItem> { new() { TestCode = seed.MicroTest.Code } }
        };
        db.MonitoringOccurrences.Add(backfilledOccurrence);
        await db.SaveChangesAsync();

        // Generating a new draft should recognize the backfilled occurrence as already planned
        var draft = await planService.GenerateDraftAsync(2026, seed.AdminUser.Id);

        // Neither retired nor rescheduled for that key
        Assert.DoesNotContain(draft.ToRetire, r => r.OccurrenceId == backfilledOccurrence.Id);
        Assert.DoesNotContain(draft.ToReschedule, m => m.OccurrenceId == backfilledOccurrence.Id);
    }

    // B4 + B6: Link only to the sample's lab
    [Fact]
    public async Task Link_OnlyToTheSamplesLab()
    {
        await using var db = NewDb();
        var seed = SeedEnvironment(db);
        var clock = MakeClock();
        var status = new OccurrenceStatusService(db, clock);
        var scope = new UserSectionScopeService(db);
        var linkService = new MonitoringLinkService(db, clock, status, scope);

        var approvedRevision = new MonitoringPlanRevision
        {
            Year = 2026,
            RevisionNumber = 1,
            Status = PlanRevisionStatus.Approved,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            GeneratedAt = Now.UtcDateTime,
            GeneratedByUserId = seed.AdminUser.Id
        };
        db.MonitoringPlanRevisions.Add(approvedRevision);

        var point = new WaterSamplingPoint
        {
            Code = "WP-01",
            Location = "Loop A",
            TestingFrequency = "Weekly",
            WaterDepartmentId = seed.WaterDept.Id,
            FrequencyType = FrequencyType.Weekly,
            FrequencyInterval = 1,
            SamplingWeekday = DayOfWeek.Sunday,
            AssignedTestCodes = new() { seed.MicroTest.Code, seed.FpTest.Code }
        };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        // Add one MICRO occurrence and one FP occurrence for the current week (touching today)
        var microOccurrence = new MonitoringOccurrence
        {
            CreatedByRevisionId = approvedRevision.Id,
            Category = MonitoringCategory.Water,
            TargetType = MonitoringTargetType.WaterSamplingPoint,
            WaterSamplingPointId = point.Id,
            LabSectionId = seed.Micro.Id,
            PeriodKey = "2026-W43",
            Sequence = 1,
            FrequencySnapshot = "Weekly, Sunday (own)",
            TargetDate = new DateOnly(2026, 10, 25),
            WindowStart = new DateOnly(2026, 10, 25),
            WindowEnd = new DateOnly(2026, 10, 29),
            OriginalWindowEnd = new DateOnly(2026, 10, 29),
            Items = new List<MonitoringOccurrenceItem> { new() { TestCode = seed.MicroTest.Code } }
        };
        var fpOccurrence = new MonitoringOccurrence
        {
            CreatedByRevisionId = approvedRevision.Id,
            Category = MonitoringCategory.Water,
            TargetType = MonitoringTargetType.WaterSamplingPoint,
            WaterSamplingPointId = point.Id,
            LabSectionId = seed.Fp.Id,
            PeriodKey = "2026-W43",
            Sequence = 1,
            FrequencySnapshot = "Weekly, Sunday (own)",
            TargetDate = new DateOnly(2026, 10, 25),
            WindowStart = new DateOnly(2026, 10, 25),
            WindowEnd = new DateOnly(2026, 10, 29),
            OriginalWindowEnd = new DateOnly(2026, 10, 29),
            Items = new List<MonitoringOccurrenceItem> { new() { TestCode = seed.FpTest.Code } }
        };
        db.MonitoringOccurrences.AddRange(microOccurrence, fpOccurrence);

        // Water sample received for FP lab
        var sample = new Sample
        {
            Category = SampleCategory.Water,
            WaterDepartmentId = seed.WaterDept.Id,
            LabSectionId = seed.Fp.Id,
            ControlNumber = "W-2026-001",
            ReferenceNumber = "REF-W-001"
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        // 1. Checklist returns ONLY the FP occurrence
        var checklist = await linkService.GetChecklistAsync(sample.Id);
        var allOccurrences = checklist.Sections.SelectMany(s => s.Occurrences).ToList();
        Assert.Contains(allOccurrences, o => o.OccurrenceId == fpOccurrence.Id);
        Assert.DoesNotContain(allOccurrences, o => o.OccurrenceId == microOccurrence.Id);

        // Add fresh unsaved location for linking
        sample.Locations = new List<SampleLocation>
        {
            new()
            {
                LocationType = LocationType.WaterSamplingPoint,
                WaterSamplingPointId = point.Id,
                TestOrder = new TestOrder { TestCode = seed.FpTest.Code }
            }
        };

        // 2. Linking sample to MICRO occurrence throws BusinessRuleException
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            linkService.LinkAsync(sample, new[] { microOccurrence.Id }, seed.FpUser.Id));

        // 3. Linking sample to FP occurrence succeeds
        await linkService.LinkAsync(sample, new[] { fpOccurrence.Id }, seed.FpUser.Id);
        Assert.Equal(fpOccurrence.Id, sample.Locations.Single().MonitoringOccurrenceId);
    }

    // B5 + B6: Due list for an FP-only user shows no MICRO water occurrences
    [Fact]
    public async Task DueList_FpOnlyUser_ShowsNoMicroWaterOccurrences()
    {
        await using var db = NewDb();
        var seed = SeedEnvironment(db);
        var clock = MakeClock();
        var status = new OccurrenceStatusService(db, clock);
        var signatures = new ElectronicSignatureService(db);
        var scope = new UserSectionScopeService(db);
        var occurrenceService = new MonitoringOccurrenceService(db, clock, status, signatures, scope);

        var approvedRevision = new MonitoringPlanRevision
        {
            Year = 2026,
            RevisionNumber = 1,
            Status = PlanRevisionStatus.Approved,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            GeneratedAt = Now.UtcDateTime,
            GeneratedByUserId = seed.AdminUser.Id
        };
        db.MonitoringPlanRevisions.Add(approvedRevision);

        var point = new WaterSamplingPoint
        {
            Code = "WP-01",
            Location = "Loop A",
            TestingFrequency = "Weekly",
            WaterDepartmentId = seed.WaterDept.Id,
            FrequencyType = FrequencyType.Weekly,
            FrequencyInterval = 1,
            SamplingWeekday = DayOfWeek.Sunday,
            AssignedTestCodes = new() { seed.MicroTest.Code, seed.FpTest.Code }
        };
        db.WaterSamplingPoints.Add(point);

        // An EM Room (non-water, should be visible to both)
        var dept = new Department { Name = "Manufacturing" };
        var room = new Room { Name = "Room 101", Department = dept };
        db.Rooms.Add(room);
        var roomConfig = new RoomTestConfiguration { Room = room, TestType = "PassiveAirSample", TestCode = "TAMC" };
        db.RoomTestConfigurations.Add(roomConfig);

        var microOccurrence = new MonitoringOccurrence
        {
            CreatedByRevisionId = approvedRevision.Id,
            Category = MonitoringCategory.Water,
            TargetType = MonitoringTargetType.WaterSamplingPoint,
            WaterSamplingPointId = point.Id,
            LabSectionId = seed.Micro.Id,
            PeriodKey = "2026-W43",
            Sequence = 1,
            FrequencySnapshot = "Weekly, Sunday (own)",
            TargetDate = new DateOnly(2026, 10, 25),
            WindowStart = new DateOnly(2026, 10, 25),
            WindowEnd = new DateOnly(2026, 10, 29),
            OriginalWindowEnd = new DateOnly(2026, 10, 29),
            Items = new List<MonitoringOccurrenceItem> { new() { TestCode = seed.MicroTest.Code } }
        };
        var fpOccurrence = new MonitoringOccurrence
        {
            CreatedByRevisionId = approvedRevision.Id,
            Category = MonitoringCategory.Water,
            TargetType = MonitoringTargetType.WaterSamplingPoint,
            WaterSamplingPointId = point.Id,
            LabSectionId = seed.Fp.Id,
            PeriodKey = "2026-W43",
            Sequence = 1,
            FrequencySnapshot = "Weekly, Sunday (own)",
            TargetDate = new DateOnly(2026, 10, 25),
            WindowStart = new DateOnly(2026, 10, 25),
            WindowEnd = new DateOnly(2026, 10, 29),
            OriginalWindowEnd = new DateOnly(2026, 10, 29),
            Items = new List<MonitoringOccurrenceItem> { new() { TestCode = seed.FpTest.Code } }
        };
        var roomOccurrence = new MonitoringOccurrence
        {
            CreatedByRevisionId = approvedRevision.Id,
            Category = MonitoringCategory.EnvironmentalMonitoring,
            TargetType = MonitoringTargetType.Room,
            RoomId = room.Id,
            PeriodKey = "2026-W43",
            Sequence = 1,
            FrequencySnapshot = "Weekly (own)",
            WindowStart = new DateOnly(2026, 10, 25),
            WindowEnd = new DateOnly(2026, 10, 29),
            OriginalWindowEnd = new DateOnly(2026, 10, 29),
            Items = new List<MonitoringOccurrenceItem> { new() { RoomTestConfigurationId = roomConfig.Id } }
        };
        db.MonitoringOccurrences.AddRange(microOccurrence, fpOccurrence, roomOccurrence);
        await db.SaveChangesAsync();

        // 1. Due list for FP user contains FP water and room, but NO MICRO water
        var fpDue = await occurrenceService.GetDueAsync(seed.FpUser.Id, null);
        Assert.Contains(fpDue.Items, i => i.Id == fpOccurrence.Id);
        Assert.Contains(fpDue.Items, i => i.Id == roomOccurrence.Id);
        Assert.DoesNotContain(fpDue.Items, i => i.Id == microOccurrence.Id);

        // Responses carry labSectionId and labSectionCode
        var fpSummary = fpDue.Items.Single(i => i.Id == fpOccurrence.Id);
        Assert.Equal(seed.Fp.Id, fpSummary.LabSectionId);
        Assert.Equal(seed.Fp.Code, fpSummary.LabSectionCode);

        // 2. Due list for MICRO user contains MICRO water and room, but NO FP water
        var microDue = await occurrenceService.GetDueAsync(seed.MicroUser.Id, null);
        Assert.Contains(microDue.Items, i => i.Id == microOccurrence.Id);
        Assert.Contains(microDue.Items, i => i.Id == roomOccurrence.Id);
        Assert.DoesNotContain(microDue.Items, i => i.Id == fpOccurrence.Id);

        // 3. Due list with labSectionId query filter narrows results
        var filteredDue = await occurrenceService.GetDueAsync(seed.FpUser.Id, null, seed.Fp.Id);
        Assert.Single(filteredDue.Items, i => i.Id == fpOccurrence.Id);
        Assert.DoesNotContain(filteredDue.Items, i => i.Id == roomOccurrence.Id);

        // 4. Detail endpoint hides out-of-scope water occurrence
        await Assert.ThrowsAsync<NotFoundException>(() =>
            occurrenceService.GetDetailAsync(seed.FpUser.Id, microOccurrence.Id));
        var allowedDetail = await occurrenceService.GetDetailAsync(seed.FpUser.Id, fpOccurrence.Id);
        Assert.Equal(seed.Fp.Id, allowedDetail.LabSectionId);
        Assert.Equal(seed.Fp.Code, allowedDetail.LabSectionCode);

        // 5. Manual water occurrence creation
        var manualReq = new CreateManualOccurrenceRequest(
            TargetDate: new DateOnly(2026, 10, 28),
            WaterSamplingPointId: point.Id,
            LabSectionId: seed.Fp.Id,
            Reason: "Special re-sampling after sanitization");
        var manualResp = await occurrenceService.CreateManualAsync(manualReq, seed.FpUser.Id);
        Assert.Equal(seed.Fp.Id, manualResp.LabSectionId);
        Assert.Equal(seed.Fp.Code, manualResp.LabSectionCode);
        Assert.True(manualResp.Manual);
    }
}

using System.Text.Json;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Competency;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class CompetencyAssignmentPostgresTests(PostgresTestFixture fixture)
{
    private readonly ActionGuardScenarios _seed = new(fixture);
    private static readonly DateTimeOffset Now = new(2037, 2, 1, 10, 30, 0, TimeSpan.Zero);

    [PostgresFact]
    public async Task Per_test_assignment_refuses_unqualified_analyst()
    {
        var setup = await ArrangeAsync();
        await using (var db = fixture.CreateDbContext())
        await using (var services = Services(db))
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                services.GetRequiredService<SampleAssignmentService>().AssignTestsAsync(
                    setup.Sample.Id, [new(setup.Orders[0].Id, setup.Analyst.Id)], setup.Head.Id));
            Assert.Equal("Competency.AssignmentRefused", error.Code);
            Assert.Contains(Reason(setup, 0), error.Message);
        }
        await AssertAssignmentsAsync(setup, assigned: false);
    }

    [PostgresFact]
    public async Task Whole_sample_refuses_all_tests_when_some_are_unqualified()
    {
        var setup = await ArrangeAsync(qualified: true);
        await using (var db = fixture.CreateDbContext())
        await using (var services = Services(db))
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                services.GetRequiredService<SampleAssignmentService>().AssignAnalystAsync(
                    setup.Sample.Id, setup.Analyst.Id, setup.Head.Id));
            Assert.Equal("Competency.AssignmentRefused", error.Code);
            Assert.Contains(Reason(setup, 1), error.Message);
            Assert.Contains(Reason(setup, 2), error.Message);
            Assert.Equal(2, error.ErrorDetails?.Count);
        }
        await AssertAssignmentsAsync(setup, assigned: false);
    }

    [PostgresFact]
    public async Task Bulk_assignment_reports_refused_sample_without_saving_assignments()
    {
        var setup = await ArrangeAsync(qualified: true);
        await using (var db = fixture.CreateDbContext())
        await using (var services = Services(db))
        {
            var result = await services.GetRequiredService<SampleBulkAssignmentService>()
                .AssignAsync(new BulkAssignRequest([setup.Sample.Id], setup.Analyst.Id, null), setup.Head.Id);
            var row = Assert.Single(result.Rows);
            Assert.False(row.Assigned);
            Assert.Equal("Competency.AssignmentRefused", row.RefusedCode);
            Assert.Equal(2, row.ErrorDetails?.Count);
            Assert.Contains(Reason(setup, 1), row.RefusedReason);
        }
        await AssertAssignmentsAsync(setup, assigned: false);
    }

    [PostgresFact]
    public async Task Qualified_assignee_is_saved_with_assignment_audit()
    {
        var setup = await ArrangeAsync(qualified: true);
        await AssignFirstAsync(setup);
        await AssertAssignmentsAsync(setup, assigned: true);
    }

    [PostgresFact]
    public async Task Gate_off_allows_unqualified_assignee_with_assignment_audit()
    {
        var setup = await ArrangeAsync(gate: false);
        await AssignFirstAsync(setup);
        await AssertAssignmentsAsync(setup, assigned: true);
    }

    [PostgresFact]
    public async Task Options_rank_qualified_analyst_first_and_return_per_test_reasons()
    {
        var setup = await ArrangeAsync(qualified: true);
        await using var db = fixture.CreateDbContext();
        await using var services = Services(db);
        var options = await services.GetRequiredService<SampleAssignmentService>()
            .GetAssignmentOptionsAsync(setup.Sample.Id, setup.Head.Id);
        var first = options.Tests.Single(t => t.TestOrderId == setup.Orders[0].Id);
        Assert.Equal(setup.Analyst.Id, first.EligibleUserIds[0]);
        // The section is shared with other test classes, so only this test's own users are asserted on.
        var refusal = Assert.Single(first.CompetencyRefusals, r => r.UserId == setup.Head.Id);
        Assert.DoesNotContain(first.CompetencyRefusals, r => r.UserId == setup.Analyst.Id);
        Assert.Equal("Competency.AssigneeNotQualified", refusal.Code);
        Assert.Equal(setup.Orders[0].TestCode, refusal.Params["test"]);
        Assert.Contains(setup.Head.FullName, refusal.Reason);
        var second = options.Tests.Single(t => t.TestOrderId == setup.Orders[1].Id);
        Assert.Contains(second.CompetencyRefusals, r => r.UserId == setup.Analyst.Id && r.Reason == Reason(setup, 1));
    }

    [PostgresFact]
    public async Task Take_check_and_action_refuse_unqualified_user()
    {
        var setup = await ArrangeAsync();
        await using (var db = fixture.CreateDbContext())
        await using (var services = Services(db))
        {
            var take = services.GetRequiredService<TestSelfAssignmentService>();
            var check = await take.CheckTakeAsync(setup.Orders[0].Id, setup.Analyst.Id);
            Assert.False(check.Allowed);
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => take.TakeAsync(setup.Orders[0].Id, setup.Analyst.Id));
            Assert.Equal("Competency.NotQualified", error.Code);
            Assert.Equal($"You are not qualified for {setup.Orders[0].TestCode}.", error.Message);
        }
        await AssertAssignmentsAsync(setup, assigned: false, taken: true);
    }

    [PostgresFact]
    public async Task Take_saves_qualified_user_with_audit()
    {
        var setup = await ArrangeAsync(qualified: true);
        await using (var db = fixture.CreateDbContext())
        await using (var services = Services(db))
        {
            await services.GetRequiredService<TestSelfAssignmentService>().TakeAsync(setup.Orders[0].Id, setup.Analyst.Id);
        }
        await AssertAssignmentsAsync(setup, assigned: true, taken: true);
    }

    [PostgresFact]
    public Task EM_auto_assignment_refuses_unqualified_user() => AssertAutoRefusalAsync(SampleCategory.EnvironmentalMonitoring);

    [PostgresFact]
    public Task Water_auto_assignment_refuses_unqualified_user() => AssertAutoRefusalAsync(SampleCategory.Water);

    [PostgresFact]
    public Task After_cleaning_auto_assignment_refuses_unqualified_user() => AssertAutoRefusalAsync(SampleCategory.AfterCleaning);

    private async Task AssertAutoRefusalAsync(SampleCategory category)
    {
        var section = await _seed.SectionAsync();
        var analyst = await _seed.AnalystAsync(section);
        var code = $"TEST-{section.Code}";
        int sampleId;
        int configurationId;
        await using (var arrange = fixture.CreateDbContext())
        {
            await EnableAsync(arrange, section.Id);
            var cause = new CauseOfTesting { Name = code, IsActive = true };
            arrange.CausesOfTesting.Add(cause);
            await arrange.SaveChangesAsync();
            var sample = new Sample
            {
                ReferenceNumber = $"AUTO-{section.Code}", Category = category, LabSectionId = section.Id,
                CauseOfTestingId = cause.Id, ReceivedByUserId = analyst.Id, ReceivedAt = Now.UtcDateTime,
                PreparationStatus = SamplePreparationStatus.NeedsPreparation
            };
            arrange.TestDefinitions.Add(new() { Code = code, DisplayName = code, SectionId = section.Id });
            arrange.Samples.Add(sample);
            switch (category)
            {
                case SampleCategory.EnvironmentalMonitoring:
                    var department = new Department { Name = code };
                    arrange.Departments.Add(department);
                    await arrange.SaveChangesAsync();
                    sample.DepartmentId = department.Id;
                    var config = new RoomTestConfiguration
                    {
                        Room = new Room { Name = code, DepartmentId = department.Id }, TestCode = code, TestType = "PassiveAirSample"
                    };
                    arrange.RoomTestConfigurations.Add(config);
                    await arrange.SaveChangesAsync();
                    configurationId = config.Id;
                    break;
                case SampleCategory.Water:
                    var water = new WaterDepartment { Name = code };
                    arrange.Add(water);
                    await arrange.SaveChangesAsync();
                    sample.WaterDepartmentId = water.Id;
                    var point = new WaterSamplingPoint { Code = code, WaterDepartmentId = water.Id, AssignedTestCodes = [code] };
                    arrange.WaterSamplingPoints.Add(point);
                    await arrange.SaveChangesAsync();
                    configurationId = point.Id;
                    break;
                default:
                    var machine = new Machine { Name = code };
                    arrange.Add(machine);
                    await arrange.SaveChangesAsync();
                    sample.MachineId = machine.Id;
                    var part = new MachinePartConfiguration
                    {
                        MachinePart = new MachinePart { MachineId = machine.Id, Name = code }, TestType = "Swab", TestCode = code
                    };
                    arrange.MachinePartConfigurations.Add(part);
                    await arrange.SaveChangesAsync();
                    configurationId = part.Id;
                    break;
            }
            sampleId = sample.Id;
        }

        await using (var act = fixture.CreateDbContext())
        await using (var services = Services(act))
        {
            Task Start() => category switch
            {
                SampleCategory.EnvironmentalMonitoring => services.GetRequiredService<EMWorkflowEngine>().PrepareAsync(sampleId, [configurationId], analyst.Id),
                SampleCategory.Water => services.GetRequiredService<WaterWorkflowEngine>().PrepareAsync(sampleId, [configurationId], analyst.Id, "RoomTemperature"),
                _ => services.GetRequiredService<AfterCleaningWorkflowEngine>().PrepareAsync(sampleId, [configurationId], analyst.Id),
            };
            act.CurrentUserId = analyst.Id;
            var error = await Assert.ThrowsAsync<BusinessRuleException>(Start);
            Assert.Equal("Competency.NotQualified", error.Code);
            Assert.Equal($"You are not qualified for {code}.", error.Message);
        }

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SamplePreparationStatus.NeedsPreparation, await verify.Samples.AsNoTracking()
            .Where(sample => sample.Id == sampleId).Select(sample => sample.PreparationStatus).SingleAsync());
        Assert.False(await verify.TestOrders.AsNoTracking().AnyAsync(order => order.SampleId == sampleId));
        Assert.False(await verify.SampleLocations.AsNoTracking().AnyAsync(location => location.SampleId == sampleId));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.SampleId == sampleId && a.UserId == analyst.Id));
    }

    [PostgresFact]
    public Task EM_auto_assignment_assigns_qualified_user() =>
        AssertAutoAssignmentAsync(SampleCategory.EnvironmentalMonitoring, gate: true, qualified: true);

    [PostgresFact]
    public Task EM_auto_assignment_assigns_user_when_gate_off() =>
        AssertAutoAssignmentAsync(SampleCategory.EnvironmentalMonitoring, gate: false, qualified: false);

    [PostgresFact]
    public Task Water_auto_assignment_assigns_qualified_user() =>
        AssertAutoAssignmentAsync(SampleCategory.Water, gate: true, qualified: true);

    [PostgresFact]
    public Task Water_auto_assignment_assigns_user_when_gate_off() =>
        AssertAutoAssignmentAsync(SampleCategory.Water, gate: false, qualified: false);

    [PostgresFact]
    public Task After_cleaning_auto_assignment_assigns_qualified_user() =>
        AssertAutoAssignmentAsync(SampleCategory.AfterCleaning, gate: true, qualified: true);

    [PostgresFact]
    public Task After_cleaning_auto_assignment_assigns_user_when_gate_off() =>
        AssertAutoAssignmentAsync(SampleCategory.AfterCleaning, gate: false, qualified: false);

    private async Task AssertAutoAssignmentAsync(SampleCategory category, bool gate, bool qualified)
    {
        var section = await _seed.SectionAsync();
        var analyst = await _seed.AnalystAsync(section);
        var code = $"TEST-{section.Code}";
        int sampleId;
        int configurationId;
        await using (var arrange = fixture.CreateDbContext())
        {
            if (gate)
                await EnableAsync(arrange, section.Id);
            var cause = new CauseOfTesting { Name = code, IsActive = true };
            arrange.CausesOfTesting.Add(cause);
            await arrange.SaveChangesAsync();
            var sample = new Sample
            {
                ReferenceNumber = $"AUTO-{section.Code}", Category = category, LabSectionId = section.Id,
                CauseOfTestingId = cause.Id, ReceivedByUserId = analyst.Id, ReceivedAt = Now.UtcDateTime,
                PreparationStatus = SamplePreparationStatus.NeedsPreparation
            };
            var definition = new TestDefinition { Code = code, DisplayName = code, SectionId = section.Id };
            arrange.TestDefinitions.Add(definition);
            arrange.Samples.Add(sample);
            switch (category)
            {
                case SampleCategory.EnvironmentalMonitoring:
                    var department = new Department { Name = code };
                    arrange.Departments.Add(department);
                    await arrange.SaveChangesAsync();
                    sample.DepartmentId = department.Id;
                    var config = new RoomTestConfiguration
                    {
                        Room = new Room { Name = code, DepartmentId = department.Id }, TestCode = code, TestType = "PassiveAirSample"
                    };
                    arrange.RoomTestConfigurations.Add(config);
                    await arrange.SaveChangesAsync();
                    configurationId = config.Id;
                    break;
                case SampleCategory.Water:
                    var water = new WaterDepartment { Name = code };
                    arrange.Add(water);
                    await arrange.SaveChangesAsync();
                    sample.WaterDepartmentId = water.Id;
                    var point = new WaterSamplingPoint { Code = code, WaterDepartmentId = water.Id, AssignedTestCodes = [code] };
                    arrange.WaterSamplingPoints.Add(point);
                    await arrange.SaveChangesAsync();
                    configurationId = point.Id;
                    break;
                default:
                    var machine = new Machine { Name = code };
                    arrange.Add(machine);
                    await arrange.SaveChangesAsync();
                    sample.MachineId = machine.Id;
                    var part = new MachinePartConfiguration
                    {
                        MachinePart = new MachinePart { MachineId = machine.Id, Name = code }, TestType = "Swab", TestCode = code
                    };
                    arrange.MachinePartConfigurations.Add(part);
                    await arrange.SaveChangesAsync();
                    configurationId = part.Id;
                    break;
            }
            sampleId = sample.Id;
            await arrange.SaveChangesAsync();
            if (qualified)
            {
                var head = await _seed.HeadAsync(section);
                await QualifyAsync(arrange, analyst.Id, definition.Id, head.Id);
                await arrange.SaveChangesAsync();
            }
        }

        await using (var act = fixture.CreateDbContext())
        await using (var services = Services(act))
        {
            Task Prepare() => category switch
            {
                SampleCategory.EnvironmentalMonitoring => services.GetRequiredService<EMWorkflowEngine>().PrepareAsync(sampleId, [configurationId], analyst.Id),
                SampleCategory.Water => services.GetRequiredService<WaterWorkflowEngine>().PrepareAsync(sampleId, [configurationId], analyst.Id, "RoomTemperature"),
                _ => services.GetRequiredService<AfterCleaningWorkflowEngine>().PrepareAsync(sampleId, [configurationId], analyst.Id)
            };
            act.CurrentUserId = analyst.Id;
            await Prepare();
        }

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SamplePreparationStatus.Ready, await verify.Samples.AsNoTracking()
            .Where(sample => sample.Id == sampleId).Select(sample => sample.PreparationStatus).SingleAsync());
        var orders = await verify.TestOrders.AsNoTracking().Where(order => order.SampleId == sampleId).ToListAsync();
        Assert.NotEmpty(orders);
        Assert.All(orders, order => Assert.Equal(analyst.Id, order.AssignedAnalystId));
        Assert.True(await verify.SampleLocations.AsNoTracking().AnyAsync(location => location.SampleId == sampleId));
        Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.SampleId == sampleId &&
            a.UserId == analyst.Id && a.EntityName == nameof(TestOrder)));
    }

    [PostgresFact]
    public async Task Correction_creates_new_test_without_unqualified_inherited_analyst()
    {
        var setup = await ArrangeAsync();
        var sampleId = setup.Sample.Id;
        var newCode = $"NEW-{setup.Sample.ReferenceNumber}";
        int newItemId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var before = new Item { Name = setup.Sample.ReferenceNumber, Code = setup.Sample.ReferenceNumber, Category = SampleCategory.RawMaterial };
            var after = new Item { Name = newCode, Code = newCode, Category = SampleCategory.RawMaterial, IsActive = true };
            after.AssignedTests.Add(new SampleTest { TestCode = newCode, DisplayName = newCode });
            arrange.AddRange(before, after);
            arrange.TestDefinitions.Add(new() { Code = newCode, DisplayName = newCode, SectionId = setup.Orders[0].SectionId });
            await arrange.SaveChangesAsync();
            newItemId = after.Id;
            var source = await arrange.Samples.Include(sample => sample.TestOrders).SingleAsync(sample => sample.Id == sampleId);
            source.ItemId = before.Id;
            source.Category = SampleCategory.RawMaterial;
            source.Status = SampleStatus.Received;
            source.ControlNumber = "CTRL";
            source.SampledBy = "Sampling analyst";
            source.BatchNumber = "BATCH";
            source.TestOrders.Single(order => order.Id == setup.Orders[0].Id).AssignedAnalystId = setup.Analyst.Id;
            await arrange.SaveChangesAsync();
        }
        await using (var act = fixture.CreateDbContext())
        {
            var time = new FixedTimeProvider(Now);
            var service = new SampleCorrectionService(act, new ElectronicSignatureService(act, time),
                new AuditEventService(act, fixture.CreateSequenceHelper(act), time), time, Guard(act));
            var request = new SampleCorrectionRequest("CTRL", "Sampling analyst", setup.Sample.CauseOfTestingId, "BATCH", ItemId: newItemId);
            await service.CorrectAsync(sampleId, request, "Correct the received item", ActionGuardScenarios.Password, setup.Head.Id, null);
        }
        await using var verify = fixture.CreateDbContext();
        var corrected = await verify.Samples.AsNoTracking().Include(sample => sample.TestOrders).SingleAsync(sample => sample.Id == sampleId);
        Assert.Equal(newItemId, corrected.ItemId);
        var order = Assert.Single(corrected.TestOrders);
        Assert.Equal(newCode, order.TestCode);
        Assert.Null(order.AssignedAnalystId);
        Assert.Equal(WorkflowStep.Waiting, order.CurrentStep);
        await AssertUnassignedCreationAuditAsync(verify, order, setup.Head.Id);
        Assert.True(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == "Sample" &&
            s.EntityId == sampleId && s.UserId == setup.Head.Id && s.MeaningOfSignature == SignatureMeaning.SampleCorrected));
    }

    [PostgresFact]
    public Task Correction_keeps_qualified_carried_analyst_and_clears_unqualified_one() =>
        AssertCorrectionInheritanceAsync(gate: true, qualified: true);

    [PostgresFact]
    public Task Correction_gate_off_keeps_unqualified_carried_analyst() =>
        AssertCorrectionInheritanceAsync(gate: false, qualified: false);

    private async Task AssertCorrectionInheritanceAsync(bool gate, bool qualified)
    {
        var setup = await ArrangeAsync(gate: gate);
        var sampleId = setup.Sample.Id;
        var keptCode = $"KEPT-{setup.Sample.ReferenceNumber}";
        var clearedCode = $"CLEARED-{setup.Sample.ReferenceNumber}";
        int newItemId;
        int keptAnalystId;
        int clearedAnalystId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var otherSection = new DocumentSection
            {
                Code = $"S-{setup.Sample.ReferenceNumber}", Name = $"Section {setup.Sample.ReferenceNumber}",
                DepartmentId = fixture.SeededDepartmentId
            };
            arrange.DocumentSections.Add(otherSection);
            var before = new Item { Name = setup.Sample.ReferenceNumber, Code = setup.Sample.ReferenceNumber, Category = SampleCategory.RawMaterial };
            var after = new Item { Name = keptCode, Code = keptCode, Category = SampleCategory.RawMaterial, IsActive = true };
            after.AssignedTests.Add(new SampleTest { TestCode = keptCode, DisplayName = keptCode });
            after.AssignedTests.Add(new SampleTest { TestCode = clearedCode, DisplayName = clearedCode });
            arrange.AddRange(before, after);
            await arrange.SaveChangesAsync();
            arrange.TestDefinitions.AddRange(
                new() { Code = keptCode, DisplayName = keptCode, SectionId = setup.Orders[0].SectionId },
                new() { Code = clearedCode, DisplayName = clearedCode, SectionId = otherSection.Id });
            await arrange.SaveChangesAsync();
            newItemId = after.Id;
            if (gate)
            {
                arrange.CompetencySectionSettings.Add(new() { SectionId = otherSection.Id, GateEnabled = true, ChangedAt = Now.UtcDateTime });
                await arrange.SaveChangesAsync();
            }
            var clearedAnalyst = await _seed.AnalystAsync(otherSection);
            clearedAnalystId = clearedAnalyst.Id;
            keptAnalystId = setup.Analyst.Id;
            var source = await arrange.Samples.Include(sample => sample.TestOrders).SingleAsync(sample => sample.Id == sampleId);
            source.ItemId = before.Id;
            source.Category = SampleCategory.RawMaterial;
            source.Status = SampleStatus.Received;
            source.ControlNumber = "CTRL";
            source.SampledBy = "Sampling analyst";
            source.BatchNumber = "BATCH";
            source.TestOrders.Single(order => order.Id == setup.Orders[0].Id).AssignedAnalystId = keptAnalystId;
            var otherOrder = source.TestOrders.Single(order => order.Id == setup.Orders[1].Id);
            otherOrder.SectionId = otherSection.Id;
            otherOrder.AssignedAnalystId = clearedAnalystId;
            if (qualified)
            {
                var definitionId = await arrange.TestDefinitions.AsNoTracking()
                    .Where(definition => definition.Code == keptCode).Select(definition => definition.Id).SingleAsync();
                await QualifyAsync(arrange, keptAnalystId, definitionId, setup.Head.Id);
            }
            await arrange.SaveChangesAsync();
        }
        await using (var act = fixture.CreateDbContext())
        {
            var time = new FixedTimeProvider(Now);
            var service = new SampleCorrectionService(act, new ElectronicSignatureService(act, time),
                new AuditEventService(act, fixture.CreateSequenceHelper(act), time), time, Guard(act));
            var request = new SampleCorrectionRequest("CTRL", "Sampling analyst", setup.Sample.CauseOfTestingId, "BATCH", ItemId: newItemId);
            await service.CorrectAsync(sampleId, request, "Correct the received item", ActionGuardScenarios.Password, setup.Head.Id, null);
        }
        await using var verify = fixture.CreateDbContext();
        var corrected = await verify.Samples.AsNoTracking().Include(sample => sample.TestOrders).SingleAsync(sample => sample.Id == sampleId);
        Assert.Equal(newItemId, corrected.ItemId);
        var kept = Assert.Single(corrected.TestOrders, order => order.TestCode == keptCode);
        Assert.Equal(WorkflowStep.Waiting, kept.CurrentStep);
        Assert.Equal(keptAnalystId, kept.AssignedAnalystId);
        var cleared = Assert.Single(corrected.TestOrders, order => order.TestCode == clearedCode);
        Assert.Equal(WorkflowStep.Waiting, cleared.CurrentStep);
        int? expectedCleared = gate ? null : clearedAnalystId;
        Assert.Equal(expectedCleared, cleared.AssignedAnalystId);
    }

    [PostgresFact]
    public async Task Retained_sample_retest_succeeds_without_unqualified_inherited_analyst()
    {
        var (origin, section, head) = await ArrangeApprovalAsync();
        var orderId = Assert.Single(origin.Orders).Id;
        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = head.Id;
            await Approval(act).DecideAsync(origin.Sample.Id, head.Id, ActionGuardScenarios.Password,
                ApprovalDecision.RetestRetainedSample, null, null, selectedTestOrderIds: [orderId], sectionId: section.Id);
        }
        await using var verify = fixture.CreateDbContext();
        var retest = await verify.Samples.AsNoTracking().Include(sample => sample.TestOrders)
            .SingleAsync(sample => sample.OriginSampleId == origin.Sample.Id);
        Assert.Equal(SampleStatus.Received, retest.Status);
        Assert.Equal(head.Id, retest.ReceivedByUserId);
        Assert.Equal(Now.UtcDateTime, retest.ReceivedAt);
        var order = Assert.Single(retest.TestOrders);
        Assert.Equal(origin.Orders[0].TestCode, order.TestCode);
        Assert.Equal(WorkflowStep.Waiting, order.CurrentStep);
        Assert.Null(order.AssignedAnalystId);
        Assert.True(await verify.TestOrders.AsNoTracking().Where(t => t.Id == orderId).Select(t => t.IsSuperseded).SingleAsync());
        await AssertUnassignedCreationAuditAsync(verify, order, head.Id);
        var signoff = await verify.SampleSectionSignoffs.AsNoTracking().SingleAsync(s => s.SampleId == origin.Sample.Id);
        Assert.Equal(SectionSignoffStatus.RetestRequested, signoff.Status);
        Assert.NotNull(signoff.ApprovalSignatureId);
    }

    [PostgresFact]
    public Task Retained_sample_retest_keeps_qualified_carried_analyst_and_clears_unqualified_one() =>
        AssertRetestInheritanceAsync(gate: true, qualified: true);

    [PostgresFact]
    public Task Retained_sample_retest_gate_off_keeps_unqualified_carried_analyst() =>
        AssertRetestInheritanceAsync(gate: false, qualified: false);

    private async Task AssertRetestInheritanceAsync(bool gate, bool qualified)
    {
        var (origin, section, head) = await ArrangeApprovalAsync(gate);
        var keptAnalyst = await _seed.AnalystAsync(section);
        var clearedAnalyst = await _seed.AnalystAsync(section);
        var keptCode = $"KEPT-{Guid.NewGuid():N}"[..14];
        var clearedCode = $"CLEARED-{Guid.NewGuid():N}"[..15];
        int secondOrderId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var keptOrder = await arrange.TestOrders.SingleAsync(order => order.Id == origin.Orders[0].Id);
            keptOrder.TestCode = keptCode;
            keptOrder.AssignedAnalystId = keptAnalyst.Id;
            arrange.TestDefinitions.AddRange(
                new() { Code = keptCode, DisplayName = keptCode, SectionId = section.Id },
                new() { Code = clearedCode, DisplayName = clearedCode, SectionId = section.Id });
            await arrange.SaveChangesAsync();
            var secondOrder = new TestOrder
            {
                SampleId = origin.Sample.Id, TestCode = clearedCode, SectionId = section.Id,
                Status = ApprovalStatus.Reviewed, CurrentStep = WorkflowStep.Reviewed, AssignedAnalystId = clearedAnalyst.Id
            };
            arrange.TestOrders.Add(secondOrder);
            await arrange.SaveChangesAsync();
            secondOrderId = secondOrder.Id;
            if (qualified)
            {
                var definitionId = await arrange.TestDefinitions.AsNoTracking()
                    .Where(definition => definition.Code == keptCode).Select(definition => definition.Id).SingleAsync();
                await QualifyAsync(arrange, keptAnalyst.Id, definitionId, head.Id);
                await arrange.SaveChangesAsync();
            }
        }
        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = head.Id;
            await Approval(act).DecideAsync(origin.Sample.Id, head.Id, ActionGuardScenarios.Password,
                ApprovalDecision.RetestRetainedSample, null, null,
                selectedTestOrderIds: [origin.Orders[0].Id, secondOrderId], sectionId: section.Id);
        }
        await using var verify = fixture.CreateDbContext();
        var retest = await verify.Samples.AsNoTracking().Include(sample => sample.TestOrders)
            .SingleAsync(sample => sample.OriginSampleId == origin.Sample.Id);
        Assert.Equal(SampleStatus.Received, retest.Status);
        var kept = Assert.Single(retest.TestOrders, order => order.TestCode == keptCode);
        Assert.Equal(WorkflowStep.Waiting, kept.CurrentStep);
        Assert.Equal(keptAnalyst.Id, kept.AssignedAnalystId);
        var cleared = Assert.Single(retest.TestOrders, order => order.TestCode == clearedCode);
        Assert.Equal(WorkflowStep.Waiting, cleared.CurrentStep);
        int? expectedCleared = gate ? null : clearedAnalyst.Id;
        Assert.Equal(expectedCleared, cleared.AssignedAnalystId);
        Assert.True(await verify.TestOrders.AsNoTracking().Where(order => order.Id == origin.Orders[0].Id || order.Id == secondOrderId)
            .AllAsync(order => order.IsSuperseded));
    }

    [PostgresFact]
    public async Task Requesting_new_samples_refuses_unqualified_assignee_before_signature()
    {
        var (origin, section, head) = await ArrangeApprovalAsync();
        var first = await _seed.AnalystAsync(section);
        var second = await _seed.AnalystAsync(section);
        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = head.Id;
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Approval(act).DecideAsync(
                origin.Sample.Id, head.Id, ActionGuardScenarios.Password, ApprovalDecision.NewSampleRequest, null, null,
                selectedTestOrderIds: [origin.Orders[0].Id], newSampleAnalystOneId: first.Id, newSampleAnalystTwoId: second.Id, sectionId: section.Id));
            Assert.Equal("Competency.AssignmentRefused", error.Code);
            Assert.Contains(first.FullName, error.Message);
        }
        await using var verify = fixture.CreateDbContext();
        var signoff = await verify.SampleSectionSignoffs.AsNoTracking().SingleAsync(s => s.SampleId == origin.Sample.Id);
        Assert.Equal(SectionSignoffStatus.UnderApproval, signoff.Status);
        Assert.Null(signoff.ApprovalSignatureId);
        Assert.False(await verify.Samples.AsNoTracking().AnyAsync(sample => sample.OriginSampleId == origin.Sample.Id));
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == "Sample" && s.EntityId == origin.Sample.Id));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.SampleId == origin.Sample.Id && a.UserId == head.Id));
    }

    private async Task<(ActionGuardScenarios.SampleSeed Origin, DocumentSection Section, User Head)> ArrangeApprovalAsync(bool gate = true)
    {
        var section = await _seed.SectionAsync();
        var head = await _seed.UserAsync(RoleType.SectionHead, [.. ActionGuardScenarios.HeadCodes, PermissionConstants.OosManage], section);
        var reviewer = await _seed.ReviewerAsync(section);
        var analyst = await _seed.AnalystAsync(section);
        var origin = await _seed.UnderApprovalAsync(section, reviewer.Id, analystId: analyst.Id);
        await using var db = fixture.CreateDbContext();
        if (!await db.CausesOfTesting.AnyAsync(c => c.Name == "Retest"))
            db.CausesOfTesting.Add(new() { Name = "Retest", IsActive = true });
        if (gate)
            await EnableAsync(db, section.Id);
        else
            await db.SaveChangesAsync();
        return (origin, section, head);
    }

    [PostgresFact]
    public async Task Oos_draft_refuses_unqualified_analyst_without_creating_plan()
    {
        var setup = await ArrangeOosAsync(gate: true);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DraftOosAsync(setup));
        Assert.Equal("Competency.AssignmentRefused", error.Code);
        Assert.Contains(setup.Analyst.FullName, error.Message);
        await AssertOosNotSignedAsync(setup, null);
    }

    [PostgresFact]
    public async Task Oos_sign_revalidates_competency_after_draft()
    {
        var setup = await ArrangeOosAsync(gate: false);
        var draft = await DraftOosAsync(setup);
        await using (var arrange = fixture.CreateDbContext())
            await EnableAsync(arrange, setup.Investigation.SectionId);
        await using (var act = fixture.CreateDbContext())
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Oos(act).SignAndCreateAsync(
                draft.Id, ActionGuardScenarios.Password, draft.InvestigationVersion, setup.Head.Id, null));
            Assert.Equal("Competency.AssignmentRefused", error.Code);
            Assert.Contains(setup.Analyst.FullName, error.Message);
        }
        await AssertOosNotSignedAsync(setup, draft);
    }

    private sealed record OosSetup(OosInvestigation Investigation, User Head, User Analyst, int OrderId);

    private async Task<OosSetup> ArrangeOosAsync(bool gate)
    {
        var (origin, section, head) = await ArrangeApprovalAsync(gate);
        var order = Assert.Single(origin.Orders);
        var analyst = await _seed.AnalystAsync(section);
        await using var db = fixture.CreateDbContext();
        var investigation = new OosInvestigation
        {
            Number = $"PLAN-{section.Code}", SampleId = origin.Sample.Id, SectionId = section.Id,
            Status = OosInvestigationStatus.PhaseOne, Kind = OosInvestigationKind.Oos,
            OpenedAtUtc = Now.UtcDateTime, OpenedByUserId = head.Id, TemplateSnapshotJson = "{}"
        };
        db.OosInvestigations.Add(investigation);
        await db.SaveChangesAsync();
        var signature = await new ElectronicSignatureService(db).SignAsync(head.Id, ActionGuardScenarios.Password,
            SignatureMeaning.OosPhaseOneConcluded, nameof(OosInvestigation), investigation.Id, null, null);
        db.OosInvestigationResults.Add(new()
        {
            InvestigationId = investigation.Id, TestOrderId = order.Id, OutcomeAtOpening = OosOutcomeClass.Fails, AddedAtUtc = Now.UtcDateTime
        });
        await db.SaveChangesAsync();
        investigation.PhaseOneSignatureId = signature.Id;
        investigation.PhaseOneSignedAtUtc = Now.UtcDateTime;
        investigation.PhaseOneSignedByUserId = head.Id;
        investigation.Status = OosInvestigationStatus.AwaitingRetest;
        await db.SaveChangesAsync();
        return new(investigation, head, analyst, order.Id);
    }

    private async Task<OosRetestPlanDto> DraftOosAsync(OosSetup setup)
    {
        await using var act = fixture.CreateDbContext();
        var request = new SaveRetestPlanRequest(OosRetestSource.RetainedSample, 1, [setup.OrderId], [setup.Analyst.Id], "Compare to limits", null);
        return await Oos(act).DraftAsync(setup.Investigation.Id, request, setup.Investigation.Version, setup.Head.Id);
    }

    private async Task AssertOosNotSignedAsync(OosSetup setup, OosRetestPlanDto? draft)
    {
        await using var verify = fixture.CreateDbContext();
        var plans = await verify.OosRetestPlans.AsNoTracking().Where(p => p.InvestigationId == setup.Investigation.Id).ToListAsync();
        Assert.False(await verify.Samples.AsNoTracking().AnyAsync(s => s.OriginSampleId == setup.Investigation.SampleId));
        var audit = verify.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(OosRetestPlan) && a.UserId == setup.Head.Id);
        if (draft is null)
        {
            Assert.Empty(plans);
            Assert.False(await audit.AnyAsync());
            return;
        }
        var plan = Assert.Single(plans);
        Assert.Equal(draft.Id, plan.Id);
        Assert.Null(plan.SignedAtUtc);
        Assert.Null(plan.SignedByUserId);
        Assert.Null(plan.SignatureId);
        Assert.Null(plan.SamplesCreatedAtUtc);
        Assert.False(await verify.OosRetestPlanSamples.AsNoTracking().AnyAsync(l => l.PlanId == plan.Id));
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == nameof(OosRetestPlan) && s.EntityId == plan.Id));
        Assert.Equal("Create", (await audit.SingleAsync()).Action);
    }

    private static async Task AssertUnassignedCreationAuditAsync(MicroLimsDbContext verify, TestOrder order, int actor)
    {
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(TestOrder) &&
            a.EntityId == order.Id.ToString() && a.Action == "Create");
        Assert.Equal(actor, audit.UserId);
        Assert.Null(audit.PreviousValue);
        using var values = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.Equal(JsonValueKind.Null, values.RootElement.GetProperty(nameof(TestOrder.AssignedAnalystId)).ValueKind);
    }

    private static async Task EnableAsync(MicroLimsDbContext db, int sectionId)
    {
        db.CompetencySectionSettings.Add(new() { SectionId = sectionId, GateEnabled = true, ChangedAt = Now.UtcDateTime });
        await db.SaveChangesAsync();
    }

    private async Task QualifyAsync(MicroLimsDbContext db, int userId, int testDefinitionId, int signedBy)
    {
        var id = await fixture.CreateSequenceHelper(db).ReserveIdAsync<AnalystQualification>();
        db.AnalystQualifications.Add(new()
        {
            Id = id, UserId = userId, TestDefinitionId = testDefinitionId,
            Basis = QualificationBasis.ObservedPractice, Evidence = "Observed testing",
            QualifiedOn = DateOnly.FromDateTime(Now.UtcDateTime), SignedByUserId = signedBy,
            Signature = new()
            {
                EntityId = id, EntityType = nameof(AnalystQualification), UserId = signedBy,
                MeaningOfSignature = SignatureMeaning.AnalystQualified, SignedAt = Now.UtcDateTime
            }
        });
    }

    private static CompetencyGuard Guard(MicroLimsDbContext db) =>
        new(db, new LabClock(new FixedTimeProvider(Now), LabClock.ResolveTimeZone("Africa/Cairo")), new ItemSopResolver(db));

    private static SampleApprovalService Approval(MicroLimsDbContext db)
    {
        var time = new FixedTimeProvider(Now);
        return new(db, TestServiceFactory.ReviewGate(db), TestServiceFactory.SampleSummary(db), TestServiceFactory.Archive(db),
            TestServiceFactory.ResultProjection(db), new ReferenceNumberGenerator(db, time), new UserSectionScopeService(db),
            new SegregationOfDutiesGuard(db), new TestOrderOutcomeService(db), new OosGate(db, new OosOpeningRule(db)),
            time, competency: Guard(db));
    }

    private static OosRetestPlanService Oos(MicroLimsDbContext db)
    {
        var time = new FixedTimeProvider(Now);
        return new(db, TestServiceFactory.OosInvestigation(db), new UserSectionScopeService(db), new SegregationOfDutiesGuard(db),
            new ElectronicSignatureService(db, time), new RetestSampleFactory(db, new ReferenceNumberGenerator(db, time), time), time, Guard(db));
    }

    private static string Reason(Setup setup, int index) =>
        $"{setup.Analyst.FullName} is not qualified for {setup.Orders[index].TestCode}.";

    private async Task AssignFirstAsync(Setup setup)
    {
        await using var db = fixture.CreateDbContext();
        await using var services = Services(db);
        await services.GetRequiredService<SampleAssignmentService>().AssignTestsAsync(
            setup.Sample.Id, [new(setup.Orders[0].Id, setup.Analyst.Id)], setup.Head.Id);
    }

    private async Task AssertAssignmentsAsync(Setup setup, bool assigned, bool taken = false)
    {
        await using var db = fixture.CreateDbContext();
        var orders = await db.TestOrders.AsNoTracking().Where(t => t.SampleId == setup.Sample.Id)
            .OrderBy(t => t.Id).Select(t => new { Order = t, Version = EF.Property<uint>(t, "xmin") }).ToListAsync();
        Assert.Equal(setup.Orders.Length, orders.Count);
        foreach (var (persisted, original) in orders.Zip(setup.Orders))
        {
            var order = persisted.Order;
            var changed = assigned && original.Id == setup.Orders[0].Id;
            Assert.Equal(changed ? setup.Analyst.Id : null, order.AssignedAnalystId);
            Assert.Equal(original.Status, order.Status);
            Assert.Equal(original.CurrentStep, order.CurrentStep);
            Assert.Equal(changed, setup.Versions[original.Id] != persisted.Version);
        }

        var action = taken ? "TEST_TAKEN" : "AssignedAnalyst";
        var audit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.SampleId == setup.Sample.Id && a.Action == action).ToListAsync();
        if (!assigned)
        {
            Assert.Empty(audit);
            return;
        }

        var entry = Assert.Single(audit);
        Assert.Equal(setup.Orders[0].Id.ToString(), entry.EntityId);
        Assert.Equal(taken ? setup.Analyst.Id : setup.Head.Id, entry.UserId);
        Assert.Equal(Now.UtcDateTime, entry.Timestamp);
        Assert.Equal($"{setup.Orders[0].TestCode}: Unassigned", entry.PreviousValue);
        Assert.Equal($"{setup.Orders[0].TestCode}: {setup.Analyst.FullName}", entry.NewValue);
    }

    private static ServiceProvider Services(MicroLimsDbContext db)
    {
        var time = new FixedTimeProvider(Now);
        return new ServiceCollection()
            .AddSingleton<IMicroLimsDbContext>(db)
            .AddSingleton<TimeProvider>(time)
            .AddSingleton<ILabClock>(new LabClock(time, LabClock.ResolveTimeZone("Africa/Cairo")))
            .AddScoped<ItemSopResolver>()
            .AddScoped<ICompetencyGuard, CompetencyGuard>()
            .AddScoped<IUserSectionScopeService, UserSectionScopeService>()
            .AddScoped<ITestWorkspaceService, TestingWorkspaceService>()
            .AddScoped<ReferenceNumberGenerator>()
            .AddScoped<EMWorkflowEngine>()
            .AddScoped<WaterWorkflowEngine>()
            .AddScoped<AfterCleaningWorkflowEngine>()
            .AddScoped<TestSelfAssignmentService>()
            .AddScoped<SampleAssignmentService>()
            .AddScoped<SampleBulkAssignmentService>()
            .BuildServiceProvider();
    }

    private sealed record Setup(Sample Sample, TestOrder[] Orders, Dictionary<int, uint> Versions, User Head, User Analyst);

    private async Task<Setup> ArrangeAsync(bool gate = true, bool qualified = false)
    {
        await using var db = fixture.CreateDbContext();
        var key = Guid.NewGuid().ToString("N")[..8];
        var section = new DocumentSection { Code = key, Name = key, DepartmentId = fixture.SeededDepartmentId };
        var cause = new CauseOfTesting { Name = key, IsActive = true };
        db.AddRange(section, cause);
        await db.SaveChangesAsync();

        var head = await _seed.HeadAsync(section);
        var analyst = await _seed.AnalystAsync(section);
        var sample = new Sample
        {
            ReferenceNumber = key, CauseOfTestingId = cause.Id, Category = SampleCategory.FinishedProduct,
            ReceivedByUserId = head.Id, ReceivedAt = Now.UtcDateTime, Status = SampleStatus.InTesting
        };
        var orders = Enumerable.Range(0, 3).Select(i => new TestOrder
        {
            Sample = sample, TestCode = $"{key}-{i}", SectionId = section.Id,
            Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting
        }).ToArray();
        var definition = new TestDefinition { Code = orders[0].TestCode, DisplayName = key, SectionId = section.Id };
        db.TestDefinitions.Add(definition);
        db.TestOrders.AddRange(orders);
        if (gate)
            db.CompetencySectionSettings.Add(new() { SectionId = section.Id, GateEnabled = true, ChangedAt = Now.UtcDateTime });
        await db.SaveChangesAsync();

        if (qualified)
        {
            var id = await fixture.CreateSequenceHelper(db).ReserveIdAsync<AnalystQualification>();
            db.AnalystQualifications.Add(new()
            {
                Id = id, UserId = analyst.Id, TestDefinitionId = definition.Id,
                Basis = QualificationBasis.ObservedPractice, Evidence = "Observed testing",
                QualifiedOn = DateOnly.FromDateTime(Now.UtcDateTime), SignedByUserId = head.Id,
                Signature = new()
                {
                    EntityId = id, EntityType = nameof(AnalystQualification), UserId = head.Id,
                    MeaningOfSignature = SignatureMeaning.AnalystQualified, SignedAt = Now.UtcDateTime
                }
            });
            await db.SaveChangesAsync();
        }

        var versions = orders.ToDictionary(t => t.Id, t => db.Entry(t).Property<uint>("xmin").CurrentValue);
        return new(sample, orders, versions, head, analyst);
    }
}

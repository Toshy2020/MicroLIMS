using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class TestOrderOutcomePostgresTests(PostgresTestFixture fixture)
{
    private enum Source { Count, Location, Biochemical, Pathogen, Result, None }

    [PostgresFact]
    public Task Active_count_reading_takes_precedence() => CheckAsync(Source.Count, false);

    [PostgresFact]
    public Task Latest_location_takes_precedence() => CheckAsync(Source.Location, false);

    [PostgresFact]
    public Task Latest_recorded_biochemical_detection_takes_precedence() => CheckAsync(Source.Biochemical, false);

    [PostgresFact]
    public Task Any_conforming_growth_counts_as_detection() => CheckAsync(Source.Pathogen, true);

    [PostgresFact]
    public Task Latest_result_prefers_interpreted_value() => CheckAsync(Source.Result, true);

    [PostgresFact]
    public Task Order_without_results_conforms() => CheckAsync(Source.None, false);

    [PostgresFact]
    public async Task Action_and_alert_statuses_are_reported_and_fail_as_before()
    {
        var actionId = await SeedAsync(Source.Count, ResultStatus.ActionLimitExceeded);
        var alertId = await SeedAsync(Source.Location, ResultStatus.AlertLimitExceeded);
        var emptyId = await SeedAsync(Source.None, ResultStatus.WithinLimits);
        await using var db = fixture.CreateDbContext();
        var service = new TestOrderOutcomeService(db);
        var outcomes = await service.GetOutcomesAsync([actionId, alertId, emptyId, actionId]);

        Assert.Equal(3, outcomes.Count);
        Assert.Equal(new TestOrderOutcome(true, ResultStatus.ActionLimitExceeded), outcomes[actionId]);
        Assert.Equal(new TestOrderOutcome(true, ResultStatus.AlertLimitExceeded), outcomes[alertId]);
        Assert.Equal(new TestOrderOutcome(false, null), outcomes[emptyId]);
    }

    private async Task CheckAsync(Source source, bool fails, ResultStatus status = ResultStatus.WithinLimits)
    {
        var id = await SeedAsync(source, status);
        await using var db = fixture.CreateDbContext();
        var service = new TestOrderOutcomeService(db);
        var outcome = await service.GetOutcomeAsync(id);
        Assert.Equal(fails, outcome.Fails);
        Assert.Equal(source <= Source.Location ? status : (ResultStatus?)null, outcome.Status);
    }

    private async Task<int> SeedAsync(Source source, ResultStatus status)
    {
        await using var db = fixture.CreateDbContext();
        var userId = fixture.SeededUserId;
        var cause = new CauseOfTesting { Name = $"Outcome {Guid.NewGuid():N}", IsActive = true };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();
        var sample = new Sample
        {
            ReferenceNumber = $"OUT-{Guid.NewGuid():N}"[..20],
            Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = cause.Id,
            ReceivedByUserId = userId
        };
        var order = new TestOrder
        {
            Sample = sample, TestCode = $"T{Guid.NewGuid():N}"[..12], SectionId = fixture.SeededSectionId,
            IsSuperseded = true
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        if (source <= Source.Result)
        {
            db.Results.Add(new Result { TestOrderId = order.Id, RawValue = "Absent", EnteredByUserId = userId });
            await db.SaveChangesAsync();
            db.Results.Add(new Result
            {
                TestOrderId = order.Id, RawValue = "Absent", InterpretedValue = "dEtEcTeD", EnteredByUserId = userId
            });
        }

        if (source <= Source.Pathogen)
        {
            db.PathogenObservations.Add(new PathogenObservation
            {
                TestOrderId = order.Id, StepName = "First", StepOrder = 1, ObservedByUserId = userId,
                Observation = GrowthObservation.GrowthConforming
            });
            await db.SaveChangesAsync();
            db.PathogenObservations.Add(new PathogenObservation
            {
                TestOrderId = order.Id, StepName = "Second", StepOrder = 2, ObservedByUserId = userId,
                Observation = GrowthObservation.NoGrowth
            });
        }

        if (source <= Source.Biochemical)
        {
            var incubation = new Incubation { TestOrderId = order.Id, StepName = "Incubation", StepNumber = 1 };
            db.Incubations.Add(incubation);
            await db.SaveChangesAsync();
            foreach (var (step, detected) in new (string, bool?)[] { ("First", true), ("Second", false), ("Third", null) })
            {
                db.WorkflowStepResults.Add(new WorkflowStepResult
                {
                    TestOrderId = order.Id, IncubationId = incubation.Id, StepName = step,
                    SubmittedByUserId = userId, BiochemicalOrganismDetected = detected
                });
                await db.SaveChangesAsync();
            }
        }

        if (source <= Source.Location)
        {
            db.SampleLocations.Add(new SampleLocation
            {
                SampleId = sample.Id, TestOrderId = order.Id, Status = ResultStatus.OutOfSpecification
            });
            await db.SaveChangesAsync();
            db.SampleLocations.Add(new SampleLocation
            {
                SampleId = sample.Id, TestOrderId = order.Id,
                Status = source == Source.Location ? status : ResultStatus.OutOfSpecification
            });
        }

        if (source == Source.Count)
        {
            foreach (var (readingStatus, active) in new[]
            {
                (ResultStatus.OutOfSpecification, true), (status, true), (ResultStatus.OutOfSpecification, false)
            })
            {
                db.CountTestReadings.Add(new CountTestReading
                {
                    TestOrderId = order.Id, EnteredByUserId = userId, Status = readingStatus, IsActive = active
                });
                await db.SaveChangesAsync();
            }
        }

        await db.SaveChangesAsync();
        return order.Id;
    }
}

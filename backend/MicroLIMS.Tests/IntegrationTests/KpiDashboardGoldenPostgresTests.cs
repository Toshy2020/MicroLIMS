using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Dashboard figures against a dataset small enough to calculate by hand. Period: 1-31 Oct 2026, "now" 15 Oct 12:00 UTC.
//
// Lab samples (profiles: KPIG_TAMC = 2 units / 5 days, KPIG_GRAV = 4 units / 10 days)
//   G0  received  9/10 08:00, approved  9/20 08:00, TAMC           -> prior period
//   G1  received 10/01 08:00, approved 10/03 08:00, TAMC  (2.0 d)  prepared 10/01 09:00, submit 10/02 09:00, review 10/02 15:00
//   G2  received 10/02 08:00, approved 10/06 08:00, GRAV  (4.0 d)  prepared 10/02 09:00, submit 10/04 09:00, review 10/05 12:00 (27 h: late); test returned once
//   G3  received 10/03 08:00, approved 10/08 08:00, TAMC  (5.0 d)  prepared 10/03 09:00, submit 10/07 09:00, review 10/07 21:00
//   G4  retest decision signed 10/06 (signature only, ApprovedAt null), OOS group, its TAMC test superseded
//   G5  received 10/01 08:00, approved 10/12 08:00, TAMC (11.0 d)  no preparation record
//   G6  open GRAV test (running), prepared 10/12 09:00
//   E1  environmental monitoring: 6 within, 1 alert, 1 action
// Other lab: GX approved 10/04 (3.0 d)
// Incubations on G1's and G3's tests: one read on time, one 10 h late. One return event on G2's test.
[Collection("PostgresDatabaseCollection")]
public class KpiDashboardGoldenPostgresTests
{
    private readonly PostgresTestFixture _fixture;
    public KpiDashboardGoldenPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static DateTime Utc(int month, int day, int hour = 0) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    [PostgresFact]
    public async Task Dashboard_MatchesHandCalculatedGoldenDataset()
    {
        await using var db = _fixture.CreateDbContext();
        var user = _fixture.SeededUserId;

        var lab = new DocumentSection { Name = "KPI golden lab", Code = "KPIG-A", DepartmentId = _fixture.SeededDepartmentId, IsActive = true };
        var otherLab = new DocumentSection { Name = "KPI golden other", Code = "KPIG-B", DepartmentId = _fixture.SeededDepartmentId, IsActive = true };
        var cause = new CauseOfTesting { Name = "KPIG routine", IsActive = true };
        db.AddRange(lab, otherLab, cause);
        await db.SaveChangesAsync();

        db.TestDefinitions.AddRange(
            new TestDefinition { Code = "KPIG_TAMC", DisplayName = "KPIG count", SectionId = lab.Id, WorkflowType = WorkflowType.CountTest },
            new TestDefinition { Code = "KPIG_GRAV", DisplayName = "KPIG gravimetric", SectionId = lab.Id, WorkflowType = WorkflowType.Gravimetric });
        db.WorkflowProfileVersions.AddRange(
            Profile(WorkflowType.CountTest, "KPIG_TAMC", 2m, 5m, user),
            Profile(WorkflowType.Gravimetric, "KPIG_GRAV", 4m, 10m, user));

        var samples = new Dictionary<string, Sample>();
        void AddSample(string tag, SampleCategory category, DateTime received, DateTime? approved, ApprovalDecision? decision,
            SampleStatus status, string? oos = null)
        {
            var s = new Sample
            {
                ReferenceNumber = $"KPIG-{tag}", ControlNumber = $"KPIG-CN-{tag}", SampledBy = "kpi", Category = category,
                CauseOfTestingId = cause.Id, ReceivedByUserId = user, ReceivedAt = received, ApprovedAt = approved,
                ApprovalDecision = decision, Status = status, OosGroupCode = oos
            };
            samples[tag] = s;
            db.Samples.Add(s);
        }
        AddSample("G0", SampleCategory.FinishedProduct, Utc(9, 10, 8), Utc(9, 20, 8), ApprovalDecision.Approve, SampleStatus.Approved);
        AddSample("G1", SampleCategory.FinishedProduct, Utc(10, 1, 8), Utc(10, 3, 8), ApprovalDecision.Approve, SampleStatus.Approved);
        AddSample("G2", SampleCategory.FinishedProduct, Utc(10, 2, 8), Utc(10, 6, 8), ApprovalDecision.Approve, SampleStatus.Approved);
        AddSample("G3", SampleCategory.FinishedProduct, Utc(10, 3, 8), Utc(10, 8, 8), ApprovalDecision.Approve, SampleStatus.Approved);
        AddSample("G4", SampleCategory.FinishedProduct, Utc(10, 4, 8), null, ApprovalDecision.RetestRetainedSample, SampleStatus.RetestRequested, "KPIG-OOS1");
        AddSample("G5", SampleCategory.FinishedProduct, Utc(10, 1, 8), Utc(10, 12, 8), ApprovalDecision.Approve, SampleStatus.Approved);
        AddSample("G6", SampleCategory.FinishedProduct, Utc(10, 12, 8), null, null, SampleStatus.InTesting);
        AddSample("E1", SampleCategory.EnvironmentalMonitoring, Utc(10, 5, 8), null, null, SampleStatus.InTesting);
        AddSample("GX", SampleCategory.FinishedProduct, Utc(10, 1, 8), Utc(10, 4, 8), ApprovalDecision.Approve, SampleStatus.Approved);
        await db.SaveChangesAsync();

        var tests = new Dictionary<string, TestOrder>();
        void AddTest(string tag, string code, int sectionId, ApprovalStatus status, WorkflowStep step, bool superseded = false)
        {
            var t = new TestOrder
            {
                SampleId = samples[tag].Id, TestCode = code, SectionId = sectionId, Status = status, CurrentStep = step,
                AssignedAnalystId = user, IsSuperseded = superseded
            };
            tests[tag] = t;
            db.TestOrders.Add(t);
        }
        AddTest("G0", "KPIG_TAMC", lab.Id, ApprovalStatus.Approved, WorkflowStep.Approved);
        AddTest("G1", "KPIG_TAMC", lab.Id, ApprovalStatus.Approved, WorkflowStep.Approved);
        AddTest("G2", "KPIG_GRAV", lab.Id, ApprovalStatus.Approved, WorkflowStep.Approved);
        AddTest("G3", "KPIG_TAMC", lab.Id, ApprovalStatus.Approved, WorkflowStep.Approved);
        AddTest("G4", "KPIG_TAMC", lab.Id, ApprovalStatus.Approved, WorkflowStep.Approved, superseded: true);
        AddTest("G5", "KPIG_TAMC", lab.Id, ApprovalStatus.Approved, WorkflowStep.Approved);
        AddTest("G6", "KPIG_GRAV", lab.Id, ApprovalStatus.InProgress, WorkflowStep.Running);
        AddTest("E1", "KPIG_EM", lab.Id, ApprovalStatus.ResultEntered, WorkflowStep.Ready);
        AddTest("GX", "KPIG_TAMC", otherLab.Id, ApprovalStatus.Approved, WorkflowStep.Approved);
        await db.SaveChangesAsync();

        void Prepared(string tag, DateTime at) =>
            db.SamplePreparations.Add(new SamplePreparation { SampleId = samples[tag].Id, PreparedAt = at, PreparedByUserId = user, Technique = "PourPlate" });
        // signedAt: the decision signature, as SampleApprovalService writes it for every decision.
        void Signoff(string tag, int sectionId, DateTime? submitted, DateTime? reviewed, DateTime? approved, SectionSignoffStatus status,
            ApprovalDecision decision, DateTime? signedAt = null) =>
            db.SampleSectionSignoffs.Add(new SampleSectionSignoff
            {
                SampleId = samples[tag].Id, SectionId = sectionId, Status = status, SubmittedForReviewAt = submitted,
                ReviewedAt = reviewed, ApprovedAt = approved, ApprovalDecision = decision,
                ApprovalSignature = signedAt is { } at
                    ? new ElectronicSignature
                    {
                        UserId = user, UserFullNameSnapshot = "KPIG", UsernameSnapshot = "kpig", RoleSnapshot = "SectionHead",
                        MeaningOfSignature = SignatureMeaning.RetestRequested, EntityType = "Sample", EntityId = samples[tag].Id, SignedAt = at
                    }
                    : null
            });
        Signoff("G0", lab.Id, null, null, Utc(9, 20, 8), SectionSignoffStatus.Approved, ApprovalDecision.Approve);
        Prepared("G1", Utc(10, 1, 9));
        Signoff("G1", lab.Id, Utc(10, 2, 9), Utc(10, 2, 15), Utc(10, 3, 8), SectionSignoffStatus.Approved, ApprovalDecision.Approve);
        Prepared("G2", Utc(10, 2, 9));
        Signoff("G2", lab.Id, Utc(10, 4, 9), Utc(10, 5, 12), Utc(10, 6, 8), SectionSignoffStatus.Approved, ApprovalDecision.Approve);
        Prepared("G3", Utc(10, 3, 9));
        Signoff("G3", lab.Id, Utc(10, 7, 9), Utc(10, 7, 21), Utc(10, 8, 8), SectionSignoffStatus.Approved, ApprovalDecision.Approve);
        // Production shape of a retest decision: signature written, ApprovedAt stays null.
        Signoff("G4", lab.Id, null, null, null, SectionSignoffStatus.RetestRequested, ApprovalDecision.RetestRetainedSample, signedAt: Utc(10, 6, 8));
        Signoff("G5", lab.Id, Utc(10, 11, 8), Utc(10, 11, 12), Utc(10, 12, 8), SectionSignoffStatus.Approved, ApprovalDecision.Approve);
        Prepared("G6", Utc(10, 12, 9));
        Signoff("GX", otherLab.Id, null, null, Utc(10, 4, 8), SectionSignoffStatus.Approved, ApprovalDecision.Approve);

        db.Incubations.AddRange(
            new Incubation { TestOrderId = tests["G1"].Id, StepNumber = 1, StepName = "Incubation", StartedAt = Utc(10, 1, 10), CompletedAt = Utc(10, 5, 10), ExpectedReadingAt = Utc(10, 5, 10) },
            new Incubation { TestOrderId = tests["G3"].Id, StepNumber = 1, StepName = "Incubation", StartedAt = Utc(10, 3, 10), CompletedAt = Utc(10, 6, 20), ExpectedReadingAt = Utc(10, 6, 10) });
        db.TestReturnEvents.Add(new TestReturnEvent
        {
            TestOrderId = tests["G2"].Id, ReviewerUserId = user, AssignedAnalystId = user, Reason = "KPIG re-count", ReturnedAt = Utc(10, 5, 10)
        });

        string[] levels = { "W", "W", "W", "W", "W", "W", "A", "X" };
        for (var i = 0; i < levels.Length; i++)
        {
            db.ResultRecords.Add(new ResultRecord
            {
                SampleId = samples["E1"].Id, TestOrderId = tests["E1"].Id, Category = SampleCategory.EnvironmentalMonitoring,
                SubjectName = "KPIG Room A", TestCode = "KPIG_EM", TestDisplayName = "KPIG plate",
                ResultLevel = levels[i] == "W" ? ResultLevel.WithinLimit : levels[i] == "A" ? ResultLevel.AlertLevel : ResultLevel.ActionLevel,
                ResultEnteredAt = Utc(10, 6, 8), SourceTable = "KpigLocation", SourceId = 9_000_000 + i, ReferenceNumber = "KPIG-E1",
                // UpdatedAt is a non-nullable DateTime with no initializer: left unset it is Kind=Unspecified,
                // which Npgsql rejects for timestamptz.
                UpdatedAt = Utc(10, 6, 8), ResultEnteredByUserId = user, ResultEnteredByName = "KPIG", SampleStatus = SampleStatus.InTesting
            });
        }
        await db.SaveChangesAsync();
        // If a NOT NULL or FK violation is reported for a row above, add that column to the seed row; set every
        // non-nullable DateTime explicitly with Kind=Utc (Npgsql rejects Unspecified for timestamptz). Keep the numbers.

        var clock = new FixedTimeProvider(new DateTimeOffset(Utc(10, 15, 12)));
        var service = TestServiceFactory.KpiDashboard(db, clock, new StubSectionScope(_ => new[] { lab.Id }));

        var dto = await service.GetDashboardAsync(user, true, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null, null, null, null);

        Assert.Equal(Utc(11, 1), dto.To);
        Assert.Equal(Utc(8, 31), dto.PriorFrom);
        Assert.All(dto.Kpis, k => Assert.Null(k.Error));
        KpiResult R(string key) => dto.Kpis.Single(k => k.Key == key).Result!;

        // Tile 1: [2, 4, 5, 11] days
        var tat = R("sample-tat");
        Assert.Equal(4.5m, tat.Value);
        Assert.Equal(9.2m, tat.Extras["p90"]);
        Assert.Equal(4, tat.N);
        Assert.Equal(10m, tat.Prior!.Value);                  // G0, September
        Assert.Equal(1, tat.Prior.N);

        // Tile 2: G1 and G3 on time, G2 late (review 27 h); G5 has no preparation record
        var onTime = R("on-time-all-stages");
        Assert.Equal(0.6667m, onTime.Value);
        Assert.Equal(3, onTime.N);
        Assert.Equal(1, onTime.Gaps.Single(g => g.Code == "NO_ASSIGNMENT_TIME").Count);
        Assert.DoesNotContain(onTime.Gaps, g => g.Code == "DEFAULT_TARGET");

        // Tile 3: units 2 + 2 + 2 of 2 + 4 + 2 + 2 (G2 was returned)
        var rft = R("right-first-time");
        Assert.Equal(6m, rft.Numerator);
        Assert.Equal(10m, rft.Denominator);
        Assert.Equal(0.6m, rft.Value);
        Assert.Equal(4, rft.N);
        Assert.Equal(KpiStatus.Ok, rft.Status);
        Assert.Equal(1m, rft.Prior!.Value);                   // G0: 2 of 2 units

        // Tiles 4 and 5: five decided samples, one retest (G4), one OOS group (G4)
        Assert.Equal(0.2m, R("retest-rate").Value);
        Assert.Equal(0.2m, R("oos-rate").Value);
        Assert.Equal(5, R("retest-rate").N);
        Assert.Equal(0m, R("retest-rate").Prior!.Value);

        // Tile 6 and aging: G6's running test, 3.125 days of a 10-day target
        Assert.Equal(1m, R("wip-backlog").Value);
        var aging = R("backlog-aging");
        Assert.Equal(1m, aging.Series.Single(p => p.Group == "Testing" && p.Label == "within50").Value);
        Assert.Equal(1m, aging.Value);

        // Tile 7: one read on time, one 10 h late
        var incubation = R("incubation-reads-on-time");
        Assert.Equal(0.5m, incubation.Value);
        Assert.Equal(2, incubation.N);

        // Tile 8: 2 excursions of 8 results
        var em = R("em-excursion-rate");
        Assert.Equal(0.25m, em.Value);
        Assert.Equal(0.125m, em.Extras["alertRate"]);
        Assert.Equal(0.125m, em.Extras["actionRate"]);
        Assert.Contains("AdverseTrend", em.Flags);

        // Supporting KPIs
        var stage = R("stage-tat");
        Assert.Equal(2m, stage.Series.Single(p => p.Label == "Testing").Value);   // [1, 2, 4] days, G5 has no clock start
        Assert.Equal(3, stage.Series.Single(p => p.Label == "Testing").N);
        Assert.Equal(2, stage.Series.Single(p => p.Label == "Incubation").N);
        var returns = R("returns-by-test");
        Assert.Equal(1m, returns.Value);
        Assert.Equal("KPIG_GRAV", Assert.Single(returns.Series).Label);

        // Records trace back to the figure
        var page = await service.GetRecordsAsync("sample-tat", user, true, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null, null, null, null, 1, 2);
        Assert.Equal(4, page.Total);
        Assert.Equal(new[] { "KPIG-CN-G1", "KPIG-CN-G2" }, page.Items.Select(i => i.ControlNumber));

        // Section isolation: the other lab sees only its own sample
        var otherService = TestServiceFactory.KpiDashboard(db, clock, new StubSectionScope(_ => new[] { otherLab.Id }));
        var otherDto = await otherService.GetDashboardAsync(user, true, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null, null, null, null);
        var otherTat = otherDto.Kpis.Single(k => k.Key == "sample-tat").Result!;
        Assert.Equal(3m, otherTat.Value);
        Assert.Equal(1, otherTat.N);
    }

    private static WorkflowProfileVersion Profile(WorkflowType type, string testCode, decimal units, decimal target, int userId) => new()
    {
        WorkflowType = type, TestCode = testCode, WorkUnits = units, TargetAnalystDays = target,
        EffectiveFrom = new DateOnly(2026, 1, 1), Status = WorkflowProfileStatus.Effective,
        ReasonForChange = "KPI golden dataset", CreatedByUserId = userId
    };
}

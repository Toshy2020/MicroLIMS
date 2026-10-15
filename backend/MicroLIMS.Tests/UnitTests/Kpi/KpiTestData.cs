using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Tests.UnitTests.Kpi;

// Fixed-clock fixtures for the KPI tests. October 2026 is the period under
// test and "now" is 15 Oct 12:00 UTC; the prior period is 31 Aug - 30 Sep.
// KPIs read the clock from KpiQuery.Now, so no wall-clock value reaches them.
internal static class KpiTestData
{
    public static DateTime Utc(int month, int day, int hour = 0, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    public static readonly DateTime Now = Utc(10, 15, 12);
    public static readonly DateTime PeriodFrom = Utc(10, 1);
    public static readonly DateTime PeriodTo = Utc(11, 1);

    public static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public static KpiQuery Query(IReadOnlyList<int>? sections = null, string? category = null, string? location = null,
        string? testCode = null, int? analystId = null, bool canViewTeam = true, int viewer = 1,
        DateTime? from = null, DateTime? to = null) =>
        new(from ?? PeriodFrom, to ?? PeriodTo, sections, category, location, testCode, analystId, Now, viewer, canViewTeam);

    public static void Analyst(MicroLimsDbContext db, int id, string name) =>
        db.Users.Add(new User { Id = id, FullName = name, Username = $"user{id}", PasswordHash = "x", RoleId = 1 });

    public static void Definition(MicroLimsDbContext db, string code, WorkflowType type, int sectionId = 1) =>
        db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = code, SectionId = sectionId, WorkflowType = type });

    public static void Profile(MicroLimsDbContext db, WorkflowType type, string? testCode, decimal? units, decimal? target, DateOnly from) =>
        db.WorkflowProfileVersions.Add(new WorkflowProfileVersion
        {
            WorkflowType = type, TestCode = testCode, WorkUnits = units, TargetAnalystDays = target,
            EffectiveFrom = from, Status = WorkflowProfileStatus.Effective, ReasonForChange = "seed", CreatedByUserId = 1
        });

    // A sample with its final decision and one test (id = sample id * 10).
    public static Sample ApprovedSample(MicroLimsDbContext db, int id, DateTime received, DateTime approved,
        string testCode = "TAMC", int sectionId = 1, SampleCategory category = SampleCategory.FinishedProduct,
        int? originSampleId = null, int? analyst = 1, ApprovalDecision decision = ApprovalDecision.Approve,
        ApprovalStatus testStatus = ApprovalStatus.Approved, bool superseded = false)
    {
        var sample = new Sample
        {
            Id = id, ControlNumber = $"CN-{id}", ReferenceNumber = $"REF-{id}", Category = category,
            ReceivedAt = received, ApprovedAt = approved, ApprovalDecision = decision,
            Status = SampleStatus.Approved, OriginSampleId = originSampleId
        };
        db.Samples.Add(sample);
        Test(db, sample, id * 10, testCode, sectionId, testStatus, WorkflowStep.Approved, analyst, superseded);
        return sample;
    }

    public static TestOrder Test(MicroLimsDbContext db, Sample sample, int id, string code, int sectionId = 1,
        ApprovalStatus status = ApprovalStatus.Approved, WorkflowStep step = WorkflowStep.Approved,
        int? analyst = 1, bool superseded = false)
    {
        var test = new TestOrder
        {
            Id = id, SampleId = sample.Id, Sample = sample, TestCode = code, SectionId = sectionId,
            Status = status, CurrentStep = step, AssignedAnalystId = analyst, IsSuperseded = superseded
        };
        db.TestOrders.Add(test);
        return test;
    }

    // signedAt: the decision signature's SignedAt. Production writes it for every decision, including
    // retest decisions that leave ApprovedAt null; leave it null for fixtures that do not care.
    public static SampleSectionSignoff Signoff(MicroLimsDbContext db, int sampleId, int sectionId, DateTime? submitted,
        DateTime? reviewed, DateTime? approved, SectionSignoffStatus status = SectionSignoffStatus.Approved,
        ApprovalDecision? decision = ApprovalDecision.Approve, DateTime? signedAt = null)
    {
        var signoff = new SampleSectionSignoff
        {
            SampleId = sampleId, SectionId = sectionId, Status = status, SubmittedForReviewAt = submitted,
            ReviewedAt = reviewed, ApprovedAt = approved, ApprovalDecision = decision
        };
        if (signedAt is { } at)
            signoff.ApprovalSignature = new ElectronicSignature
            {
                UserId = 2, UserFullNameSnapshot = "Head", UsernameSnapshot = "head", RoleSnapshot = "SectionHead",
                MeaningOfSignature = SignatureMeaning.RetestRequested, EntityType = "Sample", EntityId = sampleId, SignedAt = at
            };
        db.SampleSectionSignoffs.Add(signoff);
        return signoff;
    }

    public static void Prepared(MicroLimsDbContext db, int sampleId, DateTime at) =>
        db.SamplePreparations.Add(new SamplePreparation { SampleId = sampleId, PreparedAt = at, PreparedByUserId = 1, Technique = "PourPlate" });

    public static void Timeline(MicroLimsDbContext db, int sampleId, DateTime prepared, DateTime submitted,
        DateTime reviewed, DateTime approved, int sectionId = 1)
    {
        Prepared(db, sampleId, prepared);
        Signoff(db, sampleId, sectionId, submitted, reviewed, approved);
    }

    public static void Return(MicroLimsDbContext db, int testOrderId, DateTime at, string? reason = "re-count") =>
        db.TestReturnEvents.Add(new TestReturnEvent { TestOrderId = testOrderId, ReviewerUserId = 2, AssignedAnalystId = 1, Reason = reason, ReturnedAt = at });
}

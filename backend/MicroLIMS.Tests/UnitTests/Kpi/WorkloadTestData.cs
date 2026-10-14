using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;

namespace MicroLIMS.Tests.UnitTests.Kpi;

// Shared seed for the S3 workload tests. Every DateTime is UTC so the same data also loads into Postgres.
internal static class WorkloadTestData
{
    public const int Alice = 1, Bob = 2, Sam = 3, Dana = 4, Carol = 5;

    public static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly WeekFrom = new(2026, 10, 5);
    public static readonly DateOnly WeekTo = new(2026, 10, 11);

    public static DateTime At(int day, int hour = 9) => new(2026, 10, day, hour, 0, 0, DateTimeKind.Utc);
    public static FixedTimeProvider Clock() => new(Now);

    public static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CurrentUserId = Sam;
        return db;
    }

    // NOTE: on a MIGRATED Postgres database QC/MICRO already exist (AddOrgSectionSegregation). Postgres callers must run
    // DELETE FROM "DocumentSections"; DELETE FROM "DocumentDepartments"; before this (Task 9 does).
    // Sections 1 (Micro) and 2 (FP). Alice, Bob and Carol work in section 1, Dana in section 2, Sam (Section Head) in section 1.
    public static async Task SeedBaselineAsync(MicroLimsDbContext db)
    {
        db.DocumentDepartments.Add(new DocumentDepartment { Id = 1, Code = "QC", Name = "Quality Control" });
        db.DocumentSections.AddRange(
            new DocumentSection { Id = 1, Code = "MICRO", Name = "Microbiology", DepartmentId = 1 },
            new DocumentSection { Id = 2, Code = "FP", Name = "Finished Product", DepartmentId = 1 });
        db.Roles.AddRange(
            new Role { Id = 1, Type = RoleType.Analyst, Name = "Analyst" },
            new Role { Id = 2, Type = RoleType.SectionHead, Name = "Section Head" });
        db.Users.AddRange(
            NewUser(Alice, "Alice Analyst", 1), NewUser(Bob, "Bob Analyst", 1), NewUser(Sam, "Sam Head", 2),
            NewUser(Dana, "Dana Analyst", 1), NewUser(Carol, "carol analyst", 1));
        db.UserOrgMemberships.AddRange(
            Member(Alice, 1), Member(Bob, 1), Member(Sam, 1), Member(Dana, 2), Member(Carol, 1));
        db.CausesOfTesting.Add(new CauseOfTesting { Id = 1, Name = "Routine" });
        db.TestDefinitions.AddRange(
            Definition("TAMC", WorkflowType.CountTest), Definition("PH", WorkflowType.Measurement),
            Definition("OBS", WorkflowType.Observation), Definition("NOPROF", WorkflowType.Gravimetric));
        db.WorkflowProfileVersions.AddRange(
            Profile(null, WorkflowType.CountTest, 2m, 5m),
            Profile(null, WorkflowType.Measurement, 4m, 10m),
            Profile(null, WorkflowType.Observation, 1m, 3m));
        await db.SaveChangesAsync();
    }

    public static WorkflowProfileVersion Profile(string? testCode, WorkflowType type, decimal? units, decimal? target,
        DateOnly? effectiveFrom = null, WorkflowProfileStatus status = WorkflowProfileStatus.Effective) => new()
    {
        WorkflowType = type, TestCode = testCode, WorkUnits = units, TargetAnalystDays = target,
        EffectiveFrom = effectiveFrom ?? new DateOnly(2026, 1, 1), Status = status,
        ReasonForChange = "Seed", CreatedByUserId = Sam
    };

    // A delivered test: Approved order, assigned, with its section signoff approved inside the reference week unless told otherwise.
    // Order id == sample id unless sampleId is given (one test per sample keeps the on-time maths per test).
    public static async Task DeliverAsync(MicroLimsDbContext db, int id, string code, int analystId, DateTime approvedAt,
        DateTime? preparedAt = null, DateTime? submittedAt = null, bool returned = false, int? sampleId = null,
        int sectionId = 1, bool superseded = false, SectionSignoffStatus signoff = SectionSignoffStatus.Approved,
        bool noPreparation = false)
    {
        var sid = sampleId ?? id;
        await EnsureSampleAsync(db, sid);
        db.TestOrders.Add(new TestOrder
        {
            Id = id, SampleId = sid, TestCode = code, SectionId = sectionId,
            Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved,
            AssignedAnalystId = analystId, IsSuperseded = superseded
        });
        if (!await db.SampleSectionSignoffs.AnyAsync(s => s.SampleId == sid && s.SectionId == sectionId))
        {
            db.SampleSectionSignoffs.Add(new SampleSectionSignoff
            {
                SampleId = sid, SectionId = sectionId, Status = signoff,
                SubmittedForReviewAt = submittedAt ?? approvedAt.AddHours(-2), ApprovedAt = approvedAt,
                ApprovalDecision = signoff == SectionSignoffStatus.Approved ? ApprovalDecision.Approve : ApprovalDecision.Reject
            });
        }
        if (!noPreparation && !await db.SamplePreparations.AnyAsync(p => p.SampleId == sid))
            db.SamplePreparations.Add(new SamplePreparation { SampleId = sid, PreparedByUserId = analystId, PreparedAt = preparedAt ?? At(5) });
        if (returned)
            db.TestReturnEvents.Add(new TestReturnEvent { TestOrderId = id, ReviewerUserId = Sam, AssignedAnalystId = analystId, ReturnedAt = approvedAt.AddHours(-30) });
        await db.SaveChangesAsync();
    }

    // An open test (Pending/InProgress). Its sample defaults to 1000 + id so it never collides with a delivered test's sample.
    public static async Task OpenAsync(MicroLimsDbContext db, int id, string code, int? analystId,
        ApprovalStatus status = ApprovalStatus.InProgress, int? sampleId = null, int sectionId = 1)
    {
        var sid = sampleId ?? 1000 + id;
        await EnsureSampleAsync(db, sid);
        db.TestOrders.Add(new TestOrder
        {
            Id = id, SampleId = sid, TestCode = code, SectionId = sectionId, Status = status,
            CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = analystId
        });
        await db.SaveChangesAsync();
    }

    public static KpiQuery Query(DateOnly from, DateOnly to, int viewer = Sam, bool team = true, int? analystId = null,
        IReadOnlyList<int>? sections = null) =>
        new(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            sections, null, null, null, analystId, Now.UtcDateTime, viewer, team);

    public static KpiQuery Week(int viewer = Sam, bool team = true, int? analystId = null) =>
        Query(WeekFrom, WeekTo, viewer, team, analystId);

    private static async Task EnsureSampleAsync(MicroLimsDbContext db, int sid)
    {
        if (await db.Samples.AnyAsync(s => s.Id == sid)) return;
        db.Samples.Add(new Sample
        {
            Id = sid, ReferenceNumber = $"R{sid:D5}", ControlNumber = $"FP-{sid}", Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = 1, ReceivedByUserId = Sam, ReceivedAt = At(5, 8)
        });
        await db.SaveChangesAsync();
    }

    private static User NewUser(int id, string name, int roleId) =>
        new() { Id = id, FullName = name, Username = name.Replace(' ', '.').ToLowerInvariant(), RoleId = roleId, PasswordHash = "x" };

    private static UserOrgMembership Member(int userId, int sectionId) =>
        new() { UserId = userId, DepartmentId = 1, SectionId = sectionId };

    private static TestDefinition Definition(string code, WorkflowType type) =>
        new() { Code = code, DisplayName = code, SectionId = 1, WorkflowType = type, IsActive = true };
}

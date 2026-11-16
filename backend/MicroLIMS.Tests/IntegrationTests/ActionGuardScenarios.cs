using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The refusal states of the gated actions (C8 Task 5), one arrangement each, with the exception type and the exact
// message the action gives in that state. Used twice: ActionGuardCharacterisationPostgresTests pins what each action
// refuses, and ActionAvailabilityPostgresTests proves the availability reason is that same sentence.
//
// Every helper opens its own DbContext; an Attempt runs the action the way its endpoint does, minus the permission
// policy: the controller's section-scope guard first where the controller has one, then the service.
internal sealed class ActionGuardScenarios
{
    public const string Password = "ActionGuard-Test-1!";
    public const string OtherSection = "This test belongs to a laboratory section you are not assigned to.";
    public static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private static readonly string PasswordHash = TestPasswords.Hash(Password);
    private readonly PostgresTestFixture _fixture;

    public ActionGuardScenarios(PostgresTestFixture fixture) => _fixture = fixture;

    // UserId acts on RecordId (sample, test order, media lot or document revision, by action).
    public sealed record Target(int UserId, int RecordId, int? SectionId = null, int? ApprovalTaskId = null, int? OtherUserId = null);

    public sealed record Arranged(Target Target, string Message);

    // Unreadable: the acting user may not read the record at all, so the availability endpoint answers 404 and
    // never shows this sentence.
    public sealed record Refusal(string Name, Type ExceptionType, Func<Task<Arranged>> Arrange, bool Unreadable = false)
    {
        public override string ToString() => Name;
    }

    private static Refusal Rule(string name, Func<Task<Arranged>> arrange) => new(name, typeof(BusinessRuleException), arrange);
    private static Refusal Denied(string name, Func<Task<Arranged>> arrange, bool unreadable = false) =>
        new(name, typeof(UnauthorizedAccessException), arrange, unreadable);

    // ---- permission sets ----

    public static readonly string[] ReviewerCodes = { PermissionConstants.SamplesReview };
    public static readonly string[] HeadCodes =
    {
        PermissionConstants.SamplesReview, PermissionConstants.SamplesApprove, PermissionConstants.SamplesCorrect,
        PermissionConstants.SamplesAssignAnalyst, PermissionConstants.MediaPrepare, PermissionConstants.MediaRelease
    };
    public static readonly string[] AnalystCodes = { PermissionConstants.TestsSelfAssign };

    private static string[] WithViewAll(string[] codes) => codes.Append(PermissionConstants.SectionsViewAll).ToArray();

    // ---- arrange helpers ----

    public async Task<DocumentSection> SectionAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var template = await db.DocumentSections.AsNoTracking().FirstAsync(s => s.Code == "MICRO");
        var uid = Guid.NewGuid().ToString("N")[..8];
        var section = new DocumentSection { DepartmentId = template.DepartmentId, Name = $"Guard {uid}", Code = $"G{uid}", IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        return section;
    }

    // A role of its own holding exactly these permission codes.
    public async Task<User> UserAsync(RoleType roleType, string[] codes, params DocumentSection[] memberOf)
    {
        await using var db = _fixture.CreateDbContext();
        var uid = Guid.NewGuid().ToString("N")[..8];
        var role = new Role { Type = roleType, Name = $"Guard {roleType} {uid}", IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        foreach (var code in codes)
        {
            var permission = await db.Permissions.AsNoTracking().SingleAsync(p => p.Code == code);
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

        var user = new User { Username = $"guard_{uid}", FullName = $"Guard {roleType} {uid}", PasswordHash = PasswordHash, RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        foreach (var section in memberOf)
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = section.DepartmentId, SectionId = section.Id });
        await db.SaveChangesAsync();
        return user;
    }

    public Task<User> ReviewerAsync(params DocumentSection[] memberOf) => UserAsync(RoleType.Reviewer, ReviewerCodes, memberOf);
    public Task<User> HeadAsync(params DocumentSection[] memberOf) => UserAsync(RoleType.SectionHead, HeadCodes, memberOf);
    public Task<User> AnalystAsync(params DocumentSection[] memberOf) => UserAsync(RoleType.Analyst, AnalystCodes, memberOf);

    // Reads every section (Sections.ViewAll) and belongs to none: readable, but refused wherever writing follows membership.
    public Task<User> OutsiderAsync(RoleType roleType, string[] codes) => UserAsync(roleType, WithViewAll(codes));

    public sealed record SampleSeed(Sample Sample, List<TestOrder> Orders);

    // One section's tests on a new sample, in the given state. signoff = null leaves the section without a row.
    public async Task<SampleSeed> SampleAsync(DocumentSection section, SampleStatus status, ApprovalStatus orderStatus, WorkflowStep step,
        SectionSignoffStatus? signoff = null, int? reviewedBy = null, int orders = 1, int? analystId = null, string? testCode = null)
    {
        await using var db = _fixture.CreateDbContext();
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is null)
        {
            cause = new CauseOfTesting { Name = "Routine Testing", IsActive = true };
            db.CausesOfTesting.Add(cause);
            await db.SaveChangesAsync();
        }

        var receiver = await db.Users.AsNoTracking().OrderBy(u => u.Id).FirstAsync();
        var uid = Guid.NewGuid().ToString("N")[..10];
        var sample = new Sample
        {
            ReferenceNumber = $"AG-{uid}", Category = SampleCategory.FinishedProduct, CauseOfTestingId = cause.Id,
            ReceivedAt = Now.AddHours(-3), ReceivedByUserId = receiver.Id, Status = status,
            ReviewedByUserId = signoff is null ? reviewedBy : null, ReviewedAt = signoff is null && reviewedBy is not null ? Now.AddHours(-1) : null
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var seed = new SampleSeed(sample, new List<TestOrder>());
        await AddOrdersAsync(db, seed, section, orderStatus, step, signoff, reviewedBy, orders, analystId, testCode);
        return seed;
    }

    // Tests of another section on the same sample.
    public async Task AddSectionAsync(SampleSeed seed, DocumentSection section, ApprovalStatus orderStatus, WorkflowStep step,
        SectionSignoffStatus? signoff = null, int? reviewedBy = null, int orders = 1, int? analystId = null)
    {
        await using var db = _fixture.CreateDbContext();
        await AddOrdersAsync(db, seed, section, orderStatus, step, signoff, reviewedBy, orders, analystId, null);
    }

    private static async Task AddOrdersAsync(MicroLimsDbContext db, SampleSeed seed, DocumentSection section, ApprovalStatus orderStatus,
        WorkflowStep step, SectionSignoffStatus? signoff, int? reviewedBy, int orders, int? analystId, string? testCode)
    {
        for (var i = 0; i < orders; i++)
        {
            var order = new TestOrder
            {
                SampleId = seed.Sample.Id, TestCode = testCode ?? $"T{Guid.NewGuid():N}"[..12], SectionId = section.Id,
                Status = orderStatus, CurrentStep = step, AssignedAnalystId = analystId
            };
            db.TestOrders.Add(order);
            seed.Orders.Add(order);
        }

        if (signoff is { } signoffStatus)
            db.SampleSectionSignoffs.Add(new SampleSectionSignoff
            {
                SampleId = seed.Sample.Id, SectionId = section.Id, Status = signoffStatus,
                ReviewedByUserId = reviewedBy, ReviewedAt = reviewedBy is null ? null : Now.AddHours(-1)
            });
        await db.SaveChangesAsync();
    }

    public Task<SampleSeed> UnderReviewAsync(DocumentSection section, int orders = 1, int? analystId = null, string? testCode = null) =>
        SampleAsync(section, SampleStatus.UnderReview, ApprovalStatus.ResultEntered, WorkflowStep.Ready, SectionSignoffStatus.UnderReview,
            orders: orders, analystId: analystId, testCode: testCode);

    public Task<SampleSeed> UnderApprovalAsync(DocumentSection section, int reviewedBy, int orders = 1, int? analystId = null, string? testCode = null) =>
        SampleAsync(section, SampleStatus.UnderApproval, ApprovalStatus.Reviewed, WorkflowStep.Reviewed, SectionSignoffStatus.UnderApproval,
            reviewedBy, orders, analystId, testCode);

    public Task<SampleSeed> InTestingAsync(DocumentSection section, int orders = 1, int? analystId = null,
        ApprovalStatus orderStatus = ApprovalStatus.Pending, WorkflowStep step = WorkflowStep.Waiting, string? testCode = null) =>
        SampleAsync(section, SampleStatus.InTesting, orderStatus, step, orders: orders, analystId: analystId, testCode: testCode);

    public async Task<string> TestDefinitionAsync(DocumentSection section, WorkflowType workflowType, bool requiresSystemSuitability = false,
        MoistureRole moistureRole = MoistureRole.None)
    {
        await using var db = _fixture.CreateDbContext();
        var code = $"AG{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        db.TestDefinitions.Add(new TestDefinition
        {
            Code = code, DisplayName = $"Guard test {code}", SectionId = section.Id, WorkflowType = workflowType,
            RequiresSystemSuitability = requiresSystemSuitability, MoistureRole = moistureRole, IsActive = true
        });
        await db.SaveChangesAsync();
        return code;
    }

    // An active analysis entry on the order, with one active result. Returns the result's id.
    public async Task<int> AnalysisAsync(int testOrderId, User enteredBy, string? conditionsJson = null,
        ResultStatus comparison = ResultStatus.WithinLimits)
    {
        await using var db = _fixture.CreateDbContext();
        var signature = new ElectronicSignature
        {
            UserId = enteredBy.Id, UserFullNameSnapshot = enteredBy.FullName, UsernameSnapshot = enteredBy.Username, RoleSnapshot = nameof(RoleType.Analyst),
            MeaningOfSignature = SignatureMeaning.Reviewed, EntityType = "TestOrder", EntityId = testOrderId, SignedAt = Now.AddHours(-2)
        };
        db.ElectronicSignatures.Add(signature);
        await db.SaveChangesAsync();
        var analysis = new TestAnalysis
        {
            TestOrderId = testOrderId, AnalysisType = WorkflowType.Titration, AnalysedAt = Now.AddHours(-2), IsActive = true,
            EnteredByUserId = enteredBy.Id, EnteredAt = Now.AddHours(-2), SignatureId = signature.Id, ConditionsJson = conditionsJson
        };
        db.TestAnalyses.Add(analysis);
        await db.SaveChangesAsync();
        var result = new ParameterResult
        {
            TestAnalysisId = analysis.Id, TestOrderId = testOrderId, ParameterName = "Result", ReportedValue = 2.5m, ReportedDisplay = "2.5",
            Unit = "%", ComparisonStatus = comparison, IsActive = true
        };
        db.ParameterResults.Add(result);
        await db.SaveChangesAsync();
        return result.Id;
    }

    public async Task UpdateAsync<T>(int id, Action<T> change) where T : class
    {
        await using var db = _fixture.CreateDbContext();
        var entity = await db.Set<T>().FindAsync(id) ?? throw new Xunit.Sdk.XunitException($"{typeof(T).Name} {id} not found.");
        change(entity);
        await db.SaveChangesAsync();
    }

    public sealed record MediaSeed(Media Media, DocumentSection Section);

    public async Task<MediaSeed> MediaAsync(DocumentSection section, int preparedBy, MediaEvaluationStatus? evaluation = MediaEvaluationStatus.Completed,
        EvaluationOutcome? outcome = EvaluationOutcome.Conform, ApprovalGateStatus approval = ApprovalGateStatus.PendingReview)
    {
        await using var db = _fixture.CreateDbContext();
        var material = new Material
        {
            SectionId = section.Id, MaterialType = MaterialType.DehydratedMedia, MaterialName = $"TSA {Guid.NewGuid():N}"[..12],
            ManufacturerName = "Acme", BatchNumber = $"B-{Guid.NewGuid():N}"[..10], ReceivingDate = Now.AddDays(-30),
            ExpiryDate = Now.AddDays(300), Location = "Shelf", QuantityReceived = 100m, QuantityRemaining = 100m, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        var media = new Media
        {
            MaterialId = material.Id, LotNumber = $"LOT-{Guid.NewGuid():N}"[..14], ManufacturerLot = material.BatchNumber, ManufacturerName = "Acme",
            TotalWeight = 10m, TotalVolume = "500 ml", AutoclaveProgram = "P1", LoadType = "agar (500 ml)", Temperature = 121m, CycleTime = 15,
            CycleNumber = 1, Ph = 7.0m, ExpiryDate = Now.AddDays(60), PreparedAt = Now.AddDays(-5), PreparedByUserId = preparedBy,
            Status = MediaStatus.Prepared, ApprovalStatus = approval
        };
        db.Media.Add(media);
        await db.SaveChangesAsync();
        if (evaluation is { } evaluationStatus)
        {
            db.MediaEvaluations.Add(new MediaEvaluation
            {
                MediaId = media.Id, EvaluationType = EvaluationType.GrowthPromotion, Status = evaluationStatus,
                Outcome = evaluationStatus == MediaEvaluationStatus.Completed ? outcome : null, AssignedAt = Now.AddDays(-4),
                CompletedAt = evaluationStatus == MediaEvaluationStatus.Completed ? Now.AddDays(-1) : null
            });
            await db.SaveChangesAsync();
        }

        return new MediaSeed(media, section);
    }

    public sealed record DocumentSeed(int AuthorId, int ReviewerId, int ApproverId, int MasterId, int RevisionId, int TaskId, int DocumentTypeId);

    // A revision that passed technical review and has a pending approval task for the approver. The approver's
    // role is of approverRole and holds approverCodes (Documents.Approve is needed to be assigned at all).
    public async Task<DocumentSeed> DocumentAwaitingApprovalAsync(RoleType approverRole = RoleType.Reviewer, string[]? approverCodes = null, bool withTask = true)
    {
        var approver = await UserAsync(approverRole, approverCodes ?? new[] { PermissionConstants.DocumentsApprove });

        await using var db = _fixture.CreateDbContext();
        var analystRole = await db.Roles.AsNoTracking().Where(r => r.Type == RoleType.Analyst && r.Name == "Analyst").FirstAsync();
        var reviewerRole = await db.Roles.AsNoTracking().Where(r => r.Type == RoleType.Reviewer && r.Name == "Reviewer").FirstAsync();
        var uid = Guid.NewGuid().ToString("N")[..8];
        var author = new User { FullName = $"Guard Author {uid}", Username = $"gauth_{uid}", PasswordHash = PasswordHash, RoleId = analystRole.Id, IsActive = true };
        var reviewer = new User { FullName = $"Guard Doc Reviewer {uid}", Username = $"grev_{uid}", PasswordHash = PasswordHash, RoleId = reviewerRole.Id, IsActive = true };
        db.Users.AddRange(author, reviewer);
        var docType = new DocumentType { Code = $"G{uid}".ToUpperInvariant(), Name = $"Guard type {uid}", DefaultReviewCycleMonths = 24, IsActive = true };
        db.DocumentTypes.Add(docType);
        await db.SaveChangesAsync();

        var seq = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seq);
        var auth = new DocumentAuthorizationService(db);
        var master = await new DocumentMasterService(db, seq, audit, auth).RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-AG-{Guid.NewGuid():N}"[..16],
            Title: $"Action guard SOP {uid}",
            DocumentTypeId: docType.Id,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: author.Id,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Testing",
            Keywords: new List<string> { "Guard" },
            InitialRevisionNumber: "01",
            ReviewCycleMonths: 24), author.Id);
        var revisionId = master.Revisions[0].Id;

        await new DocumentFileService(db, new InMemoryFileStorageService(), audit, auth).UploadRevisionFileAsync(
            revisionId, FileRole.ControlledPdf, "guard.pdf", "application/pdf", Encoding.UTF8.GetBytes("%PDF-1.4 guard"), author.Id);

        var reviews = new DocumentReviewService(db, audit, auth);
        var reviewTask = await reviews.SubmitForReviewAsync(revisionId, new SubmitForReviewRequest { ReviewerUserId = reviewer.Id, SubmissionNotes = "Ready" }, author.Id);
        await reviews.DecideReviewAsync(reviewTask.Id, new ReviewDecisionRequest { Decision = ReviewDecision.CompleteReview, ReviewNotes = "No findings" }, reviewer.Id);

        var taskId = 0;
        if (withTask)
            taskId = (await new DocumentApprovalService(db, audit, auth, new ElectronicSignatureService(db))
                .CreateApprovalTaskAsync(revisionId, new CreateApprovalTaskRequest(approver.Id, Now.AddDays(7), "Please approve"), author.Id)).Id;

        return new DocumentSeed(author.Id, reviewer.Id, approver.Id, master.Id, revisionId, taskId, docType.Id);
    }

    // ---- the actions, as their endpoints run them (minus the permission policy) ----

    public async Task CompleteReviewAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        await TestServiceFactory.SampleReview(db).CompleteReviewAsync(t.RecordId, t.UserId, Password, "Reviewed", null, t.SectionId);
    }

    public async Task ReturnToAnalystAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        await new UserSectionScopeService(db).EnsureTestOrderAccessAsync(t.UserId, t.RecordId);
        await TestServiceFactory.SampleReview(db).ReturnToAnalystAsync(t.RecordId, t.UserId, "Recount the plates");
    }

    public async Task DecideAsync(Target t, ApprovalDecision decision)
    {
        await using var db = _fixture.CreateDbContext();
        await TestServiceFactory.SampleApproval(db).DecideAsync(t.RecordId, t.UserId, Password, decision, "Decided", null, sectionId: t.SectionId);
    }

    public Task ApproveAsync(Target t) => DecideAsync(t, ApprovalDecision.Approve);
    public Task RejectAsync(Target t) => DecideAsync(t, ApprovalDecision.Reject);

    public async Task VoidAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        await new UserSectionScopeService(db).EnsureSampleAccessAsync(t.UserId, t.RecordId);
        await TestServiceFactory.SampleCorrection(db).VoidAsync(t.RecordId, "Received in error", Password, t.UserId, null);
    }

    public async Task AssignAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        var scope = new UserSectionScopeService(db);
        await scope.EnsureSampleAccessAsync(t.UserId, t.RecordId);
        await new SampleAssignmentService(db, TestServiceFactory.TestingWorkspace(db), scope, new FixedTimeProvider(new DateTimeOffset(Now)))
            .AssignAnalystAsync(t.RecordId, t.OtherUserId, t.UserId);
    }

    private static TestSelfAssignmentService SelfAssignment(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db), new FixedTimeProvider(new DateTimeOffset(Now)));

    public async Task TakeAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        await SelfAssignment(db).TakeAsync(t.RecordId, t.UserId);
    }

    public async Task ReleaseAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        await SelfAssignment(db).ReleaseAsync(t.RecordId, t.UserId);
    }

    public async Task ReleaseMediaAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        await new UserSectionScopeService(db).EnsureMediaAccessAsync(t.UserId, t.RecordId);
        await TestServiceFactory.MediaRelease(db).DecideAsync(t.RecordId, t.UserId, Password, true, "Released", null);
    }

    public async Task ApproveDocumentAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new DocumentApprovalService(db, new AuditEventService(db, _fixture.CreateSequenceHelper(db)),
            new DocumentAuthorizationService(db), new ElectronicSignatureService(db));
        await service.ExecuteApprovalDecisionAsync(t.ApprovalTaskId ?? 0,
            new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Approve, "Approved for use", null, Password), t.UserId);
    }

    // ---- allowed states ----

    public async Task<Target> ReviewAllowedAsync()
    {
        var section = await SectionAsync();
        var reviewer = await ReviewerAsync(section);
        var seed = await UnderReviewAsync(section, orders: 2);
        return new Target(reviewer.Id, seed.Sample.Id);
    }

    public async Task<Target> ReturnAllowedAsync()
    {
        var section = await SectionAsync();
        var reviewer = await ReviewerAsync(section);
        var analyst = await AnalystAsync(section);
        var code = await TestDefinitionAsync(section, WorkflowType.CountTest);
        var seed = await UnderReviewAsync(section, analystId: analyst.Id, testCode: code);
        return new Target(reviewer.Id, seed.Orders[0].Id);
    }

    public async Task<Target> ApprovalAllowedAsync()
    {
        var section = await SectionAsync();
        var reviewer = await ReviewerAsync(section);
        var head = await HeadAsync(section);
        var seed = await UnderApprovalAsync(section, reviewer.Id, orders: 2);
        return new Target(head.Id, seed.Sample.Id);
    }

    public async Task<Target> VoidAllowedAsync()
    {
        var section = await SectionAsync();
        var head = await HeadAsync(section);
        var seed = await InTestingAsync(section);
        return new Target(head.Id, seed.Sample.Id);
    }

    public async Task<Target> AssignAllowedAsync()
    {
        var section = await SectionAsync();
        var head = await HeadAsync(section);
        var analyst = await AnalystAsync(section);
        var seed = await InTestingAsync(section, orders: 2);
        return new Target(head.Id, seed.Sample.Id, OtherUserId: analyst.Id);
    }

    public async Task<Target> TakeAllowedAsync()
    {
        var section = await SectionAsync();
        var analyst = await AnalystAsync(section);
        var seed = await InTestingAsync(section);
        return new Target(analyst.Id, seed.Orders[0].Id);
    }

    // The analyst took the test and has recorded nothing.
    public async Task<Target> ReleaseAllowedAsync()
    {
        var target = await TakeAllowedAsync();
        await TakeAsync(target);
        return target;
    }

    public async Task<Target> MediaReleaseAllowedAsync()
    {
        var section = await SectionAsync();
        var head = await HeadAsync(section);
        var analyst = await AnalystAsync(section);
        var seed = await MediaAsync(section, analyst.Id);
        return new Target(head.Id, seed.Media.Id);
    }

    public async Task<Target> DocumentApproveAllowedAsync()
    {
        var doc = await DocumentAwaitingApprovalAsync();
        return new Target(doc.ApproverId, doc.RevisionId, ApprovalTaskId: doc.TaskId);
    }

    // ---- refusal states ----

    public IReadOnlyList<Refusal> ReviewCompleteRefusals() => new[]
    {
        Denied("other section", async () =>
        {
            var section = await SectionAsync();
            var outsider = await OutsiderAsync(RoleType.Reviewer, ReviewerCodes);
            var seed = await UnderReviewAsync(section);
            return new Arranged(new Target(outsider.Id, seed.Sample.Id), OtherSection);
        }),
        // Reads every section, belongs to none, and names the section: refused for the section, not for its state.
        Denied("chosen section is not mine", async () =>
        {
            var section = await SectionAsync();
            var outsider = await OutsiderAsync(RoleType.Reviewer, ReviewerCodes);
            var seed = await UnderReviewAsync(section);
            return new Arranged(new Target(outsider.Id, seed.Sample.Id, section.Id), OtherSection);
        }),
        Rule("performed the test", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var seed = await UnderReviewAsync(section, orders: 2, analystId: reviewer.Id);
            return new Arranged(new Target(reviewer.Id, seed.Sample.Id), "You cannot review a sample you tested.");
        }),
        Rule("still in testing", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var seed = await InTestingAsync(section);
            return new Arranged(new Target(reviewer.Id, seed.Sample.Id), "Sample must be under review before it can be reviewed.");
        }),
        Rule("a test is not ready", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var seed = await UnderReviewAsync(section, orders: 2);
            await UpdateAsync<TestOrder>(seed.Orders[1].Id, o => o.CurrentStep = WorkflowStep.Incubating);
            return new Arranged(new Target(reviewer.Id, seed.Sample.Id), "Cannot complete review: one or more tests are not in Ready step.");
        }),
        Rule("already reviewed", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var earlier = await ReviewerAsync(section);
            var seed = await UnderApprovalAsync(section, earlier.Id);
            return new Arranged(new Target(reviewer.Id, seed.Sample.Id), "Sample must be under review before it can be reviewed.");
        }),
        Rule("section has no tests on the sample", async () =>
        {
            var section = await SectionAsync();
            var other = await SectionAsync();
            var reviewer = await ReviewerAsync(section, other);
            var seed = await UnderReviewAsync(section);
            return new Arranged(new Target(reviewer.Id, seed.Sample.Id, other.Id), "This sample has no tests in the selected laboratory section.");
        }),
        Rule("two sections waiting, none chosen", async () =>
        {
            var section = await SectionAsync();
            var second = await SectionAsync();
            var reviewer = await ReviewerAsync(section, second);
            var seed = await UnderReviewAsync(section);
            await AddSectionAsync(seed, second, ApprovalStatus.ResultEntered, WorkflowStep.Ready, SectionSignoffStatus.UnderReview);
            return new Arranged(new Target(reviewer.Id, seed.Sample.Id), "Choose a laboratory section - more than one section of this sample is waiting.");
        }),
    };

    public IReadOnlyList<Refusal> ReturnToAnalystRefusals() => new[]
    {
        Denied("other section", async () =>
        {
            var section = await SectionAsync();
            var outsider = await OutsiderAsync(RoleType.Reviewer, ReviewerCodes);
            var code = await TestDefinitionAsync(section, WorkflowType.CountTest);
            var seed = await UnderReviewAsync(section, testCode: code);
            return new Arranged(new Target(outsider.Id, seed.Orders[0].Id), OtherSection);
        }),
        Rule("already reviewed", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var earlier = await ReviewerAsync(section);
            var code = await TestDefinitionAsync(section, WorkflowType.CountTest);
            var seed = await UnderApprovalAsync(section, earlier.Id, testCode: code);
            return new Arranged(new Target(reviewer.Id, seed.Orders[0].Id),
                "Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead.");
        }),
        Rule("no result entered", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var code = await TestDefinitionAsync(section, WorkflowType.CountTest);
            var seed = await InTestingAsync(section, testCode: code);
            return new Arranged(new Target(reviewer.Id, seed.Orders[0].Id),
                "Cannot return a test order in Pending status. Only test orders in ResultEntered status can be returned to the analyst.");
        }),
        Rule("test definition missing", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var seed = await UnderReviewAsync(section);
            return new Arranged(new Target(reviewer.Id, seed.Orders[0].Id), $"Test definition \"{seed.Orders[0].TestCode}\" not found.");
        }),
        Rule("workflow cannot be returned", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var code = await TestDefinitionAsync(section, WorkflowType.Observation);
            var seed = await UnderReviewAsync(section, testCode: code);
            return new Arranged(new Target(reviewer.Id, seed.Orders[0].Id),
                $"Return to Analyst is only supported for Count Test and result-entry (analysis) workflows. \"{code}\" is a Observation workflow.");
        }),
        // The water result was read by an assay of the same sample, and that assay is already reviewed.
        Rule("an assay that used this moisture result is reviewed", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var analyst = await AnalystAsync(section);
            var waterCode = await TestDefinitionAsync(section, WorkflowType.Titration, moistureRole: MoistureRole.Water);
            var assayCode = await TestDefinitionAsync(section, WorkflowType.Titration);
            var seed = await UnderReviewAsync(section, testCode: waterCode);
            await AddSectionAsync(seed, section, ApprovalStatus.Reviewed, WorkflowStep.Reviewed);
            var assay = seed.Orders[1];
            await UpdateAsync<TestOrder>(assay.Id, o => o.TestCode = assayCode);
            var waterResultId = await AnalysisAsync(seed.Orders[0].Id, analyst);
            await AnalysisAsync(assay.Id, analyst, $"{{\"moisture\":{{\"parameterResultId\":{waterResultId}}}}}");
            return new Arranged(new Target(reviewer.Id, seed.Orders[0].Id), $"Cannot return {waterCode}: assay {assayCode} has already been reviewed.");
        }),
    };

    // Refused for Approve and for Reject alike.
    public IReadOnlyList<Refusal> ApprovalRefusals() => new[]
    {
        Denied("other section", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var outsider = await OutsiderAsync(RoleType.SectionHead, HeadCodes);
            var seed = await UnderApprovalAsync(section, reviewer.Id);
            return new Arranged(new Target(outsider.Id, seed.Sample.Id), OtherSection);
        }),
        Rule("reviewed it", async () =>
        {
            var section = await SectionAsync();
            var head = await HeadAsync(section);
            var seed = await UnderApprovalAsync(section, head.Id);
            return new Arranged(new Target(head.Id, seed.Sample.Id), "You cannot approve a sample you reviewed.");
        }),
        // Recorded before per-section sign-off: no sign-off row, the review is on the sample itself.
        Rule("reviewed it (sample-level review, no sign-off row)", async () =>
        {
            var section = await SectionAsync();
            var head = await HeadAsync(section);
            var seed = await SampleAsync(section, SampleStatus.UnderApproval, ApprovalStatus.Reviewed, WorkflowStep.Reviewed, reviewedBy: head.Id);
            return new Arranged(new Target(head.Id, seed.Sample.Id), "You cannot approve a sample you reviewed.");
        }),
        Rule("performed the test", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var head = await HeadAsync(section);
            var seed = await UnderApprovalAsync(section, reviewer.Id, orders: 2, analystId: head.Id);
            return new Arranged(new Target(head.Id, seed.Sample.Id), "You cannot approve a sample you tested.");
        }),
        Rule("not reviewed yet", async () =>
        {
            var section = await SectionAsync();
            var head = await HeadAsync(section);
            var seed = await UnderReviewAsync(section);
            return new Arranged(new Target(head.Id, seed.Sample.Id), "Sample must be under approval before a decision can be made.");
        }),
        Rule("already decided", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var head = await HeadAsync(section);
            var seed = await SampleAsync(section, SampleStatus.Approved, ApprovalStatus.Approved, WorkflowStep.Approved, SectionSignoffStatus.Approved, reviewer.Id);
            return new Arranged(new Target(head.Id, seed.Sample.Id), "Sample must be under approval before a decision can be made.");
        }),
    };

    // Prerequisites of an approval only: in these states Reject is allowed.
    public IReadOnlyList<Refusal> ApproveOnlyRefusals() => new[]
    {
        Rule("system suitability required on a test that cannot supply one", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var head = await HeadAsync(section);
            var code = await TestDefinitionAsync(section, WorkflowType.CountTest, requiresSystemSuitability: true);
            var seed = await UnderApprovalAsync(section, reviewer.Id, testCode: code);
            return new Arranged(new Target(head.Id, seed.Sample.Id),
                $"Cannot approve section: test order {seed.Orders[0].Id} (\"{code}\") requires system suitability, but only HPLC workspace tests (assay, dissolution) can supply one. Clear \"Requires system suitability\" for this test in Test Master.");
        }),
        Rule("no HPLC run with a passed system suitability", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var head = await HeadAsync(section);
            var code = await TestDefinitionAsync(section, WorkflowType.HplcMethodAssay, requiresSystemSuitability: true);
            var seed = await UnderApprovalAsync(section, reviewer.Id, testCode: code);
            return new Arranged(new Target(head.Id, seed.Sample.Id),
                $"Cannot approve section: test order {seed.Orders[0].Id} (\"{code}\") lacks an HPLC run with a passed system suitability.");
        }),
        Rule("no active analysis entry", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var head = await HeadAsync(section);
            var code = await TestDefinitionAsync(section, WorkflowType.Titration);
            var seed = await UnderApprovalAsync(section, reviewer.Id, testCode: code);
            return new Arranged(new Target(head.Id, seed.Sample.Id),
                $"Cannot approve section: test order {seed.Orders[0].Id} (\"{code}\") lacks an active titration entry.");
        }),
        Rule("an analysis waits for its next stage", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var analyst = await AnalystAsync(section);
            var head = await HeadAsync(section);
            var code = await TestDefinitionAsync(section, WorkflowType.Titration);
            var seed = await UnderApprovalAsync(section, reviewer.Id, testCode: code);
            await AnalysisAsync(seed.Orders[0].Id, analyst, comparison: ResultStatus.NextStageRequired);
            return new Arranged(new Target(head.Id, seed.Sample.Id),
                $"Cannot approve section: test order {seed.Orders[0].Id} (\"{code}\") has a pending stage.");
        }),
    };

    public IReadOnlyList<Refusal> VoidRefusals() => new[]
    {
        Denied("other section", async () =>
        {
            var section = await SectionAsync();
            var outsider = await OutsiderAsync(RoleType.SectionHead, HeadCodes);
            var seed = await InTestingAsync(section);
            return new Arranged(new Target(outsider.Id, seed.Sample.Id), OtherSection);
        }),
        Rule("already voided", async () =>
        {
            var section = await SectionAsync();
            var head = await HeadAsync(section);
            var seed = await SampleAsync(section, SampleStatus.Voided, ApprovalStatus.Voided, WorkflowStep.Waiting, SectionSignoffStatus.Voided);
            return new Arranged(new Target(head.Id, seed.Sample.Id), "Sample is already voided.");
        }),
    };

    public IReadOnlyList<Refusal> AssignRefusals() => new[]
    {
        Denied("other section", async () =>
        {
            var section = await SectionAsync();
            var outsider = await OutsiderAsync(RoleType.SectionHead, HeadCodes);
            var seed = await InTestingAsync(section);
            return new Arranged(new Target(outsider.Id, seed.Sample.Id), OtherSection);
        }),
        Rule("every test is closed", async () =>
        {
            var section = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var head = await HeadAsync(section);
            var seed = await SampleAsync(section, SampleStatus.Approved, ApprovalStatus.Approved, WorkflowStep.Approved, SectionSignoffStatus.Approved, reviewer.Id);
            return new Arranged(new Target(head.Id, seed.Sample.Id), "This sample has no open tests you can assign.");
        }),
        Rule("the open tests are another section's", async () =>
        {
            var section = await SectionAsync();
            var second = await SectionAsync();
            var reviewer = await ReviewerAsync(section);
            var head = await HeadAsync(section);
            var seed = await SampleAsync(section, SampleStatus.InTesting, ApprovalStatus.Approved, WorkflowStep.Approved, SectionSignoffStatus.Approved, reviewer.Id);
            await AddSectionAsync(seed, second, ApprovalStatus.Pending, WorkflowStep.Waiting);
            return new Arranged(new Target(head.Id, seed.Sample.Id), "This sample has no open tests you can assign.");
        }),
    };

    public IReadOnlyList<Refusal> TakeRefusals() => new[]
    {
        Denied("no permission", async () =>
        {
            var section = await SectionAsync();
            var user = await UserAsync(RoleType.Analyst, Array.Empty<string>(), section);
            var seed = await InTestingAsync(section);
            return new Arranged(new Target(user.Id, seed.Orders[0].Id), "You do not have permission to take or release tests.");
        }),
        Denied("other section", async () =>
        {
            var section = await SectionAsync();
            var elsewhere = await SectionAsync();
            var analyst = await AnalystAsync(elsewhere);
            var seed = await InTestingAsync(section);
            return new Arranged(new Target(analyst.Id, seed.Orders[0].Id), OtherSection);
        }, unreadable: true),
        Rule("taking is switched off in the section", async () =>
        {
            var section = await SectionAsync();
            var analyst = await AnalystAsync(section);
            var seed = await InTestingAsync(section);
            await using (var db = _fixture.CreateDbContext())
            {
                db.SectionSelfAssignmentSettings.Add(new SectionSelfAssignmentSetting { SectionId = section.Id, Enabled = false, ChangedAt = Now.AddDays(-1) });
                await db.SaveChangesAsync();
            }

            return new Arranged(new Target(analyst.Id, seed.Orders[0].Id), "Taking tests is switched off in this section. Ask a Section Head to assign this test.");
        }),
        Rule("already taken", async () =>
        {
            var section = await SectionAsync();
            var analyst = await AnalystAsync(section);
            var holder = await AnalystAsync(section);
            var seed = await InTestingAsync(section, analystId: holder.Id);
            return new Arranged(new Target(analyst.Id, seed.Orders[0].Id), $"Already taken by {holder.FullName}.");
        }),
        Rule("closed", async () =>
        {
            var section = await SectionAsync();
            var analyst = await AnalystAsync(section);
            var seed = await UnderReviewAsync(section);
            return new Arranged(new Target(analyst.Id, seed.Orders[0].Id), "This test is closed, replaced or outsourced and can no longer be taken.");
        }),
        Rule("locked out", async () =>
        {
            var section = await SectionAsync();
            var analyst = await AnalystAsync(section);
            await UpdateAsync<User>(analyst.Id, u => u.LockedUntil = Now.AddHours(1));
            var seed = await InTestingAsync(section);
            return new Arranged(new Target(analyst.Id, seed.Orders[0].Id), $"User '{analyst.FullName}' is locked out.");
        }),
        Rule("role cannot be assigned tests", async () =>
        {
            var section = await SectionAsync();
            var qa = await UserAsync(RoleType.Qa, AnalystCodes, section);
            var seed = await InTestingAsync(section);
            return new Arranged(new Target(qa.Id, seed.Orders[0].Id),
                $"'{qa.FullName}' cannot be assigned tests - only Analysts, Reviewers and Section Heads can.");
        }),
        Rule("reads the section but is not a member", async () =>
        {
            var section = await SectionAsync();
            var outsider = await OutsiderAsync(RoleType.Analyst, AnalystCodes);
            var seed = await InTestingAsync(section);
            return new Arranged(new Target(outsider.Id, seed.Orders[0].Id), $"'{outsider.FullName}' is not a member of the {section.Name} laboratory section.");
        }),
    };

    public IReadOnlyList<Refusal> ReleaseRefusals() => new[]
    {
        Denied("no permission", async () =>
        {
            var section = await SectionAsync();
            var user = await UserAsync(RoleType.Analyst, Array.Empty<string>(), section);
            var seed = await InTestingAsync(section, analystId: user.Id);
            return new Arranged(new Target(user.Id, seed.Orders[0].Id), "You do not have permission to take or release tests.");
        }),
        Rule("held by someone else", async () =>
        {
            var section = await SectionAsync();
            var analyst = await AnalystAsync(section);
            var holder = await AnalystAsync(section);
            var seed = await InTestingAsync(section, analystId: holder.Id);
            return new Arranged(new Target(analyst.Id, seed.Orders[0].Id), "Only the analyst who holds this test can release it.");
        }),
        // Put on the test by someone else's assignment, not by Take: there is no TEST_TAKEN row of the analyst's own.
        Rule("assigned, not taken", async () =>
        {
            var section = await SectionAsync();
            var analyst = await AnalystAsync(section);
            var seed = await InTestingAsync(section, analystId: analyst.Id);
            return new Arranged(new Target(analyst.Id, seed.Orders[0].Id), "This test can no longer be released. Ask a Section Head to reassign it.");
        }),
    };

    public IReadOnlyList<Refusal> MediaReleaseRefusals() => new[]
    {
        Denied("other section", async () =>
        {
            var section = await SectionAsync();
            var analyst = await AnalystAsync(section);
            var outsider = await OutsiderAsync(RoleType.SectionHead, HeadCodes);
            var seed = await MediaAsync(section, analyst.Id);
            return new Arranged(new Target(outsider.Id, seed.Media.Id),
                "This record was made from another laboratory section's material and you are not assigned to that section.");
        }),
        Rule("already decided", async () =>
        {
            var section = await SectionAsync();
            var analyst = await AnalystAsync(section);
            var head = await HeadAsync(section);
            var seed = await MediaAsync(section, analyst.Id, approval: ApprovalGateStatus.Approved);
            return new Arranged(new Target(head.Id, seed.Media.Id), $"Media lot {seed.Media.LotNumber} has already been decided (Approved).");
        }),
        Rule("no evaluation", async () =>
        {
            var section = await SectionAsync();
            var analyst = await AnalystAsync(section);
            var head = await HeadAsync(section);
            var seed = await MediaAsync(section, analyst.Id, evaluation: null);
            return new Arranged(new Target(head.Id, seed.Media.Id), $"Media lot {seed.Media.LotNumber} has no evaluation record.");
        }),
        Rule("evaluation not complete", async () =>
        {
            var section = await SectionAsync();
            var analyst = await AnalystAsync(section);
            var head = await HeadAsync(section);
            var seed = await MediaAsync(section, analyst.Id, evaluation: MediaEvaluationStatus.InProgress);
            return new Arranged(new Target(head.Id, seed.Media.Id), $"Media lot {seed.Media.LotNumber}'s evaluation is not complete yet.");
        }),
        Rule("evaluation did not conform", async () =>
        {
            var section = await SectionAsync();
            var analyst = await AnalystAsync(section);
            var head = await HeadAsync(section);
            var seed = await MediaAsync(section, analyst.Id, outcome: EvaluationOutcome.NonConform);
            return new Arranged(new Target(head.Id, seed.Media.Id), $"Media lot {seed.Media.LotNumber}'s evaluation did not conform - it cannot be released.");
        }),
        Rule("prepared the lot", async () =>
        {
            var section = await SectionAsync();
            var head = await HeadAsync(section);
            var seed = await MediaAsync(section, head.Id);
            return new Arranged(new Target(head.Id, seed.Media.Id), "You cannot approve a media lot you prepared or evaluated.");
        }),
    };

    public IReadOnlyList<Refusal> DocumentApproveRefusals() => new[]
    {
        Denied("inactive user", async () =>
        {
            var doc = await DocumentAwaitingApprovalAsync();
            await UpdateAsync<User>(doc.ApproverId, u => u.IsActive = false);
            return new Arranged(new Target(doc.ApproverId, doc.RevisionId, ApprovalTaskId: doc.TaskId), "User is inactive or not found.");
        }, unreadable: true),
        Rule("already decided", async () =>
        {
            var doc = await DocumentAwaitingApprovalAsync();
            var target = new Target(doc.ApproverId, doc.RevisionId, ApprovalTaskId: doc.TaskId);
            await ApproveDocumentAsync(target);
            return new Arranged(target, "Cannot execute decision on approval task in status 'Approved'.");
        }),
        Denied("wrote the revision", async () =>
        {
            var doc = await DocumentAwaitingApprovalAsync();
            return new Arranged(new Target(doc.AuthorId, doc.RevisionId, ApprovalTaskId: doc.TaskId),
                "Segregation of Duties Violation: The author of a document revision cannot approve or decide upon their own revision.");
        }),
        Denied("reviewed the revision", async () =>
        {
            var doc = await DocumentAwaitingApprovalAsync();
            return new Arranged(new Target(doc.ReviewerId, doc.RevisionId, ApprovalTaskId: doc.TaskId),
                "Segregation of Duties Violation: A technical reviewer for this revision cannot approve or decide upon this revision.");
        }),
        Denied("not the assigned approver", async () =>
        {
            var doc = await DocumentAwaitingApprovalAsync();
            var other = await UserAsync(RoleType.Reviewer, new[] { PermissionConstants.DocumentsApprove });
            return new Arranged(new Target(other.Id, doc.RevisionId, ApprovalTaskId: doc.TaskId),
                "Only the assigned approver or Document Controller may execute an approval decision.");
        }),
        Denied("role no longer holds Documents.Approve", async () =>
        {
            var doc = await DocumentAwaitingApprovalAsync();
            await using (var db = _fixture.CreateDbContext())
            {
                var roleId = await db.Users.AsNoTracking().Where(u => u.Id == doc.ApproverId).Select(u => u.RoleId).SingleAsync();
                db.RolePermissions.RemoveRange(await db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync());
                await db.SaveChangesAsync();
            }

            return new Arranged(new Target(doc.ApproverId, doc.RevisionId, ApprovalTaskId: doc.TaskId),
                "Your role does not hold the Documents.Approve permission required to execute an approval decision.");
        }),
        Rule("document type requires a QA approver", async () =>
        {
            var doc = await DocumentAwaitingApprovalAsync();
            await UpdateAsync<DocumentType>(doc.DocumentTypeId, t => t.RequiresQaApprover = true);
            return new Arranged(new Target(doc.ApproverId, doc.RevisionId, ApprovalTaskId: doc.TaskId), "This document type requires approval by Quality Assurance.");
        }),
        Rule("no active controlled PDF", async () =>
        {
            var doc = await DocumentAwaitingApprovalAsync();
            await using (var db = _fixture.CreateDbContext())
            {
                foreach (var file in await db.RevisionFiles.Where(f => f.DocumentRevisionId == doc.RevisionId).ToListAsync())
                    file.IsActive = false;
                await db.SaveChangesAsync();
            }

            return new Arranged(new Target(doc.ApproverId, doc.RevisionId, ApprovalTaskId: doc.TaskId),
                "Cannot approve document revision: Prerequisites not met. Errors: An active Controlled PDF file is required.");
        }),
        Rule("an approval signature already exists", async () =>
        {
            var doc = await DocumentAwaitingApprovalAsync();
            await using (var db = _fixture.CreateDbContext())
            {
                db.ElectronicSignatures.Add(new ElectronicSignature
                {
                    UserId = doc.ReviewerId, UserFullNameSnapshot = "Earlier signer", UsernameSnapshot = "earlier", RoleSnapshot = nameof(RoleType.Reviewer),
                    MeaningOfSignature = SignatureMeaning.Approved, EntityType = "DocumentRevision", EntityId = doc.RevisionId, SignedAt = Now.AddHours(-1)
                });
                await db.SaveChangesAsync();
            }

            return new Arranged(new Target(doc.ApproverId, doc.RevisionId, ApprovalTaskId: doc.TaskId),
                "An electronic signature for approval has already been applied to revision 01.");
        }),
    };

    // ---- the catalogue both test classes walk ----

    // Kind: which availability endpoint lists the action (sample, test-order, media, document-revision).
    public sealed record ActionCase(string Action, string Kind, Func<Task<Target>> Allowed, Func<Target, Task> Attempt,
        Func<Target, Task> AssertDone, IReadOnlyList<Refusal> Refusals)
    {
        public override string ToString() => Action;
    }

    public IReadOnlyList<ActionCase> Actions() => new[]
    {
        new ActionCase("review.complete", "sample", ReviewAllowedAsync, CompleteReviewAsync, AssertReviewedAsync, ReviewCompleteRefusals()),
        new ActionCase("review.return", "test-order", ReturnAllowedAsync, ReturnToAnalystAsync, AssertReturnedAsync, ReturnToAnalystRefusals()),
        new ActionCase("approval.approve", "sample", ApprovalAllowedAsync, ApproveAsync, t => AssertDecidedAsync(t, SampleStatus.Approved),
            ApprovalRefusals().Concat(ApproveOnlyRefusals()).ToList()),
        new ActionCase("approval.reject", "sample", ApprovalAllowedAsync, RejectAsync, t => AssertDecidedAsync(t, SampleStatus.Rejected), ApprovalRefusals()),
        new ActionCase("sample.void", "sample", VoidAllowedAsync, VoidAsync, AssertVoidedAsync, VoidRefusals()),
        new ActionCase("sample.assign", "sample", AssignAllowedAsync, AssignAsync, AssertAssignedAsync, AssignRefusals()),
        new ActionCase("test.take", "test-order", TakeAllowedAsync, TakeAsync, t => AssertHolderAsync(t, t.UserId), TakeRefusals()),
        new ActionCase("test.release", "test-order", ReleaseAllowedAsync, ReleaseAsync, t => AssertHolderAsync(t, null), ReleaseRefusals()),
        new ActionCase("media.release", "media", MediaReleaseAllowedAsync, ReleaseMediaAsync, AssertMediaReleasedAsync, MediaReleaseRefusals()),
        new ActionCase("document.approve", "document-revision", DocumentApproveAllowedAsync, ApproveDocumentAsync, AssertDocumentApprovedAsync, DocumentApproveRefusals()),
    };

    // ---- what each action leaves behind, read in a fresh context ----

    private async Task AssertReviewedAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        var signoff = await db.SampleSectionSignoffs.AsNoTracking().SingleAsync(s => s.SampleId == t.RecordId);
        Assert.Equal(SectionSignoffStatus.UnderApproval, signoff.Status);
        Assert.Equal(t.UserId, signoff.ReviewedByUserId);
        Assert.Equal(SampleStatus.UnderApproval, (await db.Samples.AsNoTracking().SingleAsync(s => s.Id == t.RecordId)).Status);
    }

    private async Task AssertReturnedAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        var order = await db.TestOrders.AsNoTracking().SingleAsync(o => o.Id == t.RecordId);
        Assert.Equal(WorkflowStep.Incubating, order.CurrentStep);
        var returned = await db.TestReturnEvents.AsNoTracking().SingleAsync(e => e.TestOrderId == t.RecordId);
        Assert.Equal(t.UserId, returned.ReviewerUserId);
    }

    private async Task AssertDecidedAsync(Target t, SampleStatus expected)
    {
        await using var db = _fixture.CreateDbContext();
        Assert.Equal(expected, (await db.Samples.AsNoTracking().SingleAsync(s => s.Id == t.RecordId)).Status);
        var signoff = await db.SampleSectionSignoffs.AsNoTracking().SingleAsync(s => s.SampleId == t.RecordId);
        Assert.Equal(t.UserId, signoff.ApprovedByUserId);
    }

    private async Task AssertVoidedAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        Assert.Equal(SampleStatus.Voided, (await db.Samples.AsNoTracking().SingleAsync(s => s.Id == t.RecordId)).Status);
        Assert.All(await db.TestOrders.AsNoTracking().Where(o => o.SampleId == t.RecordId).ToListAsync(), o => Assert.Equal(ApprovalStatus.Voided, o.Status));
    }

    private async Task AssertAssignedAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        var orders = await db.TestOrders.AsNoTracking().Where(o => o.SampleId == t.RecordId).ToListAsync();
        Assert.NotEmpty(orders);
        Assert.All(orders, o => Assert.Equal(t.OtherUserId, o.AssignedAnalystId));
    }

    private async Task AssertHolderAsync(Target t, int? expected)
    {
        await using var db = _fixture.CreateDbContext();
        Assert.Equal(expected, (await db.TestOrders.AsNoTracking().SingleAsync(o => o.Id == t.RecordId)).AssignedAnalystId);
    }

    private async Task AssertMediaReleasedAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        var media = await db.Media.AsNoTracking().SingleAsync(m => m.Id == t.RecordId);
        Assert.Equal(ApprovalGateStatus.Approved, media.ApprovalStatus);
        Assert.True(media.IsReleasedForUse);
        Assert.Equal(t.UserId, media.ApprovedByUserId);
    }

    private async Task AssertDocumentApprovedAsync(Target t)
    {
        await using var db = _fixture.CreateDbContext();
        var task = await db.DocumentApprovalTasks.AsNoTracking().SingleAsync(a => a.Id == t.ApprovalTaskId);
        Assert.Equal(DocumentApprovalTaskStatus.Approved, task.Status);
        Assert.Equal(t.UserId, task.DecisionByUserId);
    }
}

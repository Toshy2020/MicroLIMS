using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MyWork;
using MicroLIMS.Application.Services.MyWork.Sources;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The review and approval sources of My Work against a real database, and the set-based Segregation of
// Duties check they lock with. Every test builds its own laboratory section, so queues seeded by other
// tests never leak in. Arrange, act and assert each use their own DbContext.
[Collection("PostgresDatabaseCollection")]
public class MyWorkReviewSourcesPostgresTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresTestFixture _fixture;

    public MyWorkReviewSourcesPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class Settings : ISystemSettingsReader
    {
        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(7);
        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
    }

    private sealed class RecordingErrors : IErrorCaptureService
    {
        public List<ErrorCaptureRequest> Captured { get; } = new();
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default)
        {
            Captured.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            Interlocked.Increment(ref _count);
            return result;
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return ValueTask.FromResult(result);
        }
    }

    private static MyWorkService BuildService(MicroLimsDbContext db, RecordingErrors? errors = null) =>
        new(db, new UserSectionScopeService(db),
            new LabClock(new FixedTimeProvider(new DateTimeOffset(Now)), LabClock.ResolveTimeZone("Africa/Cairo")),
            new Settings(), errors ?? new RecordingErrors(),
            new IMyWorkSource[] { new ReviewSource(db), new ApprovalSource(db) });

    private async Task<MyWorkResponse> GetAsync(int userId, CommandCounter? counter = null)
    {
        await using var actDb = counter is null ? _fixture.CreateDbContext() : _fixture.CreateDbContext(counter);
        var errors = new RecordingErrors();
        var response = await BuildService(actDb, errors).GetAsync(userId, null);
        Assert.True(errors.Captured.Count == 0, string.Join(" | ", errors.Captured.Select(e => e.Message)));
        Assert.Empty(response.UnavailableSources);
        return response;
    }

    private async Task<MyWorkSummary> SummaryAsync(int userId)
    {
        await using var actDb = _fixture.CreateDbContext();
        var errors = new RecordingErrors();
        var summary = await BuildService(actDb, errors).GetSummaryAsync(userId);
        Assert.Empty(errors.Captured);
        return summary;
    }

    // Sample items are keyed "review:{sample}:{section}" (one per sample and section); this is the sample part.
    private static string SampleKey(MyWorkItem item) => item.Key[..item.Key.LastIndexOf(':')];

    private static MyWorkGroup? Group(MyWorkResponse r, string key) => r.Groups.SingleOrDefault(g => g.Key == key);

    // ---- arrange helpers ----

    private sealed record World(DocumentSection Lab, DocumentSection Other, int CauseId);

    private static async Task<DocumentSection> NewSectionAsync(MicroLimsDbContext db, string label)
    {
        var template = await db.DocumentSections.AsNoTracking().FirstAsync(s => s.Code == "MICRO");
        var uid = Guid.NewGuid().ToString("N")[..8];
        var section = new DocumentSection { DepartmentId = template.DepartmentId, Name = $"{label} {uid}", Code = $"T{uid}", IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        return section;
    }

    private static async Task<World> WorldAsync(MicroLimsDbContext db)
    {
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is null)
        {
            cause = new CauseOfTesting { Name = "Routine Testing", IsActive = true };
            db.CausesOfTesting.Add(cause);
            await db.SaveChangesAsync();
        }

        return new World(await NewSectionAsync(db, "Lab"), await NewSectionAsync(db, "Other"), cause.Id);
    }

    private static async Task<User> NewUserAsync(MicroLimsDbContext db, string label, string[] permissions, params DocumentSection[] memberOf)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var role = new Role { Type = RoleType.Analyst, Name = $"Role {label} {uid}", IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        foreach (var code in permissions)
        {
            var permission = await db.Permissions.SingleAsync(p => p.Code == code);
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

        var user = new User { Username = $"{label}_{uid}", FullName = $"{label} {uid}", PasswordHash = "x", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        foreach (var section in memberOf)
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = section.DepartmentId, SectionId = section.Id });
        await db.SaveChangesAsync();
        return user;
    }

    private static readonly string[] ReviewerPermissions = { "Samples.Review" };
    private static readonly string[] ApproverPermissions = { "Samples.Approve" };

    private sealed record TestSpec(DocumentSection Section, int? AssignedTo = null);

    // A sample with one test per spec, all finished (Ready), the sample and each spec'd section in the given state.
    private static async Task<(Sample Sample, List<TestOrder> Orders)> NewSampleAsync(MicroLimsDbContext db, World w, int receivedBy,
        SampleStatus status, SectionSignoffStatus signoff, DateTime? enteredStatusAt, int? reviewedBy, params TestSpec[] tests)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var sample = new Sample
        {
            ReferenceNumber = $"RV-{uid}",
            Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = w.CauseId,
            ReceivedAt = Now.AddHours(-2),
            ReceivedByUserId = receivedBy,
            Status = status,
            ReviewedByUserId = reviewedBy,
            ReviewedAt = reviewedBy is null ? null : enteredStatusAt
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var orders = new List<TestOrder>();
        foreach (var spec in tests)
        {
            var order = new TestOrder
            {
                SampleId = sample.Id,
                TestCode = $"T{Guid.NewGuid():N}"[..12],
                SectionId = spec.Section.Id,
                Status = status == SampleStatus.UnderApproval ? ApprovalStatus.Reviewed : ApprovalStatus.ResultEntered,
                CurrentStep = status == SampleStatus.UnderApproval ? WorkflowStep.Reviewed : WorkflowStep.Ready,
                AssignedAnalystId = spec.AssignedTo
            };
            db.TestOrders.Add(order);
            orders.Add(order);
        }

        foreach (var sectionId in tests.Select(t => t.Section.Id).Distinct())
        {
            db.SampleSectionSignoffs.Add(new SampleSectionSignoff
            {
                SampleId = sample.Id,
                SectionId = sectionId,
                Status = signoff,
                SubmittedForReviewAt = signoff == SectionSignoffStatus.UnderReview ? enteredStatusAt : null,
                ReviewedByUserId = signoff == SectionSignoffStatus.UnderApproval ? reviewedBy : null,
                ReviewedAt = signoff == SectionSignoffStatus.UnderApproval ? enteredStatusAt : null
            });
        }

        await db.SaveChangesAsync();
        return (sample, orders);
    }

    private static Task<(Sample Sample, List<TestOrder> Orders)> UnderReviewAsync(MicroLimsDbContext db, World w, DocumentSection section,
        DateTime? since = null, params int?[] assignedTo) =>
        NewSampleAsync(db, w, 1, SampleStatus.UnderReview, SectionSignoffStatus.UnderReview, since ?? Now.AddHours(-1), null,
            (assignedTo.Length == 0 ? new int?[] { null } : assignedTo).Select(a => new TestSpec(section, a)).ToArray());

    private static Task<(Sample Sample, List<TestOrder> Orders)> UnderApprovalAsync(MicroLimsDbContext db, World w, DocumentSection section,
        int? reviewedBy = null, DateTime? since = null, params int?[] assignedTo) =>
        NewSampleAsync(db, w, 1, SampleStatus.UnderApproval, SectionSignoffStatus.UnderApproval, since ?? Now.AddHours(-1), reviewedBy,
            (assignedTo.Length == 0 ? new int?[] { null } : assignedTo).Select(a => new TestSpec(section, a)).ToArray());

    private static async Task<Incubation> NewIncubationAsync(MicroLimsDbContext db, TestOrder order)
    {
        var incubation = new Incubation
        {
            TestOrderId = order.Id, StepNumber = 1, StepName = "Plate incubation", StartedAt = Now.AddDays(-2), CompletedAt = Now.AddDays(-1)
        };
        db.Incubations.Add(incubation);
        await db.SaveChangesAsync();
        return incubation;
    }

    private static async Task<Material> NewMaterialAsync(MicroLimsDbContext db, DocumentSection section)
    {
        var material = new Material
        {
            SectionId = section.Id, MaterialType = MaterialType.DehydratedMedia, MaterialName = $"TSA {Guid.NewGuid():N}"[..12],
            ManufacturerName = "Acme", BatchNumber = $"B-{Guid.NewGuid():N}"[..10], ReceivingDate = Now.AddDays(-30),
            ExpiryDate = Now.AddDays(300), Location = "Shelf", QuantityReceived = 100m, QuantityRemaining = 100m, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        return material;
    }

    private static async Task<Media> NewMediaAwaitingApprovalAsync(MicroLimsDbContext db, DocumentSection section, int preparedBy,
        EvaluationOutcome outcome = EvaluationOutcome.Conform, ApprovalGateStatus approval = ApprovalGateStatus.PendingReview,
        int? challengeReadBy = null)
    {
        var material = await NewMaterialAsync(db, section);
        var media = new Media
        {
            MaterialId = material.Id, LotNumber = $"LOT-{Guid.NewGuid():N}"[..14], ManufacturerLot = material.BatchNumber, ManufacturerName = "Acme",
            TotalWeight = 10m, TotalVolume = "500 ml", AutoclaveProgram = "P1", LoadType = "agar (500 ml)", Temperature = 121m, CycleTime = 15,
            CycleNumber = 1, Ph = 7.0m, ExpiryDate = Now.AddDays(60), PreparedAt = Now.AddDays(-5), PreparedByUserId = preparedBy,
            Status = MediaStatus.Prepared, ApprovalStatus = approval
        };
        db.Media.Add(media);
        await db.SaveChangesAsync();
        var evaluation = new MediaEvaluation
        {
            MediaId = media.Id, EvaluationType = EvaluationType.GrowthPromotion, Status = MediaEvaluationStatus.Completed,
            Outcome = outcome, AssignedAt = Now.AddDays(-4), CompletedAt = Now.AddHours(-1)
        };
        if (challengeReadBy is int reader)
        {
            var organism = new Organism { ScientificName = $"Strain {Guid.NewGuid():N}"[..14] };
            db.Organisms.Add(organism);
            await db.SaveChangesAsync();
            evaluation.Challenges.Add(new MediaEvaluationChallenge { OrganismId = organism.Id, ReadByUserId = reader });
        }

        db.MediaEvaluations.Add(evaluation);
        await db.SaveChangesAsync();
        return media;
    }

    // ---- the eight conditions ----

    private static readonly string[] Conditions =
    {
        "assigned analyst", "prepared the sample", "result entered", "count reading entered",
        "pathogen observation", "workflow step submitted", "test analysis entered", "confirmatory plate recorded"
    };

    private static async Task RecordAsync(MicroLimsDbContext db, World w, Sample sample, TestOrder order, int condition, int userId)
    {
        switch (condition)
        {
            case 0:
                (await db.TestOrders.SingleAsync(t => t.Id == order.Id)).AssignedAnalystId = userId;
                break;
            case 1:
                db.SamplePreparations.Add(new SamplePreparation
                {
                    SampleId = sample.Id, Amount = 10m, Technique = "PourPlate", PreparedByUserId = userId, Neutralizer = "Tween", Diluent = "Buffer"
                });
                break;
            case 2:
                db.Results.Add(new Result { TestOrderId = order.Id, RawValue = "10", Type = ResultType.Numeric, EnteredByUserId = userId });
                break;
            case 3:
                db.CountTestReadings.Add(new CountTestReading
                {
                    TestOrderId = order.Id, PlateReadings = "10", DilutionFactor = 1m, ReportedResult = "10", EnteredByUserId = userId
                });
                break;
            case 4:
                db.PathogenObservations.Add(new PathogenObservation
                {
                    TestOrderId = order.Id, StepName = "Selective Plating", StepOrder = 1, Observation = GrowthObservation.GrowthConforming, ObservedByUserId = userId
                });
                break;
            case 5:
            {
                var incubation = await NewIncubationAsync(db, order);
                db.WorkflowStepResults.Add(new WorkflowStepResult
                {
                    IncubationId = incubation.Id, TestOrderId = order.Id, StepName = "Selective Plating", SubmittedByUserId = userId
                });
                break;
            }
            case 6:
            {
                var signature = new ElectronicSignature
                {
                    UserId = userId, UserFullNameSnapshot = "Analyst", UsernameSnapshot = "analyst", RoleSnapshot = "Analyst",
                    MeaningOfSignature = SignatureMeaning.Reviewed, EntityType = "TestAnalysis", EntityId = order.Id
                };
                db.ElectronicSignatures.Add(signature);
                await db.SaveChangesAsync();
                db.TestAnalyses.Add(new TestAnalysis
                {
                    TestOrderId = order.Id, AnalysisType = WorkflowType.Dissolution, AnalysedAt = Now, EnteredByUserId = userId,
                    IsActive = true, SignatureId = signature.Id
                });
                break;
            }
            case 7:
            {
                var incubation = await NewIncubationAsync(db, order);
                var material = await NewMaterialAsync(db, w.Lab);
                var someoneElse = await NewUserAsync(db, "setup", Array.Empty<string>());
                db.WorkflowStepResults.Add(new WorkflowStepResult
                {
                    IncubationId = incubation.Id, TestOrderId = order.Id, StepName = "Confirmatory Plating", SubmittedByUserId = someoneElse.Id,
                    ConfirmatoryObservations =
                    {
                        new ConfirmatoryPlateObservation { MaterialId = material.Id, Observation = GrowthObservation.GrowthConforming, RecordedByUserId = userId }
                    }
                });
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(condition));
        }

        await db.SaveChangesAsync();
    }

    // ---- tests ----

    [PostgresFact]
    public async Task Batch_MatchesSingleCheck_ForEachOfTheEightConditions()
    {
        await PostgresCases.RunAsync(Enumerable.Range(0, Conditions.Length), async condition =>
        {
            int me, other, positive, negative;
            await using (var arrange = _fixture.CreateDbContext())
            {
                var w = await WorldAsync(arrange);
                me = (await NewUserAsync(arrange, "me", Array.Empty<string>(), w.Lab)).Id;
                other = (await NewUserAsync(arrange, "other", Array.Empty<string>(), w.Lab)).Id;
                var (sampleP, ordersP) = await UnderReviewAsync(arrange, w, w.Lab);
                var (sampleN, ordersN) = await UnderReviewAsync(arrange, w, w.Lab);
                positive = ordersP[0].Id;
                negative = ordersN[0].Id;
                // Positive: I did it. Negative: somebody else did exactly the same thing on another test.
                await RecordAsync(arrange, w, sampleP, ordersP[0], condition, me);
                await RecordAsync(arrange, w, sampleN, ordersN[0], condition, other);
            }

            await using var actDb = _fixture.CreateDbContext();
            var guard = new SegregationOfDutiesGuard(actDb);
            var performed = await guard.GetTestOrderIdsPerformedByAsync(me, new[] { positive, negative, int.MaxValue });

            Assert.Contains(positive, performed);
            Assert.DoesNotContain(negative, performed);
            Assert.DoesNotContain(int.MaxValue, performed);
            // The single check is the batch with one id, so the two cannot disagree.
            Assert.True(await guard.DidUserPerformTestAsync(positive, me));
            Assert.False(await guard.DidUserPerformTestAsync(negative, me));
            Assert.False(await guard.DidUserPerformTestAsync(int.MaxValue, me));
        });
    }

    [PostgresFact]
    public async Task UnderReviewInMySection_Appears_OtherSection_Absent()
    {
        int r, mine, mixedSample;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            r = (await NewUserAsync(arrange, "rev", ReviewerPermissions, w.Lab)).Id;
            mine = (await UnderReviewAsync(arrange, w, w.Lab)).Sample.Id;
            await UnderReviewAsync(arrange, w, w.Other);
            // One sample tested by both sections: only my section's half is mine.
            var (sample, _) = await NewSampleAsync(arrange, w, 1, SampleStatus.UnderReview, SectionSignoffStatus.UnderReview, Now.AddHours(-1), null,
                new TestSpec(w.Lab), new TestSpec(w.Other));
            mixedSample = sample.Id;
            // Not under review at all.
            await NewSampleAsync(arrange, w, 1, SampleStatus.InTesting, SectionSignoffStatus.InTesting, null, null, new TestSpec(w.Lab));
        }

        var response = await GetAsync(r);
        var group = Assert.IsType<MyWorkGroup>(Group(response, "review"));
        Assert.Equal(2, group.Count);
        Assert.Equal(0, group.LockedCount);
        Assert.Equal(new[] { $"review:{mine}", $"review:{mixedSample}" }.OrderBy(k => k), group.Items.Select(SampleKey).OrderBy(k => k));
        var item = group.Items.Single(i => SampleKey(i) == $"review:{mine}");
        Assert.Equal("sample-review", item.Target.Type);
        Assert.Equal(mine, item.Target.SampleId);
        Assert.False(item.Locked);
        Assert.Null(Group(response, "approval"));
        Assert.Equal(2, (await SummaryAsync(r)).Total);
    }

    [PostgresFact]
    public async Task SectionsViewAll_DoesNotWidenMyReviewQueue()
    {
        int reader;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            reader = (await NewUserAsync(arrange, "viewer", new[] { "Samples.Review", "Sections.ViewAll" }, w.Lab)).Id;
            await UnderReviewAsync(arrange, w, w.Lab);
            await UnderReviewAsync(arrange, w, w.Other);
        }

        var group = Assert.IsType<MyWorkGroup>(Group(await GetAsync(reader), "review"));
        Assert.Equal(1, group.Count);
        Assert.Single(group.Items);
    }

    [PostgresFact]
    public async Task WithoutSamplesReviewPermission_SourceDoesNotApply()
    {
        int noPermission, approverOnly;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            noPermission = (await NewUserAsync(arrange, "plain", Array.Empty<string>(), w.Lab)).Id;
            approverOnly = (await NewUserAsync(arrange, "approver", ApproverPermissions, w.Lab)).Id;
            await UnderReviewAsync(arrange, w, w.Lab);
        }

        Assert.Empty((await GetAsync(noPermission)).Groups);
        Assert.Null(Group(await GetAsync(approverOnly), "review"));
        Assert.Equal(0, (await SummaryAsync(noPermission)).Total);
    }

    [PostgresFact]
    public async Task SampleWhereIPerformedOneOfThreeTests_IsLocked_NotCounted_AndReviewCompleteStillRejects()
    {
        int r, lockedSample, openSample;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            r = (await NewUserAsync(arrange, "rev", ReviewerPermissions, w.Lab)).Id;
            var other = (await NewUserAsync(arrange, "analyst", Array.Empty<string>(), w.Lab)).Id;
            // Three tests, mine is the middle one.
            lockedSample = (await UnderReviewAsync(arrange, w, w.Lab, null, other, r, other)).Sample.Id;
            openSample = (await UnderReviewAsync(arrange, w, w.Lab, null, other)).Sample.Id;
        }

        var response = await GetAsync(r);
        var group = Assert.IsType<MyWorkGroup>(Group(response, "review"));
        Assert.Equal(1, group.Count);                // the locked one is not counted
        Assert.Equal(1, group.LockedCount);
        Assert.Equal(2, group.Items.Count);          // but it is listed
        var locked = group.Items.Single(i => i.Locked);
        Assert.Equal($"review:{lockedSample}", SampleKey(locked));
        Assert.Equal("You cannot review a sample you tested.", locked.LockReason);
        Assert.Empty(locked.Actions);
        Assert.Equal($"review:{openSample}", SampleKey(group.Items.Single(i => !i.Locked)));
        Assert.Equal(1, (await SummaryAsync(r)).Total);   // nor in the badge

        // Review Focus 2: the decision path agrees. The locked sample is refused, the open one gets past the rule.
        await using (var actDb = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                TestServiceFactory.SampleReview(actDb).CompleteReviewAsync(lockedSample, r, "not-the-password", null, null));
            Assert.Contains("cannot review a sample you tested", ex.Message);
            Assert.Equal(locked.LockReason, ex.Message);   // My Work and the decision refuse in the same words
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var failure = await Record.ExceptionAsync(() =>
                TestServiceFactory.SampleReview(actDb).CompleteReviewAsync(openSample, r, "not-the-password", null, null));
            Assert.NotNull(failure);   // wrong password, but only after the Segregation of Duties rule let it through
            Assert.DoesNotContain("cannot review a sample you tested", failure!.Message);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Samples.AsNoTracking().SingleAsync(s => s.Id == lockedSample);
        Assert.Equal(SampleStatus.UnderReview, persisted.Status);
        Assert.Empty(await assertDb.SampleSectionSignoffs.AsNoTracking().Where(s => s.SampleId == lockedSample && s.ReviewedByUserId == r).ToListAsync());
    }

    [PostgresFact]
    public async Task LockedItems_SortAfterUnlocked()
    {
        int r, lockedOld, openNew;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            r = (await NewUserAsync(arrange, "rev", ReviewerPermissions, w.Lab)).Id;
            // The locked sample is the older and more urgent one; it must still come last.
            lockedOld = (await UnderReviewAsync(arrange, w, w.Lab, Now.AddDays(-5), r)).Sample.Id;
            openNew = (await UnderReviewAsync(arrange, w, w.Lab, Now.AddHours(-1))).Sample.Id;
        }

        var group = Assert.IsType<MyWorkGroup>(Group(await GetAsync(r), "review"));
        Assert.Equal(new[] { $"review:{openNew}", $"review:{lockedOld}" }, group.Items.Select(SampleKey));
        Assert.Equal(MyWorkUrgencyLevel.None, group.Items[0].Urgency);
        Assert.Equal(MyWorkUrgencyLevel.Overdue, group.Items[1].Urgency);
    }

    [PostgresFact]
    public async Task UnderApproval_AppearsForApprover_LockedWhenIPerformed()
    {
        int h, openApproval, performed, reviewedByMe;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            h = (await NewUserAsync(arrange, "head", ApproverPermissions, w.Lab)).Id;
            var other = (await NewUserAsync(arrange, "analyst", Array.Empty<string>(), w.Lab)).Id;
            openApproval = (await UnderApprovalAsync(arrange, w, w.Lab, other, Now.AddDays(-3))).Sample.Id;
            performed = (await UnderApprovalAsync(arrange, w, w.Lab, other, null, h, other)).Sample.Id;
            reviewedByMe = (await UnderApprovalAsync(arrange, w, w.Lab, h)).Sample.Id;
            await UnderApprovalAsync(arrange, w, w.Other, other);
            await UnderReviewAsync(arrange, w, w.Lab);   // under review, not approval
        }

        var response = await GetAsync(h);
        var group = Assert.IsType<MyWorkGroup>(Group(response, "approval"));
        Assert.Null(Group(response, "review"));
        Assert.Equal(1, group.Count);
        Assert.Equal(2, group.LockedCount);
        Assert.Equal(3, group.Items.Count);
        Assert.Equal($"approval:{openApproval}", SampleKey(group.Items[0]));
        Assert.Equal("sample-approval", group.Items[0].Target.Type);
        Assert.Equal(MyWorkUrgencyLevel.Overdue, group.Items[0].Urgency);
        Assert.Equal("You cannot approve a sample you tested.", group.Items.Single(i => SampleKey(i) == $"approval:{performed}").LockReason);
        Assert.Equal("You cannot approve a sample you reviewed.", group.Items.Single(i => SampleKey(i) == $"approval:{reviewedByMe}").LockReason);
        Assert.Equal(1, (await SummaryAsync(h)).Total);

        // The decision endpoint refuses both locked samples, with the matching rule.
        await using (var actDb = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                TestServiceFactory.SampleApproval(actDb).DecideAsync(performed, h, "not-the-password", ApprovalDecision.Approve, null, null));
            Assert.Equal("You cannot approve a sample you tested.", ex.Message);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                TestServiceFactory.SampleApproval(actDb).DecideAsync(reviewedByMe, h, "not-the-password", ApprovalDecision.Approve, null, null));
            Assert.Equal("You cannot approve a sample you reviewed.", ex.Message);
        }
    }

    [PostgresFact]
    public async Task MediaAwaitingApproval_AppearsOnlyWithMediaRelease()
    {
        int releaser, preparer, evaluator, approverOnly, qualified, notConform, decided, otherSection;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            releaser = (await NewUserAsync(arrange, "releaser", new[] { "Media.Release" }, w.Lab)).Id;
            preparer = (await NewUserAsync(arrange, "preparer", new[] { "Media.Release" }, w.Lab)).Id;
            evaluator = (await NewUserAsync(arrange, "evaluator", new[] { "Media.Release" }, w.Lab)).Id;
            approverOnly = (await NewUserAsync(arrange, "approver", ApproverPermissions, w.Lab)).Id;
            var other = (await NewUserAsync(arrange, "other", Array.Empty<string>(), w.Lab)).Id;
            qualified = (await NewMediaAwaitingApprovalAsync(arrange, w.Lab, preparer, challengeReadBy: evaluator)).Id;
            notConform = (await NewMediaAwaitingApprovalAsync(arrange, w.Lab, other, EvaluationOutcome.NonConform)).Id;
            decided = (await NewMediaAwaitingApprovalAsync(arrange, w.Lab, other, approval: ApprovalGateStatus.Approved)).Id;
            otherSection = (await NewMediaAwaitingApprovalAsync(arrange, w.Other, other)).Id;
            _ = (notConform, decided, otherSection);
        }

        var response = await GetAsync(releaser);
        var group = Assert.IsType<MyWorkGroup>(Group(response, "approval"));
        var item = Assert.Single(group.Items);
        Assert.Equal($"media:{qualified}", item.Key);
        Assert.Equal("media-approval", item.Target.Type);
        Assert.Equal(qualified, item.Target.MediaId);
        Assert.False(item.Locked);
        Assert.Equal(1, group.Count);
        Assert.Equal(1, (await SummaryAsync(releaser)).Total);

        // Samples.Approve alone does not bring the media in.
        Assert.Empty((await GetAsync(approverOnly)).Groups);

        // The preparer and the person who read its challenge see it, locked - the release decision refuses both.
        foreach (var who in new[] { preparer, evaluator })
        {
            var locked = Assert.Single(Assert.IsType<MyWorkGroup>(Group(await GetAsync(who), "approval")).Items);
            Assert.True(locked.Locked);
            Assert.NotNull(locked.LockReason);
            await using var actDb = _fixture.CreateDbContext();
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                TestServiceFactory.MediaRelease(actDb).DecideAsync(qualified, who, "not-the-password", true, null, null));
            Assert.Contains("cannot approve a media lot you prepared or evaluated", ex.Message);
        }

        Assert.Equal(0, (await SummaryAsync(preparer)).Total);
    }

    [PostgresFact]
    public async Task QueryCount_DoesNotGrowWithQueueSize()
    {
        int user;
        World world;
        DocumentSection lab;
        await using (var arrange = _fixture.CreateDbContext())
        {
            world = await WorldAsync(arrange);
            lab = world.Lab;
            user = (await NewUserAsync(arrange, "all", new[] { "Samples.Review", "Samples.Approve", "Media.Release" }, lab)).Id;
            await SeedQueueAsync(arrange, world, user, 5);
        }

        var small = new CommandCounter();
        var smallResponse = await GetAsync(user, small);
        Assert.Equal(5 * 3, smallResponse.Groups.Sum(g => g.Items.Count));

        await using (var arrange = _fixture.CreateDbContext())
            await SeedQueueAsync(arrange, world, user, 35);

        var large = new CommandCounter();
        var largeResponse = await GetAsync(user, large);
        Assert.Equal(40 + MyWorkService.GroupCap, largeResponse.Groups.Sum(g => g.Items.Count));
        Assert.True(small.Count > 0);
        Assert.Equal(small.Count, large.Count);
    }

    // Per round: one review sample, one approval sample and one media lot; every third review/approval sample is mine to lock.
    private static async Task SeedQueueAsync(MicroLimsDbContext db, World w, int userId, int count)
    {
        var other = (await NewUserAsync(db, "analyst", Array.Empty<string>(), w.Lab)).Id;
        for (var i = 0; i < count; i++)
        {
            var mine = i % 3 == 0 ? userId : other;
            await UnderReviewAsync(db, w, w.Lab, null, mine);
            await UnderApprovalAsync(db, w, w.Lab, other, null, mine);
            await NewMediaAwaitingApprovalAsync(db, w.Lab, i % 2 == 0 ? userId : other);
        }
    }
}

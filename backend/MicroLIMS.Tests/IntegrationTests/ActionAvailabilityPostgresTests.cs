using System.Data.Common;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Action availability (C8 Task 5) against a real database: the reason shown for a gated action is the sentence
// the action itself refuses with, in every refusal state of ActionGuardScenarios, and asking changes nothing.
// Availability is always asked in one DbContext and the action attempted in another.
[Collection("PostgresDatabaseCollection")]
public class ActionAvailabilityPostgresTests
{
    private readonly PostgresTestFixture _fixture;
    private readonly ActionGuardScenarios _scenarios;

    public ActionAvailabilityPostgresTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
        _scenarios = new ActionGuardScenarios(fixture);
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

    private ActionAvailabilityService NewService(MicroLimsDbContext db)
    {
        var scope = new UserSectionScopeService(db);
        var clock = new FixedTimeProvider(new DateTimeOffset(ActionGuardScenarios.Now));
        var documents = new DocumentAuthorizationService(db);
        return new ActionAvailabilityService(db, scope,
            TestServiceFactory.SampleReview(db), TestServiceFactory.SampleApproval(db),
            new SampleAssignmentService(db, TestServiceFactory.TestingWorkspace(db), scope, clock),
            new TestSelfAssignmentService(db, scope, clock),
            TestServiceFactory.MediaRelease(db),
            new DocumentApprovalService(db, new AuditEventService(db, _fixture.CreateSequenceHelper(db)), documents, new ElectronicSignatureService(db)),
            documents);
    }

    private static Task<IReadOnlyList<ActionAvailability>?> AskAsync(ActionAvailabilityService service, string kind, ActionGuardScenarios.Target t) =>
        kind switch
        {
            "sample" => service.ForSampleAsync(t.RecordId, t.SectionId, t.UserId),
            "test-order" => service.ForTestOrderAsync(t.RecordId, t.UserId),
            "media" => service.ForMediaAsync(t.RecordId, t.UserId),
            "document-revision" => service.ForDocumentRevisionAsync(t.RecordId, t.UserId),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    // Asked in a context of its own, as a request would.
    private async Task<IReadOnlyList<ActionAvailability>?> AvailabilityAsync(string kind, ActionGuardScenarios.Target t)
    {
        await using var db = _fixture.CreateDbContext();
        return await AskAsync(NewService(db), kind, t);
    }

    private ActionAvailabilityController NewController(MicroLimsDbContext db, int userId) =>
        new(NewService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
                }
            }
        };

    private async Task<IActionResult> GetAsync(string kind, int id, int userId, int? sectionId = null)
    {
        await using var db = _fixture.CreateDbContext();
        var controller = NewController(db, userId);
        return kind switch
        {
            "sample" => await controller.ForSample(id, sectionId, CancellationToken.None),
            "test-order" => await controller.ForTestOrder(id, CancellationToken.None),
            "media" => await controller.ForMedia(id, CancellationToken.None),
            "document-revision" => await controller.ForDocumentRevision(id, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private ActionGuardScenarios.ActionCase Case(string action) => _scenarios.Actions().Single(a => a.Action == action);

    // ---- the reason is the refusal ----

    private Task ReasonEqualsThrownMessageAsync(string actionName)
    {
        var action = Case(actionName);
        return PostgresCases.RunAsync(action.Refusals, async refusal =>
        {
            var arranged = await refusal.Arrange();

            var listed = await AvailabilityAsync(action.Kind, arranged.Target);

            // The action, in a fresh context, refuses with the scenario's exception.
            var ex = await Record.ExceptionAsync(() => action.Attempt(arranged.Target));
            Assert.NotNull(ex);
            Assert.IsAssignableFrom(refusal.ExceptionType, ex);
            Assert.Equal(arranged.Message, ex.Message);

            if (refusal.Unreadable)
            {
                // The caller may not read the record: no reason is given at all.
                Assert.Null(listed);
                return;
            }

            Assert.NotNull(listed);
            var availability = Assert.Single(listed, a => a.Action == action.Action);
            Assert.False(availability.Allowed);
            Assert.Equal(ex.Message, availability.Reason);
        });
    }

    [PostgresFact] public Task ReviewComplete_Availability_Reason_Equals_ThrownMessage() => ReasonEqualsThrownMessageAsync(GatedActions.ReviewComplete);
    [PostgresFact] public Task ReviewReturn_Availability_Reason_Equals_ThrownMessage() => ReasonEqualsThrownMessageAsync(GatedActions.ReviewReturn);
    [PostgresFact] public Task ApprovalApprove_Availability_Reason_Equals_ThrownMessage() => ReasonEqualsThrownMessageAsync(GatedActions.ApprovalApprove);
    [PostgresFact] public Task ApprovalReject_Availability_Reason_Equals_ThrownMessage() => ReasonEqualsThrownMessageAsync(GatedActions.ApprovalReject);
    [PostgresFact] public Task SampleVoid_Availability_Reason_Equals_ThrownMessage() => ReasonEqualsThrownMessageAsync(GatedActions.SampleVoid);
    [PostgresFact] public Task SampleAssign_Availability_Reason_Equals_ThrownMessage() => ReasonEqualsThrownMessageAsync(GatedActions.SampleAssign);
    [PostgresFact] public Task TestTake_Availability_Reason_Equals_ThrownMessage() => ReasonEqualsThrownMessageAsync(GatedActions.TestTake);
    [PostgresFact] public Task TestRelease_Availability_Reason_Equals_ThrownMessage() => ReasonEqualsThrownMessageAsync(GatedActions.TestRelease);
    [PostgresFact] public Task MediaRelease_Availability_Reason_Equals_ThrownMessage() => ReasonEqualsThrownMessageAsync(GatedActions.MediaRelease);
    [PostgresFact] public Task DocumentApprove_Availability_Reason_Equals_ThrownMessage() => ReasonEqualsThrownMessageAsync(GatedActions.DocumentApprove);

    // The approval prerequisites hold back Approve only: on the same sample Reject is offered, and works.
    [PostgresFact]
    public Task ApprovalPrerequisitesMissing_RejectIsStillAvailable_AndSucceeds() =>
        PostgresCases.RunAsync(_scenarios.ApproveOnlyRefusals(), async refusal =>
        {
            var arranged = await refusal.Arrange();

            var listed = await AvailabilityAsync("sample", arranged.Target);

            Assert.NotNull(listed);
            Assert.Equal(arranged.Message, Assert.Single(listed, a => a.Action == GatedActions.ApprovalApprove).Reason);
            var reject = Assert.Single(listed, a => a.Action == GatedActions.ApprovalReject);
            Assert.True(reject.Allowed);
            Assert.Null(reject.Reason);
            await _scenarios.RejectAsync(arranged.Target);
            await Case(GatedActions.ApprovalReject).AssertDone(arranged.Target);
        });

    [PostgresFact]
    public Task AllowedState_AvailabilityAllowed_AndActionSucceeds() =>
        PostgresCases.RunAsync(_scenarios.Actions(), async action =>
        {
            var target = await action.Allowed();

            var listed = await AvailabilityAsync(action.Kind, target);

            Assert.NotNull(listed);
            var availability = Assert.Single(listed, a => a.Action == action.Action);
            Assert.True(availability.Allowed);
            Assert.Null(availability.Reason);
            await action.Attempt(target);
            await action.AssertDone(target);
        });

    // Each endpoint lists exactly its own actions, whatever the state.
    [PostgresFact]
    public async Task EachEndpoint_ListsItsActions()
    {
        var expected = new Dictionary<string, string[]>
        {
            ["sample"] = new[] { GatedActions.ReviewComplete, GatedActions.ApprovalApprove, GatedActions.ApprovalReject, GatedActions.SampleVoid, GatedActions.SampleAssign },
            ["test-order"] = new[] { GatedActions.ReviewReturn, GatedActions.TestTake, GatedActions.TestRelease },
            ["media"] = new[] { GatedActions.MediaRelease },
            ["document-revision"] = new[] { GatedActions.DocumentApprove }
        };
        var targets = new Dictionary<string, ActionGuardScenarios.Target>
        {
            ["sample"] = await _scenarios.ReviewAllowedAsync(),
            ["test-order"] = await _scenarios.TakeAllowedAsync(),
            ["media"] = await _scenarios.MediaReleaseAllowedAsync(),
            ["document-revision"] = await _scenarios.DocumentApproveAllowedAsync()
        };

        foreach (var (kind, actions) in expected)
        {
            var listed = await AvailabilityAsync(kind, targets[kind]);
            Assert.NotNull(listed);
            Assert.Equal(actions, listed.Select(a => a.Action).ToArray());
        }
    }

    // A revision that was never sent for approval has no approval decision to offer.
    [PostgresFact]
    public async Task DocumentRevision_WithoutAnApprovalTask_ListsNothing()
    {
        var doc = await _scenarios.DocumentAwaitingApprovalAsync(withTask: false);

        var listed = await AvailabilityAsync("document-revision", new ActionGuardScenarios.Target(doc.ApproverId, doc.RevisionId));

        Assert.NotNull(listed);
        Assert.Empty(listed);
    }

    // ---- the permission the endpoint's policy asks for ----

    private sealed record PolicyCase(string Action, string Permission, Type Controller, string Method)
    {
        public override string ToString() => Action;
    }

    private static readonly PolicyCase[] PolicyCases =
    {
        new(GatedActions.ReviewComplete, PermissionConstants.SamplesReview, typeof(SampleReviewController), nameof(SampleReviewController.Complete)),
        new(GatedActions.ReviewReturn, PermissionConstants.SamplesReview, typeof(SampleReviewController), nameof(SampleReviewController.ReturnTestToAnalyst)),
        new(GatedActions.ApprovalApprove, PermissionConstants.SamplesApprove, typeof(SampleApprovalController), nameof(SampleApprovalController.Decide)),
        new(GatedActions.ApprovalReject, PermissionConstants.SamplesApprove, typeof(SampleApprovalController), nameof(SampleApprovalController.Decide)),
        new(GatedActions.SampleVoid, PermissionConstants.SamplesCorrect, typeof(SampleController), nameof(SampleController.Void)),
        new(GatedActions.SampleAssign, PermissionConstants.SamplesAssignAnalyst, typeof(SampleController), nameof(SampleController.AssignAnalyst)),
        new(GatedActions.MediaRelease, PermissionConstants.MediaRelease, typeof(MediaController), nameof(MediaController.DecideRelease)),
        new(GatedActions.MediaRelease, PermissionConstants.MediaPrepare, typeof(MediaController), nameof(MediaController.DecideRelease)),
    };

    // These actions are refused by the endpoint's permission policy, which answers 403 with no sentence. The
    // availability answer uses the one generic sentence, for the permission the endpoint really asks for.
    [PostgresFact]
    public Task RoleWithoutThePermission_GetsTheGenericSentence() =>
        PostgresCases.RunAsync(PolicyCases, async policy =>
        {
            // The endpoint carries the policy.
            var method = policy.Controller.GetMethod(policy.Method);
            Assert.NotNull(method);
            var policies = method.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(policy.Controller.GetCustomAttributes<AuthorizeAttribute>())
                .Select(a => a.Policy)
                .ToList();
            Assert.Contains(policy.Permission, policies);

            var action = Case(policy.Action);
            var target = await action.Allowed();
            Assert.True(Assert.Single((await AvailabilityAsync(action.Kind, target)) ?? Array.Empty<ActionAvailability>(), a => a.Action == action.Action).Allowed);

            await using (var db = _fixture.CreateDbContext())
            {
                var roleId = await db.Users.AsNoTracking().Where(u => u.Id == target.UserId).Select(u => u.RoleId).SingleAsync();
                db.RolePermissions.RemoveRange(await db.RolePermissions.Where(rp => rp.RoleId == roleId && rp.Permission!.Code == policy.Permission).ToListAsync());
                await db.SaveChangesAsync();
            }

            var listed = await AvailabilityAsync(action.Kind, target);

            Assert.NotNull(listed);
            var availability = Assert.Single(listed, a => a.Action == action.Action);
            Assert.False(availability.Allowed);
            Assert.Equal("Your role does not include this action.", availability.Reason);
        });

    // ---- read-only ----

    private sealed record Footprint(int Audit, int AuditChanges, int SecurityAudit, int Notifications, int EmailOutbox, int Signatures,
        int ReviewEvents, int Signoffs, int WorkflowHistory, string Record);

    private async Task<Footprint> FootprintAsync(string kind, int recordId)
    {
        await using var db = _fixture.CreateDbContext();
        object? record = kind switch
        {
            "sample" => new
            {
                Sample = await db.Samples.AsNoTracking().SingleAsync(s => s.Id == recordId),
                Orders = await db.TestOrders.AsNoTracking().Where(o => o.SampleId == recordId).OrderBy(o => o.Id).ToListAsync(),
                Signoffs = await db.SampleSectionSignoffs.AsNoTracking().Where(s => s.SampleId == recordId).OrderBy(s => s.Id).ToListAsync()
            },
            "test-order" => await db.TestOrders.AsNoTracking().SingleAsync(o => o.Id == recordId),
            "media" => await db.Media.AsNoTracking().SingleAsync(m => m.Id == recordId),
            "document-revision" => new
            {
                Revision = await db.DocumentRevisions.AsNoTracking().SingleAsync(r => r.Id == recordId),
                Tasks = await db.DocumentApprovalTasks.AsNoTracking().Where(t => t.DocumentRevisionId == recordId).OrderBy(t => t.Id).ToListAsync()
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        return new Footprint(
            await db.AuditLogs.AsNoTracking().CountAsync(),
            await db.AuditEventChanges.AsNoTracking().CountAsync(),
            await db.SecurityAuditEvents.AsNoTracking().CountAsync(),
            await db.NotificationLogs.AsNoTracking().CountAsync(),
            await db.EmailOutbox.AsNoTracking().CountAsync(),
            await db.ElectronicSignatures.AsNoTracking().CountAsync(),
            await db.ReviewWorkflowEvents.AsNoTracking().CountAsync(),
            await db.SampleSectionSignoffs.AsNoTracking().CountAsync(),
            await db.WorkflowHistories.AsNoTracking().CountAsync(),
            JsonSerializer.Serialize(record));
    }

    [PostgresFact]
    public async Task Availability_ChangesNothing()
    {
        // Allowed states of every endpoint, and the approval of a sample that has no sign-off row yet: the action
        // adds that row while deciding, the availability answer must not.
        var noSignoffRow = await _scenarios.ApprovalRefusals().Single(r => r.Name.Contains("no sign-off row")).Arrange();
        var cases = new List<(string Kind, ActionGuardScenarios.Target Target)>
        {
            ("sample", await _scenarios.ReviewAllowedAsync()),
            ("sample", await _scenarios.ApprovalAllowedAsync()),
            ("sample", noSignoffRow.Target),
            ("test-order", await _scenarios.ReturnAllowedAsync()),
            ("test-order", await _scenarios.ReleaseAllowedAsync()),
            ("media", await _scenarios.MediaReleaseAllowedAsync()),
            ("document-revision", await _scenarios.DocumentApproveAllowedAsync())
        };

        await PostgresCases.RunAsync(cases, async c =>
        {
            var before = await FootprintAsync(c.Kind, c.Target.RecordId);

            var listed = await AvailabilityAsync(c.Kind, c.Target);

            Assert.NotNull(listed);
            Assert.NotEmpty(listed);
            Assert.Equal(before, await FootprintAsync(c.Kind, c.Target.RecordId));
        });
    }

    // The context that answered holds nothing to save and queued no live signal.
    [PostgresFact]
    public async Task Availability_LeavesNothingPendingInItsContext()
    {
        var target = await _scenarios.ApprovalAllowedAsync();

        await using var db = _fixture.CreateDbContext();
        var listed = await NewService(db).ForSampleAsync(target.RecordId, null, target.UserId);

        Assert.NotNull(listed);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    // ---- 404 ----

    private static void AssertNotFound(IActionResult result)
    {
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var body = Assert.IsType<ApiResponse<object>>(notFound.Value);
        Assert.False(body.Success);
        Assert.Equal("Record not found.", body.Message);
        Assert.Null(body.Data);
    }

    [PostgresFact]
    public async Task RecordINotReadable_404()
    {
        // Member of another section, no Sections.ViewAll: none of the section's records is readable.
        var section = await _scenarios.SectionAsync();
        var elsewhere = await _scenarios.SectionAsync();
        var stranger = await _scenarios.UserAsync(RoleType.SectionHead, ActionGuardScenarios.HeadCodes, elsewhere);
        var analyst = await _scenarios.AnalystAsync(section);
        var sample = await _scenarios.UnderReviewAsync(section);
        var media = await _scenarios.MediaAsync(section, analyst.Id);

        AssertNotFound(await GetAsync("sample", sample.Sample.Id, stranger.Id));
        AssertNotFound(await GetAsync("sample", sample.Sample.Id, stranger.Id, section.Id));
        AssertNotFound(await GetAsync("test-order", sample.Orders[0].Id, stranger.Id));
        AssertNotFound(await GetAsync("media", media.Media.Id, stranger.Id));

        // A voided document is readable only by the Document Controller or audit oversight.
        var doc = await _scenarios.DocumentAwaitingApprovalAsync();
        Assert.IsType<OkObjectResult>(await GetAsync("document-revision", doc.RevisionId, doc.ApproverId));
        await _scenarios.UpdateAsync<DocumentMaster>(doc.MasterId, m => m.RecordStatus = DocumentRecordStatus.Void);
        AssertNotFound(await GetAsync("document-revision", doc.RevisionId, doc.ApproverId));

        // The same records answer for someone who may read them.
        var member = await _scenarios.HeadAsync(section);
        Assert.IsType<OkObjectResult>(await GetAsync("sample", sample.Sample.Id, member.Id));
        Assert.IsType<OkObjectResult>(await GetAsync("test-order", sample.Orders[0].Id, member.Id));
        Assert.IsType<OkObjectResult>(await GetAsync("media", media.Media.Id, member.Id));
    }

    [PostgresFact]
    public async Task UnknownRecord_404()
    {
        var section = await _scenarios.SectionAsync();
        var head = await _scenarios.HeadAsync(section);
        const int unknown = int.MaxValue;

        foreach (var kind in new[] { "sample", "test-order", "media", "document-revision" })
            AssertNotFound(await GetAsync(kind, unknown, head.Id));
    }

    [PostgresFact]
    public async Task KnownRecord_200_WithTheList()
    {
        var target = await _scenarios.ReviewAllowedAsync();

        var ok = Assert.IsType<OkObjectResult>(await GetAsync("sample", target.RecordId, target.UserId));

        var body = Assert.IsType<ApiResponse<IReadOnlyList<ActionAvailability>>>(ok.Value);
        Assert.True(body.Success);
        Assert.NotNull(body.Data);
        Assert.True(Assert.Single(body.Data, a => a.Action == GatedActions.ReviewComplete).Allowed);
    }

    [Fact]
    public void Controller_RequiresAnAuthenticatedCaller_AndServesTheFourRoutes()
    {
        var controller = typeof(ActionAvailabilityController);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/actions", controller.GetCustomAttribute<RouteAttribute>()?.Template);
        var routes = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.GetCustomAttribute<HttpGetAttribute>()?.Template)
            .Where(t => t is not null)
            .OrderBy(t => t)
            .ToArray();
        Assert.Equal(new[] { "document-revision/{id:int}", "media/{id:int}", "sample/{id:int}", "test-order/{id:int}" }, routes);
    }

    // ---- cost ----

    private async Task<int> CountSampleQueriesAsync(ActionGuardScenarios.Target target, Action<IReadOnlyList<ActionAvailability>> assert)
    {
        var counter = new CommandCounter();
        await using var db = _fixture.CreateDbContext(counter);
        var service = NewService(db);
        var before = counter.Count;
        var listed = await service.ForSampleAsync(target.RecordId, target.SectionId, target.UserId);
        Assert.NotNull(listed);
        assert(listed);
        return counter.Count - before;
    }

    // A sample under approval whose tests are all analysis tests with an active entry, so the approval check walks
    // every order through the system-suitability and analysis prerequisites.
    private async Task<ActionGuardScenarios.Target> UnderApprovalWithAnalysesAsync(int orders)
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var code = await _scenarios.TestDefinitionAsync(section, WorkflowType.Titration);
        var seed = await _scenarios.UnderApprovalAsync(section, reviewer.Id, orders, analyst.Id, code);
        foreach (var order in seed.Orders)
            await _scenarios.AnalysisAsync(order.Id, analyst);
        return new ActionGuardScenarios.Target(head.Id, seed.Sample.Id);
    }

    [PostgresFact]
    public async Task Availability_UsesAConstantNumberOfQueries()
    {
        // Under review: one test against eight.
        async Task<ActionGuardScenarios.Target> UnderReviewAsync(int orders)
        {
            var section = await _scenarios.SectionAsync();
            var reviewer = await _scenarios.ReviewerAsync(section);
            var seed = await _scenarios.UnderReviewAsync(section, orders);
            return new ActionGuardScenarios.Target(reviewer.Id, seed.Sample.Id);
        }

        void ReviewAllowed(IReadOnlyList<ActionAvailability> listed) =>
            Assert.True(Assert.Single(listed, a => a.Action == GatedActions.ReviewComplete).Allowed);
        void ApproveAllowed(IReadOnlyList<ActionAvailability> listed) =>
            Assert.True(Assert.Single(listed, a => a.Action == GatedActions.ApprovalApprove).Allowed);

        var reviewOne = await CountSampleQueriesAsync(await UnderReviewAsync(1), ReviewAllowed);
        var reviewEight = await CountSampleQueriesAsync(await UnderReviewAsync(8), ReviewAllowed);
        Assert.Equal(reviewOne, reviewEight);

        var approveOne = await CountSampleQueriesAsync(await UnderApprovalWithAnalysesAsync(1), ApproveAllowed);
        var approveEight = await CountSampleQueriesAsync(await UnderApprovalWithAnalysesAsync(8), ApproveAllowed);
        Assert.Equal(approveOne, approveEight);

        // A ceiling, so a change that adds a query per call is noticed too.
        Assert.InRange(approveEight, 1, 60);
    }

    // The action itself costs the same pre-signing checks whatever the number of tests: the approval that follows
    // an availability answer is the same decision.
    [PostgresFact]
    public async Task ApprovalWithAnalyses_AllowedByAvailability_Succeeds()
    {
        var target = await UnderApprovalWithAnalysesAsync(3);

        var listed = await AvailabilityAsync("sample", target);

        Assert.NotNull(listed);
        Assert.True(Assert.Single(listed, a => a.Action == GatedActions.ApprovalApprove).Allowed);
        await _scenarios.ApproveAsync(target);
        await Case(GatedActions.ApprovalApprove).AssertDone(target);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.BackgroundServices;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.BackgroundJobs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class BackgroundJobRunTests
{
    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeWork : IBackgroundJobWork
    {
        public FakeWork(string name) => JobName = name;
        public string JobName { get; }
        public Func<CancellationToken, Task<int?>> Body { get; set; } = _ => Task.FromResult<int?>(3);
        public Task<int?> RunOnceAsync(CancellationToken cancellationToken) => Body(cancellationToken);
    }

    private const string Housekeeping = "SessionTokenCleanupWorker";
    private const string Gxp = "EquipmentOverdueWorker";

    private static (MicroLimsDbContext db, BackgroundJobRunRecorder recorder, FakeWork work, BackgroundJobAdminService admin) Create()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var services = new ServiceCollection();
        services.AddSingleton<IMicroLimsDbContext>(db);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var recorder = new BackgroundJobRunRecorder(scopes, new FixedTime(), NullLogger<BackgroundJobRunRecorder>.Instance);
        var work = new FakeWork(Housekeeping);
        var admin = new BackgroundJobAdminService(
            db, recorder, new[] { work }, new SecurityAuditService(db, new SystemSecurityRequestContext(), new FixedTime()));
        return (db, recorder, work, admin);
    }

    [Fact]
    public async Task Recorder_Success_RecordsStartFinishAndItems()
    {
        var (db, recorder, _, _) = Create();

        var result = await recorder.RunAsync(Housekeeping, BackgroundJobTrigger.Schedule, null, null, _ => Task.FromResult<int?>(7), default);

        Assert.Equal(BackgroundJobOutcome.Succeeded, result.Outcome);
        var row = await db.BackgroundJobRuns.SingleAsync();
        Assert.Equal(BackgroundJobOutcome.Succeeded, row.Outcome);
        Assert.Equal(7, row.ItemsProcessed);
        Assert.NotNull(row.FinishedAt);
        Assert.Equal(BackgroundJobTrigger.Schedule, row.TriggeredBy);
        Assert.False(recorder.IsRunning(Housekeeping));
    }

    [Fact]
    public async Task Recorder_Failure_IsRecordedWithTruncatedError_AndNotRethrown()
    {
        var (db, recorder, _, _) = Create();

        var result = await recorder.RunAsync(Housekeeping, BackgroundJobTrigger.Schedule, null, null,
            _ => throw new InvalidOperationException(new string('x', 5000)), default);

        Assert.Equal(BackgroundJobOutcome.Failed, result.Outcome);
        var row = await db.BackgroundJobRuns.SingleAsync();
        Assert.Equal(BackgroundJobOutcome.Failed, row.Outcome);
        Assert.Equal(BackgroundJobRun.MaxErrorLength, row.Error!.Length);
        Assert.StartsWith("InvalidOperationException", row.Error);
    }

    [Fact]
    public async Task Recorder_IdleScheduledPassOfHighFrequencyJob_LeavesNoRow_ButFailureDoes()
    {
        var (db, recorder, _, _) = Create();
        const string frequent = "DatabaseHealthMonitorWorker";

        await recorder.RunAsync(frequent, BackgroundJobTrigger.Schedule, null, null, _ => Task.FromResult<int?>(null), default);
        Assert.Empty(db.BackgroundJobRuns);
        Assert.NotNull(recorder.LastPassFinishedAt(frequent));

        await recorder.RunAsync(frequent, BackgroundJobTrigger.Schedule, null, null, _ => throw new Exception("boom"), default);
        Assert.Equal(BackgroundJobOutcome.Failed, (await db.BackgroundJobRuns.SingleAsync()).Outcome);
    }

    [Fact]
    public async Task Recorder_ClosesRunningRowLeftByAStoppedHost()
    {
        var (db, recorder, _, _) = Create();
        db.BackgroundJobRuns.Add(new BackgroundJobRun { JobName = Housekeeping, StartedAt = DateTime.UtcNow.AddHours(-2) });
        await db.SaveChangesAsync();

        await recorder.RunAsync(Housekeeping, BackgroundJobTrigger.Schedule, null, null, _ => Task.FromResult<int?>(0), default);

        Assert.DoesNotContain(db.BackgroundJobRuns, r => r.Outcome == BackgroundJobOutcome.Running);
        Assert.Contains(db.BackgroundJobRuns, r => r.Error != null && r.Error.StartsWith("Interrupted"));
    }

    [Fact]
    public void Catalog_ClassifiesEveryRegisteredWorker()
    {
        var workers = typeof(SessionTokenCleanupWorker).Assembly.GetTypes()
            .Where(t => typeof(Microsoft.Extensions.Hosting.BackgroundService).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();
        Assert.NotEmpty(workers);

        foreach (var type in workers)
        {
            var name = (string)type.GetField("ProcessName")!.GetRawConstantValue()!;
            var def = BackgroundJobCatalog.Find(name);
            Assert.True(def is not null, $"{type.Name} is not classified in BackgroundJobCatalog.");
            Assert.False(string.IsNullOrWhiteSpace(def!.ClassificationReason));
            // Only housekeeping workers expose a manual entry point.
            Assert.Equal(
                def.Classification == BackgroundJobClassification.Housekeeping,
                typeof(IBackgroundJobWork).IsAssignableFrom(type));
        }

        Assert.Equal(workers.Count, BackgroundJobCatalog.All.Count);
    }

    [Fact]
    public async Task ManualRun_Housekeeping_RunsRecordsAndAudits()
    {
        var (db, _, _, admin) = Create();

        var result = await admin.RunManuallyAsync(Housekeeping, 42, "  Clearing before audit  ");

        Assert.Equal(ManualJobRunStatus.Completed, result.Status);
        var row = await db.BackgroundJobRuns.SingleAsync();
        Assert.Equal(BackgroundJobTrigger.Manual, row.TriggeredBy);
        Assert.Equal(42, row.TriggeredByUserId);
        Assert.Equal("Clearing before audit", row.Reason);
        Assert.Equal(3, row.ItemsProcessed);
        var evt = await db.SecurityAuditEvents.SingleAsync(e => e.EventCode == SecurityEventCodes.BackgroundJobManuallyTriggered);
        Assert.Equal(42, evt.ActorUserId);
        Assert.Equal("Clearing before audit", evt.Reason);
    }

    [Fact]
    public async Task ManualRun_Gxp_IsRefused_AndNothingIsRecorded()
    {
        var (db, _, _, admin) = Create();

        var result = await admin.RunManuallyAsync(Gxp, 1, "because");

        Assert.Equal(ManualJobRunStatus.NotAllowed, result.Status);
        Assert.Contains("GxP", result.Message);
        Assert.Empty(db.BackgroundJobRuns);
        Assert.Empty(db.SecurityAuditEvents);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ManualRun_RequiresAReason(string? reason)
    {
        var (db, _, _, admin) = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.RunManuallyAsync(Housekeeping, 1, reason));
        Assert.Empty(db.BackgroundJobRuns);
    }

    [Fact]
    public async Task ManualRun_UnknownJob_IsNotFound()
    {
        var (_, _, _, admin) = Create();
        await Assert.ThrowsAsync<MicroLIMS.Shared.Exceptions.NotFoundException>(() => admin.RunManuallyAsync("Nope", 1, "r"));
    }

    [Fact]
    public async Task ManualRun_WhileTheJobIsRunning_IsRefused()
    {
        var (_, recorder, work, admin) = Create();
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        work.Body = async _ => { started.SetResult(); await release.Task; return 1; };

        var first = admin.RunManuallyAsync(Housekeeping, 1, "first");
        await started.Task;
        Assert.True(recorder.IsRunning(Housekeeping));

        var second = await admin.RunManuallyAsync(Housekeeping, 2, "second");
        Assert.Equal(ManualJobRunStatus.AlreadyRunning, second.Status);

        release.SetResult();
        Assert.Equal(ManualJobRunStatus.Completed, (await first).Status);
    }

    [Fact]
    public async Task List_ShowsEveryJobWithClassificationAndLastRun()
    {
        var (_, _, _, admin) = Create();
        await admin.RunManuallyAsync(Housekeeping, 1, "r");

        var jobs = await admin.ListAsync();

        Assert.Equal(BackgroundJobCatalog.All.Count, jobs.Count);
        var token = jobs.Single(j => j.Name == Housekeeping);
        Assert.True(token.CanRunManually);
        Assert.NotNull(token.LastRun);
        Assert.NotNull(token.LastSuccessAt);
        Assert.Null(token.LastFailureAt);
        Assert.False(jobs.Single(j => j.Name == Gxp).CanRunManually);

        var runs = await admin.GetRunsAsync(Housekeeping, 1, 10);
        Assert.Single(runs.Items);
    }
}

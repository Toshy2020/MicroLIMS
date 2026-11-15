using System.Collections.Concurrent;
using MicroLIMS.Application.Abstractions.Live;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

/// <summary>
/// "Changed by someone else" banner: the DbContext queues a data-free record signal (with the actor)
/// for saved rows of the mapped entity types, and only after commit.
/// </summary>
[Collection("PostgresDatabaseCollection")]
public class RecordChangedSignalsPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public RecordChangedSignalsPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class Recorder : ILiveSignalPublisher
    {
        public ConcurrentQueue<LiveSignal> Signals { get; } = new();

        public Task PublishAsync(IReadOnlyCollection<LiveSignal> signals, CancellationToken ct = default)
        {
            foreach (var s in signals) Signals.Enqueue(s);
            return Task.CompletedTask;
        }
    }

    private MicroLimsDbContext CreatePublishingDb(Recorder recorder)
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseNpgsql(_fixture.TestConnectionString!)
            .Options;
        return new MicroLimsDbContext(options, TimeProvider.System, recorder);
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private async Task<int> SeedSampleAsync()
    {
        var tag = Tag();
        await using var arrangeDb = _fixture.CreateDbContext();
        var item = new Item { Code = "ITM-" + tag, Name = "Item " + tag, Category = SampleCategory.RawMaterial };
        var cause = new CauseOfTesting { Name = "Routine-" + tag };
        arrangeDb.Items.Add(item);
        arrangeDb.CausesOfTesting.Add(cause);
        await arrangeDb.SaveChangesAsync();
        var sample = new Sample
        {
            Category = SampleCategory.RawMaterial,
            ItemId = item.Id,
            ControlNumber = "CTL-" + tag,
            ReferenceNumber = "REF-" + tag,
            CauseOfTestingId = cause.Id,
            ReceivedByUserId = _fixture.SeededUserId,
            Status = SampleStatus.Received
        };
        arrangeDb.Samples.Add(sample);
        await arrangeDb.SaveChangesAsync();
        return sample.Id;
    }

    [PostgresFact]
    public async Task SavingASample_QueuesRecordSignalWithActor()
    {
        var sampleId = await SeedSampleAsync();
        var recorder = new Recorder();

        await using (var actDb = CreatePublishingDb(recorder))
        {
            actDb.CurrentUserId = _fixture.SeededUserId;
            var sample = await actDb.Samples.SingleAsync(s => s.Id == sampleId);
            sample.Status = SampleStatus.Received == sample.Status ? SampleStatus.InTesting : SampleStatus.Received;
            await actDb.SaveChangesAsync();
        }

        var signal = Assert.Single(recorder.Signals, s => s.Key == $"record:sample:{sampleId}");
        Assert.Equal(LiveSignalKinds.Record, signal.Kind);
        Assert.Equal(_fixture.SeededUserId, signal.ActorUserId);

        // The change really was persisted and audited in the same save.
        await using var assertDb = _fixture.CreateDbContext();
        Assert.True(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.EntityName == "Sample" && a.EntityId == sampleId.ToString() && a.UserId == _fixture.SeededUserId && a.Action == "Update"));
    }

    [PostgresFact]
    public async Task AddingAMappedEntity_QueuesSignalWithItsRealId()
    {
        var recorder = new Recorder();
        var tag = Tag();
        int itemId;
        await using (var actDb = CreatePublishingDb(recorder))
        {
            actDb.CurrentUserId = _fixture.SeededUserId;
            var item = new Item { Code = "ITM-" + tag, Name = "Item " + tag, Category = SampleCategory.RawMaterial };
            actDb.Items.Add(item);
            await actDb.SaveChangesAsync();
            itemId = item.Id;
        }

        Assert.Contains(recorder.Signals, s => s.Key == $"record:item:{itemId}" && s.ActorUserId == _fixture.SeededUserId);
    }

    [PostgresFact]
    public async Task TypesNotInTheMap_QueueNothing()
    {
        var recorder = new Recorder();
        await using (var actDb = CreatePublishingDb(recorder))
        {
            actDb.CurrentUserId = _fixture.SeededUserId;
            actDb.CausesOfTesting.Add(new CauseOfTesting { Name = "Unmapped-" + Tag() });
            await actDb.SaveChangesAsync();
        }

        Assert.DoesNotContain(recorder.Signals, s => s.Kind == LiveSignalKinds.Record);
    }

    [PostgresFact]
    public async Task RolledBackSave_QueuesNothingThatSurvives()
    {
        var sampleId = await SeedSampleAsync();
        var recorder = new Recorder();

        await using (var actDb = CreatePublishingDb(recorder))
        {
            actDb.CurrentUserId = _fixture.SeededUserId;
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                UnitOfWork.RunAsync<int>(actDb, async () =>
                {
                    var sample = await actDb.Samples.SingleAsync(s => s.Id == sampleId);
                    sample.Status = SampleStatus.InTesting;
                    await actDb.SaveChangesAsync();
                    throw new InvalidOperationException("Abort UoW");
                }));

            Assert.Empty(recorder.Signals);

            // A later, unrelated commit on the same context must not carry the rolled-back signal.
            actDb.ChangeTracker.Clear();
            actDb.CausesOfTesting.Add(new CauseOfTesting { Name = "After-" + Tag() });
            await actDb.SaveChangesAsync();
        }

        Assert.DoesNotContain(recorder.Signals, s => s.Key == $"record:sample:{sampleId}");
        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Samples.AsNoTracking().SingleAsync(s => s.Id == sampleId);
        Assert.Equal(SampleStatus.Received, persisted.Status);
    }
}

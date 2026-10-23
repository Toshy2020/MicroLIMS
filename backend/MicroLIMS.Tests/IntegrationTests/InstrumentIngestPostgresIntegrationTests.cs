using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Tests.Fixtures;
using MicroLIMS.Tests.UnitTests;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres-backed on purpose: the receipt races on unique indexes and runs
// under the append-only triggers, neither of which exist in the InMemory
// provider. Each racer gets its own context, service and storage, exactly as
// two simultaneous requests would.
[Collection("PostgresDatabaseCollection")]
public class InstrumentIngestPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public InstrumentIngestPostgresIntegrationTests(PostgresTestFixture fixture) => _fixture = fixture;

    private async Task<InstrumentAgentContext> SeedAgentAsync()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        await using var db = _fixture.CreateDbContext();
        var equipment = new Equipment
        {
            Name = $"HPLC {uid}", Code = $"HPLC-IR-{uid}", Type = EquipmentType.Hplc, SectionId = _fixture.SeededSectionId
        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();
        var agent = new InstrumentAgent
        {
            Code = $"AG-IR-{uid}", Name = "Agent", EquipmentId = equipment.Id, SectionId = _fixture.SeededSectionId,
            TimeZoneId = "Europe/Berlin", KeyHash = $"{uid}-hash", KeyPrefix = uid[..4],
            IsActive = true, CreatedAt = DateTime.UtcNow, CreatedByUserId = _fixture.SeededUserId
        };
        db.InstrumentAgents.Add(agent);
        await db.SaveChangesAsync();
        return new InstrumentAgentContext(agent.Id, agent.Code, equipment.Id, _fixture.SeededSectionId, agent.TimeZoneId);
    }

    private (MicroLimsDbContext Db, InstrumentIngestService Service) NewService()
    {
        var db = _fixture.CreateDbContext();
        var service = new InstrumentIngestService(db, new InMemoryFileStorageService(),
            new AuditEventService(db, new DatabaseSequenceHelper(db)), new LabClock(TimeProvider.System, TimeZoneInfo.Utc));
        return (db, service);
    }

    private static IngestRequest Request(byte[] content, string sequence) =>
        InstrumentIngestReceiveTests.Request(content, sequence, agentClock: DateTime.UtcNow);

    // Starts every racer together so their reads happen before either insert.
    private static async Task<T[]> RaceAsync<T>(params Func<Task<T>>[] racers)
    {
        var gate = new TaskCompletionSource();
        var tasks = racers.Select(async r => { await gate.Task; return await r(); }).ToArray();
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    private static byte[] Body(string text) => System.Text.Encoding.UTF8.GetBytes($"{text} {Guid.NewGuid()}");

    // preexisting: the injection already has version 1, so the racers meet on
    // the version indexes instead of on the injection row.
    private async Task<int> StartAsync(InstrumentAgentContext agent, bool preexisting)
    {
        if (!preexisting) return 0;
        var (db, service) = NewService();
        await using var _ = db;
        await service.ReceiveAsync(agent, Request(Body("base"), "SEQ-RACE"), default);
        return 1;
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_same_hash_creates_one_version(bool preexisting)
    {
        for (var round = 0; round < 5; round++)
        {
            var agent = await SeedAgentAsync();
            var before = await StartAsync(agent, preexisting);
            var request = Request(Body("report body"), "SEQ-RACE");
            var (dbA, serviceA) = NewService();
            var (dbB, serviceB) = NewService();
            await using var _a = dbA;
            await using var _b = dbB;

            var receipts = await RaceAsync(
                () => serviceA.ReceiveAsync(agent, request, default),
                () => serviceB.ReceiveAsync(agent, request, default));

            Assert.Equal(receipts[0].VersionId, receipts[1].VersionId);
            Assert.Equal(receipts[0].IngestId, receipts[1].IngestId);
            Assert.Equal(before + 1, receipts[0].VersionNo);
            Assert.Equal(1, receipts.Count(r => !r.AlreadyReceived));
            Assert.Equal(1, receipts.Count(r => r.AlreadyReceived));

            await using var check = _fixture.CreateDbContext();
            Assert.Equal(1, await check.InstrumentIngests.CountAsync(i => i.EquipmentId == agent.EquipmentId));
            Assert.Equal(before + 1, await check.InstrumentIngestVersions.CountAsync(v => v.InstrumentIngestId == receipts[0].IngestId));
            Assert.Equal(1, await check.InstrumentIngestPayloads.CountAsync(p => p.InstrumentIngestVersionId == receipts[0].VersionId));
            // One INGEST_RECEIVED for the version, written by the winner only.
            Assert.Equal(1, await check.AuditLogs.CountAsync(a =>
                a.ActionCode == "INGEST_RECEIVED" && a.EntityId == receipts[0].VersionId.ToString()));
        }
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_different_hashes_get_consecutive_versions_and_only_the_newest_is_pending(bool preexisting)
    {
        for (var round = 0; round < 5; round++)
        {
            var agent = await SeedAgentAsync();
            var before = await StartAsync(agent, preexisting);
            var (dbA, serviceA) = NewService();
            var (dbB, serviceB) = NewService();
            await using var _a = dbA;
            await using var _b = dbB;

            var receipts = await RaceAsync(
                () => serviceA.ReceiveAsync(agent, Request(Body("integration A"), "SEQ-RACE"), default),
                () => serviceB.ReceiveAsync(agent, Request(Body("integration B"), "SEQ-RACE"), default));

            Assert.All(receipts, r => Assert.False(r.AlreadyReceived));
            Assert.Equal(receipts[0].IngestId, receipts[1].IngestId);
            Assert.Equal(new[] { before + 1, before + 2 }, receipts.Select(r => r.VersionNo).Order().ToArray());

            await using var check = _fixture.CreateDbContext();
            var versions = await check.InstrumentIngestVersions.Where(v => v.InstrumentIngestId == receipts[0].IngestId)
                .OrderBy(v => v.VersionNo).ToListAsync();
            Assert.Equal(before + 2, versions.Count);
            Assert.All(versions.SkipLast(1), v => Assert.Equal(InstrumentIngestStatus.Superseded, v.Status));
            Assert.Equal(InstrumentIngestStatus.PendingReview, versions[^1].Status);
        }
    }

    [PostgresFact]
    public async Task Reintegration_after_a_resend_stays_consistent_under_the_append_only_triggers()
    {
        var agent = await SeedAgentAsync();
        var (db, service) = NewService();
        await using var _ = db;
        var first = Request(System.Text.Encoding.UTF8.GetBytes($"first {Guid.NewGuid()}"), "SEQ-SEQUENTIAL");
        var second = Request(System.Text.Encoding.UTF8.GetBytes($"second {Guid.NewGuid()}"), "SEQ-SEQUENTIAL");

        var r1 = await service.ReceiveAsync(agent, first, default);
        var r1Again = await service.ReceiveAsync(agent, first, default);
        var r2 = await service.ReceiveAsync(agent, second, default);

        Assert.True(r1Again.AlreadyReceived);
        Assert.Equal(r1.VersionId, r1Again.VersionId);
        Assert.Equal(2, r2.VersionNo);

        await using var check = _fixture.CreateDbContext();
        var old = await check.InstrumentIngestVersions.AsNoTracking().SingleAsync(v => v.Id == r1.VersionId);
        Assert.Equal(InstrumentIngestStatus.Superseded, old.Status);

        // The agent's generic audit rows carry no user.
        var generic = await check.AuditLogs.Where(a => a.EntityName == nameof(InstrumentIngestVersion)
                                                       && a.SystemProcessName == $"InstrumentAgent:{agent.Code}").ToListAsync();
        Assert.NotEmpty(generic);
        Assert.All(generic, a => { Assert.Null(a.UserId); Assert.Equal(ActorType.System, a.ActorType); });
    }

    // jsonb rejects U+0000 and unpaired surrogates; the manifest must be
    // stored (as a JSON string) and quarantined instead of failing the receipt.
    [PostgresTheory]
    [InlineData("[{\"path\":\"a\\ud800\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]", false)]
    [InlineData("[{\"path\":\"a\\udc00\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]", false)]
    [InlineData("[{\"path\":\"a\\uD800\\u0041\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]", false)]
    [InlineData("[{\"path\":\"a\\u0000\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]", false)]
    [InlineData("[{\"path\":\"a{LONE}b\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]", false)]
    [InlineData("a{NUL}b", false)]
    [InlineData("[]", false)]
    [InlineData("[{\"path\":\"a\\ud83d\\ude00\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]", true)]
    public async Task Manifests_jsonb_cannot_hold_are_stored_and_quarantined_not_a_server_error(string manifest, bool valid)
    {
        var agent = await SeedAgentAsync();
        var (db, service) = NewService();
        await using var _ = db;
        var content = System.Text.Encoding.UTF8.GetBytes($"report {Guid.NewGuid()}");

        var receipt = await service.ReceiveAsync(agent,
            InstrumentIngestReceiveTests.Request(content, "SEQ-MANIFEST", manifest: InstrumentIngestReceiveTests.Real(manifest), agentClock: DateTime.UtcNow), default);

        Assert.Equal(valid ? InstrumentIngestStatus.PendingReview : InstrumentIngestStatus.Quarantined, receipt.Status);
        await using var check = _fixture.CreateDbContext();
        Assert.Single(await check.InstrumentIngestPayloads.Where(p => p.InstrumentIngestVersionId == receipt.VersionId).ToListAsync());
    }
}

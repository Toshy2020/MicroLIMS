using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Postgres-backed on purpose: unique indexes and the append-only triggers
// only exist in a real database (the InMemory provider has neither).
[Collection("PostgresDatabaseCollection")]
public class InstrumentIngestModelTests
{
    private const string RaiseException = "P0001";

    private readonly PostgresTestFixture _fixture;

    public InstrumentIngestModelTests(PostgresTestFixture fixture) => _fixture = fixture;

    [Fact]
    public void InstrumentIngestStatus_names_and_values_never_change()
    {
        Assert.Equal(new[] { "Adopted", "Dismissed", "PendingReview", "Quarantined", "Superseded" },
            Enum.GetNames<InstrumentIngestStatus>().Order());
        Assert.Equal(1, (int)InstrumentIngestStatus.Quarantined);
        Assert.Equal(2, (int)InstrumentIngestStatus.PendingReview);
        Assert.Equal(3, (int)InstrumentIngestStatus.Adopted);
        Assert.Equal(4, (int)InstrumentIngestStatus.Superseded);
        Assert.Equal(5, (int)InstrumentIngestStatus.Dismissed);
    }

    private sealed record Seeded(
        string Uid, int AgentId, int EquipmentId, int IngestId, int OtherIngestId, int VersionId, int PayloadId);

    private async Task<Seeded> SeedAsync()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        await using var db = _fixture.CreateDbContext();
        var equipment = new Equipment
        {
            Name = $"HPLC {uid}", Code = $"HPLC-II-{uid}", Type = EquipmentType.Hplc, SectionId = _fixture.SeededSectionId
        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        var agent = new InstrumentAgent
        {
            Code = $"AG-{uid}", Name = "Agent", EquipmentId = equipment.Id, SectionId = _fixture.SeededSectionId,
            TimeZoneId = "Africa/Cairo", KeyHash = $"{uid}-hash", KeyPrefix = uid[..4],
            IsActive = true, CreatedAt = DateTime.UtcNow, CreatedByUserId = _fixture.SeededUserId
        };
        db.InstrumentAgents.Add(agent);
        await db.SaveChangesAsync();

        InstrumentIngest Ingest(string sequence) => new()
        {
            InstrumentAgentId = agent.Id, EquipmentId = equipment.Id, SequenceName = sequence,
            InjectionNumber = 1, AcquiredAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)
        };
        var ingest = Ingest($"SEQ-{uid}");
        var other = Ingest($"SEQ2-{uid}");
        db.InstrumentIngests.AddRange(ingest, other);
        await db.SaveChangesAsync();

        var version = NewVersion(ingest.Id, 1, $"{uid}-sha");
        db.InstrumentIngestVersions.Add(version);
        await db.SaveChangesAsync();

        var payload = new InstrumentIngestPayload
        {
            InstrumentIngestVersionId = version.Id, ParserVersion = "1.0",
            ManifestJson = "{\"files\":[]}", ParsedJson = "{\"peaks\":[]}", CreatedAt = DateTime.UtcNow
        };
        db.InstrumentIngestPayloads.Add(payload);
        await db.SaveChangesAsync();

        return new Seeded(uid, agent.Id, equipment.Id, ingest.Id, other.Id, version.Id, payload.Id);
    }

    private static InstrumentIngestVersion NewVersion(int ingestId, int versionNo, string sha) => new()
    {
        InstrumentIngestId = ingestId, VersionNo = versionNo, ReportSha256 = sha,
        ReportStorageKey = $"ingest/{sha}.pdf", ReportFileName = "report.pdf",
        Status = InstrumentIngestStatus.PendingReview, ReceivedAt = DateTime.UtcNow,
        AcquiredAtLocalText = "01-Oct-26 10:00:00", TimeZoneIdUsed = "Africa/Cairo", ClockOffsetSeconds = 0
    };

    private async Task<PostgresException> RawFailsAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var db = _fixture.CreateDbContext();
        return await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, parameters));
    }

    [PostgresFact]
    public async Task Unique_indexes_reject_duplicate_identity_hash_and_version_number()
    {
        var s = await SeedAsync();

        await using (var dupIngest = _fixture.CreateDbContext())
        {
            dupIngest.InstrumentIngests.Add(new InstrumentIngest
            {
                InstrumentAgentId = s.AgentId, EquipmentId = s.EquipmentId, SequenceName = $"SEQ-{s.Uid}",
                InjectionNumber = 1, AcquiredAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)
            });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => dupIngest.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)ex.InnerException!).SqlState);
        }

        await using (var dupHash = _fixture.CreateDbContext())
        {
            dupHash.InstrumentIngestVersions.Add(NewVersion(s.IngestId, 2, $"{s.Uid}-sha"));
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => dupHash.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)ex.InnerException!).SqlState);
        }

        await using (var dupNo = _fixture.CreateDbContext())
        {
            dupNo.InstrumentIngestVersions.Add(NewVersion(s.IngestId, 1, $"{s.Uid}-sha-other"));
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => dupNo.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)ex.InnerException!).SqlState);
        }

        // Same hash under a different ingest is a different report - allowed.
        await using var ok = _fixture.CreateDbContext();
        ok.InstrumentIngestVersions.Add(NewVersion(s.OtherIngestId, 1, $"{s.Uid}-sha"));
        await ok.SaveChangesAsync();
    }

    [PostgresTheory]
    [InlineData("ReportSha256", "'changed'")]
    [InlineData("ReportStorageKey", "'ingest/changed.pdf'")]
    [InlineData("ReportFileName", "'changed.pdf'")]
    [InlineData("OperatorAsReported", "'someone else'")]
    [InlineData("AcquiredAtLocalText", "'02-Oct-26 10:00:00'")]
    [InlineData("ReceivedAt", "now() + interval '1 day'")]
    [InlineData("TimeZoneIdUsed", "'UTC'")]
    [InlineData("ClockOffsetSeconds", "42")]
    [InlineData("VersionNo", "99")]
    public async Task Version_evidence_columns_cannot_be_updated(string column, string newValue)
    {
        var s = await SeedAsync();
        var ex = await RawFailsAsync(
            $"UPDATE \"InstrumentIngestVersions\" SET \"{column}\" = {newValue} WHERE \"Id\" = @id",
            new NpgsqlParameter("id", s.VersionId));
        Assert.Equal(RaiseException, ex.SqlState);
    }

    [PostgresFact]
    public async Task Version_ingest_reassignment_is_rejected()
    {
        var s = await SeedAsync();
        var ex = await RawFailsAsync(
            "UPDATE \"InstrumentIngestVersions\" SET \"InstrumentIngestId\" = @other WHERE \"Id\" = @id",
            new NpgsqlParameter("other", s.OtherIngestId), new NpgsqlParameter("id", s.VersionId));
        Assert.Equal(RaiseException, ex.SqlState);
    }

    [PostgresFact]
    public async Task Version_review_outcome_is_updatable_and_rewriting_equal_evidence_is_allowed()
    {
        var s = await SeedAsync();

        // Through EF, so the xmin concurrency token is part of the UPDATE.
        await using (var db = _fixture.CreateDbContext())
        {
            var version = await db.InstrumentIngestVersions.SingleAsync(v => v.Id == s.VersionId);
            version.Status = InstrumentIngestStatus.Adopted;
            version.StatusReason = "reviewed";
            version.ResolvedByUserId = _fixture.SeededUserId;
            version.ResolvedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var version = await db.InstrumentIngestVersions.AsNoTracking().SingleAsync(v => v.Id == s.VersionId);
            Assert.Equal(InstrumentIngestStatus.Adopted, version.Status);
            Assert.Equal("reviewed", version.StatusReason);
            Assert.Equal(_fixture.SeededUserId, version.ResolvedByUserId);
        }

        // Writing the same evidence value back is not a change.
        await using var raw = _fixture.CreateDbContext();
        var rows = await raw.Database.ExecuteSqlRawAsync(
            "UPDATE \"InstrumentIngestVersions\" SET \"ReportSha256\" = \"ReportSha256\", \"Status\" = 4 WHERE \"Id\" = @id",
            new NpgsqlParameter("id", s.VersionId));
        Assert.Equal(1, rows);
    }

    [PostgresFact]
    public async Task Version_cannot_be_deleted()
    {
        var s = await SeedAsync();
        var ex = await RawFailsAsync(
            "DELETE FROM \"InstrumentIngestVersions\" WHERE \"Id\" = @id", new NpgsqlParameter("id", s.VersionId));
        Assert.Equal(RaiseException, ex.SqlState);
    }

    [PostgresTheory]
    [InlineData("ParserVersion", "'2.0'")]
    [InlineData("ManifestJson", "'[1]'::jsonb")]
    [InlineData("ParsedJson", "'[2]'::jsonb")]
    [InlineData("CreatedAt", "now() + interval '1 day'")]
    public async Task Payload_cannot_be_updated(string column, string newValue)
    {
        var s = await SeedAsync();
        var ex = await RawFailsAsync(
            $"UPDATE \"InstrumentIngestPayloads\" SET \"{column}\" = {newValue} WHERE \"Id\" = @id",
            new NpgsqlParameter("id", s.PayloadId));
        Assert.Equal(RaiseException, ex.SqlState);
    }

    [PostgresFact]
    public async Task Payload_update_is_rejected_even_when_nothing_changes_and_through_EF()
    {
        var s = await SeedAsync();
        var ex = await RawFailsAsync(
            "UPDATE \"InstrumentIngestPayloads\" SET \"ParserVersion\" = \"ParserVersion\" WHERE \"Id\" = @id",
            new NpgsqlParameter("id", s.PayloadId));
        Assert.Equal(RaiseException, ex.SqlState);

        await using var db = _fixture.CreateDbContext();
        var payload = await db.InstrumentIngestPayloads.SingleAsync(p => p.Id == s.PayloadId);
        payload.ParserVersion = "2.0";
        var efEx = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(RaiseException, ((PostgresException)efEx.InnerException!).SqlState);
    }

    [PostgresFact]
    public async Task Payload_cannot_be_deleted()
    {
        var s = await SeedAsync();
        var ex = await RawFailsAsync(
            "DELETE FROM \"InstrumentIngestPayloads\" WHERE \"Id\" = @id", new NpgsqlParameter("id", s.PayloadId));
        Assert.Equal(RaiseException, ex.SqlState);
    }

    [PostgresTheory]
    [InlineData("SequenceName", "'renamed'")]
    [InlineData("InjectionNumber", "7")]
    [InlineData("AcquiredAtUtc", "now()")]
    public async Task Ingest_identity_columns_cannot_be_updated(string column, string newValue)
    {
        var s = await SeedAsync();
        var ex = await RawFailsAsync(
            $"UPDATE \"InstrumentIngests\" SET \"{column}\" = {newValue} WHERE \"Id\" = @id",
            new NpgsqlParameter("id", s.IngestId));
        Assert.Equal(RaiseException, ex.SqlState);
    }

    [PostgresFact]
    public async Task Ingest_agent_and_equipment_cannot_be_reassigned()
    {
        var s = await SeedAsync();
        var other = await SeedAsync();

        var agentEx = await RawFailsAsync(
            "UPDATE \"InstrumentIngests\" SET \"InstrumentAgentId\" = @v WHERE \"Id\" = @id",
            new NpgsqlParameter("v", other.AgentId), new NpgsqlParameter("id", s.IngestId));
        Assert.Equal(RaiseException, agentEx.SqlState);

        var equipmentEx = await RawFailsAsync(
            "UPDATE \"InstrumentIngests\" SET \"EquipmentId\" = @v WHERE \"Id\" = @id",
            new NpgsqlParameter("v", other.EquipmentId), new NpgsqlParameter("id", s.IngestId));
        Assert.Equal(RaiseException, equipmentEx.SqlState);
    }

    [PostgresFact]
    public async Task Ingest_cannot_be_deleted()
    {
        var s = await SeedAsync();
        var ex = await RawFailsAsync(
            "DELETE FROM \"InstrumentIngests\" WHERE \"Id\" = @id", new NpgsqlParameter("id", s.OtherIngestId));
        Assert.Equal(RaiseException, ex.SqlState);
    }
}

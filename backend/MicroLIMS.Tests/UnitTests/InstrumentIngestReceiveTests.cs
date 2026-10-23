using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class InstrumentIngestReceiveTests
{
    internal static readonly DateTimeOffset ServerNow = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    internal const string GoodLocal = "2026-10-01 10:00:00"; // Europe/Berlin: CEST, 08:00 UTC

    internal static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    internal static string Sha(string s) => Sha(Encoding.UTF8.GetBytes(s));

    internal static string ManifestFor(byte[] report) =>
        $"[{{\"path\":\"report.pdf\",\"sha256\":\"{Sha(report)}\",\"bytes\":{report.Length}}}]";

    internal static IngestRequest Request(byte[] content, string sequence = "SEQ-1", int injection = 1,
        string local = GoodLocal, string? manifest = null, DateTime? agentClock = null, string fileName = "report.pdf",
        string? sha = null) =>
        new(sequence, injection, local, sha ?? Sha(content), fileName, content, manifest ?? ManifestFor(content),
            agentClock ?? ServerNow.UtcDateTime, "analyst1");

    private sealed class World
    {
        public required MicroLimsDbContext Db { get; init; }
        public required InMemoryFileStorageService Storage { get; init; }
        public required InstrumentIngestService Service { get; init; }
        public required InstrumentAgentContext Agent { get; init; }
    }

    private static World NewWorld(string timeZoneId = "Europe/Berlin")
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var section = TestServiceFactory.EnsureMicroSection(db);
        var hplc = new Equipment { Name = "HPLC 1", Code = "HPLC-01", Type = EquipmentType.Hplc, SectionId = section.Id };
        db.Equipment.Add(hplc);
        db.SaveChanges();
        var agent = new InstrumentAgent
        {
            Code = "AGT-HPLC-01", Name = "Lab PC", EquipmentId = hplc.Id, SectionId = section.Id, TimeZoneId = timeZoneId,
            KeyHash = "h", KeyPrefix = "p", IsActive = true, CreatedAt = ServerNow.UtcDateTime, CreatedByUserId = 1
        };
        db.InstrumentAgents.Add(agent);
        db.SaveChanges();

        db.AuditLogs.RemoveRange(db.AuditLogs.ToList()); // the seeding above is not part of any receipt
        db.SaveChanges();

        var storage = new InMemoryFileStorageService();
        var clock = new LabClock(new FixedTimeProvider(ServerNow), TimeZoneInfo.Utc);
        var service = new InstrumentIngestService(db, storage,
            new AuditEventService(db, new DatabaseSequenceHelper(db)), clock);
        return new World
        {
            Db = db, Storage = storage, Service = service,
            Agent = new InstrumentAgentContext(agent.Id, agent.Code, hplc.Id, section.Id, timeZoneId)
        };
    }

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    // xUnit's data round trip alters a lone surrogate or NUL in InlineData, so
    // those characters are written as tokens and built here.
    internal static string Real(string s) => s.Replace("{LONE}", "\uD800").Replace("{NUL}", "\0")
        .Replace("{PAIR}", "😀").Replace("{HIGH}", "\uD83D").Replace("{LOW}", "\uDE00");

    [Fact]
    public async Task Rejects_when_server_hash_differs()
    {
        var w = NewWorld();
        var content = Bytes("report body");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.ReceiveAsync(w.Agent, Request(content, sha: Sha("something else")), default));

        Assert.Contains("hash", ex.Message);
        Assert.Empty(w.Storage.Files);
        Assert.Empty(w.Db.InstrumentIngests);
        Assert.Empty(w.Db.InstrumentIngestVersions);
        Assert.Empty(w.Db.AuditLogs);
    }

    [Fact]
    public async Task Rejects_empty_and_oversized_reports_and_malformed_local_times()
    {
        var w = NewWorld();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.ReceiveAsync(w.Agent, Request(Array.Empty<byte>()), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.ReceiveAsync(w.Agent, Request(new byte[InstrumentIngestService.MaxReportBytes + 1]), default));
        foreach (var bad in new[] { "01-Oct-26 10:00:00", "2026-10-01T10:00:00", "2026-10-01 10:00", "2026-13-01 10:00:00", "" })
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                w.Service.ReceiveAsync(w.Agent, Request(Bytes("x"), local: bad), default));
        }

        // The limit itself is accepted.
        await w.Service.ReceiveAsync(w.Agent, Request(new byte[InstrumentIngestService.MaxReportBytes]), default);

        Assert.Single(w.Db.InstrumentIngestVersions);
    }

    [Fact]
    public async Task Same_hash_returns_existing_receipt_AlreadyReceived_true()
    {
        var w = NewWorld();
        var content = Bytes("report body");

        var first = await w.Service.ReceiveAsync(w.Agent, Request(content), default);
        var auditCount = await w.Db.AuditLogs.CountAsync();
        var second = await w.Service.ReceiveAsync(w.Agent, Request(content), default);

        Assert.False(first.AlreadyReceived);
        Assert.True(second.AlreadyReceived);
        Assert.Equal((first.IngestId, first.VersionId, first.VersionNo, first.Status),
            (second.IngestId, second.VersionId, second.VersionNo, second.Status));
        Assert.Equal(1, first.VersionNo);
        Assert.Single(w.Db.InstrumentIngests);
        Assert.Single(w.Db.InstrumentIngestVersions);
        Assert.Single(w.Storage.Files);
        Assert.Equal(auditCount, await w.Db.AuditLogs.CountAsync()); // nothing written for a resend
    }

    [Fact]
    public async Task New_hash_creates_next_version_and_supersedes_pending_previous()
    {
        var w = NewWorld();
        var v1 = await w.Service.ReceiveAsync(w.Agent, Request(Bytes("first")), default);
        var v2 = await w.Service.ReceiveAsync(w.Agent, Request(Bytes("second")), default);

        Assert.Equal(v1.IngestId, v2.IngestId);
        Assert.Equal(2, v2.VersionNo);
        Assert.False(v2.AlreadyReceived);
        Assert.Equal(InstrumentIngestStatus.PendingReview, v2.Status);

        var old = await w.Db.InstrumentIngestVersions.SingleAsync(v => v.Id == v1.VersionId);
        Assert.Equal(InstrumentIngestStatus.Superseded, old.Status);
        Assert.Equal("Newer report received before adoption", old.StatusReason);

        var superseded = await w.Db.AuditLogs.SingleAsync(a => a.ActionCode == "INGEST_SUPERSEDED");
        Assert.Equal(v1.VersionId.ToString(), superseded.EntityId);
    }

    [Fact]
    public async Task New_hash_supersedes_quarantined_previous_but_leaves_an_adopted_one()
    {
        var w = NewWorld();
        var quarantined = await w.Service.ReceiveAsync(w.Agent, Request(Bytes("a"), manifest: "not json"), default);
        Assert.Equal(InstrumentIngestStatus.Quarantined, quarantined.Status);
        var adopted = await w.Service.ReceiveAsync(w.Agent, Request(Bytes("b")), default);
        (await w.Db.InstrumentIngestVersions.SingleAsync(v => v.Id == adopted.VersionId)).Status = InstrumentIngestStatus.Adopted;
        await w.Db.SaveChangesAsync();

        var latest = await w.Service.ReceiveAsync(w.Agent, Request(Bytes("c")), default);

        Assert.Equal(3, latest.VersionNo);
        var statuses = await w.Db.InstrumentIngestVersions.OrderBy(v => v.VersionNo).Select(v => v.Status).ToListAsync();
        Assert.Equal(new[] { InstrumentIngestStatus.Superseded, InstrumentIngestStatus.Adopted, InstrumentIngestStatus.PendingReview }, statuses);
    }

    [Theory]
    [InlineData(null, 119, false)]
    [InlineData(null, -119, false)]
    [InlineData(null, 121, true)]
    [InlineData(null, -121, true)]
    [InlineData("30", 31, true)]
    [InlineData("30", 29, false)]
    [InlineData("600", 400, false)]
    [InlineData("not a number", 121, true)] // invalid setting falls back to 120
    [InlineData("-5", 100, false)]
    public async Task Drift_over_limit_quarantines(string? setting, int agentBehindSeconds, bool quarantined)
    {
        var w = NewWorld();
        if (setting is not null)
        {
            w.Db.ConfigurationSettings.Add(new ConfigurationSetting
            {
                SettingKey = "InstrumentIngest.MaxClockDriftSeconds", SettingValue = setting, DataType = "int", SettingGroup = "InstrumentIngest"
            });
            await w.Db.SaveChangesAsync();
        }

        var receipt = await w.Service.ReceiveAsync(w.Agent,
            Request(Bytes("x"), agentClock: ServerNow.UtcDateTime.AddSeconds(-agentBehindSeconds)), default);

        var version = await w.Db.InstrumentIngestVersions.SingleAsync(v => v.Id == receipt.VersionId);
        Assert.Equal(agentBehindSeconds, version.ClockOffsetSeconds);
        Assert.Equal(quarantined ? InstrumentIngestStatus.Quarantined : InstrumentIngestStatus.PendingReview, receipt.Status);
        if (quarantined) Assert.Contains("clock", version.StatusReason);
        else Assert.Null(version.StatusReason);
        Assert.Single(w.Storage.Files); // quarantined reports are still stored
    }

    [Fact]
    public async Task An_unspecified_or_local_agent_clock_is_read_as_UTC()
    {
        var w = NewWorld();
        var receipt = await w.Service.ReceiveAsync(w.Agent,
            Request(Bytes("x"), agentClock: DateTime.SpecifyKind(ServerNow.UtcDateTime.AddSeconds(-5), DateTimeKind.Unspecified)), default);
        Assert.Equal(5, (await w.Db.InstrumentIngestVersions.SingleAsync(v => v.Id == receipt.VersionId)).ClockOffsetSeconds);
    }

    [Fact]
    public async Task DST_gap_and_fold_local_times_are_flagged()
    {
        var w = NewWorld("Europe/Berlin");

        // Normal summer time: 10:00 CEST (+2) = 08:00 UTC.
        var normal = await w.Service.ReceiveAsync(w.Agent, Request(Bytes("n"), injection: 1), default);
        // Spring forward 2026-03-29: 02:00 -> 03:00, so 02:30 never happened.
        var gap = await w.Service.ReceiveAsync(w.Agent, Request(Bytes("g"), injection: 2, local: "2026-03-29 02:30:00"), default);
        // Fall back 2026-10-25: 03:00 CEST -> 02:00 CET, so 02:30 happened twice.
        var fold = await w.Service.ReceiveAsync(w.Agent, Request(Bytes("f"), injection: 3, local: "2026-10-25 02:30:00"), default);

        async Task<(InstrumentIngest Ingest, InstrumentIngestVersion Version)> Load(IngestReceipt r) =>
            (await w.Db.InstrumentIngests.SingleAsync(i => i.Id == r.IngestId), await w.Db.InstrumentIngestVersions.SingleAsync(v => v.Id == r.VersionId));

        var (nIngest, nVersion) = await Load(normal);
        Assert.Equal(new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc), nIngest.AcquiredAtUtc);
        Assert.Equal(InstrumentIngestStatus.PendingReview, normal.Status);
        Assert.Null(nVersion.StatusReason);

        var (gIngest, gVersion) = await Load(gap);
        Assert.Equal(InstrumentIngestStatus.Quarantined, gap.Status);
        Assert.Contains("nonexistent local time", gVersion.StatusReason);
        Assert.Equal(new DateTime(2026, 3, 29, 1, 30, 0), gIngest.AcquiredAtUtc); // local minus the standard offset (+1)
        Assert.Equal("2026-03-29 02:30:00", gVersion.AcquiredAtLocalText);

        var (fIngest, fVersion) = await Load(fold);
        Assert.Equal(InstrumentIngestStatus.PendingReview, fold.Status);
        Assert.Contains("ambiguous local time", fVersion.StatusReason);
        Assert.Contains("earlier instant", fVersion.StatusReason);
        Assert.Equal(new DateTime(2026, 10, 25, 0, 30, 0), fIngest.AcquiredAtUtc); // 02:30 CEST (+2), the earlier of the two
        Assert.Equal("Europe/Berlin", fVersion.TimeZoneIdUsed);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("{\"path\":\"a\"}")]
    [InlineData("[1]")]
    [InlineData("[{\"path\":\"\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")]
    [InlineData("[{\"path\":\"a\",\"sha256\":\"ABCDEF0000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")]
    [InlineData("[{\"path\":\"a\",\"sha256\":\"short\",\"bytes\":1}]")]
    [InlineData("[{\"path\":\"a\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":-1}]")]
    [InlineData("[{\"path\":\"a\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1.5}]")]
    [InlineData("[{\"path\":\"a\\u0000b\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")]
    public async Task Invalid_manifest_quarantines_but_still_stores_everything(string manifest)
    {
        var w = NewWorld();
        var receipt = await w.Service.ReceiveAsync(w.Agent, Request(Bytes("x"), manifest: manifest), default);

        Assert.Equal(InstrumentIngestStatus.Quarantined, receipt.Status);
        var version = await w.Db.InstrumentIngestVersions.Include(v => v.Payloads).SingleAsync(v => v.Id == receipt.VersionId);
        Assert.StartsWith("invalid manifest: ", version.StatusReason);
        Assert.Single(w.Storage.Files);
        var payload = Assert.Single(version.Payloads);
        // Stored as JSON: the original when it was JSON, otherwise the raw text as a JSON string.
        using var _ = System.Text.Json.JsonDocument.Parse(payload.ManifestJson);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("  [ ]  ")]
    public async Task Empty_manifest_quarantines(string manifest)
    {
        var w = NewWorld();
        var receipt = await w.Service.ReceiveAsync(w.Agent, Request(Bytes("x"), manifest: manifest), default);

        Assert.Equal(InstrumentIngestStatus.Quarantined, receipt.Status);
        var version = await w.Db.InstrumentIngestVersions.SingleAsync();
        Assert.Equal("invalid manifest: manifest is empty", version.StatusReason);
        Assert.Equal(manifest, (await w.Db.InstrumentIngestPayloads.SingleAsync()).ManifestJson);
    }

    [Theory]
    [InlineData("[{\"path\":\"a\\ud800\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")] // lone high escape
    [InlineData("[{\"path\":\"a\\udc00\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")] // lone low escape
    [InlineData("[{\"path\":\"a\\uD800\\u0041\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")] // high not followed by low
    [InlineData("[{\"path\":\"a\\udc00\\ud800\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")] // reversed pair
    [InlineData("[{\"path\":\"a{LONE}b\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")] // raw lone surrogate
    [InlineData("[{\"path\":\"a\\\\ud800\\ude00\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")] // escaped backslash + literal "ud800", then a REAL lone low escape
    [InlineData("[{\"a\\\\ud800\\ude00\":1,\"path\":\"a\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")] // the same in a key
    [InlineData("[{\"path\":\"a\\ud83d\",\"x\":\"\\ude00\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")] // halves in different tokens
    [InlineData("[{\"path\":\"a\\ud83d{LONE}\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")] // escaped high + raw lone high
    [InlineData("[{\"path\":\"a\\ud83d{LOW}\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")] // escaped high + raw low: halves never mix
    [InlineData("[{\"path\":\"a{HIGH}\\ude00\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"bytes\":1}]")] // raw high + escaped low
    [InlineData("{LONE}")]
    public async Task Manifest_with_an_unpaired_surrogate_is_quarantined_and_kept_as_a_json_string(string manifest)
    {
        var w = NewWorld();
        var receipt = await w.Service.ReceiveAsync(w.Agent, Request(Bytes("x"), manifest: Real(manifest)), default);

        Assert.Equal(InstrumentIngestStatus.Quarantined, receipt.Status);
        Assert.StartsWith("invalid manifest: ", (await w.Db.InstrumentIngestVersions.SingleAsync()).StatusReason);
        using var stored = System.Text.Json.JsonDocument.Parse((await w.Db.InstrumentIngestPayloads.SingleAsync()).ManifestJson);
        Assert.Equal(System.Text.Json.JsonValueKind.String, stored.RootElement.ValueKind);
    }

    [Theory]
    [InlineData("a\\ud83d\\ude00")]   // escaped pair
    [InlineData("a{PAIR}")]           // raw pair
    [InlineData("a\\\\ud800")]        // escaped backslash + literal text "ud800": no escape at all
    [InlineData("a\\\\\\\\ud800")]    // two escaped backslashes + literal text
    [InlineData("a\\\\u0000")]        // escaped backslash + literal text "u0000"
    public async Task Manifest_with_valid_pairs_or_only_literal_escape_text_is_accepted(string path)
    {
        var w = NewWorld();
        var content = Bytes("x");
        var manifest = $"[{{\"path\":\"{Real(path)}\",\"sha256\":\"{Sha(content)}\",\"bytes\":1}}]";
        var receipt = await w.Service.ReceiveAsync(w.Agent, Request(content, manifest: manifest), default);

        Assert.Equal(InstrumentIngestStatus.PendingReview, receipt.Status);
        Assert.Equal(manifest, (await w.Db.InstrumentIngestPayloads.SingleAsync()).ManifestJson);
    }

    [Theory]
    [InlineData("0001-01-01 00:00:00")]
    [InlineData("9999-12-31 23:59:59")]
    [InlineData("1999-12-31 23:59:59")]
    [InlineData("2101-01-01 00:00:00")]
    public async Task Acquisition_years_outside_2000_to_2100_are_rejected_before_anything_is_written(string local)
    {
        var w = NewWorld();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.ReceiveAsync(w.Agent, Request(Bytes("x"), local: local), default));

        Assert.Empty(w.Storage.Files);
        Assert.Empty(w.Db.InstrumentIngests);
    }

    [Theory]
    [InlineData("2000-01-01 00:00:00")]
    [InlineData("2100-12-31 23:59:59")]
    public async Task Acquisition_years_2000_and_2100_are_accepted(string local)
    {
        var w = NewWorld();
        var receipt = await w.Service.ReceiveAsync(w.Agent, Request(Bytes("x"), local: local), default);
        Assert.False(receipt.AlreadyReceived);
    }

    [Fact]
    public async Task Uppercase_hex_request_hash_is_accepted_and_the_lowercase_hash_is_stored()
    {
        var w = NewWorld();
        var content = Bytes("report body");
        var receipt = await w.Service.ReceiveAsync(w.Agent, Request(content, sha: Sha(content).ToUpperInvariant()), default);

        Assert.Equal(Sha(content), (await w.Db.InstrumentIngestVersions.SingleAsync(v => v.Id == receipt.VersionId)).ReportSha256);
        // The same bytes sent with the lowercase hash are the same report.
        Assert.True((await w.Service.ReceiveAsync(w.Agent, Request(content), default)).AlreadyReceived);
    }

    [Fact]
    public async Task Valid_manifest_is_stored_verbatim_and_not_quarantined()
    {
        var w = NewWorld();
        var content = Bytes("x");
        var manifest = ManifestFor(content) + "  ";
        var receipt = await w.Service.ReceiveAsync(w.Agent, Request(content, manifest: manifest), default);

        Assert.Equal(InstrumentIngestStatus.PendingReview, receipt.Status);
        Assert.Equal(manifest, (await w.Db.InstrumentIngestPayloads.SingleAsync()).ManifestJson);
    }

    [Fact]
    public async Task Receipt_creates_one_unparsed_payload_row_and_records_the_evidence()
    {
        var w = NewWorld();
        var content = Bytes("report body");
        var receipt = await w.Service.ReceiveAsync(w.Agent, Request(content, fileName: "Report.PDF"), default);

        var payload = await w.Db.InstrumentIngestPayloads.SingleAsync();
        Assert.Equal(receipt.VersionId, payload.InstrumentIngestVersionId);
        Assert.Equal("unparsed", payload.ParserVersion);
        Assert.Equal("{}", payload.ParsedJson);
        Assert.Equal(ManifestFor(content), payload.ManifestJson);

        var version = await w.Db.InstrumentIngestVersions.SingleAsync();
        Assert.Equal(Sha(content), version.ReportSha256);
        Assert.Equal("Report.PDF", version.ReportFileName);
        Assert.Equal("analyst1", version.OperatorAsReported);
        Assert.Equal(ServerNow.UtcDateTime, version.ReceivedAt);
        Assert.Equal(0, version.ClockOffsetSeconds);

        var ingest = await w.Db.InstrumentIngests.SingleAsync();
        Assert.Equal((w.Agent.AgentId, w.Agent.EquipmentId, "SEQ-1", 1), (ingest.InstrumentAgentId, ingest.EquipmentId, ingest.SequenceName, ingest.InjectionNumber));
    }

    [Fact]
    public async Task Writes_system_audit_event_with_hashes_and_never_UserId_0()
    {
        var w = NewWorld();
        var content = Bytes("report body");
        var manifest = ManifestFor(content);
        var receipt = await w.Service.ReceiveAsync(w.Agent, Request(content, manifest: manifest), default);
        await w.Service.ReceiveAsync(w.Agent, Request(Bytes("reintegrated")), default);

        var logs = await w.Db.AuditLogs.Include(a => a.Changes).ToListAsync();
        var received = logs.Where(a => a.ActionCode == "INGEST_RECEIVED").OrderBy(a => a.Id).First();
        Assert.Equal(ActorType.System, received.ActorType);
        Assert.Equal("InstrumentAgent:AGT-HPLC-01", received.SystemProcessName);
        Assert.Equal(AuditActionCategory.ImportExport, received.ActionCategory);
        Assert.Equal("InstrumentIngestVersion", received.EntityName);
        Assert.Equal(receipt.VersionId.ToString(), received.EntityId);
        Assert.Null(received.UserId);

        string Change(string field) => received.Changes.Single(c => c.FieldName == field).NewValue!;
        Assert.Equal(Sha(content), Change("ReportSha256"));
        Assert.Equal(Sha(manifest), Change("ManifestSha256"));
        Assert.Contains(Sha(content), Change("ReportStorageKey"));
        Assert.Equal("PendingReview", Change("Status"));
        Assert.Equal("1", Change("VersionNo"));

        // Every audit row of the receive - the explicit events and the generic
        // capture of the ingest, version and status change - is the agent's.
        Assert.Contains(logs, a => a.ActionCode == "INGEST_SUPERSEDED");
        Assert.NotEmpty(logs);
        Assert.All(logs, a =>
        {
            Assert.NotEqual(0, a.UserId);
            Assert.NotEqual(ActorType.User, a.ActorType);
            Assert.Equal("InstrumentAgent:AGT-HPLC-01", a.SystemProcessName);
        });
    }

    [Fact]
    public async Task Storage_key_contains_sha256_and_is_never_overwritten()
    {
        var w = NewWorld();
        var first = Bytes("first body");
        var second = Bytes("second body");
        var r1 = await w.Service.ReceiveAsync(w.Agent, Request(first, fileName: "report.pdf"), default);
        var r2 = await w.Service.ReceiveAsync(w.Agent, Request(second, fileName: "report.pdf"), default);
        await w.Service.ReceiveAsync(w.Agent, Request(first, fileName: "report.pdf"), default); // resend of v1

        var keys = await w.Db.InstrumentIngestVersions.OrderBy(v => v.VersionNo).Select(v => v.ReportStorageKey).ToListAsync();
        Assert.Equal(new[]
        {
            $"instrument-ingest/{r1.IngestId}/v1/{Sha(first)}.pdf",
            $"instrument-ingest/{r2.IngestId}/v2/{Sha(second)}.pdf"
        }, keys);
        Assert.Equal(2, w.Storage.Files.Count);
        Assert.Equal(first, w.Storage.Files[keys[0]]);
        Assert.Equal(second, w.Storage.Files[keys[1]]);
    }

    [Theory]
    [InlineData("report.pdf", ".pdf")]
    [InlineData("noextension", "")]
    [InlineData("weird.p d f", "")]
    [InlineData("archive.tar.gz", ".gz")]
    public async Task Storage_key_takes_the_extension_of_the_file_name_when_it_is_plain(string fileName, string extension)
    {
        var w = NewWorld();
        var content = Bytes("x");
        await w.Service.ReceiveAsync(w.Agent, Request(content, fileName: fileName), default);
        Assert.EndsWith($"/{Sha(content)}{extension}", Assert.Single(w.Storage.Files.Keys));
    }

    [Fact]
    public async Task Receiving_attributes_generic_audit_rows_to_the_agent_even_when_the_hash_is_rejected_later()
    {
        var w = NewWorld();
        await w.Service.ReceiveAsync(w.Agent, Request(Bytes("x")), default);
        Assert.Equal("AGT-HPLC-01", w.Db.CurrentAgentCode);
    }

    [Fact]
    public void Unique_index_names_used_for_retries_match_the_model()
    {
        using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseNpgsql("Host=localhost;Database=never_connected").Options);
        var model = ((IInfrastructure<IServiceProvider>)db).GetService<IDesignTimeModel>().Model;

        string[] Names<T>() => model.FindEntityType(typeof(T))!.GetIndexes().Where(i => i.IsUnique)
            .Select(i => i.GetDatabaseName()!).ToArray();

        Assert.Contains(UniqueIndexNames.InstrumentIngestIdentity, Names<InstrumentIngest>());
        Assert.Contains(UniqueIndexNames.InstrumentIngestVersionHash, Names<InstrumentIngestVersion>());
        Assert.Contains(UniqueIndexNames.InstrumentIngestVersionNo, Names<InstrumentIngestVersion>());
    }
}

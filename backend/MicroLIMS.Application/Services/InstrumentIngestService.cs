using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

// Receives one report from an authenticated instrument agent: checks the
// bytes against the agent's hash, files them, and records an immutable
// versioned receipt. Nothing is interpreted here and nothing is ever
// rejected after the hash checks - a report with a doubtful clock, time or
// manifest is kept and Quarantined for a human to look at.
//
// A resend of the same bytes is the same receipt; different bytes for the
// same injection (a reintegration) become the next version, and earlier
// versions that nobody adopted are superseded.
public class InstrumentIngestService
{
    public const int MaxReportBytes = 26_214_400; // 25 MB
    public const string MaxClockDriftSettingKey = "InstrumentIngest.MaxClockDriftSeconds";
    public const int DefaultMaxClockDriftSeconds = 120;
    public const string LocalTimeFormat = "yyyy-MM-dd HH:mm:ss";
    public const int MinAcquisitionYear = 2000;
    public const int MaxAcquisitionYear = 2100;
    public const string UnparsedParserVersion = "unparsed";
    public const string SupersededReason = "Newer report received before adoption";

    private static readonly Regex ExtensionPattern = new(@"^\.[A-Za-z0-9]{1,10}$", RegexOptions.Compiled);
    private static readonly Regex Sha256Pattern = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    private readonly IMicroLimsDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IAuditEventService _audit;
    private readonly ILabClock _clock;

    public InstrumentIngestService(IMicroLimsDbContext db, IFileStorageService storage, IAuditEventService audit, ILabClock clock)
    {
        _db = db;
        _storage = storage;
        _audit = audit;
        _clock = clock;
    }

    public Task<IngestReceipt> ReceiveAsync(InstrumentAgentContext agent, IngestRequest request, CancellationToken ct)
    {
        // Service-level callers have no middleware; this attributes the
        // generic audit rows of the ingest and version to the agent.
        _db.CurrentAgentCode = agent.Code;

        // Everything that can reject the request is checked before any
        // file or row is written.
        var content = request.ReportContent ?? Array.Empty<byte>();
        if (content.Length == 0)
            throw new BusinessRuleException("The report file is empty.");
        if (content.Length > MaxReportBytes)
            throw new BusinessRuleException($"The report file is larger than the {MaxReportBytes / (1024 * 1024)} MB limit.");
        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (!string.Equals(sha256, request.ReportSha256?.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            throw new BusinessRuleException("The report hash does not match the received bytes; the report was not stored.");
        if (string.IsNullOrWhiteSpace(request.SequenceName) || request.SequenceName.Length > 200)
            throw new BusinessRuleException("The sequence name is required and must be at most 200 characters.");
        if (string.IsNullOrWhiteSpace(request.ReportFileName) || request.ReportFileName.Length > 260)
            throw new BusinessRuleException("The report file name is required and must be at most 260 characters.");
        if (request.OperatorAsReported is { Length: > 200 })
            throw new BusinessRuleException("The operator name is longer than 200 characters.");

        var acquisition = ConvertAcquisitionTime(agent, request.AcquiredAtLocalText);
        var manifest = ReadManifest(request.ManifestJson);

        return UnitOfWork.RunAsync(_db, () => ReceiveCoreAsync(agent, request, content, sha256, acquisition, manifest, ct));
    }

    private async Task<IngestReceipt> ReceiveCoreAsync(
        InstrumentAgentContext agent, IngestRequest request, byte[] content, string sha256,
        Acquisition acquisition, Manifest manifest, CancellationToken ct)
    {
        var now = _clock.UtcNow.UtcDateTime;
        var ingest = await FindOrCreateIngestAsync(agent, request, acquisition.Utc, ct);

        var existing = await FindByHashAsync(ingest.Id, sha256, ct);
        if (existing is not null) return Receipt(existing, alreadyReceived: true);

        var quarantine = new List<string>();
        var notes = new List<string>();
        if (acquisition.Gap) quarantine.Add("nonexistent local time (DST gap)");
        if (acquisition.Fold) notes.Add("ambiguous local time (DST fold); earlier instant used");
        if (manifest.Problem is not null) quarantine.Add($"invalid manifest: {manifest.Problem}");

        var agentClock = request.AgentClockUtc.Kind == DateTimeKind.Local
            ? request.AgentClockUtc.ToUniversalTime()
            : DateTime.SpecifyKind(request.AgentClockUtc, DateTimeKind.Utc);
        var offsetSeconds = (long)Math.Round((now - agentClock).TotalSeconds);
        var clockOffset = (int)Math.Clamp(offsetSeconds, int.MinValue, int.MaxValue);
        var limit = await ReadDriftLimitAsync(ct);
        if (Math.Abs(offsetSeconds) > limit)
            quarantine.Add($"agent clock differs from the server by {clockOffset} s (limit {limit} s)");

        var status = quarantine.Count > 0 ? InstrumentIngestStatus.Quarantined : InstrumentIngestStatus.PendingReview;
        var reasons = quarantine.Concat(notes).ToList();
        var reason = reasons.Count == 0 ? null : string.Join("; ", reasons);

        var extension = Path.GetExtension(request.ReportFileName);
        if (!ExtensionPattern.IsMatch(extension)) extension = string.Empty;

        // The version number is the next free one; a clash (another receipt
        // for this injection committed first) recomputes it once.
        for (var attempt = 0; ; attempt++)
        {
            var versionNo = (await _db.InstrumentIngestVersions
                .Where(v => v.InstrumentIngestId == ingest.Id)
                .Select(v => (int?)v.VersionNo).MaxAsync(ct) ?? 0) + 1;

            // The key is deterministic, so a lost race rewrites identical
            // bytes; it is stored before the row because the row's key is
            // immutable once inserted.
            var key = await _storage.SaveAsync($"instrument-ingest/{ingest.Id}/v{versionNo}/{sha256}{extension}", content);

            var version = new InstrumentIngestVersion
            {
                InstrumentIngestId = ingest.Id,
                VersionNo = versionNo,
                ReportSha256 = sha256,
                ReportStorageKey = key,
                ReportFileName = request.ReportFileName,
                Status = status,
                StatusReason = reason,
                ReceivedAt = now,
                AcquiredAtLocalText = request.AcquiredAtLocalText,
                TimeZoneIdUsed = agent.TimeZoneId,
                ClockOffsetSeconds = clockOffset,
                OperatorAsReported = request.OperatorAsReported
            };
            var payload = new InstrumentIngestPayload
            {
                ParserVersion = UnparsedParserVersion,
                ManifestJson = manifest.StoredJson,
                ParsedJson = "{}",
                CreatedAt = now
            };
            version.Payloads.Add(payload);
            _db.InstrumentIngestVersions.Add(version);

            if (await _db.TrySaveChangesAsync(UniqueIndexNames.InstrumentIngestVersionHash, UniqueIndexNames.InstrumentIngestVersionNo))
            {
                var superseded = await SupersedePreviousAsync(ingest.Id, version.Id, ct);
                await RecordAuditAsync(agent, ingest, version, manifest.StoredJson, superseded, ct);
                return Receipt(version, alreadyReceived: false);
            }

            _db.Entry(payload).State = EntityState.Detached;
            _db.Entry(version).State = EntityState.Detached;

            var winner = await FindByHashAsync(ingest.Id, sha256, ct);
            if (winner is not null) return Receipt(winner, alreadyReceived: true);
            if (attempt >= 1)
                throw new InvalidOperationException(
                    "Another report for this injection was received at the same moment. Nothing was saved - send the report again.");
        }
    }

    private async Task<InstrumentIngest> FindOrCreateIngestAsync(
        InstrumentAgentContext agent, IngestRequest request, DateTime acquiredAtUtc, CancellationToken ct)
    {
        // Same equipment, sequence, injection and instant is the same
        // injection, whichever agent reported it first.
        Task<InstrumentIngest?> Find() => _db.InstrumentIngests.FirstOrDefaultAsync(i =>
            i.EquipmentId == agent.EquipmentId && i.SequenceName == request.SequenceName
            && i.InjectionNumber == request.InjectionNumber && i.AcquiredAtUtc == acquiredAtUtc, ct);

        var ingest = await Find();
        if (ingest is not null) return ingest;

        ingest = new InstrumentIngest
        {
            InstrumentAgentId = agent.AgentId,
            EquipmentId = agent.EquipmentId,
            SequenceName = request.SequenceName,
            InjectionNumber = request.InjectionNumber,
            AcquiredAtUtc = acquiredAtUtc
        };
        _db.InstrumentIngests.Add(ingest);
        if (await _db.TrySaveChangesAsync(UniqueIndexNames.InstrumentIngestIdentity)) return ingest;

        // Another receipt created it first: use theirs.
        _db.Entry(ingest).State = EntityState.Detached;
        return await Find()
            ?? throw new InvalidOperationException("The injection could not be recorded; send the report again.");
    }

    private Task<InstrumentIngestVersion?> FindByHashAsync(int ingestId, string sha256, CancellationToken ct) =>
        _db.InstrumentIngestVersions.FirstOrDefaultAsync(v => v.InstrumentIngestId == ingestId && v.ReportSha256 == sha256, ct);

    // Reports nobody has adopted are replaced by the newer one. An adopted
    // version is left alone: re-adoption needs a human reason.
    private async Task<List<(InstrumentIngestVersion Version, InstrumentIngestStatus Was)>> SupersedePreviousAsync(int ingestId, int newVersionId, CancellationToken ct)
    {
        var previous = await _db.InstrumentIngestVersions
            .Where(v => v.InstrumentIngestId == ingestId && v.Id != newVersionId
                        && (v.Status == InstrumentIngestStatus.PendingReview || v.Status == InstrumentIngestStatus.Quarantined))
            .OrderBy(v => v.VersionNo)
            .ToListAsync(ct);
        var was = previous.Select(v => (v, v.Status)).ToList();
        if (previous.Count == 0) return was;

        foreach (var v in previous) { v.Status = InstrumentIngestStatus.Superseded; v.StatusReason = SupersededReason; }
        await _db.SaveChangesAsync(ct);
        return was;
    }

    private async Task RecordAuditAsync(
        InstrumentAgentContext agent, InstrumentIngest ingest, InstrumentIngestVersion version, string manifestJson,
        List<(InstrumentIngestVersion Version, InstrumentIngestStatus Was)> superseded, CancellationToken ct)
    {
        var process = $"InstrumentAgent:{agent.Code}";
        var context = $"{agent.Code} / equipment {agent.EquipmentId} / ingest {ingest.Id}";

        await _audit.RecordSystemEventAsync(process, "INGEST_RECEIVED", AuditActionCategory.ImportExport,
            nameof(InstrumentIngestVersion), sourceContext: context, entityId: version.Id.ToString(),
            changes: new[]
            {
                new AuditFieldChange("ReportSha256", null, version.ReportSha256),
                new AuditFieldChange("ReportStorageKey", null, version.ReportStorageKey),
                new AuditFieldChange("ManifestSha256", null, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson))).ToLowerInvariant()),
                new AuditFieldChange("Status", null, version.Status.ToString()),
                new AuditFieldChange("StatusReason", null, version.StatusReason),
                new AuditFieldChange("VersionNo", null, version.VersionNo.ToString(CultureInfo.InvariantCulture))
            },
            cancellationToken: ct);

        foreach (var (old, wasStatus) in superseded)
        {
            await _audit.RecordSystemEventAsync(process, "INGEST_SUPERSEDED", AuditActionCategory.ImportExport,
                nameof(InstrumentIngestVersion), reason: SupersededReason, sourceContext: context, entityId: old.Id.ToString(),
                changes: new[] { new AuditFieldChange("Status", wasStatus.ToString(), InstrumentIngestStatus.Superseded.ToString()) },
                cancellationToken: ct);
        }
    }

    private async Task<int> ReadDriftLimitAsync(CancellationToken ct)
    {
        var text = await _db.ConfigurationSettings.AsNoTracking()
            .Where(s => s.SettingKey == MaxClockDriftSettingKey)
            .Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) && limit >= 0
            ? limit
            : DefaultMaxClockDriftSeconds;
    }

    private static IngestReceipt Receipt(InstrumentIngestVersion v, bool alreadyReceived) =>
        new(v.InstrumentIngestId, v.Id, v.VersionNo, v.Status, alreadyReceived);

    private sealed record Acquisition(DateTime Utc, bool Gap, bool Fold);

    // The instrument PC writes local wall-clock time. A time that never
    // happened (spring-forward gap) is kept with the zone's standard offset
    // and quarantined; a time that happened twice (fall-back fold) takes the
    // earlier instant and is flagged in the reason.
    private static Acquisition ConvertAcquisitionTime(InstrumentAgentContext agent, string? localText)
    {
        if (!DateTime.TryParseExact(localText, LocalTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            throw new BusinessRuleException($"The acquisition time must be written as {LocalTimeFormat} (instrument local time).");
        // Extreme years would overflow the offset arithmetic below and are never real.
        if (local.Year is < MinAcquisitionYear or > MaxAcquisitionYear)
            throw new BusinessRuleException($"The acquisition year must be between {MinAcquisitionYear} and {MaxAcquisitionYear}.");

        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(agent.TimeZoneId ?? string.Empty); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            throw new BusinessRuleException($"The agent's time zone '{agent.TimeZoneId}' is not known to the server.");
        }

        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
            return new Acquisition(DateTime.SpecifyKind(local - zone.BaseUtcOffset, DateTimeKind.Utc), Gap: true, Fold: false);
        if (zone.IsAmbiguousTime(local))
            return new Acquisition(DateTime.SpecifyKind(local - zone.GetAmbiguousTimeOffsets(local).Max(), DateTimeKind.Utc), Gap: false, Fold: true);
        return new Acquisition(DateTime.SpecifyKind(local - zone.GetUtcOffset(local), DateTimeKind.Utc), Gap: false, Fold: false);
    }

    private sealed record Manifest(string StoredJson, string? Problem);

    // [{ "path": non-empty, "sha256": 64 lowercase hex, "bytes": integer >= 0 }].
    // Whatever the agent sent is kept (as JSON, or as a JSON string when it
    // is not JSON); only the verdict differs.
    private static Manifest ReadManifest(string? text)
    {
        text ??= string.Empty;
        var wrapped = JsonSerializer.Serialize(ReplaceUnstorable(text));
        try
        {
            // jsonb cannot hold U+0000 or a lone surrogate, raw or as a \u
            // escape; such text is stored as a plain JSON string instead.
            if (HasUnstorableCharacters(text))
                return new Manifest(wrapped, "manifest contains a NUL character or an unpaired surrogate");

            using var doc = JsonDocument.Parse(text);
            var problem = Validate(doc.RootElement);
            return new Manifest(text, problem);
        }
        catch (JsonException)
        {
            return new Manifest(wrapped, "manifest is not valid JSON");
        }
    }

    // jsonb rejects U+0000 and unpaired surrogates in the DECODED strings, so
    // the text is read like the JSON parser reads it: inside a string token a
    // backslash consumes the next character and \uXXXX becomes one UTF-16
    // unit. Each string token is followed by a newline so a pair cannot span
    // two tokens; raw characters are checked as they stand.
    private static bool HasUnstorableCharacters(string text)
    {
        // Raw U+0000 / lone surrogates anywhere. A raw half never pairs with
        // an escaped half (the parser would see an invalid UTF-16 string).
        if (ReplaceUnstorable(text) != text) return true;

        var decoded = new StringBuilder(text.Length);
        var inString = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (!inString) { decoded.Append(c); inString = c == '"'; continue; }
            if (c == '"') { decoded.Append('\n'); inString = false; }
            else if (c != (char)92 || i + 1 >= text.Length) decoded.Append(c); // 92 is the backslash
            else if (text[++i] == 'u' && i + 4 < text.Length
                     && int.TryParse(text.AsSpan(i + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var unit))
            {
                decoded.Append((char)unit);
                i += 4;
            }
            else decoded.Append(text[i]); // any other escaped character stands for itself here
        }

        var s = decoded.ToString();
        return ReplaceUnstorable(s) != s;
    }

    private static string ReplaceUnstorable(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                sb.Append(text[i]).Append(text[i + 1]);
                i++;
            }
            else sb.Append(text[i] == '\0' || char.IsSurrogate(text[i]) ? '�' : text[i]);
        }
        return sb.ToString();
    }

    private static string? Validate(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) return "manifest is not a JSON array";
        if (root.GetArrayLength() == 0) return "manifest is empty";
        var i = 0;
        foreach (var e in root.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object) return $"entry {i} is not an object";
            if (!e.TryGetProperty("path", out var path) || path.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(path.GetString()))
                return $"entry {i} has no path";
            if (!e.TryGetProperty("sha256", out var sha) || sha.ValueKind != JsonValueKind.String || !Sha256Pattern.IsMatch(sha.GetString()!))
                return $"entry {i} has no valid sha256";
            if (!e.TryGetProperty("bytes", out var bytes) || bytes.ValueKind != JsonValueKind.Number || !bytes.TryGetInt64(out var n) || n < 0)
                return $"entry {i} has no valid bytes";
            i++;
        }
        return null;
    }
}

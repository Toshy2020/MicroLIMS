using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Identity of one injection reported by an instrument agent. The identity
// columns never change (DB trigger); successive reports for the same
// injection (reintegration) are InstrumentIngestVersion rows.
public class InstrumentIngest
{
    public int Id { get; set; }
    public int InstrumentAgentId { get; set; }
    public InstrumentAgent? InstrumentAgent { get; set; }
    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    public string SequenceName { get; set; } = string.Empty;
    public int InjectionNumber { get; set; }
    public DateTime AcquiredAtUtc { get; set; }

    public List<InstrumentIngestVersion> Versions { get; set; } = new();
}

// One received report. Evidence columns (hash, storage key, instrument
// timestamp text, receipt) are immutable by DB trigger; only the review
// outcome (Status, StatusReason, ResolvedBy*, ResolvedAt) may change.
public class InstrumentIngestVersion
{
    public int Id { get; set; }
    public int InstrumentIngestId { get; set; }
    public InstrumentIngest? InstrumentIngest { get; set; }
    public int VersionNo { get; set; }
    public string ReportSha256 { get; set; } = string.Empty; // lowercase hex
    public string ReportStorageKey { get; set; } = string.Empty;
    public string ReportFileName { get; set; } = string.Empty;
    public InstrumentIngestStatus Status { get; set; }
    public string? StatusReason { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string AcquiredAtLocalText { get; set; } = string.Empty;
    public string TimeZoneIdUsed { get; set; } = string.Empty;
    public int ClockOffsetSeconds { get; set; }
    public string? OperatorAsReported { get; set; }
    public int? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public List<InstrumentIngestPayload> Payloads { get; set; } = new();
}

// Parsed report data and file manifest (jsonb). Append-only by DB trigger.
public class InstrumentIngestPayload
{
    public int Id { get; set; }
    public int InstrumentIngestVersionId { get; set; }
    public InstrumentIngestVersion? InstrumentIngestVersion { get; set; }
    public string ParserVersion { get; set; } = string.Empty;
    public string ManifestJson { get; set; } = "{}";
    public string ParsedJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

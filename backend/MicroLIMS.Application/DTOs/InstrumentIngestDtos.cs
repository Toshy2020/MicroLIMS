using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

// Who is sending: built by the controller from the authenticated agent's
// claims, never from the request body.
public record InstrumentAgentContext(int AgentId, string Code, int EquipmentId, int SectionId, string TimeZoneId);

// One report file plus the agent's manifest of the files it read.
// AcquiredAtLocalText is the instrument PC's local time, "yyyy-MM-dd HH:mm:ss".
public record IngestRequest(
    string SequenceName,
    int InjectionNumber,
    string AcquiredAtLocalText,
    string ReportSha256,
    string ReportFileName,
    byte[] ReportContent,
    string ManifestJson,
    DateTime AgentClockUtc,
    string? OperatorAsReported);

public record IngestReceipt(int IngestId, int VersionId, int VersionNo, InstrumentIngestStatus Status, bool AlreadyReceived);

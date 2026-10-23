namespace MicroLIMS.Application.DTOs;

// Never carries the key or its hash; the plain key is returned once, beside
// this DTO, by register/rotate only.
public record InstrumentAgentDto(
    int Id,
    string Code,
    string Name,
    int EquipmentId,
    int SectionId,
    string TimeZoneId,
    string KeyPrefix,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? RevokedAt,
    string? RevokeReason,
    DateTime? LastSeenAt);

public record RegisterInstrumentAgentRequest(int EquipmentId, string Name, string TimeZoneId);

public record RevokeInstrumentAgentRequest(string Reason);

public record InstrumentAgentKeyResponse(InstrumentAgentDto Agent, string PlainKey);

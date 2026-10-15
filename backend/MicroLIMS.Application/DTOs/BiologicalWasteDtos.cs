namespace MicroLIMS.Application.DTOs;

public record OtherWasteItemRequest(string Description, decimal Quantity, string Unit);

public record RecordBiologicalWasteDestructionRequest(
    int AutoclaveEquipmentId,
    int AutoclaveProgramId,
    string CycleNumber,
    DateTime CycleStartedAt,
    List<int>? ThawEventIds,
    List<int>? WithdrawnCryovialIds,
    List<OtherWasteItemRequest>? OtherItems,
    string? Notes,
    string Password);

// A thawed vial that may go into a destruction cycle: more than 24 hours
// since thawing, or its batch withdrawn.
public record EligibleThawedVialDto(
    int ThawEventId,
    int CryovialId,
    string CryovialCode,
    string OrganismName,
    DateTime ThawedAt,
    string ThawedByName,
    string EligibleBecause,
    List<string> UsedInMediaLots);

// The never-thawed vials left in a withdrawn (destroyed) cryovial batch.
public record EligibleWithdrawnBatchDto(
    int CryovialId,
    string CryovialCode,
    string OrganismName,
    int VialsRemaining);

public record BiologicalWasteEligibleDto(
    List<EligibleThawedVialDto> ThawedVials,
    List<EligibleWithdrawnBatchDto> WithdrawnBatches);

public record BiologicalWasteItemDto(
    int Id,
    string ItemType,
    string Description,
    decimal Quantity,
    string Unit,
    int? ThawEventId,
    int? CryovialId);

public record BiologicalWasteDestructionDto(
    int Id,
    string Reference,
    int SectionId,
    string AutoclaveName,
    string AutoclaveCode,
    string ProgramCode,
    string ProgramName,
    string LoadType,
    decimal Temperature,
    int CycleTimeMinutes,
    string CycleNumber,
    DateTime CycleStartedAt,
    string? Notes,
    string RecordedByName,
    DateTime RecordedAt,
    string SignedByName,
    string SignedByRole,
    DateTime SignedAt,
    int CryovialItemCount,
    int OtherItemCount,
    List<BiologicalWasteItemDto> Items);

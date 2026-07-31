using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One prepared lot of a MediaType - the Media Preparation module record.
// Not usable in routine testing until it passes GPT (GptStage.Release).
public class Media
{
    public int Id { get; set; }
    public int MediaTypeId { get; set; }
    public MediaType? MediaType { get; set; }

    public string LotNumber { get; set; } = string.Empty; // auto: {TypeCode}/{seq}/{yy}, resets yearly
    public string ManufacturerLot { get; set; } = string.Empty;
    public string ManufacturerName { get; set; } = string.Empty;
    public decimal TotalWeight { get; set; }
    public string TotalVolume { get; set; } = string.Empty;
    public int? AutoclaveEquipmentId { get; set; }
    public Equipment? AutoclaveEquipment { get; set; }
    public string AutoclaveProgram { get; set; } = string.Empty;
    public string LoadType { get; set; } = string.Empty; // e.g. "liquid (100 ml)" / "agar (500 ml)"
    public decimal Temperature { get; set; }
    public int CycleTime { get; set; }
    public int CycleNumber { get; set; }
    public decimal Ph { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime PreparedAt { get; set; } = DateTime.UtcNow;

    public MediaStatus Status { get; set; } = MediaStatus.Prepared;
    public GptStage GptStage { get; set; } = GptStage.Preparation;
    public bool IsReleasedForUse => GptStage == GptStage.Release && Status == MediaStatus.Active;

    public List<GptChallengeResult> GptResults { get; set; } = new();
}

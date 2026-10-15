using System.Text.Json.Serialization;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One autoclave cycle that destroyed biological waste: thawed cryovial
// vials, the vials left in withdrawn cryovial batches, and anything else
// loaded into the same cycle. Autoclaving is the only destruction method.
// The program's parameters are copied at recording time so the record
// stays exact if the program master is edited later. Signed by the
// person recording it (SignatureMeaning.BiologicalWasteDestroyed).
public class BiologicalWasteDestruction
{
    public int Id { get; set; }
    public int SectionId { get; set; } // the autoclave's section

    public int AutoclaveEquipmentId { get; set; }
    public Equipment? AutoclaveEquipment { get; set; }
    public int AutoclaveProgramId { get; set; }
    public AutoclaveProgram? AutoclaveProgram { get; set; }
    public string ProgramCodeSnapshot { get; set; } = string.Empty;
    public string ProgramNameSnapshot { get; set; } = string.Empty;
    public string LoadTypeSnapshot { get; set; } = string.Empty;
    public decimal TemperatureSnapshot { get; set; }
    public int CycleTimeMinutesSnapshot { get; set; }
    public string CycleNumber { get; set; } = string.Empty;
    public DateTime CycleStartedAt { get; set; }
    public string? Notes { get; set; }

    public int RecordedByUserId { get; set; }
    public DateTime RecordedAt { get; set; }
    public int SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }

    public List<BiologicalWasteDestructionItem> Items { get; set; } = new();
}

public class BiologicalWasteDestructionItem
{
    public int Id { get; set; }
    public int BiologicalWasteDestructionId { get; set; }
    [JsonIgnore]
    public BiologicalWasteDestruction? BiologicalWasteDestruction { get; set; }

    public BiologicalWasteItemType ItemType { get; set; }

    // ThawedVial only - unique: a vial is destroyed once.
    public int? ThawEventId { get; set; }
    [JsonIgnore]
    public ThawEvent? ThawEvent { get; set; }

    // UnthawedCryovials only - unique: a withdrawn batch's remaining
    // vials are destroyed once (VialsRemaining drops to 0).
    public int? CryovialId { get; set; }
    [JsonIgnore]
    public Cryovial? Cryovial { get; set; }

    // A readable snapshot of what was destroyed, e.g. "ECO/01/26 - vial
    // thawed 15-Oct-2026 08:00" or free text for Other.
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}

namespace MicroLIMS.Domain.Enums;

// What one line of a biological waste destruction record covers.
// Persisted as int - append only.
public enum BiologicalWasteItemType
{
    ThawedVial,        // one thawed cryovial vial (ThawEventId)
    UnthawedCryovials, // the vials left in a withdrawn cryovial batch (CryovialId)
    Other              // anything else destroyed in the same autoclave cycle
}

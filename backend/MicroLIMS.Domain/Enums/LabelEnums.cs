namespace MicroLIMS.Domain.Enums;

// Persisted as int: append, never insert or renumber.
public enum LabelEntityType
{
    Sample = 1,
    MediaLot = 2,
    SolutionPreparation = 3,
    Cryovial = 4,
    MaterialStockLot = 5,
    EquipmentAsset = 6
}

public enum LabelSymbology
{
    Code128 = 1,
    DataMatrix = 2
}

public enum LabelLayout
{
    Standard = 1,
    Small = 2
}

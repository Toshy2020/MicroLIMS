namespace MicroLIMS.Domain.Enums;

public enum MaterialMasterCategory
{
    Reagent,
    Indicator,
    ReferenceStandard,
    PrimaryStandard,
    // "Other Materials": the entry points at a MaterialCategory (appended, stored as int).
    Other
}

namespace MicroLIMS.Domain.Enums;

// HPLC chain S2 - Solution master (spec 3.2).
public enum SolutionType
{
    MobilePhase,
    Diluent,
    Titrant,
    // Appended, never inserted: persisted as int. Microbiology general types
    // (with Diluent); MobilePhase and Titrant keep their HPLC rules.
    Reagent,
    Neutralizer
}

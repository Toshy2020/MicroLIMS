namespace MicroLIMS.Domain.Enums;

public static class SolutionTypeExtensions
{
    // Diluent, Reagent and Neutralizer: no HPLC method, no titrant fields; may
    // carry the sterilization / release flags and prepared-solution components.
    public static bool IsGeneral(this SolutionType type) =>
        type is SolutionType.Diluent or SolutionType.Reagent or SolutionType.Neutralizer;
}

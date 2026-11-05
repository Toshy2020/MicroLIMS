namespace MicroLIMS.Domain.Enums;

// What a recipe line of a Solution master refers to. Persisted as int.
public enum SolutionComponentKind
{
    Material,           // a Material master entry, picked as a stock lot
    PreparedSolution,   // a prepared bottle of another solution recipe
    Text                // free text such as "Purified water to 1000 mL"; no lot, no stock
}

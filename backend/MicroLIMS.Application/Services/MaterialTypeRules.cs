using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// Which built-in material types each laboratory's stock register offers.
// Supplements, discs, kits, ID reagents, buffers and disposable tools are no
// longer received under their own type: they are "Other materials", received
// as MaterialType.Other against a master entry of a MaterialCategory. The six
// legacy values stay in the enum (stored as int) and on lots already received.
// A section with an unrecognised code keeps every offered type.
public static class MaterialTypeRules
{
    public const int CustomTypeMaxLength = 100;

    // The types that became categories of Other materials; kept for lots received before.
    public static readonly IReadOnlySet<MaterialType> LegacyNamedTypes = new HashSet<MaterialType>
    {
        MaterialType.Supplement,
        MaterialType.AntibioticDisc,
        MaterialType.IdentificationKit,
        MaterialType.IdentificationReagent,
        MaterialType.ReferenceBuffer,
        MaterialType.DisposableTool
    };

    public static bool IsLegacyNamedType(MaterialType type) => LegacyNamedTypes.Contains(type);

    // Other, or one of the legacy named types: lots whose rules come from a material category.
    public static bool IsOtherMaterial(MaterialType type) => type == MaterialType.Other || IsLegacyNamedType(type);

    private static readonly MaterialType[] Microbiology =
    {
        MaterialType.DehydratedMedia,
        MaterialType.LyophilizedMicroorganism,
        MaterialType.Chemical,
        MaterialType.Indicator,
        MaterialType.Other
    };

    private static readonly MaterialType[] Physicochemical =
    {
        MaterialType.Chemical,
        MaterialType.ReferenceStandard,
        MaterialType.PrimaryStandard,
        MaterialType.Indicator,
        MaterialType.Other
    };

    public static IReadOnlyList<MaterialType> BuiltInTypesFor(string? sectionCode) => sectionCode switch
    {
        "MICRO" => Microbiology,
        "FP" => Physicochemical,
        _ => Enum.GetValues<MaterialType>().Where(t => t != MaterialType.WorkingStandard && !IsLegacyNamedType(t)).ToArray()
    };

    // Display label of a built-in type ("ReferenceStandard" -> "Reference Standard").
    public static string LabelOf(MaterialType type) =>
        System.Text.RegularExpressions.Regex.Replace(type.ToString(), "(?<=[a-z])(?=[A-Z])", " ");

    // Trims and collapses inner whitespace; null for blank.
    public static string? NormalizeCustomType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

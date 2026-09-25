using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// Which built-in material types each laboratory's stock register offers.
// Microbiology keeps its original list; the Physicochemical lab gets the
// general types only. Anything else a lab stocks is recorded as Other
// with a CustomType name the lab types in (see MaterialService).
// A section with an unrecognised code keeps every type.
public static class MaterialTypeRules
{
    public const int CustomTypeMaxLength = 100;

    private static readonly MaterialType[] Microbiology =
    {
        MaterialType.DehydratedMedia,
        MaterialType.LyophilizedMicroorganism,
        MaterialType.Supplement,
        MaterialType.AntibioticDisc,
        MaterialType.IdentificationKit,
        MaterialType.IdentificationReagent,
        MaterialType.Chemical,
        MaterialType.Indicator,
        MaterialType.ReferenceBuffer,
        MaterialType.DisposableTool,
        MaterialType.Other
    };

    private static readonly MaterialType[] Physicochemical =
    {
        MaterialType.Chemical,
        MaterialType.ReferenceStandard,
        MaterialType.Indicator,
        MaterialType.ReferenceBuffer,
        MaterialType.DisposableTool,
        MaterialType.Other
    };

    public static IReadOnlyList<MaterialType> BuiltInTypesFor(string? sectionCode) => sectionCode switch
    {
        "MICRO" => Microbiology,
        "FP" => Physicochemical,
        _ => Enum.GetValues<MaterialType>()
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

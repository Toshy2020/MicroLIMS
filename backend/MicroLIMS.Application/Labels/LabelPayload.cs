using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Labels;

public static class LabelPayload
{
    private static readonly IReadOnlyDictionary<string, LabelEntityType> PrefixToType =
        Enum.GetValues<LabelEntityType>()
            .ToDictionary(PrefixOf, static type => type, StringComparer.OrdinalIgnoreCase);

    public static string PrefixOf(LabelEntityType type) => type switch
    {
        LabelEntityType.Sample => "S",
        LabelEntityType.MediaLot => "MED",
        LabelEntityType.SolutionPreparation => "SP",
        LabelEntityType.Cryovial => "CRY",
        LabelEntityType.MaterialStockLot => "MAT",
        LabelEntityType.EquipmentAsset => "EQ",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported label entity type.")
    };

    public static LabelSymbology SymbologyOf(LabelEntityType type) => type switch
    {
        LabelEntityType.Sample or LabelEntityType.EquipmentAsset => LabelSymbology.Code128,
        LabelEntityType.MediaLot
            or LabelEntityType.SolutionPreparation
            or LabelEntityType.Cryovial
            or LabelEntityType.MaterialStockLot => LabelSymbology.DataMatrix,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported label entity type.")
    };

    public static string Build(LabelEntityType type, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return $"{PrefixOf(type)}:{code}";
    }

    public static bool TryParse(string? value, out LabelEntityType type, out string code)
    {
        type = default;
        code = string.Empty;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var colonIndex = value.IndexOf(':');
        if (colonIndex <= 0)
        {
            return false;
        }

        var prefix = value[..colonIndex];
        if (!PrefixToType.TryGetValue(prefix, out var matchedType))
        {
            return false;
        }

        var remainder = value[(colonIndex + 1)..];
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return false;
        }

        type = matchedType;
        code = remainder;
        return true;
    }
}

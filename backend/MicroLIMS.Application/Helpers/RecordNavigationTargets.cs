using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

/// <summary>
/// Canonical frontend route builders for searchable and scannable laboratory records.
/// </summary>
public static class RecordNavigationTargets
{
    public static string Sample(int id) => $"/receiving-testing?sampleId={id}";

    public static string TestOrder(int sampleId, int testOrderId) =>
        $"/receiving-testing?sampleId={sampleId}&testOrderId={testOrderId}";

    public static string Material() => "/inventory/materials";

    public static string EquipmentAsset() => "/inventory/equipment";

    public static string MediaLot() => "/laboratory-configuration/media";

    public static string Document(int id) => $"/document-control/documents/{id}";

    public static string SolutionPreparation(int id) => $"/preparation/{id}";

    public static string Item() => "/laboratory-configuration/items";

    public static string Cryovial(int id) => $"/cryovials/{id}/report";

    public static string ForLabelEntity(LabelEntityType type, int id) => type switch
    {
        LabelEntityType.Sample => Sample(id),
        LabelEntityType.MediaLot => MediaLot(),
        LabelEntityType.SolutionPreparation => SolutionPreparation(id),
        LabelEntityType.Cryovial => Cryovial(id),
        LabelEntityType.MaterialStockLot => Material(),
        LabelEntityType.EquipmentAsset => EquipmentAsset(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported label entity type.")
    };
}

using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public enum WorkspaceArea { Fp, RmPm }

public static class PhyschemAreas
{
    public const string SectionCode = "FP";

    public static WorkspaceArea OfCategory(SampleCategory c) =>
        c is SampleCategory.RawMaterial or SampleCategory.PackagingMaterial ? WorkspaceArea.RmPm : WorkspaceArea.Fp;

    public static bool Includes(PhyschemArea? testArea, WorkspaceArea area) => testArea switch
    {
        PhyschemArea.Both or null => true,
        PhyschemArea.FinishedProduct => area == WorkspaceArea.Fp,
        PhyschemArea.RawPackaging => area == WorkspaceArea.RmPm,
        _ => false,
    };

    public static IReadOnlyList<SampleCategory> CategoriesOf(WorkspaceArea a) => a == WorkspaceArea.RmPm
        ? new[] { SampleCategory.RawMaterial, SampleCategory.PackagingMaterial }
        : Enum.GetValues<SampleCategory>().Where(c => c is not (SampleCategory.RawMaterial or SampleCategory.PackagingMaterial)).ToArray();

    public static WorkspaceArea? Parse(string? s) => s?.ToLowerInvariant() switch
    {
        "fp" => WorkspaceArea.Fp,
        "rmpm" => WorkspaceArea.RmPm,
        null or "" => null,
        _ => throw new InvalidOperationException("area must be 'fp' or 'rmpm'."),
    };

    // Membership area (null = Both) -> workspace areas it grants.
    public static IReadOnlyList<WorkspaceArea> Grants(PhyschemArea? m) => m switch
    {
        PhyschemArea.FinishedProduct => new[] { WorkspaceArea.Fp },
        PhyschemArea.RawPackaging => new[] { WorkspaceArea.RmPm },
        _ => new[] { WorkspaceArea.Fp, WorkspaceArea.RmPm },
    };
}

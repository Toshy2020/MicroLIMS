namespace MicroLIMS.Application.DTOs.Responses;

// The group a catalog row belongs to (the five tabs of the Master Testing Materials page).
public static class TestingMaterialGroups
{
    public const string ReagentsIndicators = "ReagentsIndicators";
    public const string Standards = "Standards";
    public const string CultureMedia = "CultureMedia";
    public const string ReferenceOrganisms = "ReferenceOrganisms";
    public const string OtherMaterials = "OtherMaterials";

    public static readonly string[] All = { ReagentsIndicators, Standards, CultureMedia, ReferenceOrganisms, OtherMaterials };
}

// What kind of record a catalog row is, i.e. which screen/API edits it.
public static class TestingMaterialSourceTypes
{
    public const string MaterialMasterEntry = "MaterialMasterEntry";
    public const string MediaProduct = "MediaProduct";
    public const string ReferenceOrganism = "ReferenceOrganism";
    public const string WorkingStandard = "WorkingStandard";
}

// One row of the unified Master Testing Materials list. Id is the id in the source table
// named by SourceType. Working standards are read-only: they exist only by approving a
// qualification (Id is the stock lot's id).
public record TestingMaterialCatalogRow(
    string Group, int Id, string SourceType, string Code, string Name, string? SubCategory,
    string? OwnerSectionCode, bool IsActive, bool HasStock, bool CanEdit, bool ReadOnly);

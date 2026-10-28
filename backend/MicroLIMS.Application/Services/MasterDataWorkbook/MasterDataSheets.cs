using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Application.Services.MasterDataWorkbook.Sheets;

namespace MicroLIMS.Application.Services.MasterDataWorkbook;

// The workbook's sheets in load order: a sheet may refer only to sheets
// above it (or to records already in the database).
public static class MasterDataSheets
{
    public static IReadOnlyList<SheetHandler> Create(
        OrganismMasterDataService organisms,
        ReferenceListMasterDataService referenceLists,
        EnvironmentalMonitoringMasterDataService em,
        WaterMasterDataService water,
        MaterialMasterService materialMaster,
        ReferenceOrganismService referenceOrganisms,
        MediaProductService mediaProducts,
        EquipmentMasterDataService equipment,
        ItemService items,
        SpecificationMasterDataService specifications) => new SheetHandler[]
    {
        new OrganismsSheet(organisms),
        new ProductionStagesSheet(referenceLists),
        new EmDepartmentsSheet(em),
        new EmRoomsSheet(em),
        new WaterDepartmentsSheet(water),
        new WaterPointsSheet(water),
        new MaterialMasterSheet(materialMaster),
        new ReferenceOrganismsSheet(referenceOrganisms),
        new MediaProductsSheet(mediaProducts),
        new EquipmentSheet(equipment),
        new ItemsSheet(items),
        new SpecificationsSheet(specifications),
        new SpecificationStagesSheet(),
    };

    public static readonly IReadOnlyList<string> Instructions = new[]
    {
        "MicroLIMS master data workbook",
        "1. Each sheet is one kind of record. Row 1 is the header - do not rename or reorder the headers.",
        "2. Rows already in MicroLIMS are listed. Add new rows underneath; leave existing rows as they are.",
        "   The workbook only ADDS records. To change an existing record, use its screen in MicroLIMS.",
        "3. Use the drop-down lists where a column has one. Lists are separated by ; (for example TAMC;TYMC).",
        "4. Numbers use a dot as the decimal separator (0.5). Dates are written yyyy-MM-dd (2027-03-31).",
        "5. Load order: sheets 1-11 first. Then create test definitions and HPLC/GC/ICP methods in MicroLIMS.",
        "   Then fill Specifications and SpecificationStages and load the workbook again.",
        "6. In MicroLIMS: Master Data Workbook -> Check file. Fix every error listed, then Load and sign.",
        "7. Items, material master entries and reference organisms are loaded INACTIVE. A second person checks the load against",
        "   the approved source documents and signs Verify; that activates them.",
        "8. Required columns are marked Yes in the table below.",
    };
}

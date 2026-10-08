using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Enums;
using static MicroLIMS.Application.Services.MasterDataWorkbook.WorkbookValues;

namespace MicroLIMS.Application.Services.MasterDataWorkbook.Sheets;

internal static class SectionLookup
{
    // Resolves the code and runs the create-time section rules (active,
    // user is a member) up front, so a refusal points at SectionCode.
    public static async Task<int> IdAsync(ImportContext ctx, string code)
    {
        var id = await ctx.Db.DocumentSections.Where(s => s.Code.ToLower() == code.ToLower()).Select(s => (int?)s.Id).FirstOrDefaultAsync()
            ?? throw new RowError("SectionCode", $"No laboratory section with code '{code}'.");
        try
        {
            return await new UserSectionScopeService(ctx.Db).ResolveSectionForCreateAsync(ctx.UserId, id);
        }
        catch (InvalidOperationException e)
        {
            throw new RowError("SectionCode", e.Message);
        }
    }

    public static async Task<IReadOnlyList<string>> CodesAsync(IMicroLimsDbContext db) =>
        await db.DocumentSections.Where(s => s.IsActive).OrderBy(s => s.Code).Select(s => s.Code).ToListAsync();
}

public sealed class MaterialMasterSheet : SheetHandler
{
    private readonly MaterialMasterService _service;
    public MaterialMasterSheet(MaterialMasterService service) { _service = service; }

    public override string Name => "MaterialMaster";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("SectionCode", Required: true, Help: "Code of the lab section that owns the entry."),
        new SheetColumn("Code", Required: true, Help: "Unique within the section, e.g. RG-011."),
        new SheetColumn("Name", Required: true),
        new SheetColumn("Category", Required: true, EnumType: typeof(MaterialMasterCategory)),
        new SheetColumn("Grade"),
        new SheetColumn("Source"),
        new SheetColumn("BaseUnit", Required: true, EnumType: typeof(MaterialUnit)),
        new SheetColumn("WorkingConcentration", Help: "Indicators only."),
        new SheetColumn("Solvent", Help: "Indicators only."),
        new SheetColumn("TransitionRangeFrom", Help: "Indicators only, pH."),
        new SheetColumn("TransitionRangeTo", Help: "Indicators only, pH."),
        new SheetColumn("ColourChange", Help: "Indicators only."),
        new SheetColumn("IndicatorUse", Help: "Indicators only."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "SectionCode"), Cell(c, "Code"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db, IReadOnlyCollection<int>? sectionScope) =>
        (await db.MaterialMasterEntries.AsNoTracking().Include(e => e.Section)
            .Where(e => sectionScope == null || sectionScope.Contains(e.SectionId))
            .OrderBy(e => e.Code).ToListAsync())
        .Select(e => new Dictionary<string, string>
        {
            ["SectionCode"] = e.Section?.Code ?? string.Empty, ["Code"] = e.Code, ["Name"] = e.Name,
            ["Category"] = e.Category.ToString(), ["Grade"] = Fmt(e.Grade), ["Source"] = Fmt(e.Source),
            ["BaseUnit"] = e.BaseUnit.ToString(), ["WorkingConcentration"] = Fmt(e.WorkingConcentration),
            ["Solvent"] = Fmt(e.Solvent), ["TransitionRangeFrom"] = Fmt(e.TransitionRangeFrom),
            ["TransitionRangeTo"] = Fmt(e.TransitionRangeTo), ["ColourChange"] = Fmt(e.ColourChange),
            ["IndicatorUse"] = Fmt(e.IndicatorUse),
        }).ToList();

    public override async Task<IReadOnlyList<string>> DbChoicesAsync(IMicroLimsDbContext db, string column) =>
        column == "SectionCode" ? await SectionLookup.CodesAsync(db) : Array.Empty<string>();

    // Loaded inactive: the second-person verification of the import
    // activates it (MasterDataWorkbookService.VerifyAsync).
    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var sectionId = await SectionLookup.IdAsync(ctx, r.Text("SectionCode"));
        var created = await _service.CreateAsync(new SaveMaterialMasterEntryRequest(
            r.Text("Code"), r.Text("Name"), r.Enum<MaterialMasterCategory>("Category"),
            r.OptionalText("Grade"), r.OptionalText("Source"), r.Enum<MaterialUnit>("BaseUnit"), sectionId,
            r.OptionalText("WorkingConcentration"), r.OptionalText("Solvent"),
            r.Decimal("TransitionRangeFrom"), r.Decimal("TransitionRangeTo"),
            r.OptionalText("ColourChange"), r.OptionalText("IndicatorUse")), ctx.UserId);
        await _service.SetActiveAsync(created.Id, false, ctx.UserId);
        return Created("MaterialMasterEntry", created.Id, r);
    }
}

public sealed class MediaProductsSheet : SheetHandler
{
    private readonly MediaProductService _service;
    public MediaProductsSheet(MediaProductService service) { _service = service; }

    public override string Name => "MediaProducts";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Code", Required: true, Help: "Short unique code used in lot numbers, e.g. TSA."),
        new SheetColumn("Name", Required: true, Help: "e.g. Tryptic Soy Agar."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Code"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db, IReadOnlyCollection<int>? sectionScope) =>
        (await db.MediaProducts.AsNoTracking().OrderBy(p => p.Code).ToListAsync())
        .Select(p => new Dictionary<string, string> { ["Code"] = p.Code, ["Name"] = p.Name }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var created = await _service.CreateAsync(r.Text("Name"), r.Text("Code"));
        return Created("MediaProduct", created.Id, r);
    }
}

public sealed class EquipmentSheet : SheetHandler
{
    private readonly EquipmentMasterDataService _service;
    public EquipmentSheet(EquipmentMasterDataService service) { _service = service; }

    public override string Name => "Equipment";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Code", Required: true, Help: "Asset or equipment ID, e.g. INC-03."),
        new SheetColumn("Name", Required: true),
        new SheetColumn("Type", Required: true, EnumType: typeof(EquipmentType)),
        new SheetColumn("SectionCode", Help: "Lab section that uses it."),
        new SheetColumn("Location"),
        new SheetColumn("SetPointTemperature", Help: "Incubators: °C, dot decimal."),
        new SheetColumn("CalibrationDueDate", Help: "yyyy-MM-dd."),
        new SheetColumn("Vendor"),
        new SheetColumn("CdsSoftware", EnumType: typeof(CdsSoftware), Help: "Chromatography/ICP instruments only."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Code"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db, IReadOnlyCollection<int>? sectionScope) =>
        (await db.Equipment.AsNoTracking().Include(e => e.Section).OrderBy(e => e.Code).ToListAsync())
        .Select(e => new Dictionary<string, string>
        {
            ["Code"] = e.Code, ["Name"] = e.Name, ["Type"] = e.Type.ToString(),
            ["SectionCode"] = e.Section?.Code ?? string.Empty, ["Location"] = Fmt(e.Location),
            ["SetPointTemperature"] = Fmt(e.SetPointTemperature), ["CalibrationDueDate"] = Fmt(e.CalibrationDueDate),
            ["Vendor"] = Fmt(e.Vendor), ["CdsSoftware"] = Fmt(e.CdsSoftware),
        }).ToList();

    public override async Task<IReadOnlyList<string>> DbChoicesAsync(IMicroLimsDbContext db, string column) =>
        column == "SectionCode" ? await SectionLookup.CodesAsync(db) : Array.Empty<string>();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        int? sectionId = r.OptionalText("SectionCode") is { } code ? await SectionLookup.IdAsync(ctx, code) : null;
        var created = await _service.CreateEquipmentAsync(ctx.UserId, new CreateEquipmentRequest(
            r.Text("Name"), r.Text("Code"), r.Enum<EquipmentType>("Type"), r.OptionalText("Location"),
            r.Decimal("SetPointTemperature"), r.Date("CalibrationDueDate"), r.OptionalText("Vendor"),
            r.OptionalEnum<CdsSoftware>("CdsSoftware"), null, sectionId));
        return Created("Equipment", created.Id, r);
    }
}

public sealed class ItemsSheet : SheetHandler
{
    private readonly ItemService _service;
    public ItemsSheet(ItemService service) { _service = service; }

    public override string Name => "Items";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Code", Required: true, Help: "Product or material code, unique."),
        new SheetColumn("Name", Required: true),
        new SheetColumn("Category", Required: true, EnumType: typeof(SampleCategory), Help: "FinishedProduct, RawMaterial or PackagingMaterial."),
        new SheetColumn("SopNumber"),
        new SheetColumn("TestCodes", IsList: true, Help: "Tests assigned on receipt, from Test Master, separated by ;."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Code"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db, IReadOnlyCollection<int>? sectionScope) =>
        (await db.Items.AsNoTracking().Include(i => i.AssignedTests).OrderBy(i => i.Code).ToListAsync())
        .Select(i => new Dictionary<string, string>
        {
            ["Code"] = i.Code, ["Name"] = i.Name, ["Category"] = i.Category.ToString(),
            ["SopNumber"] = Fmt(i.SopNumber), ["TestCodes"] = FmtList(i.AssignedTests.Select(t => t.TestCode)),
        }).ToList();

    // Loaded inactive: an inactive item cannot receive samples
    // (ProductWorkflowEngine.ReceiveAsync), so nothing is tested against
    // it until a second person verifies the import.
    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var codes = r.List("TestCodes");
        var lowered = codes.Select(c => c.ToLower()).ToList();
        var tests = await ctx.Db.TestDefinitions.Where(t => lowered.Contains(t.Code.ToLower())).Select(t => new { t.Code, t.DisplayName }).ToListAsync();
        if (codes.FirstOrDefault(c => tests.All(t => !t.Code.Equals(c, StringComparison.OrdinalIgnoreCase))) is { } missing)
            throw new RowError("TestCodes", $"No test definition with code '{missing}'. Create it in Test Master first.");

        // Stored with Test Master's spelling of each code.
        var created = await _service.CreateAsync(new ItemSaveRequest(
            r.Text("Name"), r.Text("Code"), r.Enum<SampleCategory>("Category"), r.OptionalText("SopNumber"),
            codes.Select(c => tests.First(t => t.Code.Equals(c, StringComparison.OrdinalIgnoreCase)))
                .Select(t => new ItemTestRequest(t.Code, t.DisplayName)).ToList()));
        await _service.SetActiveAsync(created.Id, false);
        return Created("Item", created.Id, r);
    }
}

using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Enums;
using static MicroLIMS.Application.Services.MasterDataWorkbook.WorkbookValues;

namespace MicroLIMS.Application.Services.MasterDataWorkbook.Sheets;

public sealed class SpecificationsSheet : SheetHandler
{
    public const string SheetName = "Specifications";
    private readonly SpecificationMasterDataService _service;
    public SpecificationsSheet(SpecificationMasterDataService service) { _service = service; }

    public override string Name => SheetName;
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("ItemCode", Required: true, SourceSheet: "Items", SourceColumn: "Code"),
        new SheetColumn("TestCode", Required: true, Help: "Code from Test Master. The test must exist before this sheet is loaded."),
        new SheetColumn("ParameterName", Required: true, Help: "What is reported, e.g. TAMC or Assay of Paracetamol. Unique per item, test and stage."),
        new SheetColumn("ProductionStageRole", EnumType: typeof(ProductionStageRole), Help: "Blank = applies to every stage."),
        new SheetColumn("DisplayOrder"),
        new SheetColumn("LimitType", Required: true, EnumType: typeof(LimitType)),
        new SheetColumn("LowerLimit"), new SheetColumn("UpperLimit"),
        new SheetColumn("LowerInclusive", Help: "Yes/No, default Yes."), new SheetColumn("UpperInclusive", Help: "Yes/No, default Yes."),
        new SheetColumn("Target"), new SheetColumn("Tolerance"),
        new SheetColumn("ToleranceMode", EnumType: typeof(ToleranceMode)),
        new SheetColumn("AlertLimit", Help: "Count-tiered tests, as text, e.g. 50."),
        new SheetColumn("ActionLimit"), new SheetColumn("SpecLimit"),
        new SheetColumn("Unit", Help: "e.g. CFU/g, %."),
        new SheetColumn("DilutionFactor"),
        new SheetColumn("ExpectedResultText", Help: "Qualitative tests: the text that passes."),
        new SheetColumn("ExpectedState", EnumType: typeof(ExpectedPresence), Help: "Presence/absence tests."),
        new SheetColumn("ReferenceStandard", Help: "e.g. USP <61>."),
        new SheetColumn("SampleQuantity"), new SheetColumn("SampleQuantityUnit"),
        new SheetColumn("ResultBasis", EnumType: typeof(ResultBasis)),
        new SheetColumn("LabelClaim"), new SheetColumn("LabelClaimUnit"),
        new SheetColumn("DosageForm", EnumType: typeof(DosageForm)),
        new SheetColumn("HplcAnalyte", Help: "Method abbreviation / analyte name, e.g. PCM / Paracetamol. The method must exist."),
        new SheetColumn("IcpElement", Help: "Method abbreviation / element symbol, e.g. ICP1 / Zn. The method must exist."),
    };

    public static string SpecKey(string? item, string? test, string? parameter, string? role) => Key(item, test, parameter, role);

    public override string KeyOf(IReadOnlyDictionary<string, string> c) =>
        SpecKey(Cell(c, "ItemCode"), Cell(c, "TestCode"), Cell(c, "ParameterName"), Cell(c, "ProductionStageRole"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.Specifications.AsNoTracking()
            .Include(s => s.Item)
            .Include(s => s.HplcMethodAnalyte).ThenInclude(a => a!.HplcMethod)
            .Include(s => s.IcpMethodElement).ThenInclude(e => e!.IcpMethod)
            .OrderBy(s => s.Item!.Code).ThenBy(s => s.TestCode).ThenBy(s => s.DisplayOrder)
            .ToListAsync())
        .Select(s => new Dictionary<string, string>
        {
            ["ItemCode"] = s.Item?.Code ?? string.Empty, ["TestCode"] = s.TestCode, ["ParameterName"] = s.ParameterName,
            ["ProductionStageRole"] = Fmt(s.ProductionStageRole), ["DisplayOrder"] = Fmt(s.DisplayOrder),
            ["LimitType"] = s.LimitType.ToString(), ["LowerLimit"] = Fmt(s.LowerLimit), ["UpperLimit"] = Fmt(s.UpperLimit),
            ["LowerInclusive"] = Fmt(s.LowerInclusive), ["UpperInclusive"] = Fmt(s.UpperInclusive),
            ["Target"] = Fmt(s.Target), ["Tolerance"] = Fmt(s.Tolerance), ["ToleranceMode"] = Fmt(s.ToleranceMode),
            ["AlertLimit"] = s.AlertLimit, ["ActionLimit"] = s.ActionLimit, ["SpecLimit"] = s.SpecLimit, ["Unit"] = s.Unit,
            ["DilutionFactor"] = Fmt(s.DilutionFactor), ["ExpectedResultText"] = Fmt(s.ExpectedResultText),
            ["ExpectedState"] = Fmt(s.ExpectedState), ["ReferenceStandard"] = Fmt(s.ReferenceStandard),
            ["SampleQuantity"] = Fmt(s.SampleQuantity), ["SampleQuantityUnit"] = Fmt(s.SampleQuantityUnit),
            ["ResultBasis"] = Fmt(s.ResultBasis), ["LabelClaim"] = Fmt(s.LabelClaim), ["LabelClaimUnit"] = Fmt(s.LabelClaimUnit),
            ["DosageForm"] = Fmt(s.DosageForm),
            ["HplcAnalyte"] = s.HplcMethodAnalyte is { } a ? $"{a.HplcMethod?.Abbreviation} / {a.Name}" : string.Empty,
            ["IcpElement"] = s.IcpMethodElement is { } e ? $"{e.IcpMethod?.Abbreviation} / {e.Symbol}" : string.Empty,
        }).ToList();

    public override async Task<IReadOnlyList<string>> DbChoicesAsync(IMicroLimsDbContext db, string column) => column switch
    {
        "TestCode" => await db.TestDefinitions.Where(t => t.IsActive).OrderBy(t => t.Code).Select(t => t.Code).ToListAsync(),
        _ => Array.Empty<string>(),
    };

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var itemCode = r.Text("ItemCode");
        var itemId = await ctx.Db.Items.Where(i => i.Code == itemCode).Select(i => (int?)i.Id).FirstOrDefaultAsync()
            ?? throw new RowError("ItemCode", $"No item with code '{itemCode}'. Add it on the Items sheet.");
        var testCode = r.Text("TestCode");
        if (!await ctx.Db.TestDefinitions.AnyAsync(t => t.Code == testCode))
            throw new RowError("TestCode", $"No test definition with code '{testCode}'. Create it in Test Master first.");
        var parameter = r.Text("ParameterName");
        var role = r.OptionalEnum<ProductionStageRole>("ProductionStageRole");
        var key = SpecKey(itemCode, testCode, parameter, role?.ToString());

        var created = await _service.CreateSpecificationAsync(ctx.UserId, new CreateSpecificationRequest(
            ItemId: itemId, TestCode: testCode,
            AlertLimit: r.OptionalText("AlertLimit"), ActionLimit: r.OptionalText("ActionLimit"), SpecLimit: r.OptionalText("SpecLimit"),
            Unit: r.OptionalText("Unit"), DilutionFactor: r.Decimal("DilutionFactor"), ParameterName: parameter,
            DisplayOrder: r.Int("DisplayOrder"), LimitType: r.Enum<LimitType>("LimitType"),
            ReferenceStandard: r.OptionalText("ReferenceStandard"), LowerLimit: r.Decimal("LowerLimit"), UpperLimit: r.Decimal("UpperLimit"),
            LowerInclusive: r.Bool("LowerInclusive"), UpperInclusive: r.Bool("UpperInclusive"),
            Target: r.Decimal("Target"), Tolerance: r.Decimal("Tolerance"), ToleranceMode: r.OptionalEnum<ToleranceMode>("ToleranceMode"),
            ExpectedResultText: r.OptionalText("ExpectedResultText"), ExpectedState: r.OptionalEnum<ExpectedPresence>("ExpectedState"),
            SampleQuantity: r.Decimal("SampleQuantity"), SampleQuantityUnit: r.OptionalText("SampleQuantityUnit"),
            Stages: StagesFor(ctx, key),
            ResultBasis: r.OptionalEnum<ResultBasis>("ResultBasis"), LabelClaim: r.Decimal("LabelClaim"),
            LabelClaimUnit: r.OptionalText("LabelClaimUnit"), DosageForm: r.OptionalEnum<DosageForm>("DosageForm"),
            HplcMethodAnalyteId: await AnalyteIdAsync(ctx.Db, r.OptionalText("HplcAnalyte")),
            ProductionStageRole: role,
            IcpMethodElementId: await ElementIdAsync(ctx.Db, r.OptionalText("IcpElement"))));

        ctx.CreatedSpecifications[key] = created.Id;
        return Created("Specification", created.Id, r);
    }

    // Stage rows travel with their specification: the service validates a
    // multi-stage specification together with its stages.
    private static List<SpecificationStageDto>? StagesFor(ImportContext ctx, string specKey)
    {
        var sheet = ctx.Sheet(SpecificationStagesSheet.SheetName);
        if (sheet is null) return null;
        var stages = sheet.Rows
            .Where(row => SpecificationStagesSheet.ParentKey(row.Cells) == specKey)
            .Select(row =>
            {
                var s = new RowReader(row);
                try
                {
                    return new SpecificationStageDto(null, s.RequiredInt("StageNumber"), s.Text("StageLabel"), s.Text("AcceptanceCriteria"));
                }
                catch (RowError e)
                {
                    throw new RowError(e.Column, $"{SpecificationStagesSheet.SheetName} row {row.RowNumber}: {e.Message}");
                }
            })
            .ToList();
        return stages.Count == 0 ? null : stages;
    }

    private static async Task<int?> AnalyteIdAsync(IMicroLimsDbContext db, string? text)
    {
        if (text is null) return null;
        var (abbr, name) = Split(text, "HplcAnalyte");
        return await db.HplcMethodAnalytes.Where(a => a.HplcMethod!.Abbreviation == abbr && a.Name == name).Select(a => (int?)a.Id).FirstOrDefaultAsync()
            ?? throw new RowError("HplcAnalyte", $"No HPLC method '{abbr}' with analyte '{name}'.");
    }

    private static async Task<int?> ElementIdAsync(IMicroLimsDbContext db, string? text)
    {
        if (text is null) return null;
        var (abbr, symbol) = Split(text, "IcpElement");
        return await db.IcpMethodElements.Where(e => e.IcpMethod!.Abbreviation == abbr && e.Symbol == symbol).Select(e => (int?)e.Id).FirstOrDefaultAsync()
            ?? throw new RowError("IcpElement", $"No ICP method '{abbr}' with element '{symbol}'.");
    }

    private static (string, string) Split(string text, string column)
    {
        var parts = text.Split('/', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0
            ? (parts[0], parts[1])
            : throw new RowError(column, $"{column} must be written as 'METHOD / name'.");
    }
}

public sealed class SpecificationStagesSheet : SheetHandler
{
    public const string SheetName = "SpecificationStages";

    public override string Name => SheetName;
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("ItemCode", Required: true), new SheetColumn("TestCode", Required: true),
        new SheetColumn("ParameterName", Required: true),
        new SheetColumn("ProductionStageRole", EnumType: typeof(ProductionStageRole)),
        new SheetColumn("StageNumber", Required: true, Help: "1, 2, 3 ..."),
        new SheetColumn("StageLabel", Required: true, Help: "e.g. S1."),
        new SheetColumn("AcceptanceCriteria", Required: true),
    };

    public static string ParentKey(IReadOnlyDictionary<string, string> c) =>
        SpecificationsSheet.SpecKey(Cell(c, "ItemCode"), Cell(c, "TestCode"), Cell(c, "ParameterName"), Cell(c, "ProductionStageRole"));

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => ParentKey(c) + "|" + Key(Cell(c, "StageNumber"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.SpecificationStages.AsNoTracking().Include(s => s.Specification).ThenInclude(s => s!.Item)
            .OrderBy(s => s.Specification!.Item!.Code).ThenBy(s => s.Specification!.TestCode).ThenBy(s => s.StageNumber)
            .ToListAsync())
        .Select(s => new Dictionary<string, string>
        {
            ["ItemCode"] = s.Specification?.Item?.Code ?? string.Empty, ["TestCode"] = s.Specification?.TestCode ?? string.Empty,
            ["ParameterName"] = s.Specification?.ParameterName ?? string.Empty,
            ["ProductionStageRole"] = Fmt(s.Specification?.ProductionStageRole),
            ["StageNumber"] = Fmt(s.StageNumber), ["StageLabel"] = s.StageLabel, ["AcceptanceCriteria"] = s.AcceptanceCriteriaText,
        }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var parent = ParentKey(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ItemCode"] = r.Text("ItemCode"), ["TestCode"] = r.Text("TestCode"), ["ParameterName"] = r.Text("ParameterName"),
            ["ProductionStageRole"] = r.OptionalText("ProductionStageRole") ?? string.Empty,
        });
        if (!ctx.CreatedSpecifications.TryGetValue(parent, out var specId))
            throw new RowError(null, "Stages load only together with a new row on the Specifications sheet. To change the stages of an existing specification, use the Specifications screen.");

        var stageNumber = r.RequiredInt("StageNumber");
        var id = await ctx.Db.SpecificationStages
            .Where(s => s.SpecificationId == specId && s.StageNumber == stageNumber)
            .Select(s => (int?)s.Id).FirstOrDefaultAsync()
            ?? throw new RowError("StageNumber", $"Stage {stageNumber} was not created with its specification.");
        return Created("SpecificationStage", id, r);
    }
}

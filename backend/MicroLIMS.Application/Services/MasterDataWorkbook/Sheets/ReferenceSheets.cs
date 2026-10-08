using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Enums;
using static MicroLIMS.Application.Services.MasterDataWorkbook.WorkbookValues;

namespace MicroLIMS.Application.Services.MasterDataWorkbook.Sheets;

public sealed class OrganismsSheet : SheetHandler
{
    private readonly OrganismMasterDataService _service;
    public OrganismsSheet(OrganismMasterDataService service) { _service = service; }

    public override string Name => "Organisms";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("ScientificName", Required: true, Help: "Full scientific name, e.g. Escherichia coli. Must be unique."),
        new SheetColumn("AtccNumber", Help: "ATCC number without the prefix, e.g. 8739. Must be unique when given."),
        new SheetColumn("CommonName"),
        new SheetColumn("Description"),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "ScientificName"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.Organisms.AsNoTracking().OrderBy(o => o.ScientificName).ToListAsync())
        .Select(o => new Dictionary<string, string>
        {
            ["ScientificName"] = o.ScientificName, ["AtccNumber"] = Fmt(o.AtccNumber),
            ["CommonName"] = Fmt(o.CommonName), ["Description"] = Fmt(o.Description),
        }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var created = await _service.CreateOrganismAsync(new CreateOrganismRequest(
            r.Text("ScientificName"), r.OptionalText("AtccNumber"), r.OptionalText("CommonName"), r.OptionalText("Description")));
        return Created("Organism", created.Id, r);
    }
}

public sealed class ProductionStagesSheet : SheetHandler
{
    private readonly ReferenceListMasterDataService _service;
    public ProductionStagesSheet(ReferenceListMasterDataService service) { _service = service; }

    public override string Name => "ProductionStages";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Name", Required: true, Help: "Stage name shown on the receiving form, e.g. Bulk."),
        new SheetColumn("Role", Required: true, EnumType: typeof(ProductionStageRole), Help: "What the stage means to the system: Bulk, InProcess, Finished, Stability or Other."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Name"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.ProductionStages.AsNoTracking().OrderBy(s => s.Name).ToListAsync())
        .Select(s => new Dictionary<string, string> { ["Name"] = s.Name, ["Role"] = s.Role.ToString() }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var created = await _service.CreateProductionStageAsync(new CreateProductionStageRequest(r.Text("Name"), r.Enum<ProductionStageRole>("Role")));
        return Created("ProductionStage", created.Id, r);
    }
}

public sealed class EmDepartmentsSheet : SheetHandler
{
    private readonly EnvironmentalMonitoringMasterDataService _service;
    public EmDepartmentsSheet(EnvironmentalMonitoringMasterDataService service) { _service = service; }

    public override string Name => "EmDepartments";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Name", Required: true, Help: "Production department monitored by EM, e.g. Sterile Filling."),
        new SheetColumn("Class", Help: "Cleanroom classification, e.g. Grade B."),
        new SheetColumn("TestingFrequency", Help: "Information only, e.g. Weekly."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Name"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync())
        .Select(d => new Dictionary<string, string> { ["Name"] = d.Name, ["Class"] = d.Class, ["TestingFrequency"] = d.TestingFrequency }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var created = await _service.CreateDepartmentAsync(new CreateDepartmentRequest(
            r.Text("Name"), r.OptionalText("Class") ?? string.Empty, r.OptionalText("TestingFrequency") ?? string.Empty));
        return Created("Department", created.Id, r);
    }
}

public sealed class EmRoomsSheet : SheetHandler
{
    private readonly EnvironmentalMonitoringMasterDataService _service;
    public EmRoomsSheet(EnvironmentalMonitoringMasterDataService service) { _service = service; }

    public override string Name => "EmRooms";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Department", Required: true, SourceSheet: "EmDepartments", SourceColumn: "Name", Help: "Name of a department on the EmDepartments sheet."),
        new SheetColumn("Name", Required: true, Help: "Room name, unique within its department."),
        new SheetColumn("GradeClassification", Help: "e.g. Grade C."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Department"), Cell(c, "Name"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.Rooms.AsNoTracking().Include(r => r.Department).OrderBy(r => r.Department!.Name).ThenBy(r => r.Name).ToListAsync())
        .Select(r => new Dictionary<string, string>
        {
            ["Department"] = r.Department?.Name ?? string.Empty, ["Name"] = r.Name, ["GradeClassification"] = r.GradeClassification,
        }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var deptName = r.Text("Department");
        var deptId = await ctx.Db.Departments.Where(d => d.Name.ToLower() == deptName.ToLower()).Select(d => (int?)d.Id).FirstOrDefaultAsync()
            ?? throw new RowError("Department", $"No EM department named '{deptName}'. Add it on the EmDepartments sheet.");
        var created = await _service.CreateRoomAsync(new CreateRoomRequest(r.Text("Name"), deptId, r.OptionalText("GradeClassification") ?? string.Empty));
        return Created("Room", created.Id, r);
    }
}

public sealed class WaterDepartmentsSheet : SheetHandler
{
    private readonly WaterMasterDataService _service;
    public WaterDepartmentsSheet(WaterMasterDataService service) { _service = service; }

    public override string Name => "WaterDepartments";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Name", Required: true, Help: "Water system or area, e.g. Purified Water Loop 1."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Name"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.WaterDepartments.AsNoTracking().OrderBy(d => d.Name).ToListAsync())
        .Select(d => new Dictionary<string, string> { ["Name"] = d.Name }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var created = await _service.CreateWaterDepartmentAsync(new CreateWaterDepartmentRequest(r.Text("Name")));
        return Created("WaterDepartment", created.Id, r);
    }
}

public sealed class WaterPointsSheet : SheetHandler
{
    private readonly WaterMasterDataService _service;
    public WaterPointsSheet(WaterMasterDataService service) { _service = service; }

    public override string Name => "WaterPoints";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Code", Required: true, Help: "Sampling point code, unique, e.g. PW-01."),
        new SheetColumn("Location", Required: true),
        new SheetColumn("TestingFrequency", Help: "e.g. Daily."),
        new SheetColumn("TestCodes", IsList: true, Help: "Test codes from Test Master, separated by ; e.g. TAMC;PSEUDO."),
        new SheetColumn("WaterDepartment", SourceSheet: "WaterDepartments", SourceColumn: "Name", Help: "Name on the WaterDepartments sheet."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Code"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.WaterSamplingPoints.AsNoTracking().Include(p => p.WaterDepartment).OrderBy(p => p.Code).ToListAsync())
        .Select(p => new Dictionary<string, string>
        {
            ["Code"] = p.Code, ["Location"] = p.Location, ["TestingFrequency"] = p.TestingFrequency,
            ["TestCodes"] = FmtList(p.AssignedTestCodes), ["WaterDepartment"] = p.WaterDepartment?.Name ?? string.Empty,
        }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        int? deptId = null;
        if (r.OptionalText("WaterDepartment") is { } deptName)
            deptId = await ctx.Db.WaterDepartments.Where(d => d.Name.ToLower() == deptName.ToLower()).Select(d => (int?)d.Id).FirstOrDefaultAsync()
                ?? throw new RowError("WaterDepartment", $"No water department named '{deptName}'.");

        var codes = r.List("TestCodes");
        var known = await ctx.Db.TestDefinitions.Where(t => codes.Contains(t.Code)).Select(t => t.Code).ToListAsync();
        if (codes.Except(known).FirstOrDefault() is { } missing)
            throw new RowError("TestCodes", $"No test definition with code '{missing}'.");

        var created = await _service.CreateWaterSamplingPointAsync(new CreateWaterSamplingPointRequest(
            r.Text("Code"), r.Text("Location"), r.OptionalText("TestingFrequency") ?? string.Empty, codes.ToList(), deptId));
        return Created("WaterSamplingPoint", created.Id, r);
    }
}

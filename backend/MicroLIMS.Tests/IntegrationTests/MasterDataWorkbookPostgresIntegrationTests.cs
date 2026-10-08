using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Application.Services.MasterDataWorkbook;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Workbook;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MasterDataWorkbookPostgresIntegrationTests
{
    private const string Password = "Workbook-Signer-1!";
    private readonly PostgresTestFixture _fixture;

    public MasterDataWorkbookPostgresIntegrationTests(PostgresTestFixture fixture) { _fixture = fixture; }

    internal MasterDataWorkbookService Service(MicroLimsDbContext db, InMemoryFileStorageService? storage = null)
    {
        var scope = new UserSectionScopeService(db);
        var signatures = new ElectronicSignatureService(db);
        var audit = new AuditEventService(db, _fixture.CreateSequenceHelper(db));
        return new MasterDataWorkbookService(
            db, new ClosedXmlWorkbookFormat(), storage ?? new InMemoryFileStorageService(), signatures, TimeProvider.System,
            new OrganismMasterDataService(db), new ReferenceListMasterDataService(db),
            new EnvironmentalMonitoringMasterDataService(db), new WaterMasterDataService(db),
            new MaterialMasterService(db, scope), new MediaProductService(db, signatures, audit),
            new EquipmentMasterDataService(db, scope), new ItemService(db),
            new SpecificationMasterDataService(db, scope));
    }

    [PostgresFact]
    public async Task Download_HasAllSheetsInLoadOrder_AndCurrentRows()
    {
        await using var db = _fixture.CreateDbContext();
        var tag = Guid.NewGuid().ToString("N")[..8];
        db.Organisms.Add(new Organism { ScientificName = "Dl organism " + tag });
        await db.SaveChangesAsync();

        var (content, fileName) = await Service(db).DownloadAsync();

        Assert.EndsWith(".xlsx", fileName);
        using var wb = new XLWorkbook(new MemoryStream(content));
        Assert.Equal(
            new[] { "Instructions", "Organisms", "ProductionStages", "EmDepartments", "EmRooms", "WaterDepartments", "WaterPoints",
                    "MaterialMaster", "MediaProducts", "Equipment", "Items", "Specifications", "SpecificationStages", "Lists" },
            wb.Worksheets.Select(w => w.Name));
        Assert.Contains(wb.Worksheet("Organisms").Column(1).CellsUsed(), c => c.GetString() == "Dl organism " + tag);
    }

    // Builds an .xlsx with the given sheets (header from the first row's keys).
    internal static byte[] Workbook(params (string Sheet, Dictionary<string, string>[] Rows)[] sheets)
    {
        var content = new WorkbookContent(
            sheets.Select(s => new WorkbookSheet(s.Sheet,
                s.Rows.SelectMany(r => r.Keys).Distinct().ToList(),
                s.Rows.Select((r, i) => new WorkbookRow(i + 2, r)).ToList())).ToList(),
            Array.Empty<WorkbookColumnChoices>(), Array.Empty<string>(), Array.Empty<WorkbookInstructionRow>());
        return new ClosedXmlWorkbookFormat().Write(content);
    }

    internal static Dictionary<string, string> R(params (string Col, string Val)[] cells) => cells.ToDictionary(c => c.Col, c => c.Val);

    private async Task<User> AdminAsync(MicroLimsDbContext db)
    {
        var role = await db.Roles.FirstAsync(r => r.Type == RoleType.SystemAdministrator);
        var user = new User
        {
            FullName = "Workbook Admin", Username = ("mdw_" + Guid.NewGuid().ToString("N"))[..15],
            PasswordHash = TestPasswords.Hash(Password), RoleId = role.Id, IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [PostgresFact]
    public async Task Validate_ReportsCreatedCounts_AndLeavesNoRowsOrAudit()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var auditBefore = await db.AuditLogs.CountAsync();

        var report = await Service(db).ValidateAsync(Workbook(
            ("Organisms", new[] { R(("ScientificName", "Org " + tag)) }),
            ("EmDepartments", new[] { R(("Name", "Dept " + tag), ("Class", "Grade B")) }),
            ("EmRooms", new[] { R(("Department", "Dept " + tag), ("Name", "Room " + tag)) })), admin.Id);

        Assert.False(report.HasErrors, string.Join("\n", report.Errors.Select(e => $"{e.Sheet} {e.Row} {e.Column}: {e.Message}")));
        Assert.Equal(1, report.Sheets.Single(s => s.Sheet == "EmRooms").Created);
        Assert.False(await db.Organisms.AnyAsync(o => o.ScientificName == "Org " + tag));
        Assert.False(await db.Departments.AnyAsync(d => d.Name == "Dept " + tag));
        Assert.Equal(auditBefore, await db.AuditLogs.CountAsync());
    }

    [PostgresFact]
    public async Task OneBadRowDoesNotStopTheRest()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        // An ATCC number already in use: the unique index (not a pre-check)
        // rejects it, which poisons the transaction without a savepoint.
        db.Organisms.Add(new Organism { ScientificName = "Existing " + tag, AtccNumber = "A" + tag });
        await db.SaveChangesAsync();

        var report = await Service(db).ValidateAsync(Workbook(("Organisms", new[]
        {
            R(("ScientificName", "Dup atcc " + tag), ("AtccNumber", "A" + tag)),
            R(("ScientificName", "Fine " + tag), ("AtccNumber", "")),
        })), admin.Id);

        var error = Assert.Single(report.Errors);
        Assert.Equal(2, error.Row);
        Assert.Equal(1, report.Sheets.Single().Created);
    }

    [PostgresFact]
    public async Task Validate_UnknownReference_NamesSheetRowAndColumn()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);

        var report = await Service(db).ValidateAsync(Workbook(
            ("EmRooms", new[] { R(("Department", "No such dept"), ("Name", "R1")) })), admin.Id);

        var e = Assert.Single(report.Errors);
        Assert.Equal(("EmRooms", 2, "Department"), (e.Sheet, e.Row, e.Column));
    }

    [PostgresFact]
    public async Task Validate_CommaDecimal_IsAnError()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];

        var report = await Service(db).ValidateAsync(Workbook(("Equipment", new[]
        {
            R(("Code", "INC-" + tag), ("Name", "Incubator"), ("Type", "Incubator"), ("SetPointTemperature", "32,5")),
        })), admin.Id);

        Assert.Equal("SetPointTemperature", Assert.Single(report.Errors).Column);
    }

    [PostgresFact]
    public async Task Validate_DuplicateRowInSheet_IsAnError()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];

        var report = await Service(db).ValidateAsync(Workbook(("MediaProducts", new[]
        {
            R(("Code", "M" + tag), ("Name", "One " + tag)), R(("Code", "m" + tag), ("Name", "Two " + tag)),
        })), admin.Id);

        Assert.Equal(3, Assert.Single(report.Errors).Row);
    }

    [PostgresFact]
    public async Task Validate_UnknownSheet_IsAnError()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var report = await Service(db).ValidateAsync(Workbook(("Sheet1", new[] { R(("A", "x")) })), admin.Id);
        Assert.Contains("Unknown sheet", Assert.Single(report.Errors).Message);
    }
}

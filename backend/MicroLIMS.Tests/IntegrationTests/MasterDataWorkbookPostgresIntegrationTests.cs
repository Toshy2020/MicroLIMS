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

        var (content, fileName) = await Service(db).DownloadAsync((await AdminAsync(db)).Id);

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

    private async Task<User> AdminAsync(MicroLimsDbContext db, RoleType roleType = RoleType.SystemAdministrator)
    {
        var role = await db.Roles.FirstAsync(r => r.Type == roleType);
        var user = new User
        {
            FullName = "Workbook Admin", Username = ("mdw_" + Guid.NewGuid().ToString("N"))[..15],
            PasswordHash = TestPasswords.Hash(Password), RoleId = role.Id, IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        // Writes follow section membership for everyone: the administrator who loads the workbook belongs to the QC
        // department (all its sections). A Section Head built here has no memberships, as the tests below need.
        if (roleType == RoleType.SystemAdministrator)
        {
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = _fixture.SeededDepartmentId, SectionId = null });
            await db.SaveChangesAsync();
        }
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

    private static byte[] ItemWorkbook(string tag) => Workbook(
        ("MediaProducts", new[] { R(("Code", "P" + tag), ("Name", "Product " + tag)) }),
        ("Items", new[] { R(("Code", "IT-" + tag), ("Name", "Item " + tag), ("Category", "RawMaterial"), ("TestCodes", "")) }));

    [PostgresFact]
    public async Task Commit_CreatesRowsSignatureRecordAndFile_ItemsInactive()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var storage = new InMemoryFileStorageService();
        var file = ItemWorkbook(tag);

        var result = await Service(db, storage).ImportAsync(file, "lab.xlsx", admin.Id, Password, "initial load", "127.0.0.1");

        Assert.True(result.Committed);
        var import = await db.MasterDataImports.Include(i => i.Records).SingleAsync(i => i.Id == result.Import!.Id);
        Assert.Equal("Loaded", result.Import!.Status);
        Assert.Equal(2, import.Records.Count);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file)), import.FileSha256);
        Assert.Equal(file, storage.Files[import.StorageKey]);
        Assert.False((await db.Items.SingleAsync(i => i.Code == "IT-" + tag)).IsActive);
        Assert.True(await db.ElectronicSignatures.AnyAsync(s =>
            s.EntityType == "MasterDataImport" && s.EntityId == import.Id && s.MeaningOfSignature == SignatureMeaning.MasterDataChanged));
        Assert.Contains("IT-" + tag, result.Import.ItemsWithoutSpecifications);
    }

    [PostgresFact]
    public async Task Commit_WithErrors_CreatesNothing()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];

        var result = await Service(db).ImportAsync(Workbook(
            ("MediaProducts", new[] { R(("Code", "Q" + tag), ("Name", "Ok " + tag)) }),
            ("EmRooms", new[] { R(("Department", "No dept " + tag), ("Name", "R")) })), "x.xlsx", admin.Id, Password, null, null);

        Assert.False(result.Committed);
        Assert.True(result.Report.HasErrors);
        Assert.False(await db.MediaProducts.AnyAsync(p => p.Code == "Q" + tag));
    }

    [PostgresFact]
    public async Task Commit_WrongPassword_CreatesNothing()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];

        await Assert.ThrowsAsync<MicroLIMS.Application.Interfaces.SignatureVerificationException>(() =>
            Service(db).ImportAsync(ItemWorkbook(tag), "x.xlsx", admin.Id, "wrong", null, null));

        await using var fresh = _fixture.CreateDbContext();
        Assert.False(await fresh.Items.AnyAsync(i => i.Code == "IT-" + tag));
        Assert.False(await fresh.MasterDataImports.AnyAsync(i => i.ImportedByUserId == admin.Id));
    }

    [PostgresFact]
    public async Task ExportThenReimport_IsAllUnchanged()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        await Service(db).ImportAsync(ItemWorkbook(tag), "x.xlsx", admin.Id, Password, null, null);

        var (export, _) = await Service(db).DownloadAsync(admin.Id);
        var report = await Service(db).ValidateAsync(export, admin.Id);

        Assert.False(report.HasErrors, string.Join("\n", report.Errors.Select(e => $"{e.Sheet} {e.Row} {e.Column}: {e.Message}")));
        Assert.All(report.Sheets, s => Assert.Equal(0, s.Created));
    }

    [PostgresFact]
    public async Task Verify_BySameUser_IsRefused()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var result = await Service(db).ImportAsync(ItemWorkbook(Guid.NewGuid().ToString("N")[..8]), "x.xlsx", admin.Id, Password, null, null);

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(db).VerifyAsync(result.Import!.Id, admin.Id, Password, null, null));
        Assert.Contains("cannot verify", e.Message);
    }

    [PostgresFact]
    public async Task Verify_ByAnotherUser_ActivatesItems()
    {
        await using var db = _fixture.CreateDbContext();
        var loader = await AdminAsync(db);
        var verifier = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var result = await Service(db).ImportAsync(ItemWorkbook(tag), "x.xlsx", loader.Id, Password, null, null);

        var verified = await Service(db).VerifyAsync(result.Import!.Id, verifier.Id, Password, "checked against spec sheets", null);

        Assert.Equal("Verified", verified.Status);
        await using var fresh = _fixture.CreateDbContext();
        Assert.True((await fresh.Items.SingleAsync(i => i.Code == "IT-" + tag)).IsActive);
        Assert.True(await fresh.ElectronicSignatures.AnyAsync(s =>
            s.EntityType == "MasterDataImport" && s.EntityId == verified.Id && s.MeaningOfSignature == SignatureMeaning.Reviewed && s.UserId == verifier.Id));
    }

    [PostgresFact]
    public async Task Specifications_WithStages_LoadTogether()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        db.TestDefinitions.Add(new TestDefinition { Code = "DIS-" + tag, DisplayName = "Dissolution " + tag, SectionId = _fixture.SeededSectionId, IsActive = true });
        await db.SaveChangesAsync();

        var result = await Service(db).ImportAsync(Workbook(
            ("Items", new[] { R(("Code", "FP-" + tag), ("Name", "Tablet " + tag), ("Category", "FinishedProduct"), ("TestCodes", "DIS-" + tag)) }),
            ("Specifications", new[] { R(("ItemCode", "FP-" + tag), ("TestCode", "DIS-" + tag), ("ParameterName", "Dissolution"),
                ("LimitType", "MultiStage"), ("Unit", "%")) }),
            ("SpecificationStages", new[]
            {
                R(("ItemCode", "FP-" + tag), ("TestCode", "DIS-" + tag), ("ParameterName", "Dissolution"), ("StageNumber", "1"), ("StageLabel", "S1"), ("AcceptanceCriteria", "Each unit not less than Q+5%")),
                R(("ItemCode", "FP-" + tag), ("TestCode", "DIS-" + tag), ("ParameterName", "Dissolution"), ("StageNumber", "2"), ("StageLabel", "S2"), ("AcceptanceCriteria", "Average not less than Q")),
            })), "x.xlsx", admin.Id, Password, null, null);

        Assert.True(result.Committed, string.Join("\n", result.Report.Errors.Select(e => $"{e.Sheet} {e.Row} {e.Column}: {e.Message}")));
        Assert.Equal(2, result.Report.Sheets.Single(s => s.Sheet == "SpecificationStages").Created);
        Assert.Empty(result.Import!.ItemsWithoutSpecifications);
    }

    [PostgresFact]
    public async Task Validate_SectionTheUserIsNotIn_PointsAtSectionCode()
    {
        await using var db = _fixture.CreateDbContext();
        var head = await AdminAsync(db, RoleType.SectionHead); // no memberships
        var tag = Guid.NewGuid().ToString("N")[..8];

        var report = await Service(db).ValidateAsync(Workbook(("Equipment", new[]
        {
            R(("Code", "INC-" + tag), ("Name", "Incubator"), ("Type", "Incubator"), ("SectionCode", "MICRO")),
        })), head.Id);

        var e = Assert.Single(report.Errors);
        Assert.Equal("SectionCode", e.Column);
        Assert.Contains("not a member", e.Message);
    }

    private static byte[] MicroReagentWorkbook(string tag) => Workbook(("MaterialMaster", new[]
    {
        R(("SectionCode", "MICRO"), ("Code", "RG-" + tag), ("Name", "Reagent " + tag), ("Category", "Reagent"), ("BaseUnit", "Gram")),
    }));

    [PostgresFact]
    public async Task Verify_ByUserOutsideTheEntrySection_IsRefusedBeforeSigning()
    {
        await using var db = _fixture.CreateDbContext();
        var loader = await AdminAsync(db);
        var outsider = await AdminAsync(db, RoleType.SectionHead); // no memberships
        var tag = Guid.NewGuid().ToString("N")[..8];
        var result = await Service(db).ImportAsync(MicroReagentWorkbook(tag), "x.xlsx", loader.Id, Password, null, null);
        Assert.True(result.Committed, string.Join("\n", result.Report.Errors.Select(e => e.Message)));

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(db).VerifyAsync(result.Import!.Id, outsider.Id, Password, null, null));

        Assert.Contains("MICRO", e.Message);
        await using var fresh = _fixture.CreateDbContext();
        Assert.False(await fresh.ElectronicSignatures.AnyAsync(s => s.EntityType == "MasterDataImport" && s.UserId == outsider.Id));
    }

    [PostgresFact]
    public async Task Download_BySectionHead_HasOnlyTheirSectionsMaterialMaster()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var outsider = await AdminAsync(db, RoleType.SectionHead); // no memberships
        var tag = Guid.NewGuid().ToString("N")[..8];
        var load = await Service(db).ImportAsync(MicroReagentWorkbook(tag), "x.xlsx", admin.Id, Password, null, null);
        Assert.True(load.Committed, string.Join(" | ", load.Report.Errors.Select(e => e.Message)));

        bool Lists(byte[] content)
        {
            using var wb = new XLWorkbook(new MemoryStream(content));
            return wb.Worksheet("MaterialMaster").Column(2).CellsUsed().Any(c => c.GetString().Equals("RG-" + tag, StringComparison.OrdinalIgnoreCase));
        }

        Assert.True(Lists((await Service(db).DownloadAsync(admin.Id)).Content));
        Assert.False(Lists((await Service(db).DownloadAsync(outsider.Id)).Content));
    }

    [PostgresFact]
    public async Task Validate_RowLimit_CountsOnlyNewRows()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        db.Organisms.Add(new Organism { ScientificName = "Many " + tag });
        await db.SaveChangesAsync();

        // More rows than the limit, all naming one existing organism.
        var rows = Enumerable.Range(0, MasterDataWorkbookRunner.MaxDataRows + 1)
            .Select(_ => R(("ScientificName", "Many " + tag))).ToArray();
        var report = await Service(db).ValidateAsync(Workbook(("Organisms", rows)), admin.Id);

        Assert.DoesNotContain(report.Errors, e => e.Message.Contains("the limit is"));
        Assert.Equal(1, report.Sheets.Single().Unchanged);
    }

    [PostgresFact]
    public async Task Validate_CodesInAnotherCase_StillMatch()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        db.TestDefinitions.Add(new TestDefinition { Code = "AS-" + tag, DisplayName = "Assay " + tag, SectionId = _fixture.SeededSectionId, IsActive = true });
        await db.SaveChangesAsync();

        var report = await Service(db).ValidateAsync(Workbook(
            ("Items", new[] { R(("Code", "IT-" + tag), ("Name", "Item " + tag), ("Category", "RawMaterial"), ("TestCodes", "as-" + tag.ToLowerInvariant())) }),
            ("Specifications", new[] { R(("ItemCode", "it-" + tag.ToLowerInvariant()), ("TestCode", "as-" + tag.ToLowerInvariant()),
                ("ParameterName", "Assay"), ("LimitType", "Range"), ("LowerLimit", "95"), ("UpperLimit", "105")) })), admin.Id);

        Assert.False(report.HasErrors, string.Join("\n", report.Errors.Select(e => $"{e.Sheet} {e.Row} {e.Column}: {e.Message}")));
    }

    [PostgresFact]
    public async Task Import_TooLongComment_IsRefusedWithAMessage()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(db).ImportAsync(ItemWorkbook(tag), "x.xlsx", admin.Id, Password, new string('c', 2001), null));

        Assert.Contains("comment", e.Message);
        Assert.False(await db.Items.AnyAsync(i => i.Code == "IT-" + tag));
    }

    [PostgresFact]
    public async Task Validate_BadStageRow_IsReportedOnTheStageSheet()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        db.TestDefinitions.Add(new TestDefinition { Code = "MS-" + tag, DisplayName = "Staged " + tag, SectionId = _fixture.SeededSectionId, IsActive = true });
        await db.SaveChangesAsync();

        var report = await Service(db).ValidateAsync(Workbook(
            ("Items", new[] { R(("Code", "FP-" + tag), ("Name", "Tablet " + tag), ("Category", "FinishedProduct"), ("TestCodes", "MS-" + tag)) }),
            ("Specifications", new[] { R(("ItemCode", "FP-" + tag), ("TestCode", "MS-" + tag), ("ParameterName", "Staged"), ("LimitType", "MultiStage")) }),
            ("SpecificationStages", new[]
            {
                R(("ItemCode", "FP-" + tag), ("TestCode", "MS-" + tag), ("ParameterName", "Staged"), ("StageNumber", "1"), ("StageLabel", ""), ("AcceptanceCriteria", "x")),
            })), admin.Id);

        Assert.Contains(report.Errors, e => (e.Sheet, e.Row, e.Column) == ("SpecificationStages", 2, "StageLabel"));
        Assert.DoesNotContain(report.Errors, e => e.Sheet == "Specifications");
    }
}

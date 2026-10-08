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
}

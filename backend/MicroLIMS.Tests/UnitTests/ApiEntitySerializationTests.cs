using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// POST api/media and PUT api/masterdata/media-products/{id} hand EF entities
// straight to System.Text.Json. MediaProduct.Configurations and
// MediaConfiguration.MediaProduct point at each other, so a request that has
// tracked both sides can give the serializer a reference loop - a 500 that
// arrives AFTER the change was saved, inviting the analyst to submit again.
// These serialize the returned entities the way the API does, starting from
// an empty change tracker like a real request.
public class ApiEntitySerializationTests
{
    // The exact options Program.cs applies to MVC responses (web defaults,
    // which is what AddJsonOptions starts from, plus ApiJsonOptions).
    private static JsonSerializerOptions ApiOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        MicroLIMS.API.Json.ApiJsonOptions.Configure(options);
        return options;
    }

    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CurrentUserId = 1;
        return db;
    }

    private static MediaConfiguration NewConfiguration(MediaProduct product)
    {
        var config = new MediaConfiguration
        {
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationCondition = MediaProductTestData.Condition(product),
            RecoveryPercentMin = 50, RecoveryPercentMax = 200
        };
        MediaProductTestData.Link(config, product);
        return config;
    }

    [Fact]
    public async Task PreparedMediaLot_AsReturnedByThePrepareEndpoint_Serializes()
    {
        await using var db = NewDb();
        var autoclave = new Equipment { Name = "Autoclave 1", Code = "AUT-01", Type = EquipmentType.Autoclave };
        db.Equipment.Add(autoclave);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        db.MediaConfigurations.Add(NewConfiguration(product));
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, ManufacturerName = "Himedia", BatchNumber = "LOT-001",
            ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        MediaProductTestData.Link(material, product);
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        db.MaterialDocuments.Add(new MaterialDocument
        {
            MaterialId = material.Id, DocumentType = MaterialDocumentType.COA, OriginalFileName = "COA.pdf",
            StorageKey = $"material-documents/{material.Id}/test.pdf", FileExtension = ".pdf", ContentType = "application/pdf",
            FileSizeBytes = 1024, ContentSha256 = "COAHASH", UploadedByUserId = 1, UploadedAt = DateTime.UtcNow,
            Status = MaterialDocumentStatus.Current
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var media = await TestServiceFactory.MediaPreparation(db).PrepareAsync(new PrepareMediaRequest(
            material.Id, 10m, "500 ml", autoclave.Id, "Program A", "agar", 121m, 15, 1, 7.2m,
            DateTime.UtcNow.AddMonths(1), UserId: 1));

        var json = JsonSerializer.Serialize(media, ApiOptions());

        Assert.Contains("\"lotNumber\":\"TSA/01/", json);
    }

    [Fact]
    public async Task RenamedMediaProduct_AsReturnedByTheRenameEndpoint_Serializes()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        db.MediaConfigurations.Add(NewConfiguration(product));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var renamed = await TestServiceFactory.MediaProduct(db).RenameAsync(product.Id, "Tryptone Soya Agar");

        var json = JsonSerializer.Serialize(renamed, ApiOptions());

        Assert.Contains("\"name\":\"Tryptone Soya Agar\"", json);
    }
}

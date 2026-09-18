using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MediaProductPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public MediaProductPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task UniqueIndex_CaseInsensitiveCode_RejectsDuplicateInPostgres()
    {
        await using var db = _fixture.CreateDbContext();

        var code = NewCode();
        var product1 = new MediaProduct
        {
            Name = $"Product Code Test 1 {Guid.NewGuid():N}",
            Code = code.ToUpperInvariant()
        };
        db.MediaProducts.Add(product1);
        await db.SaveChangesAsync();

        var product2 = new MediaProduct
        {
            Name = $"Product Code Test 2 {Guid.NewGuid():N}",
            Code = code.ToLowerInvariant()
        };
        db.MediaProducts.Add(product2);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task UniqueIndex_CaseInsensitiveName_RejectsDuplicateInPostgres()
    {
        await using var db = _fixture.CreateDbContext();

        var baseName = $"Case Insensitive Product {Guid.NewGuid():N}";
        var product1 = new MediaProduct
        {
            Name = baseName.ToUpperInvariant(),
            Code = NewCode()
        };
        db.MediaProducts.Add(product1);
        await db.SaveChangesAsync();

        var product2 = new MediaProduct
        {
            Name = baseName.ToLowerInvariant(),
            Code = NewCode()
        };
        db.MediaProducts.Add(product2);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task MediaGptReportService_PostgresQueriesTranslateAndExecute()
    {
        await using var db = _fixture.CreateDbContext();

        var code = NewCode();
        var productName = $"Media GPT Test Product {code}";

        var product = new MediaProduct
        {
            Name = productName,
            Code = code
        };
        db.MediaProducts.Add(product);
        await db.SaveChangesAsync();

        var autoclave = new Equipment
        {
            Name = $"Autoclave {code}",
            Code = $"AUT-{code}",
            Type = EquipmentType.Autoclave
        };
        db.Equipment.Add(autoclave);

        var microSectionId = (await db.DocumentSections.FirstAsync(s => s.Code == "MICRO")).Id;
        var material = new Material
        {
            SectionId = microSectionId,
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = productName,
            ManufacturerName = "Himedia",
            BatchNumber = $"BATCH-{code}",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = code,
            Location = "Micro Lab",
            QuantityReceived = 500,
            QuantityRemaining = 500,
            Unit = MaterialUnit.Gram,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId,
            MediaProductId = product.Id
        };
        db.Materials.Add(material);

        var config = new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = productName,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationCondition = MediaProductTestData.Condition(product),
            RecoveryPercentMin = 50,
            RecoveryPercentMax = 200
        };
        db.MediaConfigurations.Add(config);
        await db.SaveChangesAsync();

        var mediaLot = new Media
        {
            MaterialId = material.Id,
            LotNumber = $"{code}/01/26",
            PreparedAt = DateTime.UtcNow.AddDays(-2),
            ExpiryDate = DateTime.UtcNow.AddMonths(1),
            PreparedByUserId = _fixture.SeededUserId,
            ApprovalStatus = ApprovalGateStatus.Approved,
            IsReleasedForUse = true,
            ManufacturerName = "Himedia",
            ManufacturerLot = $"MFR-{code}",
            TotalWeight = 10,
            TotalVolume = "500 ml",
            AutoclaveEquipmentId = autoclave.Id,
            AutoclaveProgram = "Program 1",
            LoadType = "agar",
            Temperature = 121m,
            CycleTime = 15,
            CycleNumber = 1,
            Ph = 7.2m
        };
        db.Media.Add(mediaLot);
        await db.SaveChangesAsync();

        var organism = new Organism
        {
            ScientificName = $"Organism {code}",
            AtccNumber = $"ATCC-{code}"
        };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();

        var evaluation = new MediaEvaluation
        {
            MediaId = mediaLot.Id,
            EvaluationType = EvaluationType.GrowthPromotion,
            Status = MediaEvaluationStatus.Completed,
            Outcome = EvaluationOutcome.Conform,
            AssignedAt = DateTime.UtcNow.AddDays(-2),
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            CompletedByUserId = _fixture.SeededUserId
        };
        db.MediaEvaluations.Add(evaluation);
        await db.SaveChangesAsync();

        var challenge = new MediaEvaluationChallenge
        {
            MediaEvaluationId = evaluation.Id,
            OrganismId = organism.Id,
            InitialInoculum = "50-100 CFU",
            OldMediaCount = 80,
            NewMediaCount = 76,
            RecoveryPercent = 95m,
            GrowthObserved = true,
            Outcome = EvaluationOutcome.Conform,
            ReadByUserId = _fixture.SeededUserId,
            ReadAt = DateTime.UtcNow.AddDays(-1)
        };
        db.MediaEvaluationChallenges.Add(challenge);
        await db.SaveChangesAsync();

        var service = new MediaGptReportService(db);

        // 1. SearchAsync with search term, mediaType filter, sorting by "MediaType"
        var searchResult = await service.SearchAsync(new MediaGptSearchRequest
        {
            Search = code.ToLowerInvariant(),
            MediaType = productName,
            SortBy = "MediaType",
            SortDescending = false,
            Page = 1,
            PageSize = 25
        });

        Assert.True(searchResult.TotalCount >= 1);
        Assert.Contains(searchResult.Items, i => i.LotNumber == mediaLot.LotNumber && i.MediaType == productName);

        // Also test SortDescending by "MediaType"
        var descResult = await service.SearchAsync(new MediaGptSearchRequest
        {
            Search = code.ToLowerInvariant(),
            MediaType = productName,
            SortBy = "MediaType",
            SortDescending = true,
            Page = 1,
            PageSize = 25
        });
        Assert.True(descResult.TotalCount >= 1);

        // 2. GetFilterOptionsAsync
        var filterOptions = await service.GetFilterOptionsAsync();
        Assert.Contains(filterOptions.MediaTypes, t => t == productName);
        Assert.NotEmpty(filterOptions.EvaluationTypes);

        // 3. GetSummaryAsync
        var summary = await service.GetSummaryAsync(
            fromDate: DateTime.UtcNow.AddDays(-5),
            toDate: DateTime.UtcNow.AddDays(1),
            mediaType: productName);

        Assert.True(summary.TotalLots >= 1);
        Assert.Contains(summary.MediaTypes, t => t.MediaType == productName);

        // 4. GetDetailAsync asserting recovery limits are returned
        var detail = await service.GetDetailAsync(mediaLot.Id);
        Assert.NotNull(detail);
        Assert.Equal(productName, detail.MediaType);
        Assert.Single(detail.Challenges);

        var ch = detail.Challenges[0];
        Assert.Equal(50m, ch.ExpectedMinRecoveryPercent);
        Assert.Equal(200m, ch.ExpectedMaxRecoveryPercent);
    }

    private static string NewCode() => "P" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();
}

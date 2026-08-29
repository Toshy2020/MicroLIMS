using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class MediaGptAndReferenceStrainReportTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task MediaGptReportService_FilteringByEvaluationTypeAndDateRange_AndSearch()
    {
        await using var db = NewDb();

        var matTsa = new Material { Id = 1, MaterialName = "Tryptic Soy Agar", MaterialType = MaterialType.DehydratedMedia, ReceivingDate = DateTime.UtcNow.AddMonths(-3) };
        var matSda = new Material { Id = 2, MaterialName = "Sabouraud Dextrose Agar", MaterialType = MaterialType.DehydratedMedia, ReceivingDate = DateTime.UtcNow.AddMonths(-3) };
        db.Materials.AddRange(matTsa, matSda);

        var now = DateTime.UtcNow;

        // Lot 1: TSA, prepared 10 days ago, GrowthPromotion, Conform
        var media1 = new Media
        {
            Id = 1,
            MaterialId = 1,
            Material = matTsa,
            LotNumber = "TSA-26-001",
            PreparedAt = now.AddDays(-10),
            ExpiryDate = now.AddMonths(3),
            ApprovalStatus = ApprovalGateStatus.Approved,
            IsReleasedForUse = true,
            PreparedByUserId = 1
        };
        var eval1 = new MediaEvaluation
        {
            Id = 1,
            MediaId = 1,
            EvaluationType = EvaluationType.GrowthPromotion,
            Status = MediaEvaluationStatus.Completed,
            Outcome = EvaluationOutcome.Conform,
            CompletedAt = now.AddDays(-9),
            CompletedByUserId = 1
        };

        // Lot 2: TSA, prepared 20 days ago, IndicationInhibition, NonConform
        var media2 = new Media
        {
            Id = 2,
            MaterialId = 1,
            Material = matTsa,
            LotNumber = "TSA-26-002",
            PreparedAt = now.AddDays(-20),
            ExpiryDate = now.AddMonths(3),
            ApprovalStatus = ApprovalGateStatus.Rejected,
            IsReleasedForUse = false,
            PreparedByUserId = 1
        };
        var eval2 = new MediaEvaluation
        {
            Id = 2,
            MediaId = 2,
            EvaluationType = EvaluationType.IndicationInhibition,
            Status = MediaEvaluationStatus.Completed,
            Outcome = EvaluationOutcome.NonConform,
            CompletedAt = now.AddDays(-19),
            CompletedByUserId = 1
        };

        // Lot 3: SDA, prepared 5 days ago, GrowthPromotion, Pending (no outcome yet)
        var media3 = new Media
        {
            Id = 3,
            MaterialId = 2,
            Material = matSda,
            LotNumber = "SDA-26-001",
            PreparedAt = now.AddDays(-5),
            ExpiryDate = now.AddMonths(3),
            ApprovalStatus = ApprovalGateStatus.PendingReview,
            IsReleasedForUse = false,
            PreparedByUserId = 2
        };
        var eval3 = new MediaEvaluation
        {
            Id = 3,
            MediaId = 3,
            EvaluationType = EvaluationType.GrowthPromotion,
            Status = MediaEvaluationStatus.InProgress,
            Outcome = null
        };

        db.Media.AddRange(media1, media2, media3);
        db.MediaEvaluations.AddRange(eval1, eval2, eval3);
        await db.SaveChangesAsync();

        var service = new MediaGptReportService(db);

        // 1. Unfiltered
        var all = await service.SearchAsync(new MediaGptSearchRequest());
        Assert.Equal(3, all.TotalCount);

        // 2. Filtered by EvaluationType: GrowthPromotion only (Lot 1 and Lot 3)
        var gptOnly = await service.SearchAsync(new MediaGptSearchRequest(EvaluationType: EvaluationType.GrowthPromotion));
        Assert.Equal(2, gptOnly.TotalCount);
        Assert.Contains(gptOnly.Items, x => x.LotNumber == "TSA-26-001");
        Assert.Contains(gptOnly.Items, x => x.LotNumber == "SDA-26-001");

        // 3. Filtered by EvaluationType: IndicationInhibition only (Lot 2)
        var indicationOnly = await service.SearchAsync(new MediaGptSearchRequest(EvaluationType: EvaluationType.IndicationInhibition));
        Assert.Equal(1, indicationOnly.TotalCount);
        Assert.Equal("TSA-26-002", indicationOnly.Items[0].LotNumber);

        // 4. Filtered by Date Range (PreparedAt between -15 days and -8 days -> Lot 1 only)
        var dateRange = await service.SearchAsync(new MediaGptSearchRequest(
            FromDate: now.AddDays(-15),
            ToDate: now.AddDays(-8)));
        Assert.Equal(1, dateRange.TotalCount);
        Assert.Equal("TSA-26-001", dateRange.Items[0].LotNumber);

        // 5. Search substring "SDA" -> Lot 3 only
        var sdaSearch = await service.SearchAsync(new MediaGptSearchRequest(Search: "SDA"));
        Assert.Equal(1, sdaSearch.TotalCount);
        Assert.Equal("SDA-26-001", sdaSearch.Items[0].LotNumber);
    }

    [Fact]
    public async Task MediaGptReportService_Summary_CalculatesAccuratePassRates()
    {
        await using var db = NewDb();

        var matTsa = new Material { Id = 1, MaterialName = "Tryptic Soy Agar", MaterialType = MaterialType.DehydratedMedia, ReceivingDate = DateTime.UtcNow };
        db.Materials.Add(matTsa);

        var now = DateTime.UtcNow;

        var m1 = new Media { Id = 1, MaterialId = 1, Material = matTsa, LotNumber = "L1", PreparedAt = now.AddDays(-3) };
        var m2 = new Media { Id = 2, MaterialId = 1, Material = matTsa, LotNumber = "L2", PreparedAt = now.AddDays(-2) };
        var m3 = new Media { Id = 3, MaterialId = 1, Material = matTsa, LotNumber = "L3", PreparedAt = now.AddDays(-1) };

        var e1 = new MediaEvaluation { Id = 1, MediaId = 1, EvaluationType = EvaluationType.GrowthPromotion, Status = MediaEvaluationStatus.Completed, Outcome = EvaluationOutcome.Conform };
        var e2 = new MediaEvaluation { Id = 2, MediaId = 2, EvaluationType = EvaluationType.GrowthPromotion, Status = MediaEvaluationStatus.Completed, Outcome = EvaluationOutcome.NonConform };
        var e3 = new MediaEvaluation { Id = 3, MediaId = 3, EvaluationType = EvaluationType.GrowthPromotion, Status = MediaEvaluationStatus.Assigned, Outcome = null };

        db.Media.AddRange(m1, m2, m3);
        db.MediaEvaluations.AddRange(e1, e2, e3);
        await db.SaveChangesAsync();

        var service = new MediaGptReportService(db);
        var summary = await service.GetSummaryAsync(null, null, null);

        Assert.Equal(3, summary.TotalLots);
        Assert.Equal(1, summary.TotalConformed);
        Assert.Equal(1, summary.TotalNonConformed);
        Assert.Equal(1, summary.TotalPending);
        // 1 conform out of 2 completed lots = 50.0%
        Assert.Equal(50.0, summary.OverallPassRatePercent);

        var tsaSummary = Assert.Single(summary.MediaTypes);
        Assert.Equal("Tryptic Soy Agar", tsaSummary.MediaType);
        Assert.Equal(3, tsaSummary.TotalLots);
        Assert.Equal(1, tsaSummary.ConformedLots);
        Assert.Equal(1, tsaSummary.NonConformedLots);
        Assert.Equal(1, tsaSummary.PendingLots);
        Assert.Equal(50.0, tsaSummary.PassRatePercent);
    }

    [Fact]
    public async Task MediaGptReportService_GetDetailAsync_FullTraceabilityAndAcceptanceLimits()
    {
        await using var db = NewDb();

        var config = new MediaConfiguration
        {
            Id = 1,
            Name = "Tryptic Soy Agar",
            EvaluationType = EvaluationType.GrowthPromotion,
            RecoveryPercentMin = 50.0m,
            RecoveryPercentMax = 200.0m
        };
        db.MediaConfigurations.Add(config);

        var organism = new Organism { Id = 10, ScientificName = "Staphylococcus aureus", AtccNumber = "6538" };
        var material = new Material { Id = 1, MaterialName = "Tryptic Soy Agar", MaterialType = MaterialType.DehydratedMedia, ReceivingDate = DateTime.UtcNow };
        db.Organisms.Add(organism);
        db.Materials.Add(material);

        var media = new Media
        {
            Id = 100,
            MaterialId = 1,
            Material = material,
            LotNumber = "TSA-2026-LOT1",
            PreparedAt = DateTime.UtcNow.AddDays(-4),
            ExpiryDate = DateTime.UtcNow.AddMonths(3),
            ApprovalStatus = ApprovalGateStatus.Approved,
            IsReleasedForUse = true,
            PreparedByUserId = 1
        };
        db.Media.Add(media);

        var eval = new MediaEvaluation
        {
            Id = 100,
            MediaId = 100,
            EvaluationType = EvaluationType.GrowthPromotion,
            Status = MediaEvaluationStatus.Completed,
            Outcome = EvaluationOutcome.Conform,
            CompletedAt = DateTime.UtcNow.AddDays(-2),
            CompletedByUserId = 1
        };

        var challenge = new MediaEvaluationChallenge
        {
            Id = 501,
            MediaEvaluationId = 100,
            OrganismId = 10,
            Organism = organism,
            InitialInoculum = "<=100 CFU",
            OldMediaCount = 80m,
            NewMediaCount = 76m,
            RecoveryPercent = 95.0m,
            Outcome = EvaluationOutcome.Conform,
            ReadAt = DateTime.UtcNow.AddDays(-2),
            ReadByUserId = 1,
            ReferenceMediaLabel = "Historical Ref Lot #99"
        };
        eval.Challenges.Add(challenge);
        db.MediaEvaluations.Add(eval);
        await db.SaveChangesAsync();

        var service = new MediaGptReportService(db);
        var detail = await service.GetDetailAsync(100);

        Assert.NotNull(detail);
        Assert.Equal("TSA-2026-LOT1", detail.LotNumber);
        Assert.Equal("Tryptic Soy Agar", detail.MediaType);
        Assert.Equal("Conform", detail.EvaluationOutcome);
        Assert.Single(detail.Challenges);

        var chDetail = detail.Challenges[0];
        Assert.Equal("Staphylococcus aureus", chDetail.OrganismName);
        Assert.Equal("6538", chDetail.AtccNumber);
        Assert.Equal(95.0m, chDetail.RecoveryPercent);
        Assert.Equal(50.0m, chDetail.ExpectedMinRecoveryPercent);
        Assert.Equal(200.0m, chDetail.ExpectedMaxRecoveryPercent);
        Assert.Equal("Historical Ref Lot #99", chDetail.ReferenceMediaLot);
        Assert.Equal("Conform", chDetail.Outcome);
    }

    [Fact]
    public async Task MediaGptReportService_ExportRowCap_ExceededReturnsFlag()
    {
        await using var db = NewDb();

        var mat = new Material { Id = 1, MaterialName = "Nutrient Agar", MaterialType = MaterialType.DehydratedMedia, ReceivingDate = DateTime.UtcNow };
        db.Materials.Add(mat);

        for (int i = 1; i <= 5; i++)
        {
            var media = new Media { Id = i, MaterialId = 1, Material = mat, LotNumber = $"LOT-{i}", PreparedAt = DateTime.UtcNow };
            var eval = new MediaEvaluation { Id = i, MediaId = i, EvaluationType = EvaluationType.GrowthPromotion };
            eval.Challenges.Add(new MediaEvaluationChallenge { Id = i * 10, MediaEvaluationId = i, OrganismId = 1, InitialInoculum = "50" });
            db.Media.Add(media);
            db.MediaEvaluations.Add(eval);
        }
        await db.SaveChangesAsync();

        var service = new MediaGptReportService(db);

        // Cap exceeded (maxRows = 3 with 5 challenge rows)
        var exceededResult = await service.GetForExportAsync(new MediaGptSearchRequest(), maxRows: 3);
        Assert.True(exceededResult.Exceeded);
        Assert.Equal(5, exceededResult.TotalCount);
        Assert.Empty(exceededResult.Items);

        // Under cap (maxRows = 10 with 5 challenge rows)
        var underCapResult = await service.GetForExportAsync(new MediaGptSearchRequest(), maxRows: 10);
        Assert.False(underCapResult.Exceeded);
        Assert.Equal(5, underCapResult.TotalCount);
        Assert.Equal(5, underCapResult.Items.Count);
    }

    [Fact]
    public async Task ReferenceStrainReportService_FilteringByStrainIdentity_ReceiptDate_AndUsageDate()
    {
        await using var db = NewDb();

        var orgSa = new Organism { Id = 1, ScientificName = "Staphylococcus aureus", AtccNumber = "6538" };
        var orgEc = new Organism { Id = 2, ScientificName = "Escherichia coli", AtccNumber = "8739" };
        db.Organisms.AddRange(orgSa, orgEc);

        var now = DateTime.UtcNow;

        var matSa = new Material
        {
            Id = 1,
            MaterialName = "S. aureus ATCC 6538 Disc",
            MaterialType = MaterialType.LyophilizedMicroorganism,
            BatchNumber = "BATCH-SA-1",
            ReceivingDate = now.AddDays(-60),
            OrganismId = 1
        };
        var matEc = new Material
        {
            Id = 2,
            MaterialName = "E. coli ATCC 8739 Disc",
            MaterialType = MaterialType.LyophilizedMicroorganism,
            BatchNumber = "BATCH-EC-1",
            ReceivingDate = now.AddDays(-10),
            OrganismId = 2
        };
        db.Materials.AddRange(matSa, matEc);

        var cryoSa = new Cryovial
        {
            Id = 1,
            Code = "SA-CRY-01",
            MaterialId = 1,
            Material = matSa,
            OrganismId = 1,
            Organism = orgSa,
            ManufacturerName = "Microbiologics",
            PreparedAt = now.AddDays(-55),
            ExpiryDate = now.AddMonths(6),
            NumberOfVialsPrepared = 10,
            VialsRemaining = 8,
            ApprovalStatus = ApprovalGateStatus.Approved,
            PreparedByUserId = 1
        };

        var cryoEc = new Cryovial
        {
            Id = 2,
            Code = "EC-CRY-01",
            MaterialId = 2,
            Material = matEc,
            OrganismId = 2,
            Organism = orgEc,
            ManufacturerName = "ATCC",
            PreparedAt = now.AddDays(-8),
            ExpiryDate = now.AddMonths(6),
            NumberOfVialsPrepared = 10,
            VialsRemaining = 10,
            ApprovalStatus = ApprovalGateStatus.PendingReview,
            PreparedByUserId = 1
        };

        db.Cryovials.AddRange(cryoSa, cryoEc);

        // Record a GPT challenge usage for Cryovial 1 on day -30
        var chUsage = new MediaEvaluationChallenge
        {
            Id = 101,
            MediaEvaluationId = 1,
            CryovialId = 1,
            OrganismId = 1,
            ReadAt = now.AddDays(-30),
            Outcome = EvaluationOutcome.Conform
        };
        db.MediaEvaluationChallenges.Add(chUsage);

        await db.SaveChangesAsync();

        var service = new ReferenceStrainReportService(db);

        // 1. Unfiltered: 2
        var all = await service.SearchAsync(new ReferenceStrainSearchRequest());
        Assert.Equal(2, all.TotalCount);

        // 2. Filter by search "aureus" or "6538" -> Cryovial 1 only
        var searchOrg = await service.SearchAsync(new ReferenceStrainSearchRequest(Search: "aureus"));
        Assert.Equal(1, searchOrg.TotalCount);
        Assert.Equal("SA-CRY-01", searchOrg.Items[0].CryovialCode);
        Assert.Equal(1, searchOrg.Items[0].DirectUsageCount);

        // 3. Filter by Receipt Date Range (received between -20 days and now -> Cryovial 2 only)
        var receiptFilter = await service.SearchAsync(new ReferenceStrainSearchRequest(
            ReceiptFromDate: now.AddDays(-20),
            ReceiptToDate: now));
        Assert.Equal(1, receiptFilter.TotalCount);
        Assert.Equal("EC-CRY-01", receiptFilter.Items[0].CryovialCode);

        // 4. Filter by Usage Date Range (used between -40 days and -20 days -> Cryovial 1 only)
        var usageFilter = await service.SearchAsync(new ReferenceStrainSearchRequest(
            UsageFromDate: now.AddDays(-40),
            UsageToDate: now.AddDays(-20)));
        Assert.Equal(1, usageFilter.TotalCount);
        Assert.Equal("SA-CRY-01", usageFilter.Items[0].CryovialCode);
    }

    [Fact]
    public async Task ReferenceStrainReportService_Detail_IncludesIdentityPanelThawHistoryAndDirectAndIndirectRollup()
    {
        await using var db = NewDb();

        var org = new Organism { Id = 1, ScientificName = "Bacillus subtilis", AtccNumber = "6633" };
        var disc = new Material { Id = 1, MaterialName = "B. subtilis Disc", MaterialType = MaterialType.LyophilizedMicroorganism, BatchNumber = "DISC-001", ReceivingDate = DateTime.UtcNow.AddDays(-90), QuantityReceived = 5m };
        var mediaMat = new Material { Id = 2, MaterialName = "TSA Agar", MaterialType = MaterialType.DehydratedMedia, ReceivingDate = DateTime.UtcNow.AddDays(-90) };
        db.Organisms.Add(org);
        db.Materials.AddRange(disc, mediaMat);

        var mediaLot = new Media { Id = 50, MaterialId = 2, Material = mediaMat, LotNumber = "TSA-LOT-50", PreparedAt = DateTime.UtcNow.AddDays(-40) };
        db.Media.Add(mediaLot);

        var cryovial = new Cryovial
        {
            Id = 10,
            Code = "BS-CRY-10",
            MaterialId = 1,
            Material = disc,
            OrganismId = 1,
            Organism = org,
            ManufacturerName = "Microbiologics",
            PreparedAt = DateTime.UtcNow.AddDays(-80),
            ExpiryDate = DateTime.UtcNow.AddMonths(12),
            NumberOfVialsPrepared = 20,
            VialsRemaining = 18,
            ApprovalStatus = ApprovalGateStatus.Approved,
            PreparedByUserId = 1
        };

        // Identity confirmation entry
        cryovial.IdentityConfirmations.Add(new IdentityConfirmationEntry
        {
            Id = 1,
            CryovialId = 10,
            MediaId = 50,
            Media = mediaLot,
            IncubationStart = DateTime.UtcNow.AddDays(-80),
            IncubationEnd = DateTime.UtcNow.AddDays(-78),
            ObservationText = "Typical colonies observed"
        });

        // Thaw history (labeled "Thaw History")
        cryovial.ThawHistory.Add(new ThawEvent
        {
            Id = 1,
            CryovialId = 10,
            ThawedAt = DateTime.UtcNow.AddDays(-45),
            ThawedByUserId = 1,
            Notes = "Thawed for media qualification run #1"
        });

        db.Cryovials.Add(cryovial);

        // Media Evaluation Challenge that directly used this cryovial
        var eval = new MediaEvaluation
        {
            Id = 20,
            MediaId = 50,
            Media = mediaLot,
            EvaluationType = EvaluationType.GrowthPromotion,
            Status = MediaEvaluationStatus.Completed,
            Outcome = EvaluationOutcome.Conform
        };

        var ch = new MediaEvaluationChallenge
        {
            Id = 201,
            MediaEvaluationId = 20,
            MediaEvaluation = eval,
            CryovialId = 10,
            OrganismId = 1,
            ReadAt = DateTime.UtcNow.AddDays(-38),
            Outcome = EvaluationOutcome.Conform
        };
        db.MediaEvaluations.Add(eval);
        db.MediaEvaluationChallenges.Add(ch);

        // Routine test orders that consumed the qualified media lot (MediaUsage)
        var order1 = new TestOrder { Id = 1001, TestCode = "TAMC" };
        var order2 = new TestOrder { Id = 1002, TestCode = "TAMC" };
        db.TestOrders.AddRange(order1, order2);

        db.MediaUsages.Add(new MediaUsage { Id = 1, MediaId = 50, TestOrderId = 1001 });
        db.MediaUsages.Add(new MediaUsage { Id = 2, MediaId = 50, TestOrderId = 1002 });

        await db.SaveChangesAsync();

        var service = new ReferenceStrainReportService(db);
        var detail = await service.GetDetailAsync(10);

        Assert.NotNull(detail);
        Assert.Equal("BS-CRY-10", detail.CryovialCode);
        Assert.Equal("Bacillus subtilis", detail.StrainName);
        Assert.Equal("6633", detail.AtccNumber);

        // Identity confirmations
        Assert.Single(detail.IdentityConfirmations);
        Assert.Equal("TSA-LOT-50", detail.IdentityConfirmations[0].MediaLotNumber);
        Assert.Equal("Typical colonies observed", detail.IdentityConfirmations[0].ObservationText);

        // Thaw history
        Assert.Single(detail.ThawHistory);
        Assert.Equal("Thawed for media qualification run #1", detail.ThawHistory[0].Notes);

        // Direct usage log
        Assert.Single(detail.DirectUsageLog);
        Assert.Equal("TSA-LOT-50", detail.DirectUsageLog[0].MediaLotNumber);
        Assert.Equal("Conform", detail.DirectUsageLog[0].Outcome);

        // Indirect rollup
        Assert.Equal(1, detail.DistinctQualifiedMediaLotsCount);
        Assert.Equal(2, detail.IndirectTestOrdersCount);
        Assert.Contains("2 test orders used media qualified with this strain batch (indirect, via GPT-qualified media lots)", detail.IndirectUsageSummary);
    }

    [Fact]
    public async Task ReferenceStrainReportService_ExportRowCap_ExceededReturnsFlag()
    {
        await using var db = NewDb();

        var org = new Organism { Id = 1, ScientificName = "C. albicans", AtccNumber = "10231" };
        var mat = new Material { Id = 1, MaterialName = "C. albicans Disc", MaterialType = MaterialType.LyophilizedMicroorganism, ReceivingDate = DateTime.UtcNow };
        db.Organisms.Add(org);
        db.Materials.Add(mat);

        for (int i = 1; i <= 4; i++)
        {
            var cryovial = new Cryovial { Id = i, Code = $"CRYO-{i}", MaterialId = 1, Material = mat, OrganismId = 1, Organism = org, PreparedAt = DateTime.UtcNow };
            db.Cryovials.Add(cryovial);
        }
        await db.SaveChangesAsync();

        var service = new ReferenceStrainReportService(db);

        // Cap exceeded (maxRows = 2 with 4 cryovials)
        var exceeded = await service.GetForExportAsync(new ReferenceStrainSearchRequest(), maxRows: 2);
        Assert.True(exceeded.Exceeded);
        Assert.Equal(4, exceeded.TotalCount);
        Assert.Empty(exceeded.Items);

        // Under cap (maxRows = 10 with 4 cryovials)
        var underCap = await service.GetForExportAsync(new ReferenceStrainSearchRequest(), maxRows: 10);
        Assert.False(underCap.Exceeded);
        Assert.Equal(4, underCap.TotalCount);
        Assert.Equal(4, underCap.Items.Count);
    }
}

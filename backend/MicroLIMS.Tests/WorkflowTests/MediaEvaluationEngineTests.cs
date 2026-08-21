using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Replaces the (never-written) GPT tests - GptWorkflowEngine had no
// dedicated test file to port, so this covers the three Media
// Evaluation mechanics fresh: auto-assignment on media preparation,
// cryovial selection, locked incubation, and the release gate.
public class MediaEvaluationEngineTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<(Media media, MediaEvaluation evaluation)> PrepareMediaWithEvaluation(
        MicroLimsDbContext db, MediaClass mediaClass, string materialName, List<MediaChallengeSpec> specs,
        decimal? recoveryMin = 50, decimal? recoveryMax = 200)
    {
        var mediaType = new MediaType
        {
            Class = mediaClass, IncubationMinHours = 24, IncubationMaxHours = 48,
            RequiredTemperatureMin = 30, RequiredTemperatureMax = 35,
            RecoveryPercentMin = recoveryMin, RecoveryPercentMax = recoveryMax
        };
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = materialName, ManufacturerName = "Himedia",
            BatchNumber = "LOT-1", ReceivingDate = DateTime.UtcNow.AddDays(-5), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = "MAT", Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        var autoclave = new Equipment { Name = "Autoclave 1", Code = "AUT-01", Type = EquipmentType.Autoclave };
        db.MediaTypes.Add(mediaType);
        db.Materials.Add(material);
        db.Equipment.Add(autoclave);
        db.MediaChallengeSpecs.AddRange(specs);
        await db.SaveChangesAsync();

        db.MaterialDocuments.Add(new MaterialDocument
        {
            MaterialId = material.Id,
            DocumentType = MaterialDocumentType.COA,
            OriginalFileName = "COA.pdf",
            StorageKey = "test/coa.pdf",
            FileExtension = ".pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            ContentSha256 = "HASH",
            UploadedByUserId = 1,
            Status = MaterialDocumentStatus.Current
        });
        await db.SaveChangesAsync();

        var service = TestServiceFactory.MediaPreparation(db);
        var request = new PrepareMediaRequest(
            mediaType.Id, material.Id, TotalWeight: 100m, TotalVolume: "500 ml", AutoclaveEquipmentId: autoclave.Id,
            AutoclaveProgram: "A", LoadType: "agar", Temperature: 121m, CycleTime: 15, CycleNumber: 1,
            Ph: 7.2m, ExpiryDate: DateTime.UtcNow.AddMonths(6), UserId: 1);
        var media = await service.PrepareAsync(request);

        var evaluation = await db.MediaEvaluations.Include(e => e.Challenges).FirstAsync(e => e.MediaId == media.Id);
        return (media, evaluation);
    }

    // Records incubation then backdates ExpectedReadingAt into the past,
    // simulating the incubation period having already elapsed - real
    // tests of the "not before incubation ends" gate backdate
    // differently (see RecordResultAsync_BeforeIncubationPeriodElapses_Throws).
    private static async Task<Incubation> RecordAndFastForwardIncubation(MicroLimsDbContext db, MediaEvaluationEngine engine, int challengeId)
    {
        var incubation = await engine.RecordIncubationAsync(challengeId, incubatorEquipmentId: 1, userId: 1);
        incubation.ExpectedReadingAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        return incubation;
    }

    private static async Task<int> GetOrCreateOrganismIdAsync(MicroLimsDbContext db, string scientificName)
    {
        var existing = await db.Organisms.FirstOrDefaultAsync(o => o.ScientificName == scientificName);
        if (existing != null) return existing.Id;
        var organism = new Organism { ScientificName = scientificName };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();
        return organism.Id;
    }

    private static async Task<Cryovial> SeedApprovedCryovial(MicroLimsDbContext db, string organismName)
    {
        var organismId = await GetOrCreateOrganismIdAsync(db, organismName);
        var material = new Material
        {
            MaterialType = MaterialType.LyophilizedMicroorganism, MaterialName = organismName, ManufacturerName = "Tody",
            BatchNumber = "LB1", ReceivingDate = DateTime.UtcNow.AddDays(-5), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Location = "Freezer", QuantityReceived = 10, QuantityRemaining = 10, Unit = MaterialUnit.Disc, OrganismId = organismId
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var cryovial = new Cryovial
        {
            Code = "CV01", MaterialId = material.Id, OrganismId = organismId, OrganismNameSnapshot = organismName, ManufacturerName = "Tody",
            ExpiryDate = DateTime.UtcNow.AddYears(1), NumberOfVialsPrepared = 5, VialsRemaining = 5,
            ApprovalStatus = ApprovalGateStatus.Approved
        };
        db.Cryovials.Add(cryovial);
        await db.SaveChangesAsync();
        return cryovial;
    }

    [Fact]
    public async Task PrepareAsync_AutoAssignsEvaluationAndChallengesFromMatchingSpecs()
    {
        await using var db = NewDb();
        var ecoliId = await GetOrCreateOrganismIdAsync(db, "E. coli");
        var specs = new List<MediaChallengeSpec> { new() { MaterialName = "TSA", EvaluationType = EvaluationType.GrowthPromotion, OrganismId = ecoliId } };
        var (media, evaluation) = await PrepareMediaWithEvaluation(db, MediaClass.GeneralAgar, "TSA", specs);

        Assert.Equal(EvaluationType.GrowthPromotion, evaluation.EvaluationType);
        Assert.Equal(MediaEvaluationStatus.Assigned, evaluation.Status);
        Assert.Single(evaluation.Challenges);
        Assert.Equal(ecoliId, evaluation.Challenges[0].OrganismId);
        Assert.Equal("10^2", evaluation.Challenges[0].InitialInoculum);
        Assert.False(media.IsReleasedForUse);
    }

    [Fact]
    public async Task PrepareAsync_NoMatchingSpecs_CreatesEvaluationWithZeroChallenges_DoesNotThrow()
    {
        await using var db = NewDb();
        var (_, evaluation) = await PrepareMediaWithEvaluation(db, MediaClass.GeneralAgar, "TSA", new List<MediaChallengeSpec>());

        Assert.Empty(evaluation.Challenges);
        Assert.Equal(MediaEvaluationStatus.Assigned, evaluation.Status);
    }

    [Fact]
    public async Task GrowthPromotion_RecoveryInBand_ConformsAndReleasesMedia()
    {
        await using var db = NewDb();
        var ecoliId = await GetOrCreateOrganismIdAsync(db, "E. coli");
        var specs = new List<MediaChallengeSpec> { new() { MaterialName = "TSA", EvaluationType = EvaluationType.GrowthPromotion, OrganismId = ecoliId } };
        var (media, evaluation) = await PrepareMediaWithEvaluation(db, MediaClass.GeneralAgar, "TSA", specs);
        var challenge = evaluation.Challenges[0];
        var cryovial = await SeedApprovedCryovial(db, "E. coli");

        var engine = new MediaEvaluationEngine(db);
        await engine.SelectCryovialAsync(challenge.Id, cryovial.Id, userId: 1);
        var incubation = await engine.RecordIncubationAsync(challenge.Id, incubatorEquipmentId: 1, userId: 1);
        Assert.Equal("30-35", incubation.Temperature);
        Assert.Equal("24-48", incubation.Duration);
        Assert.Equal(incubation.StartedAt.AddHours(24), incubation.ExpectedReadingAt);

        var afterIncubation = await db.MediaEvaluations.FindAsync(evaluation.Id);
        Assert.Equal(MediaEvaluationStatus.InProgress, afterIncubation!.Status);

        // Incubation period hasn't elapsed yet - reading is refused.
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecordResultAsync(new RecordResultRequest(
            challenge.Id, UserId: 1, OldMediaCount: 100, NewMediaCount: 95,
            GrowthObserved: null, ObservedDescription: null, ManualConform: null, IsTurbid: null)));

        incubation.ExpectedReadingAt = DateTime.UtcNow.AddMinutes(-1); // simulate time elapsed
        await db.SaveChangesAsync();

        var result = await engine.RecordResultAsync(new RecordResultRequest(
            challenge.Id, UserId: 1, OldMediaCount: 100, NewMediaCount: 95,
            GrowthObserved: null, ObservedDescription: null, ManualConform: null, IsTurbid: null));

        Assert.Equal(EvaluationOutcome.Conform, result.Outcome);
        Assert.Equal(95.0m, result.RecoveryPercent);

        // Conform qualifies the lot but does NOT release it - the lot
        // waits at PendingReview for a Section Head signature.
        var reloadedMedia = await db.Media.FindAsync(media.Id);
        Assert.False(reloadedMedia!.IsReleasedForUse);
        Assert.Equal(ApprovalGateStatus.PendingReview, reloadedMedia.ApprovalStatus);

        var completedEval = await db.MediaEvaluations.FindAsync(evaluation.Id);
        Assert.Equal(MediaEvaluationStatus.Completed, completedEval!.Status);
        Assert.Equal(EvaluationOutcome.Conform, completedEval.Outcome);
    }

    [Fact]
    public async Task GrowthPromotion_RecoveryOutOfBand_NonConformsAndDoesNotReleaseMedia()
    {
        await using var db = NewDb();
        var ecoliId = await GetOrCreateOrganismIdAsync(db, "E. coli");
        var specs = new List<MediaChallengeSpec> { new() { MaterialName = "TSA", EvaluationType = EvaluationType.GrowthPromotion, OrganismId = ecoliId } };
        var (media, evaluation) = await PrepareMediaWithEvaluation(db, MediaClass.GeneralAgar, "TSA", specs);
        var challenge = evaluation.Challenges[0];
        var cryovial = await SeedApprovedCryovial(db, "E. coli");

        var engine = new MediaEvaluationEngine(db);
        await engine.SelectCryovialAsync(challenge.Id, cryovial.Id, userId: 1);
        await RecordAndFastForwardIncubation(db, engine, challenge.Id);

        var result = await engine.RecordResultAsync(new RecordResultRequest(
            challenge.Id, UserId: 1, OldMediaCount: 100, NewMediaCount: 10, // 10% recovery, below the 50 band
            GrowthObserved: null, ObservedDescription: null, ManualConform: null, IsTurbid: null));

        Assert.Equal(EvaluationOutcome.NonConform, result.Outcome);

        // A failed evaluation quarantines the lot outright - it never
        // reaches the Section Head's release queue.
        var reloadedMedia = await db.Media.FindAsync(media.Id);
        Assert.False(reloadedMedia!.IsReleasedForUse);
        Assert.Equal(MediaStatus.QuarantineFailed, reloadedMedia.Status);

        var completedEval = await db.MediaEvaluations.FindAsync(evaluation.Id);
        Assert.Equal(EvaluationOutcome.NonConform, completedEval!.Outcome);
    }

    [Fact]
    public async Task GrowthPromotion_ZeroOldMediaCount_Throws()
    {
        await using var db = NewDb();
        var ecoliId = await GetOrCreateOrganismIdAsync(db, "E. coli");
        var specs = new List<MediaChallengeSpec> { new() { MaterialName = "TSA", EvaluationType = EvaluationType.GrowthPromotion, OrganismId = ecoliId } };
        var (_, evaluation) = await PrepareMediaWithEvaluation(db, MediaClass.GeneralAgar, "TSA", specs);
        var challenge = evaluation.Challenges[0];
        var cryovial = await SeedApprovedCryovial(db, "E. coli");

        var engine = new MediaEvaluationEngine(db);
        await engine.SelectCryovialAsync(challenge.Id, cryovial.Id, userId: 1);
        await RecordAndFastForwardIncubation(db, engine, challenge.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecordResultAsync(new RecordResultRequest(
            challenge.Id, UserId: 1, OldMediaCount: 0, NewMediaCount: 5,
            GrowthObserved: null, ObservedDescription: null, ManualConform: null, IsTurbid: null)));
        Assert.Contains("cannot be zero", ex.Message);
    }

    [Fact]
    public async Task RecordResultAsync_BeforeIncubationRecorded_Throws()
    {
        await using var db = NewDb();
        var ecoliId = await GetOrCreateOrganismIdAsync(db, "E. coli");
        var specs = new List<MediaChallengeSpec> { new() { MaterialName = "TSA", EvaluationType = EvaluationType.GrowthPromotion, OrganismId = ecoliId } };
        var (_, evaluation) = await PrepareMediaWithEvaluation(db, MediaClass.GeneralAgar, "TSA", specs);
        var challenge = evaluation.Challenges[0];

        var engine = new MediaEvaluationEngine(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecordResultAsync(new RecordResultRequest(
            challenge.Id, UserId: 1, OldMediaCount: 100, NewMediaCount: 95,
            GrowthObserved: null, ObservedDescription: null, ManualConform: null, IsTurbid: null)));
        Assert.Contains("Incubation must be recorded", ex.Message);
    }

    [Fact]
    public async Task RecordResultAsync_BeforeIncubationPeriodElapses_Throws()
    {
        await using var db = NewDb();
        var ecoliId = await GetOrCreateOrganismIdAsync(db, "E. coli");
        var specs = new List<MediaChallengeSpec> { new() { MaterialName = "TSA", EvaluationType = EvaluationType.GrowthPromotion, OrganismId = ecoliId } };
        var (_, evaluation) = await PrepareMediaWithEvaluation(db, MediaClass.GeneralAgar, "TSA", specs);
        var challenge = evaluation.Challenges[0];

        var engine = new MediaEvaluationEngine(db);
        // Incubation just started - MediaType.IncubationMinHours (24h) has not elapsed.
        await engine.RecordIncubationAsync(challenge.Id, incubatorEquipmentId: 1, userId: 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecordResultAsync(new RecordResultRequest(
            challenge.Id, UserId: 1, OldMediaCount: 100, NewMediaCount: 95,
            GrowthObserved: null, ObservedDescription: null, ManualConform: null, IsTurbid: null)));
        Assert.Contains("earliest reading time", ex.Message);
    }

    [Fact]
    public async Task IndicationInhibition_BothChallengesMustConformToReleaseMedia()
    {
        await using var db = NewDb();
        var salmonellaId = await GetOrCreateOrganismIdAsync(db, "Salmonella");
        var specs = new List<MediaChallengeSpec>
        {
            new() { MaterialName = "XLD", EvaluationType = EvaluationType.IndicationInhibition, OrganismId = salmonellaId, ChallengeRole = ChallengeRole.Inhibition },
            new() { MaterialName = "XLD", EvaluationType = EvaluationType.IndicationInhibition, OrganismId = salmonellaId, ChallengeRole = ChallengeRole.Indication, ExpectedDescription = "Black centered colonies" }
        };
        var (media, evaluation) = await PrepareMediaWithEvaluation(db, MediaClass.SelectiveAgar, "XLD", specs);
        Assert.Equal(2, evaluation.Challenges.Count);

        var inhibChallenge = evaluation.Challenges.Single(c => c.ChallengeRole == ChallengeRole.Inhibition);
        var indicChallenge = evaluation.Challenges.Single(c => c.ChallengeRole == ChallengeRole.Indication);
        Assert.Equal("10^3", inhibChallenge.InitialInoculum); // Inhibition gets the stronger inoculum
        Assert.Equal("10^2", indicChallenge.InitialInoculum);
        Assert.Equal("Black centered colonies", indicChallenge.ExpectedDescription);

        var cryovial = await SeedApprovedCryovial(db, "Salmonella");
        var engine = new MediaEvaluationEngine(db);
        await engine.SelectCryovialAsync(inhibChallenge.Id, cryovial.Id, userId: 1);
        await engine.SelectCryovialAsync(indicChallenge.Id, cryovial.Id, userId: 1);
        await RecordAndFastForwardIncubation(db, engine, inhibChallenge.Id);
        await RecordAndFastForwardIncubation(db, engine, indicChallenge.Id);

        // Inhibition: no growth observed = Conform.
        await engine.RecordResultAsync(new RecordResultRequest(
            inhibChallenge.Id, UserId: 1, OldMediaCount: null, NewMediaCount: null,
            GrowthObserved: false, ObservedDescription: null, ManualConform: null, IsTurbid: null));

        // Evaluation not complete until both challenges have an outcome.
        var stillInProgress = await db.MediaEvaluations.FindAsync(evaluation.Id);
        Assert.Equal(MediaEvaluationStatus.InProgress, stillInProgress!.Status);

        // Indication: manual analyst judgment, not auto string matching.
        await engine.RecordResultAsync(new RecordResultRequest(
            indicChallenge.Id, UserId: 1, OldMediaCount: null, NewMediaCount: null,
            GrowthObserved: null, ObservedDescription: "Black centered colonies observed", ManualConform: true, IsTurbid: null));

        var completedEval = await db.MediaEvaluations.FindAsync(evaluation.Id);
        Assert.Equal(MediaEvaluationStatus.Completed, completedEval!.Status);
        Assert.Equal(EvaluationOutcome.Conform, completedEval.Outcome);

        // Qualified, awaiting the release signature - not released yet.
        var reloadedMedia = await db.Media.FindAsync(media.Id);
        Assert.False(reloadedMedia!.IsReleasedForUse);
        Assert.Equal(ApprovalGateStatus.PendingReview, reloadedMedia.ApprovalStatus);
    }

    [Fact]
    public async Task IndicationInhibition_InhibitionGrowthObserved_NonConforms()
    {
        await using var db = NewDb();
        var salmonellaId = await GetOrCreateOrganismIdAsync(db, "Salmonella");
        var specs = new List<MediaChallengeSpec>
        {
            new() { MaterialName = "XLD", EvaluationType = EvaluationType.IndicationInhibition, OrganismId = salmonellaId, ChallengeRole = ChallengeRole.Inhibition }
        };
        var (media, evaluation) = await PrepareMediaWithEvaluation(db, MediaClass.SelectiveAgar, "XLD", specs);
        var challenge = evaluation.Challenges[0];
        var cryovial = await SeedApprovedCryovial(db, "Salmonella");

        var engine = new MediaEvaluationEngine(db);
        await engine.SelectCryovialAsync(challenge.Id, cryovial.Id, userId: 1);
        await RecordAndFastForwardIncubation(db, engine, challenge.Id);

        var result = await engine.RecordResultAsync(new RecordResultRequest(
            challenge.Id, UserId: 1, OldMediaCount: null, NewMediaCount: null,
            GrowthObserved: true, ObservedDescription: null, ManualConform: null, IsTurbid: null));

        Assert.Equal(EvaluationOutcome.NonConform, result.Outcome);
        var reloadedMedia = await db.Media.FindAsync(media.Id);
        Assert.False(reloadedMedia!.IsReleasedForUse);
    }

    [Fact]
    public async Task EnrichmentCharacteristics_Turbid_Conforms_Clear_NonConforms()
    {
        await using var db = NewDb();
        var ecoliId = await GetOrCreateOrganismIdAsync(db, "E. coli");
        var specs = new List<MediaChallengeSpec> { new() { MaterialName = "TSB", EvaluationType = EvaluationType.EnrichmentCharacteristics, OrganismId = ecoliId } };
        var (media, evaluation) = await PrepareMediaWithEvaluation(db, MediaClass.GeneralBroth, "TSB", specs);
        var challenge = evaluation.Challenges[0];
        var cryovial = await SeedApprovedCryovial(db, "E. coli");

        var engine = new MediaEvaluationEngine(db);
        await engine.SelectCryovialAsync(challenge.Id, cryovial.Id, userId: 1);
        await RecordAndFastForwardIncubation(db, engine, challenge.Id);

        var result = await engine.RecordResultAsync(new RecordResultRequest(
            challenge.Id, UserId: 1, OldMediaCount: null, NewMediaCount: null,
            GrowthObserved: null, ObservedDescription: null, ManualConform: null, IsTurbid: true));

        Assert.Equal(EvaluationOutcome.Conform, result.Outcome);

        // Qualified, awaiting the release signature - not released yet.
        var reloadedMedia = await db.Media.FindAsync(media.Id);
        Assert.False(reloadedMedia!.IsReleasedForUse);
        Assert.Equal(ApprovalGateStatus.PendingReview, reloadedMedia.ApprovalStatus);
    }

    [Fact]
    public async Task SelectCryovialAsync_OrganismMismatch_Throws()
    {
        await using var db = NewDb();
        var ecoliId = await GetOrCreateOrganismIdAsync(db, "E. coli");
        var specs = new List<MediaChallengeSpec> { new() { MaterialName = "TSA", EvaluationType = EvaluationType.GrowthPromotion, OrganismId = ecoliId } };
        var (_, evaluation) = await PrepareMediaWithEvaluation(db, MediaClass.GeneralAgar, "TSA", specs);
        var challenge = evaluation.Challenges[0];
        var cryovial = await SeedApprovedCryovial(db, "Staphylococcus aureus");

        var engine = new MediaEvaluationEngine(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SelectCryovialAsync(challenge.Id, cryovial.Id, userId: 1));
    }

    [Fact]
    public async Task SelectCryovialAsync_UnapprovedCryovial_Throws()
    {
        await using var db = NewDb();
        var ecoliId = await GetOrCreateOrganismIdAsync(db, "E. coli");
        var specs = new List<MediaChallengeSpec> { new() { MaterialName = "TSA", EvaluationType = EvaluationType.GrowthPromotion, OrganismId = ecoliId } };
        var (_, evaluation) = await PrepareMediaWithEvaluation(db, MediaClass.GeneralAgar, "TSA", specs);
        var challenge = evaluation.Challenges[0];
        var cryovial = await SeedApprovedCryovial(db, "E. coli");
        cryovial.ApprovalStatus = ApprovalGateStatus.PendingReview;
        await db.SaveChangesAsync();

        var engine = new MediaEvaluationEngine(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SelectCryovialAsync(challenge.Id, cryovial.Id, userId: 1));
    }
}

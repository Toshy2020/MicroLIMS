using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Item master diluent / neutralizer sources, lot picking at Test Preparation, lot options, where used,
// reports and the unmapped list (plan 3.3-3.5, D9, D10).
public class PreparationMaterialTraceabilityTests
{
    private const string Password = "Correct-Horse-1!";
    private const int Analyst = 8;
    private const int OtherAnalyst = 9;

    private sealed class Ctx
    {
        public MicroLimsDbContext Db = null!;
        public DocumentSection Micro = null!;
        public DocumentSection Other = null!;
        public Item Item = null!;
        public SamplePreparationService Service = null!;
        public ItemPreparationConfigurationService Config = null!;
        public PreparationMaterialService Materials = null!;
    }

    // Analyst 8 belongs to Microbiology only; analyst 9 to Microbiology and a second lab.
    private static async Task<Ctx> SetUpAsync()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var role = new Role { Type = RoleType.Analyst, Name = "Analyst" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        foreach (var id in new[] { Analyst, OtherAnalyst })
            db.Users.Add(new User { Id = id, Username = $"user{id}", FullName = $"Analyst {id}", RoleId = role.Id, PasswordHash = TestPasswords.Hash(Password), IsActive = true });

        var micro = TestServiceFactory.EnsureMicroSection(db);
        var other = new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(other);
        var item = new Item { Name = "Example Tablet", Code = "FP-0001", Category = SampleCategory.FinishedProduct };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, Analyst);
        TestServiceFactory.AssignUserToMicroSection(db, OtherAnalyst);
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = OtherAnalyst, DepartmentId = other.DepartmentId, SectionId = other.Id });
        await db.SaveChangesAsync();

        return new Ctx
        {
            Db = db, Micro = micro, Other = other, Item = item,
            Service = TestServiceFactory.SamplePreparation(db),
            Config = TestServiceFactory.ItemPreparationConfiguration(db),
            Materials = TestServiceFactory.PreparationMaterial(db)
        };
    }

    private static async Task<ItemPreparationConfiguration> AddConfigAsync(
        Ctx c, PreparationMaterialChoice? diluent, PreparationMaterialChoice? neutralizer, Item? item = null)
    {
        var config = new ItemPreparationConfiguration
        {
            ItemId = (item ?? c.Item).Id, Amount = 10m, Technique = "PourPlate", Diluent = "Legacy diluent", Neutralizer = "Legacy neutralizer",
            ApprovalStatus = ApprovalGateStatus.Approved, CreatedByUserId = 1
        };
        if (diluent is not null) PreparationMaterialService.Apply(config, PreparationMaterialRole.Diluent, new(diluent, "Diluent name"));
        if (neutralizer is not null) PreparationMaterialService.Apply(config, PreparationMaterialRole.Neutralizer, new(neutralizer, "Neutralizer name"));
        c.Db.ItemPreparationConfigurations.Add(config);
        await c.Db.SaveChangesAsync();
        return config;
    }

    private static async Task<Sample> NewSampleAsync(Ctx c, string control, Item? item = null, int? sectionId = null)
    {
        var sample = new Sample
        {
            Category = SampleCategory.FinishedProduct, ItemId = (item ?? c.Item).Id, ControlNumber = control, ReferenceNumber = "REF-" + control,
            Status = SampleStatus.Received, PreparationStatus = SamplePreparationStatus.NeedsPreparation
        };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", SectionId = sectionId ?? c.Micro.Id, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });
        c.Db.Samples.Add(sample);
        await c.Db.SaveChangesAsync();
        return sample;
    }

    private static ConfirmPreparationRequest Confirm(Sample s, int? diluentLot = null, int? neutralizerLot = null, int user = Analyst) =>
        new(s.Id, user, Password, DiluentLotId: diluentLot, NeutralizerLotId: neutralizerLot);

    // ---- Configuration save rules -------------------------------------------------

    private static Task<ItemPreparationConfigurationDto> Save(Ctx c, PreparationMaterialChoice? d, PreparationMaterialChoice? n) =>
        c.Config.UpsertAsync(c.Item.Id, new PreparationParameters(10m, "PourPlate", null, null, "ignored text", "ignored text", d, n), userId: 1);

    [Fact]
    public async Task Upsert_StoresSourceAndFk_FillsNamesFromTheMaster_AndIgnoresClientText()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db, "Tryptic Soy Broth", "TSB");
        var recipe = await PreparationTestData.RecipeAsync(c.Db, "Letheen neutralizer", SolutionType.Neutralizer);

        var dto = await Save(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.SolutionChoice(recipe));

        Assert.Equal("Tryptic Soy Broth", dto.Diluent);
        Assert.Equal("Letheen neutralizer", dto.Neutralizer);
        Assert.Equal(PreparationMaterialSource.MediaProduct, dto.DiluentSource);
        Assert.Equal(tsb.Id, dto.DiluentMediaProductId);
        Assert.Null(dto.DiluentSolutionMasterId);
        Assert.Equal(PreparationMaterialSource.SolutionRecipe, dto.NeutralizerSource);
        Assert.Equal(recipe.Id, dto.NeutralizerSolutionMasterId);
        Assert.False(dto.NeedsMaterialMapping);
    }

    [Fact]
    public async Task Upsert_MaterialEntryDiluent_AndContainedInMediumNeutralizer_AreAccepted()
    {
        var c = await SetUpAsync();
        var entry = await PreparationTestData.EntryAsync(c.Db, "Peptone water", "PW-1");

        var dto = await Save(c, PreparationTestData.EntryChoice(entry), PreparationTestData.ContainedInMedium);

        Assert.Equal(PreparationMaterialSource.MaterialEntry, dto.DiluentSource);
        Assert.Equal(entry.Id, dto.DiluentMaterialMasterEntryId);
        Assert.Equal(PreparationMaterialSource.ContainedInMedium, dto.NeutralizerSource);
        Assert.Null(dto.NeutralizerMediaProductId);
        Assert.Null(dto.NeutralizerSolutionMasterId);
        Assert.Null(dto.NeutralizerMaterialMasterEntryId);
        Assert.Equal(PreparationMaterialService.ContainedInMediumName, dto.Neutralizer);
    }

    [Fact]
    public async Task Upsert_WithoutASource_IsRejected_ForBothRoles()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);

        var d = await Assert.ThrowsAsync<InvalidOperationException>(() => Save(c, null, PreparationTestData.ContainedInMedium));
        Assert.Contains("diluent", d.Message);
        var n = await Assert.ThrowsAsync<InvalidOperationException>(() => Save(c, PreparationTestData.MediaChoice(tsb), new PreparationMaterialChoice(null)));
        Assert.Contains("neutralizer", n.Message);
        Assert.Empty(c.Db.ItemPreparationConfigurations);
    }

    [Fact]
    public async Task Upsert_ContainedInMedium_IsForTheNeutralizerOnly()
    {
        var c = await SetUpAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Save(c, PreparationTestData.ContainedInMedium, PreparationTestData.ContainedInMedium));
        Assert.Contains("neutralizer", ex.Message);
    }

    [Fact]
    public async Task Upsert_SourceWithoutItsMaster_OrWithAnotherMastersId_IsRejected()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        var recipe = await PreparationTestData.RecipeAsync(c.Db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Save(c, new PreparationMaterialChoice(PreparationMaterialSource.MediaProduct), PreparationTestData.ContainedInMedium));
        // The ids of another source must stay empty.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Save(c, new PreparationMaterialChoice(PreparationMaterialSource.MediaProduct, MediaProductId: tsb.Id, SolutionMasterId: recipe.Id), PreparationTestData.ContainedInMedium));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Save(c, PreparationTestData.MediaChoice(tsb), new PreparationMaterialChoice(PreparationMaterialSource.ContainedInMedium, MediaProductId: tsb.Id)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Save(c, new PreparationMaterialChoice(PreparationMaterialSource.MediaProduct, MediaProductId: 9999), PreparationTestData.ContainedInMedium));
    }

    [Fact]
    public async Task Upsert_InactiveMasters_AreRejected()
    {
        var c = await SetUpAsync();
        var active = await PreparationTestData.ProductAsync(c.Db, "TSB", "TSB");
        var inactiveProduct = new MediaProduct { Name = "Old broth", Code = "OLD", IsActive = false };
        c.Db.MediaProducts.Add(inactiveProduct);
        await c.Db.SaveChangesAsync();
        var inactiveRecipe = await PreparationTestData.RecipeAsync(c.Db, "Old buffer", active: false);
        var inactiveEntry = await PreparationTestData.EntryAsync(c.Db, "Old salt", "OS-1", active: false);

        foreach (var choice in new[]
        {
            PreparationTestData.MediaChoice(inactiveProduct),
            PreparationTestData.SolutionChoice(inactiveRecipe),
            PreparationTestData.EntryChoice(inactiveEntry)
        })
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Save(c, choice, PreparationTestData.ContainedInMedium));
            Assert.Contains("inactive", ex.Message);
        }
        // The neutralizer role is checked the same way.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Save(c, PreparationTestData.MediaChoice(active), PreparationTestData.MediaChoice(inactiveProduct)));
    }

    [Fact]
    public async Task Upsert_RecipeMustBeAGeneralType()
    {
        var c = await SetUpAsync();
        var mobilePhase = await PreparationTestData.RecipeAsync(c.Db, "Mobile phase A", SolutionType.MobilePhase);
        var titrant = await PreparationTestData.RecipeAsync(c.Db, "NaOH 0.1 N", SolutionType.Titrant);

        foreach (var recipe in new[] { mobilePhase, titrant })
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Save(c, PreparationTestData.SolutionChoice(recipe), PreparationTestData.ContainedInMedium));
            Assert.Contains("Diluent, Reagent or Neutralizer", ex.Message);
        }

        foreach (var type in new[] { SolutionType.Diluent, SolutionType.Reagent, SolutionType.Neutralizer })
        {
            var ok = await PreparationTestData.RecipeAsync(c.Db, "General " + type, type);
            var dto = await Save(c, PreparationTestData.SolutionChoice(ok), PreparationTestData.ContainedInMedium);
            Assert.Equal(ok.Id, dto.DiluentSolutionMasterId);
        }
    }

    [Fact]
    public async Task Upsert_ChangingAnApprovedConfiguration_ResetsApproval()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db, "TSB", "TSB");
        var bpw = await PreparationTestData.ProductAsync(c.Db, "BPW", "BPW");
        await Save(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.ContainedInMedium);
        var approved = await c.Config.ApproveAsync(c.Item.Id, userId: 3);
        Assert.Equal(ApprovalGateStatus.Approved, approved.ApprovalStatus);

        var dto = await Save(c, PreparationTestData.MediaChoice(bpw), PreparationTestData.ContainedInMedium);

        Assert.Equal(ApprovalGateStatus.PendingReview, dto.ApprovalStatus);
        Assert.Null(dto.ApprovedByUserId);
        Assert.Equal("BPW", dto.Diluent);
    }

    [Fact]
    public async Task Unmapped_ListsConfigurationsWithLegacyText_AndMapping_RemovesThem()
    {
        var c = await SetUpAsync();
        var otherItem = new Item { Name = "Other Tablet", Code = "FP-0002", Category = SampleCategory.FinishedProduct };
        c.Db.Items.Add(otherItem);
        await c.Db.SaveChangesAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        await AddConfigAsync(c, null, null);                                   // fully legacy
        await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), null, otherItem);   // neutralizer still legacy

        var unmapped = await c.Config.GetUnmappedAsync();
        Assert.Equal(2, unmapped.Count);
        Assert.All(unmapped, u => Assert.True(u.NeedsMaterialMapping));
        Assert.Contains(unmapped, u => u.ItemName == "Example Tablet");

        await c.Config.UpsertAsync(c.Item.Id, new PreparationParameters(10m, "PourPlate", null, null, "", "", PreparationTestData.MediaChoice(tsb), PreparationTestData.ContainedInMedium), 1);
        Assert.Equal(otherItem.Id, (await c.Config.GetUnmappedAsync()).Single().ItemId);
    }

    // ---- Confirmation: each source ---------------------------------------------

    [Fact]
    public async Task Confirm_MediaProduct_RecordsTheLot_AndSignsWithItsDetails()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        var lot = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/03/26");
        await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.ContainedInMedium);
        var sample = await NewSampleAsync(c, "C-1");

        var prep = await c.Service.ConfirmFromConfigurationAsync(Confirm(sample, lot.Id));

        Assert.Equal(PreparationMaterialSource.MediaProduct, prep.DiluentSource);
        Assert.Equal(lot.Id, prep.DiluentMediaId);
        Assert.Equal("TSB/03/26", prep.DiluentLotCode);
        Assert.Equal(PreparationMaterialSource.ContainedInMedium, prep.NeutralizerSource);
        Assert.Null(prep.NeutralizerLotCode);
        Assert.False(prep.NeedsMaterialMapping);

        var stored = await c.Db.SamplePreparations.AsNoTracking().SingleAsync();
        Assert.Equal(lot.Id, stored.DiluentMediaId);
        var signature = await c.Db.ElectronicSignatures.SingleAsync();
        Assert.Contains("lot TSB/03/26", signature.Comment);
        Assert.Contains("MediaProduct", signature.Comment);
    }

    [Fact]
    public async Task Confirm_SolutionRecipe_RecordsThePreparation_AndDeductsNothing()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        var recipe = await PreparationTestData.RecipeAsync(c.Db, "Letheen neutralizer", SolutionType.Neutralizer);
        var released = await PreparationTestData.SolutionLotAsync(c.Db, recipe, "NT-0001/11/2026", approval: ApprovalGateStatus.Approved);
        var noRelease = await PreparationTestData.SolutionLotAsync(c.Db, recipe, "NT-0002/11/2026", approval: null);
        var before = await c.Db.SolutionPreparations.AsNoTracking().Select(p => new { p.Id, p.FinalVolumeMl }).ToListAsync();
        var media = await PreparationTestData.MediaLotAsync(c.Db, tsb);
        await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.SolutionChoice(recipe));

        var first = await c.Service.ConfirmFromConfigurationAsync(Confirm(await NewSampleAsync(c, "C-2"), media.Id, released.Id));
        var second = await c.Service.ConfirmFromConfigurationAsync(Confirm(await NewSampleAsync(c, "C-3"), media.Id, noRelease.Id));

        Assert.Equal(released.Id, first.NeutralizerSolutionPreparationId);
        Assert.Equal("NT-0001/11/2026", first.NeutralizerLotCode);
        Assert.Equal(noRelease.Id, second.NeutralizerSolutionPreparationId);
        Assert.Equal(before, await c.Db.SolutionPreparations.AsNoTracking().Select(p => new { p.Id, p.FinalVolumeMl }).ToListAsync());
    }

    [Fact]
    public async Task Confirm_MaterialEntry_RecordsTheStockLot_AndDeductsNothing()
    {
        var c = await SetUpAsync();
        var entry = await PreparationTestData.EntryAsync(c.Db);
        var lot = await PreparationTestData.StockLotAsync(c.Db, entry, "STK-2026-7", remaining: 10);
        await AddConfigAsync(c, PreparationTestData.EntryChoice(entry), PreparationTestData.ContainedInMedium);

        var prep = await c.Service.ConfirmFromConfigurationAsync(Confirm(await NewSampleAsync(c, "C-4"), lot.Id));

        Assert.Equal(PreparationMaterialSource.MaterialEntry, prep.DiluentSource);
        Assert.Equal(lot.Id, prep.DiluentMaterialId);
        Assert.Equal("STK-2026-7", prep.DiluentLotCode);
        Assert.Equal(10, (await c.Db.Materials.AsNoTracking().SingleAsync(m => m.Id == lot.Id)).QuantityRemaining);
    }

    // ---- Confirmation: invalid lots -----------------------------------------------

    private static async Task<InvalidOperationException> ConfirmFails(Ctx c, int? diluentLot, int? neutralizerLot = null, int user = Analyst)
    {
        var sample = await NewSampleAsync(c, "F-" + Guid.NewGuid().ToString("N")[..6]);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Service.ConfirmFromConfigurationAsync(Confirm(sample, diluentLot, neutralizerLot, user)));
        Assert.Empty(await c.Db.SamplePreparations.ToListAsync());
        Assert.Empty(await c.Db.ElectronicSignatures.ToListAsync());
        return ex;
    }

    [Fact]
    public async Task Confirm_MediaLot_MustBeReleasedActiveUnexpiredAndOfTheProduct()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db, "TSB", "TSB");
        var bpw = await PreparationTestData.ProductAsync(c.Db, "BPW", "BPW");
        await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.ContainedInMedium);

        var unreleased = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/U", released: false, status: MediaStatus.Prepared);
        var expired = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/E", expiry: DateTime.UtcNow.AddDays(-1));
        var failed = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/Q", status: MediaStatus.QuarantineFailed);
        var wrongProduct = await PreparationTestData.MediaLotAsync(c.Db, bpw, "BPW/1");

        Assert.Contains("not released for use", (await ConfirmFails(c, unreleased.Id)).Message);
        Assert.Contains("expired", (await ConfirmFails(c, expired.Id)).Message);
        Assert.Contains("QuarantineFailed", (await ConfirmFails(c, failed.Id)).Message);
        Assert.Contains("not a lot of TSB", (await ConfirmFails(c, wrongProduct.Id)).Message);
        Assert.Contains("not found", (await ConfirmFails(c, 9999)).Message);
    }

    [Fact]
    public async Task Confirm_SolutionLot_MustBePreparedUnexpiredReleasedAndOfTheRecipe()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        var media = await PreparationTestData.MediaLotAsync(c.Db, tsb);
        var recipe = await PreparationTestData.RecipeAsync(c.Db, "Letheen", SolutionType.Neutralizer);
        var otherRecipe = await PreparationTestData.RecipeAsync(c.Db, "Saline", SolutionType.Diluent);
        await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.SolutionChoice(recipe));

        var pending = await PreparationTestData.SolutionLotAsync(c.Db, recipe, "NT-P", approval: ApprovalGateStatus.PendingReview);
        var rejected = await PreparationTestData.SolutionLotAsync(c.Db, recipe, "NT-R", approval: ApprovalGateStatus.Rejected);
        var expired = await PreparationTestData.SolutionLotAsync(c.Db, recipe, "NT-X", expiresAt: DateTime.UtcNow.AddDays(-1));
        var inProgress = await PreparationTestData.SolutionLotAsync(c.Db, recipe, "NT-I", status: SolutionPreparationStatus.InProgress);
        var discarded = await PreparationTestData.SolutionLotAsync(c.Db, recipe, "NT-D", status: SolutionPreparationStatus.Discarded);
        var wrongRecipe = await PreparationTestData.SolutionLotAsync(c.Db, otherRecipe, "DL-W");

        Assert.Contains("awaiting release", (await ConfirmFails(c, media.Id, pending.Id)).Message);
        Assert.Contains("rejected", (await ConfirmFails(c, media.Id, rejected.Id)).Message);
        Assert.Contains("expired", (await ConfirmFails(c, media.Id, expired.Id)).Message);
        Assert.Contains("InProgress", (await ConfirmFails(c, media.Id, inProgress.Id)).Message);
        Assert.Contains("Discarded", (await ConfirmFails(c, media.Id, discarded.Id)).Message);
        Assert.Contains("not a preparation of the configured", (await ConfirmFails(c, media.Id, wrongRecipe.Id)).Message);
    }

    [Fact]
    public async Task Confirm_StockLot_MustBeInStockUnexpiredReleasedAndOfTheEntry()
    {
        var c = await SetUpAsync();
        var entry = await PreparationTestData.EntryAsync(c.Db, "Peptone water", "PW-1");
        var otherEntry = await PreparationTestData.EntryAsync(c.Db, "Saline", "NACL-1");
        await AddConfigAsync(c, PreparationTestData.EntryChoice(entry), PreparationTestData.ContainedInMedium);

        var expired = await PreparationTestData.StockLotAsync(c.Db, entry, "STK-X", expiry: DateTime.UtcNow.AddDays(-2));
        var depleted = await PreparationTestData.StockLotAsync(c.Db, entry, "STK-D", remaining: 0);
        var awaiting = await PreparationTestData.StockLotAsync(c.Db, entry, "STK-A", approval: ApprovalGateStatus.PendingReview);
        var rejected = await PreparationTestData.StockLotAsync(c.Db, entry, "STK-R", approval: ApprovalGateStatus.Rejected);
        var wrongEntry = await PreparationTestData.StockLotAsync(c.Db, otherEntry, "STK-W");

        Assert.Contains("Expired", (await ConfirmFails(c, expired.Id)).Message);
        Assert.Contains("No stock left", (await ConfirmFails(c, depleted.Id)).Message);
        Assert.Contains("awaiting release", (await ConfirmFails(c, awaiting.Id)).Message);
        Assert.Contains("rejected", (await ConfirmFails(c, rejected.Id)).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STK-W", (await ConfirmFails(c, wrongEntry.Id)).Message);
    }

    [Fact]
    public async Task Confirm_LotInASectionTheAnalystCannotRead_IsRefused()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        var otherLabLot = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/FP", sectionId: c.Other.Id);
        await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.ContainedInMedium);

        var ex = await ConfirmFails(c, otherLabLot.Id);
        Assert.Contains("section you can't access", ex.Message);

        // An analyst who belongs to that laboratory can use it.
        var sample = await NewSampleAsync(c, "C-OK");
        var prep = await c.Service.ConfirmFromConfigurationAsync(Confirm(sample, otherLabLot.Id, user: OtherAnalyst));
        Assert.Equal(otherLabLot.Id, prep.DiluentMediaId);
    }

    [Fact]
    public async Task Confirm_WithASource_RequiresTheLot()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        var recipe = await PreparationTestData.RecipeAsync(c.Db, "Letheen", SolutionType.Neutralizer);
        var media = await PreparationTestData.MediaLotAsync(c.Db, tsb);
        await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.SolutionChoice(recipe));

        Assert.Equal("Pick the diluent lot used.", (await ConfirmFails(c, null)).Message);
        Assert.Equal("Pick the neutralizer lot used.", (await ConfirmFails(c, media.Id, null)).Message);
    }

    [Fact]
    public async Task Confirm_LotSentForALegacyOrContainedInMediumRole_IsRefused()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        var media = await PreparationTestData.MediaLotAsync(c.Db, tsb);

        // Neutralizer contained in the medium takes no lot.
        await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.ContainedInMedium);
        Assert.Contains("contained in the medium", (await ConfirmFails(c, media.Id, media.Id)).Message);

        // A legacy (unmapped) configuration takes no lot at all.
        var legacyItem = new Item { Name = "Legacy Tablet", Code = "FP-0003", Category = SampleCategory.FinishedProduct };
        c.Db.Items.Add(legacyItem);
        await c.Db.SaveChangesAsync();
        await AddConfigAsync(c, null, null, legacyItem);
        var sample = await NewSampleAsync(c, "L-1", legacyItem);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Service.ConfirmFromConfigurationAsync(Confirm(sample, media.Id)));
        Assert.Contains("not linked to a master record", ex.Message);
    }

    [Fact]
    public async Task Confirm_OnALegacyConfiguration_StillWorksWithNoLot_AndFlagsMapping()
    {
        var c = await SetUpAsync();
        await AddConfigAsync(c, null, null);
        var sample = await NewSampleAsync(c, "L-2");

        var prep = await c.Service.ConfirmFromConfigurationAsync(Confirm(sample));

        Assert.Equal("Legacy diluent", prep.Diluent);
        Assert.Null(prep.DiluentSource);
        Assert.Null(prep.DiluentLotCode);
        Assert.True(prep.NeedsMaterialMapping);
        Assert.Equal(SamplePreparationStatus.Ready, (await c.Db.Samples.AsNoTracking().SingleAsync()).PreparationStatus);
    }

    [Fact]
    public async Task ManualPrepare_RequiresASource_AndTheLot()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        var lot = await PreparationTestData.MediaLotAsync(c.Db, tsb);
        var sample = await NewSampleAsync(c, "M-1");
        PrepareSampleRequest Req(PreparationMaterialChoice? d, int? dLot) => new(
            sample.Id, 10m, "PourPlate", null, null, "free text", "free text", Analyst, Password,
            d, PreparationTestData.ContainedInMedium, DiluentLotId: dLot);

        var noSource = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Service.PrepareAsync(Req(null, null)));
        Assert.Contains("Choose where the diluent comes from", noSource.Message);
        var noLot = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Service.PrepareAsync(Req(PreparationTestData.MediaChoice(tsb), null)));
        Assert.Equal("Pick the diluent lot used.", noLot.Message);

        var prep = await c.Service.PrepareAsync(Req(PreparationTestData.MediaChoice(tsb), lot.Id));
        Assert.Equal("TSB", prep.Diluent);                           // client text ignored
        Assert.Equal(lot.Id, prep.DiluentMediaId);
        var config = await c.Db.ItemPreparationConfigurations.SingleAsync();
        Assert.Equal(tsb.Id, config.DiluentMediaProductId);          // seeds the standing configuration
        Assert.Equal(ApprovalGateStatus.PendingReview, config.ApprovalStatus);
    }

    // ---- Grouped batch: one lot pair for the whole group (D10) ------------------

    [Fact]
    public async Task BatchConfirm_AppliesTheOneLotPairToEverySample()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        var recipe = await PreparationTestData.RecipeAsync(c.Db, "Letheen", SolutionType.Neutralizer);
        var media = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/09/26");
        var neutralizer = await PreparationTestData.SolutionLotAsync(c.Db, recipe, "NT-9/2026");
        var config = await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.SolutionChoice(recipe));
        var samples = new List<Sample> { await NewSampleAsync(c, "G-1"), await NewSampleAsync(c, "G-2"), await NewSampleAsync(c, "G-3") };

        var groups = await c.Service.GetGroupedPreparationAsync(samples.Select(s => s.Id).ToList(), Analyst);
        var group = Assert.Single(groups.Groups);
        Assert.Equal(PreparationMaterialSource.MediaProduct, group.DiluentSource);
        Assert.Equal(PreparationMaterialSource.SolutionRecipe, group.NeutralizerSource);
        Assert.False(group.NeedsMaterialMapping);

        var result = await c.Service.ConfirmBatchFromConfigurationAsync(
            new BatchConfirmPreparationRequest(samples.Select(s => s.Id).ToList(), config.Id, Password, media.Id, neutralizer.Id), Analyst);

        Assert.Equal(3, result.SucceededCount);
        var stored = await c.Db.SamplePreparations.AsNoTracking().ToListAsync();
        Assert.Equal(3, stored.Count);
        Assert.All(stored, p =>
        {
            Assert.Equal(media.Id, p.DiluentMediaId);
            Assert.Equal(neutralizer.Id, p.NeutralizerSolutionPreparationId);
            Assert.Equal("TSB/09/26", p.DiluentLotCode);
        });
    }

    [Fact]
    public async Task BatchConfirm_WithAnInvalidOrMissingLot_FailsTheRunBeforeAnySignature()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        var unreleased = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/U", released: false);
        var config = await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.ContainedInMedium);
        var samples = new List<int> { (await NewSampleAsync(c, "G-4")).Id, (await NewSampleAsync(c, "G-5")).Id };

        var bad = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Service.ConfirmBatchFromConfigurationAsync(
            new BatchConfirmPreparationRequest(samples, config.Id, Password, unreleased.Id, null), Analyst));
        Assert.Contains("not released for use", bad.Message);
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Service.ConfirmBatchFromConfigurationAsync(
            new BatchConfirmPreparationRequest(samples, config.Id, Password), Analyst));
        Assert.Equal("Pick the diluent lot used.", missing.Message);

        Assert.Empty(await c.Db.SamplePreparations.ToListAsync());
        Assert.Empty(await c.Db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task BatchConfirm_OnALegacyConfiguration_StillWorksAndGroupFlagsMapping()
    {
        var c = await SetUpAsync();
        var config = await AddConfigAsync(c, null, null);
        var samples = new List<int> { (await NewSampleAsync(c, "G-6")).Id };

        var groups = await c.Service.GetGroupedPreparationAsync(samples, Analyst);
        Assert.True(groups.Groups.Single().NeedsMaterialMapping);

        var result = await c.Service.ConfirmBatchFromConfigurationAsync(new BatchConfirmPreparationRequest(samples, config.Id, Password), Analyst);
        Assert.Equal(1, result.SucceededCount);
    }

    // ---- Lot options ---------------------------------------------------------------

    [Fact]
    public async Task LotOptions_ListUsableAndUnusableLots_WithReasons_OfTheReadableSections()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        var good = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/G", expiry: DateTime.UtcNow.AddDays(20));
        var unreleased = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/U", released: false, status: MediaStatus.Prepared, expiry: DateTime.UtcNow.AddDays(10));
        await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/X", expiry: DateTime.UtcNow.AddDays(-3));              // expired: not listed
        await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/D", status: MediaStatus.Destroyed);                     // destroyed: not listed
        await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/FP", sectionId: c.Other.Id);                            // other lab: not listed
        var recipe = await PreparationTestData.RecipeAsync(c.Db, "Letheen", SolutionType.Neutralizer);
        var okPrep = await PreparationTestData.SolutionLotAsync(c.Db, recipe, "NT-1", approval: ApprovalGateStatus.Approved);
        var pendingPrep = await PreparationTestData.SolutionLotAsync(c.Db, recipe, "NT-2", approval: ApprovalGateStatus.PendingReview);
        await PreparationTestData.SolutionLotAsync(c.Db, recipe, "NT-3", expiresAt: DateTime.UtcNow.AddDays(-1));       // expired: not listed
        await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.SolutionChoice(recipe));
        var sample = await NewSampleAsync(c, "O-1");

        var options = await c.Service.GetLotOptionsForSampleAsync(sample.Id, Analyst);

        Assert.Equal(PreparationMaterialSource.MediaProduct, options.Diluent.Source);
        Assert.Equal("Diluent name", options.Diluent.Name);
        Assert.True(options.Diluent.LotRequired);
        Assert.Equal(new[] { "TSB/U", "TSB/G" }, options.Diluent.Options.Select(o => o.Code).ToArray());     // soonest expiry first
        Assert.True(options.Diluent.Options.Single(o => o.Id == good.Id).Usable);
        var bad = options.Diluent.Options.Single(o => o.Id == unreleased.Id);
        Assert.False(bad.Usable);
        Assert.Contains("not released", bad.Reason);

        Assert.Equal(new[] { okPrep.Id, pendingPrep.Id }, options.Neutralizer.Options.Select(o => o.Id).OrderBy(i => i).ToArray());
        Assert.True(options.Neutralizer.Options.Single(o => o.Id == okPrep.Id).Usable);
        Assert.Contains("awaiting release", options.Neutralizer.Options.Single(o => o.Id == pendingPrep.Id).Reason);

        // Same answer by configuration for the grouped path; an analyst of both labs also sees the other lab's lot.
        var byConfig = await c.Service.GetLotOptionsForConfigurationAsync(options.ConfigurationId, OtherAnalyst);
        Assert.Contains(byConfig.Diluent.Options, o => o.Code == "TSB/FP");
    }

    [Fact]
    public async Task LotOptions_ContainedInMediumAndLegacy_ListNothingAndRequireNothing()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        await PreparationTestData.MediaLotAsync(c.Db, tsb);
        await AddConfigAsync(c, null, PreparationTestData.ContainedInMedium);
        var sample = await NewSampleAsync(c, "O-2");

        var options = await c.Service.GetLotOptionsForSampleAsync(sample.Id, Analyst);

        Assert.True(options.NeedsMaterialMapping);
        Assert.False(options.Diluent.LotRequired);
        Assert.Null(options.Diluent.Source);
        Assert.Empty(options.Diluent.Options);
        Assert.False(options.Neutralizer.LotRequired);
        Assert.Equal(PreparationMaterialSource.ContainedInMedium, options.Neutralizer.Source);
        Assert.Empty(options.Neutralizer.Options);
    }

    [Fact]
    public async Task LotOptions_ForAChosenMaster_ServeTheManualFirstEntry()
    {
        var c = await SetUpAsync();
        var entry = await PreparationTestData.EntryAsync(c.Db);
        var lot = await PreparationTestData.StockLotAsync(c.Db, entry, "STK-1");

        var group = await c.Materials.GetLotGroupForMasterAsync(PreparationMaterialSource.MaterialEntry, entry.Id, Analyst);

        Assert.Equal(entry.Name, group.Name);
        Assert.Equal(lot.Id, Assert.Single(group.Options).Id);
        Assert.True(group.LotRequired);
    }

    // ---- Where used -----------------------------------------------------------------

    [Fact]
    public async Task WhereUsed_ListsTheSamplesThatUsedTheLot_InEitherRole_LimitedToReadableSamples()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db);
        var lot = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/W");
        var otherLot = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/V");
        await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.MediaChoice(tsb));

        var asDiluent = await NewSampleAsync(c, "W-1");
        var asNeutralizer = await NewSampleAsync(c, "W-2");
        var both = await NewSampleAsync(c, "W-3");
        var unrelated = await NewSampleAsync(c, "W-4");
        var hiddenFromAnalyst = await NewSampleAsync(c, "W-5", sectionId: c.Other.Id);
        await c.Service.ConfirmFromConfigurationAsync(Confirm(asDiluent, lot.Id, otherLot.Id));
        await c.Service.ConfirmFromConfigurationAsync(Confirm(asNeutralizer, otherLot.Id, lot.Id));
        await c.Service.ConfirmFromConfigurationAsync(Confirm(both, lot.Id, lot.Id));
        await c.Service.ConfirmFromConfigurationAsync(Confirm(unrelated, otherLot.Id, otherLot.Id));
        await c.Service.ConfirmFromConfigurationAsync(Confirm(hiddenFromAnalyst, lot.Id, otherLot.Id, user: OtherAnalyst));

        var rows = await c.Materials.GetWhereUsedAsync(lot.Id, null, null, Analyst);

        Assert.Equal(
            new[] { ("REF-W-1", "Diluent"), ("REF-W-2", "Neutralizer"), ("REF-W-3", "Diluent"), ("REF-W-3", "Neutralizer") },
            rows.Select(r => (r.SampleNumber, r.Role)).OrderBy(r => r.SampleNumber).ThenBy(r => r.Role).ToArray());
        Assert.All(rows, r => Assert.Equal("Example Tablet", r.ItemName));
        Assert.All(rows, r => Assert.Equal("Analyst 8", r.PreparedByName));

        // An analyst who also belongs to the other lab sees the sample prepared there too.
        var wide = await c.Materials.GetWhereUsedAsync(lot.Id, null, null, OtherAnalyst);
        Assert.Contains(wide, r => r.SampleNumber == "REF-W-5");
    }

    [Fact]
    public async Task WhereUsed_WorksForSolutionAndStockLots_AndNeedsExactlyOneId()
    {
        var c = await SetUpAsync();
        var recipe = await PreparationTestData.RecipeAsync(c.Db, "Saline", SolutionType.Diluent);
        var entry = await PreparationTestData.EntryAsync(c.Db);
        var solution = await PreparationTestData.SolutionLotAsync(c.Db, recipe, "DL-1");
        var stock = await PreparationTestData.StockLotAsync(c.Db, entry, "STK-9");
        await AddConfigAsync(c, PreparationTestData.SolutionChoice(recipe), PreparationTestData.EntryChoice(entry));
        var sample = await NewSampleAsync(c, "W-6");
        await c.Service.ConfirmFromConfigurationAsync(Confirm(sample, solution.Id, stock.Id));

        var bySolution = Assert.Single(await c.Materials.GetWhereUsedAsync(null, solution.Id, null, Analyst));
        Assert.Equal("Diluent", bySolution.Role);
        var byStock = Assert.Single(await c.Materials.GetWhereUsedAsync(null, null, stock.Id, Analyst));
        Assert.Equal("Neutralizer", byStock.Role);

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Materials.GetWhereUsedAsync(null, null, null, Analyst));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Materials.GetWhereUsedAsync(1, solution.Id, null, Analyst));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            var tsb = await PreparationTestData.ProductAsync(c.Db);
            var otherLabLot = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/FP2", sectionId: c.Other.Id);
            await c.Materials.GetWhereUsedAsync(otherLabLot.Id, null, null, Analyst);
        });
    }

    // ---- Reports ------------------------------------------------------------------------

    [Fact]
    public async Task SampleSummary_ShowsTheLotCodesBesideTheDiluentAndNeutralizer()
    {
        var c = await SetUpAsync();
        var tsb = await PreparationTestData.ProductAsync(c.Db, "TSB", "TSB");
        var lot = await PreparationTestData.MediaLotAsync(c.Db, tsb, "TSB/03/26");
        await AddConfigAsync(c, PreparationTestData.MediaChoice(tsb), PreparationTestData.ContainedInMedium);
        var sample = await NewSampleAsync(c, "R-1");
        // The summary joins the cause of testing (a required relationship).
        var cause = new CauseOfTesting { Name = "Routine" };
        c.Db.CausesOfTesting.Add(cause);
        await c.Db.SaveChangesAsync();
        sample.CauseOfTestingId = cause.Id;
        await c.Db.SaveChangesAsync();
        var prep = await c.Service.ConfirmFromConfigurationAsync(Confirm(sample, lot.Id));

        var summary = await TestServiceFactory.SampleSummary(c.Db).GetSummaryAsync(sample.Id);

        Assert.NotNull(summary);
        Assert.NotNull(summary.Preparation);
        Assert.Equal("TSB/03/26", summary!.Preparation!.DiluentLotCode);
        Assert.Equal("MediaProduct", summary.Preparation.DiluentSource);
        Assert.Null(summary.Preparation.NeutralizerLotCode);
        Assert.Equal(prep.Diluent, summary.Preparation.Diluent);

        var document = MicroLIMS.Application.Services.ReportDocumentMapper.ForSample(summary);
        var strip = document.Blocks.OfType<MicroLIMS.Application.Abstractions.Pdf.StripBlock>().Single(b => b.Label == "sample preparation");
        Assert.Contains(strip.Items, i => i.Label == "Diluent" && i.Sub == "lot TSB/03/26");
        Assert.Contains(strip.Items, i => i.Label == "Neutralizer" && i.Sub == null);

        var trace = await new AuditTraceabilityService(c.Db).GetTraceabilityForEntityAsync("Sample", sample.Id.ToString());
        var node = trace!.Nodes.Single(n => n.NodeType == "SamplePreparation");
        Assert.Contains("Diluent name — lot TSB/03/26", node.Description);
    }
}

using System.Globalization;
using MicroLIMS.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

// Master Testing Materials phase 3: reference organisms (strains), lyophilized receipt against a strain,
// cryovials that copy it, organism master guards and culture media deactivation.
public class ReferenceOrganismTests
{
    private const string Password = "Correct-Password-1!";
    private static readonly string Yy = DateTime.UtcNow.ToString("yy", CultureInfo.InvariantCulture);

    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CurrentUserId = 1;
        return db;
    }

    private record World(MicroLimsDbContext Db, DocumentSection Micro, int UserId, int OtherLabUserId);

    // A Microbiology analyst (the owner lab) and an analyst of another lab.
    private static async Task<World> SeedAsync()
    {
        var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var other = new DocumentSection { Name = "Other Lab", Code = "OTHER", DepartmentId = micro.DepartmentId, IsActive = true };
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.DocumentSections.Add(other);
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        User NewUser(string name) => new() { Username = name, FullName = name, RoleId = role.Id, IsActive = true, PasswordHash = TestPasswords.Hash(Password) };
        var user = NewUser("owner");
        var otherUser = NewUser("outsider");
        db.Users.AddRange(user, otherUser);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = otherUser.Id, DepartmentId = other.DepartmentId, SectionId = other.Id });
        await db.SaveChangesAsync();
        return new World(db, micro, user.Id, otherUser.Id);
    }

    private static async Task<Organism> AddOrganismAsync(MicroLimsDbContext db, string name, string? atcc = null)
    {
        var organism = new Organism { ScientificName = name, AtccNumber = atcc };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();
        return organism;
    }

    private static SaveReferenceOrganismRequest Strain(int organismId, string code = "ORG-001", CultureCollection collection = CultureCollection.ATCC,
        string? number = "8739", int? sectionId = null) => new(code, organismId, collection, number, sectionId);

    private static SaveMaterialRequest Receive(int? strainId, int? organismId = null, string? atcc = null, string name = "Free text",
        string? code = "FREE", MaterialType type = MaterialType.LyophilizedMicroorganism) =>
        new(type, name, "Microbiologics", "LOT-" + Guid.NewGuid().ToString("N")[..6],
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddYears(1), code, "Freezer", 10m, MaterialUnit.Disc, null, atcc, organismId,
            ReferenceOrganismId: strainId);

    // ---- Strain master -------------------------------------------------------------------

    [Fact]
    public async Task Create_TrimsUppercasesCode_BuildsName_AndEnforcesPrefix()
    {
        var w = await SeedAsync();
        var ecoli = await AddOrganismAsync(w.Db, "Escherichia coli");
        var service = TestServiceFactory.ReferenceOrganism(w.Db);

        var created = await service.CreateAsync(Strain(ecoli.Id, " org-001 ", number: " 8739 "), w.UserId);

        Assert.Equal("ORG-001", created.Code);
        Assert.Equal("8739", created.StrainNumber);
        Assert.Equal("Escherichia coli ATCC 8739", created.Name);
        Assert.Equal(w.Micro.Id, created.OwnerSectionId);
        Assert.True(created.IsActive);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Strain(ecoli.Id, "RG-002", number: "1"), w.UserId));
        Assert.Contains("ORG-", ex.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Strain(ecoli.Id, "ORG-", number: "1"), w.UserId));
    }

    [Fact]
    public async Task Create_StrainNumberRequiredUnlessCollectionIsOther()
    {
        var w = await SeedAsync();
        var candida = await AddOrganismAsync(w.Db, "Candida albicans");
        var service = TestServiceFactory.ReferenceOrganism(w.Db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Strain(candida.Id, "ORG-001", CultureCollection.NCTC, " "), w.UserId));
        var inHouse = await service.CreateAsync(Strain(candida.Id, "ORG-002", CultureCollection.Other, null), w.UserId);

        Assert.Null(inHouse.StrainNumber);
        Assert.Equal("Candida albicans", inHouse.Name);
    }

    [Fact]
    public async Task Create_DuplicateCodeOrCollectionAndStrain_IsRejected_ButOtherCollectionIsFine()
    {
        var w = await SeedAsync();
        var ecoli = await AddOrganismAsync(w.Db, "Escherichia coli");
        var staph = await AddOrganismAsync(w.Db, "Staphylococcus aureus");
        var service = TestServiceFactory.ReferenceOrganism(w.Db);
        await service.CreateAsync(Strain(ecoli.Id, "ORG-001", CultureCollection.ATCC, "8739"), w.UserId);

        var code = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Strain(staph.Id, "org-001", CultureCollection.ATCC, "6538"), w.UserId));
        Assert.Contains("already exists", code.Message);
        var strain = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Strain(staph.Id, "ORG-002", CultureCollection.ATCC, "8739"), w.UserId));
        Assert.Contains("ATCC 8739 is already listed as ORG-001", strain.Message);

        var nctc = await service.CreateAsync(Strain(ecoli.Id, "ORG-003", CultureCollection.NCTC, "8739"), w.UserId);
        Assert.Equal("Escherichia coli NCTC 8739", nctc.Name);
    }

    [Fact]
    public async Task Edit_IsLimitedToTheOwnerLab_ButEveryoneReads()
    {
        var w = await SeedAsync();
        var ecoli = await AddOrganismAsync(w.Db, "Escherichia coli");
        var service = TestServiceFactory.ReferenceOrganism(w.Db);
        var strain = await service.CreateAsync(Strain(ecoli.Id), w.UserId);

        var update = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(strain.Id, Strain(ecoli.Id, number: "1"), w.OtherLabUserId));
        Assert.Contains("owner lab", update.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetActiveAsync(strain.Id, false, w.OtherLabUserId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeCodeAsync(strain.Id, "ORG-009", "Reason", Password, w.OtherLabUserId, null));

        var seenByOther = Assert.Single(await service.GetAllAsync(w.OtherLabUserId));
        var seenByOwner = Assert.Single(await service.GetAllAsync(w.UserId));
        Assert.False(seenByOther.CanEdit);
        Assert.True(seenByOwner.CanEdit);
        Assert.Equal("MICRO", seenByOther.OwnerSectionCode);
    }

    [Fact]
    public async Task GetAll_FiltersActiveAndOrganism_AndReportsHasStock()
    {
        var w = await SeedAsync();
        var ecoli = await AddOrganismAsync(w.Db, "Escherichia coli");
        var staph = await AddOrganismAsync(w.Db, "Staphylococcus aureus");
        var service = TestServiceFactory.ReferenceOrganism(w.Db);
        var a = await service.CreateAsync(Strain(ecoli.Id, "ORG-001", number: "8739"), w.UserId);
        await service.CreateAsync(Strain(ecoli.Id, "ORG-002", CultureCollection.NCTC, "12900"), w.UserId);
        var c = await service.CreateAsync(Strain(staph.Id, "ORG-003", number: "6538"), w.UserId);
        await service.SetActiveAsync(c.Id, false, w.UserId);
        await new MaterialService(w.Db, new UserSectionScopeService(w.Db), new DatabaseSequenceHelper(w.Db)).CreateAsync(Receive(a.Id), w.UserId);

        Assert.Equal(3, (await service.GetAllAsync(w.UserId)).Count);
        Assert.Equal(2, (await service.GetAllAsync(w.UserId, activeOnly: true)).Count);
        Assert.Equal(2, (await service.GetAllAsync(w.UserId, organismId: ecoli.Id)).Count);
        var all = await service.GetAllAsync(w.UserId);
        Assert.True(all.Single(r => r.Code == "ORG-001").HasStock);
        Assert.False(all.Single(r => r.Code == "ORG-002").HasStock);
    }

    [Fact]
    public async Task WithStock_CodeAndIdentityAreFrozen_AndCodeChangesOnlyBySignedChange()
    {
        var w = await SeedAsync();
        var ecoli = await AddOrganismAsync(w.Db, "Escherichia coli");
        var staph = await AddOrganismAsync(w.Db, "Staphylococcus aureus");
        var service = TestServiceFactory.ReferenceOrganism(w.Db);
        var strain = await service.CreateAsync(Strain(ecoli.Id), w.UserId);
        var lot = await new MaterialService(w.Db, new UserSectionScopeService(w.Db), new DatabaseSequenceHelper(w.Db)).CreateAsync(Receive(strain.Id), w.UserId);

        var code = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(strain.Id, Strain(ecoli.Id, "ORG-002"), w.UserId));
        Assert.Contains("electronic signature", code.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(strain.Id, Strain(staph.Id), w.UserId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(strain.Id, Strain(ecoli.Id, number: "9999"), w.UserId));

        await Assert.ThrowsAsync<SignatureVerificationException>(() => service.ChangeCodeAsync(strain.Id, "ORG-002", "Renumbering", "WrongPassword!", w.UserId, null));
        Assert.Equal("ORG-001", (await w.Db.ReferenceOrganisms.SingleAsync()).Code);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeCodeAsync(strain.Id, "ORG-002", " ", Password, w.UserId, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeCodeAsync(strain.Id, "RG-002", "Renumbering", Password, w.UserId, null));

        var changed = await service.ChangeCodeAsync(strain.Id, " org-002 ", "Renumbering", Password, w.UserId, "10.0.0.1");

        Assert.Equal("ORG-002", changed.Code);
        Assert.True(changed.HasStock);
        Assert.Equal("ORG-001", (await w.Db.Materials.SingleAsync(m => m.Id == lot.Id)).Code); // lot snapshot unchanged
        var signature = Assert.Single(await w.Db.ElectronicSignatures.ToListAsync());
        Assert.Equal(SignatureMeaning.MasterDataChanged, signature.MeaningOfSignature);
        Assert.Equal(ReviewEntityTypes.ReferenceOrganism, signature.EntityType);
        Assert.Contains("ORG-001 -> ORG-002", signature.Comment);
        Assert.Single(await w.Db.AuditLogs.Where(a => a.ActionCode == ReferenceOrganismService.CodeChangedActionCode).ToListAsync());
    }

    [Fact]
    public async Task WithoutStock_CodeAndIdentityCanBeEdited_AndStrainCanBeDeactivated()
    {
        var w = await SeedAsync();
        var ecoli = await AddOrganismAsync(w.Db, "Escherichia coli");
        var service = TestServiceFactory.ReferenceOrganism(w.Db);
        var strain = await service.CreateAsync(Strain(ecoli.Id), w.UserId);

        var edited = await service.UpdateAsync(strain.Id, Strain(ecoli.Id, "ORG-007", CultureCollection.NCIMB, "8545"), w.UserId);
        var inactive = await service.SetActiveAsync(strain.Id, false, w.UserId);

        Assert.Equal("ORG-007", edited.Code);
        Assert.Equal("Escherichia coli NCIMB 8545", edited.Name);
        Assert.False(inactive.IsActive);
    }

    // ---- Lyophilized receipt -------------------------------------------------------------

    [Fact]
    public async Task Receipt_RequiresAnActiveStrain_AndNoFreeTextOrganism()
    {
        var w = await SeedAsync();
        var ecoli = await AddOrganismAsync(w.Db, "Escherichia coli");
        var staph = await AddOrganismAsync(w.Db, "Staphylococcus aureus");
        var strains = TestServiceFactory.ReferenceOrganism(w.Db);
        var strain = await strains.CreateAsync(Strain(ecoli.Id), w.UserId);
        var materials = new MaterialService(w.Db, new UserSectionScopeService(w.Db), new DatabaseSequenceHelper(w.Db));

        var none = await Assert.ThrowsAsync<InvalidOperationException>(() => materials.CreateAsync(Receive(null, organismId: ecoli.Id, atcc: "8739"), w.UserId));
        Assert.Contains("reference organism", none.Message);
        var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() => materials.CreateAsync(Receive(strain.Id, organismId: staph.Id), w.UserId));
        Assert.Contains("does not match", mismatch.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => materials.CreateAsync(Receive(9999), w.UserId));
        var wrongType = await Assert.ThrowsAsync<InvalidOperationException>(() => materials.CreateAsync(Receive(strain.Id, type: MaterialType.Chemical), w.UserId));
        Assert.Contains("only be chosen for lyophilized", wrongType.Message);

        await strains.SetActiveAsync(strain.Id, false, w.UserId);
        var inactive = await Assert.ThrowsAsync<InvalidOperationException>(() => materials.CreateAsync(Receive(strain.Id), w.UserId));
        Assert.Contains("inactive", inactive.Message);
        Assert.Empty(await w.Db.Materials.ToListAsync());
    }

    [Fact]
    public async Task Receipt_DerivesSpeciesAtccNumberNameAndCode_IgnoringClientValues()
    {
        var w = await SeedAsync();
        var ecoli = await AddOrganismAsync(w.Db, "Escherichia coli");
        var strains = TestServiceFactory.ReferenceOrganism(w.Db);
        var atcc = await strains.CreateAsync(Strain(ecoli.Id, "ORG-001", CultureCollection.ATCC, "8739"), w.UserId);
        var nctc = await strains.CreateAsync(Strain(ecoli.Id, "ORG-002", CultureCollection.NCTC, "12900"), w.UserId);
        var materials = new MaterialService(w.Db, new UserSectionScopeService(w.Db), new DatabaseSequenceHelper(w.Db));

        var fromAtcc = await materials.CreateAsync(Receive(atcc.Id, organismId: ecoli.Id, atcc: "WRONG", name: "Typed name", code: "TYPED"), w.UserId);
        var fromNctc = await materials.CreateAsync(Receive(nctc.Id, atcc: "WRONG"), w.UserId);

        Assert.Equal(ecoli.Id, fromAtcc.OrganismId);
        Assert.Equal(atcc.Id, fromAtcc.ReferenceOrganismId);
        Assert.Equal("8739", fromAtcc.AtccNumber);
        Assert.Equal("Escherichia coli ATCC 8739", fromAtcc.MaterialName);
        Assert.Equal("ORG-001", fromAtcc.Code);
        Assert.Equal(CultureCollection.ATCC, fromAtcc.CultureCollection);
        Assert.Equal(ecoli.Id, fromNctc.OrganismId);
        Assert.Null(fromNctc.AtccNumber);
        Assert.Equal("Escherichia coli NCTC 12900", fromNctc.MaterialName);
        Assert.Equal("ORG-002", fromNctc.ReferenceOrganismCode);
    }

    [Fact]
    public async Task Update_LotWithoutStrain_StaysEditable_AndSettingAStrainReDerivesTheLot()
    {
        var w = await SeedAsync();
        var ecoli = await AddOrganismAsync(w.Db, "Escherichia coli");
        var strain = await TestServiceFactory.ReferenceOrganism(w.Db).CreateAsync(Strain(ecoli.Id), w.UserId);
        var legacy = new Material
        {
            SectionId = w.Micro.Id, MaterialType = MaterialType.LyophilizedMicroorganism, MaterialName = "E. coli (old)", Code = "ECO",
            ManufacturerName = "M", BatchNumber = "OLD-1", ReceivingDate = DateTime.UtcNow.AddDays(-5), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Location = "Freezer", QuantityReceived = 5, QuantityRemaining = 5, Unit = MaterialUnit.Disc, AtccNumber = "8739"
        };
        w.Db.Materials.Add(legacy);
        await w.Db.SaveChangesAsync();
        var materials = new MaterialService(w.Db, new UserSectionScopeService(w.Db), new DatabaseSequenceHelper(w.Db));
        SaveMaterialRequest Edit(int? strainId, string location) => new(MaterialType.LyophilizedMicroorganism, "E. coli (old)", "M", "OLD-1",
            legacy.ReceivingDate, legacy.ExpiryDate, "ECO", location, 5, MaterialUnit.Disc, null, "8739", ecoli.Id, ReferenceOrganismId: strainId);

        await materials.UpdateAsync(legacy.Id, Edit(null, "Freezer 2"), w.UserId);
        var untouched = await w.Db.Materials.SingleAsync();
        Assert.Equal("Freezer 2", untouched.Location);
        Assert.Null(untouched.ReferenceOrganismId);
        Assert.Equal("E. coli (old)", untouched.MaterialName);

        await materials.UpdateAsync(legacy.Id, Edit(strain.Id, "Freezer 2"), w.UserId);
        var linked = await w.Db.Materials.SingleAsync();
        Assert.Equal(strain.Id, linked.ReferenceOrganismId);
        Assert.Equal(ecoli.Id, linked.OrganismId);
        Assert.Equal("Escherichia coli ATCC 8739", linked.MaterialName);
        Assert.Equal("ORG-001", linked.Code);

        // An update that omits the strain keeps it and keeps the snapshot.
        await materials.UpdateAsync(legacy.Id, Edit(null, "Freezer 3") with { MaterialName = "Typed", Code = "TYPED", OrganismId = null }, w.UserId);
        var kept = await w.Db.Materials.SingleAsync();
        Assert.Equal(strain.Id, kept.ReferenceOrganismId);
        Assert.Equal(ecoli.Id, kept.OrganismId);
        Assert.Equal("Escherichia coli ATCC 8739", kept.MaterialName);
        Assert.Equal("Freezer 3", kept.Location);
    }

    // ---- Cryovials -----------------------------------------------------------------------

    [Fact]
    public async Task PrepareCryovials_CopiesTheStrain_AndTheCodeUsesTheStrainCode()
    {
        var w = await SeedAsync();
        var ecoli = await AddOrganismAsync(w.Db, "Escherichia coli");
        var strain = await TestServiceFactory.ReferenceOrganism(w.Db).CreateAsync(Strain(ecoli.Id), w.UserId);
        var lot = await new MaterialService(w.Db, new UserSectionScopeService(w.Db), new DatabaseSequenceHelper(w.Db)).CreateAsync(Receive(strain.Id), w.UserId);
        w.Db.MaterialDocuments.Add(new MaterialDocument
        {
            MaterialId = lot.Id, DocumentType = MaterialDocumentType.COA, OriginalFileName = "COA.pdf", StorageKey = "t/" + Guid.NewGuid(),
            FileExtension = ".pdf", ContentType = "application/pdf", FileSizeBytes = 100, ContentSha256 = "HASH",
            UploadedByUserId = 1, Status = MaterialDocumentStatus.Current
        });
        var tsa = await MediaProductTestData.CreateOrGetAsync(w.Db, "TSA Powder", "TSA");
        var powder = new Material
        {
            SectionId = w.Micro.Id, MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-TSA", ReceivingDate = DateTime.UtcNow.AddDays(-30), ExpiryDate = DateTime.UtcNow.AddYears(1), Code = "TSA",
            Location = "Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram, MediaProductId = tsa.Id
        };
        var incubator = new Equipment { Name = "Incubator 1", Code = "INC-01", Type = EquipmentType.Incubator };
        w.Db.Materials.Add(powder);
        w.Db.Equipment.Add(incubator);
        await w.Db.SaveChangesAsync();
        var panel = new Media { MaterialId = powder.Id, LotNumber = $"TSA/01/{Yy}", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddYears(1) };
        w.Db.Media.Add(panel);
        await w.Db.SaveChangesAsync();

        var cryovial = await TestServiceFactory.Cryovial(w.Db).PrepareCryovialsAsync(new PrepareCryovialsRequest(
            lot.Id, 5, DateTime.UtcNow.AddMonths(6), "Freezer", true, "Conforms",
            new List<IdentityConfirmationRow> { new(panel.Id, incubator.Id, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), "Typical") }, 1, 1));

        Assert.Equal($"ORG-001/01/{Yy}", cryovial.Code);
        Assert.Equal(strain.Id, cryovial.ReferenceOrganismId);
        Assert.Equal("ORG-001", cryovial.ReferenceOrganismCode);
        Assert.Equal("8739", cryovial.StrainNumber);
        Assert.Equal(ecoli.Id, cryovial.OrganismId);
        Assert.Equal("Escherichia coli", cryovial.OrganismNameSnapshot);
        Assert.Equal(strain.Id, (await w.Db.Cryovials.SingleAsync()).ReferenceOrganismId);
        Assert.True((await TestServiceFactory.ReferenceOrganism(w.Db).GetAllAsync(w.UserId)).Single().HasStock);
    }

    // ---- Organism (species) master --------------------------------------------------------

    [Fact]
    public async Task OrganismDelete_IsBlockedByTargetOrganismStepsAndStrains_WithAFriendlyMessage()
    {
        var w = await SeedAsync();
        var targeted = await AddOrganismAsync(w.Db, "Salmonella enterica");
        var withStrain = await AddOrganismAsync(w.Db, "Escherichia coli");
        var free = await AddOrganismAsync(w.Db, "Unused species");
        var definition = new TestDefinition { Code = "SAL", DisplayName = "Salmonella", WorkflowType = WorkflowType.CountTest, SectionId = w.Micro.Id };
        w.Db.TestDefinitions.Add(definition);
        await w.Db.SaveChangesAsync();
        w.Db.TestWorkflowSteps.Add(new TestWorkflowStep
        {
            TestDefinitionId = definition.Id, StepOrder = 1, StepName = "Selective plating", StepType = StepType.SelectivePlating, TargetOrganismId = targeted.Id
        });
        await w.Db.SaveChangesAsync();
        await TestServiceFactory.ReferenceOrganism(w.Db).CreateAsync(Strain(withStrain.Id), w.UserId);
        var service = new OrganismMasterDataService(w.Db);

        var step = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteOrganismAsync(targeted.Id));
        Assert.Contains("Test Master target organism steps: 1", step.Message);
        var strain = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteOrganismAsync(withStrain.Id));
        Assert.Contains("Reference organisms: 1", strain.Message);
        await service.DeleteOrganismAsync(free.Id);
        Assert.Equal(2, await w.Db.Organisms.CountAsync());
    }

    [Fact]
    public async Task OrganismMaster_DuplicateAtccNumber_GetsAFriendlyMessage()
    {
        var w = await SeedAsync();
        await AddOrganismAsync(w.Db, "Escherichia coli", "8739");
        var other = await AddOrganismAsync(w.Db, "Staphylococcus aureus", "6538");
        var service = new OrganismMasterDataService(w.Db);

        var create = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateOrganismAsync(new CreateOrganismRequest("Klebsiella", " 8739 ", null, null)));
        Assert.Contains("already used by organism \"Escherichia coli\"", create.Message);
        var update = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateOrganismAsync(other.Id, new UpdateOrganismRequest("Staphylococcus aureus", "8739", null, null)));
        Assert.Contains("already used", update.Message);
        await service.UpdateOrganismAsync(other.Id, new UpdateOrganismRequest("Staphylococcus aureus", "6538", "Staph", null)); // its own number is fine
    }

    // ---- Culture media -------------------------------------------------------------------

    [Fact]
    public async Task MediaProduct_Deactivation_BlocksNewLotsAndConfigurations_ButExistingDataKeepsWorking()
    {
        var w = await SeedAsync();
        var signatures = new ElectronicSignatureService(w.Db);
        var products = new MediaProductService(w.Db, signatures, new AuditEventService(w.Db, new MicroLIMS.Persistence.Helpers.DatabaseSequenceHelper(w.Db)));
        var tsa = await products.CreateAsync("Tryptic Soy Agar", "TSA");
        var sda = await products.CreateAsync("Sabouraud Dextrose Agar", "SDA");
        Assert.True(tsa.IsActive);
        var materials = new MaterialService(w.Db, new UserSectionScopeService(w.Db), new DatabaseSequenceHelper(w.Db));
        SaveMaterialRequest Powder(int productId, string batch) => new(MaterialType.DehydratedMedia, "x", "Himedia", batch, DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddYears(1), null, "Shelf", 500, MaterialUnit.Gram, null, null, null, MediaProductId: productId);
        var existing = await materials.CreateAsync(Powder(tsa.Id, "B-1"), w.UserId);
        Assert.True((await products.GetAllAsync()).Single(p => p.Code == "TSA").HasStock);
        Assert.False((await products.GetAllAsync()).Single(p => p.Code == "SDA").HasStock);

        var off = await products.SetActiveAsync(tsa.Id, false);
        await products.SetActiveAsync(sda.Id, false);

        Assert.False(off.IsActive);
        Assert.False((await products.GetAllAsync()).Single(p => p.Code == "TSA").IsActive);
        var lot = await Assert.ThrowsAsync<InvalidOperationException>(() => materials.CreateAsync(Powder(tsa.Id, "B-2"), w.UserId));
        Assert.Contains("inactive", lot.Message);
        var condition = MediaProductTestData.Condition(sda);
        w.Db.MediaIncubationConditions.Add(condition);
        await w.Db.SaveChangesAsync();
        var config = await Assert.ThrowsAsync<InvalidOperationException>(() => new MediaMasterDataService(w.Db).CreateMediaConfigurationAsync(
            new CreateMediaConfigurationRequest(sda.Id, EvaluationType.GrowthPromotion, condition.Id, 50, 200, null)));
        Assert.Contains("inactive", config.Message);

        // The batch already received keeps working: editing it is not blocked by the product being inactive.
        await materials.UpdateAsync(existing.Id, Powder(tsa.Id, "B-1") with { Location = "Shelf 2" }, w.UserId);
        Assert.Equal("Shelf 2", (await w.Db.Materials.SingleAsync(m => m.Id == existing.Id)).Location);

        await products.SetActiveAsync(tsa.Id, true);
        await materials.CreateAsync(Powder(tsa.Id, "B-3"), w.UserId);
    }
}

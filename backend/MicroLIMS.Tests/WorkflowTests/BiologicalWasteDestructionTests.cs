using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Application.Interfaces;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Thawed vials are usable for 24 hours, then eligible for destruction; a
// withdrawn batch makes all its vials eligible at once. Destruction is one
// signed autoclave-cycle record that can also list other waste.
public class BiologicalWasteDestructionTests
{
    private const string Password = "Signer123!";

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(TimeSpan by) => Now = Now.Add(by);
    }

    private sealed record World(MicroLimsDbContext Db, Clock Clock, User User, Cryovial Batch, Equipment Autoclave, AutoclaveProgram Program);

    private static async Task<World> SeedAsync()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var section = TestServiceFactory.EnsureMicroSection(db);

        var role = new Role { Type = RoleType.Analyst, Name = "Analyst" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { FullName = "Ana Lyst", Username = "analyst", RoleId = role.Id, PasswordHash = TestPasswords.Hash(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, user.Id);

        var organism = new Organism { ScientificName = "E. coli" };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();
        var material = new Material
        {
            SectionId = section.Id, MaterialType = MaterialType.LyophilizedMicroorganism, MaterialName = "E. coli disk",
            ManufacturerName = "ATCC", BatchNumber = "LB1", ReceivingDate = DateTime.UtcNow.AddDays(-5), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Location = "Freezer", QuantityReceived = 10, QuantityRemaining = 10, Unit = MaterialUnit.Disc, OrganismId = organism.Id
        };
        db.Materials.Add(material);
        var autoclave = new Equipment { SectionId = section.Id, Name = "Autoclave 2", Code = "AUT-02", Type = EquipmentType.Autoclave };
        db.Equipment.Add(autoclave);
        await db.SaveChangesAsync();
        var program = new AutoclaveProgram
        {
            EquipmentId = autoclave.Id, ProgramCode = "P09", ProgramName = "Waste decontamination", LoadType = "Biohazard Waste",
            Temperature = 121m, CycleTimeMinutes = 30
        };
        db.AutoclavePrograms.Add(program);
        var batch = new Cryovial
        {
            Code = "ECO/01/26", MaterialId = material.Id, OrganismId = organism.Id, OrganismNameSnapshot = "E. coli", ManufacturerName = "ATCC",
            ExpiryDate = DateTime.UtcNow.AddYears(1), NumberOfVialsPrepared = 5, VialsRemaining = 5, ApprovalStatus = ApprovalGateStatus.Approved
        };
        db.Cryovials.Add(batch);
        await db.SaveChangesAsync();
        return new World(db, new Clock(), user, batch, autoclave, program);
    }

    private static BiologicalWasteDestructionService Service(World w) =>
        new(w.Db, new ElectronicSignatureService(w.Db, w.Clock), new UserSectionScopeService(w.Db), new DatabaseSequenceHelper(w.Db), w.Clock);

    private static async Task<ThawEvent> ThawAsync(World w)
    {
        var thaw = CryovialThaw.Record(w.Db, w.Batch, w.User.Id, null, w.Clock.GetUtcNow().UtcDateTime);
        await w.Db.SaveChangesAsync();
        return thaw;
    }

    private static RecordBiologicalWasteDestructionRequest Request(World w,
        List<int>? thaws = null, List<int>? batches = null, List<OtherWasteItemRequest>? others = null,
        string password = Password, int? programId = null) =>
        new(w.Autoclave.Id, programId ?? w.Program.Id, "118", w.Clock.GetUtcNow().UtcDateTime,
            thaws, batches, others, "Routine waste run", password);

    [Fact]
    public async Task ThawedVial_IsNotEligibleWithin24Hours_AndCannotBeDestroyedYet()
    {
        var w = await SeedAsync();
        var thaw = await ThawAsync(w);
        w.Clock.Advance(TimeSpan.FromHours(23));

        Assert.Empty((await Service(w).GetEligibleAsync(null)).ThawedVials);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(w).RecordAsync(Request(w, thaws: new() { thaw.Id }), w.User.Id, null));
        Assert.Contains("24-hour use window", ex.Message);
        Assert.Empty(w.Db.BiologicalWasteDestructions);
    }

    [Fact]
    public async Task ThawedVialsOver24Hours_AreDestroyedInBulk_InOneSignedCycle_WithOtherWaste()
    {
        var w = await SeedAsync();
        var first = await ThawAsync(w);
        var second = await ThawAsync(w);
        w.Clock.Advance(TimeSpan.FromHours(25));

        var eligible = await Service(w).GetEligibleAsync(null);
        Assert.Equal(new[] { first.Id, second.Id }, eligible.ThawedVials.Select(v => v.ThawEventId).OrderBy(i => i).ToArray());

        var record = await Service(w).RecordAsync(Request(w, thaws: new() { first.Id, second.Id },
            others: new() { new("Used TSA plates from GPT", 12, "plates") }), w.User.Id, "10.0.0.1");

        Assert.Equal($"BWD-{record.Id:D5}", record.Reference);
        Assert.Equal(2, record.CryovialItemCount);
        Assert.Equal(1, record.OtherItemCount);
        Assert.Equal("P09", record.ProgramCode);
        Assert.Equal(121m, record.Temperature);
        Assert.Equal("Ana Lyst", record.SignedByName);

        var signature = await w.Db.ElectronicSignatures.SingleAsync();
        Assert.Equal(SignatureMeaning.BiologicalWasteDestroyed, signature.MeaningOfSignature);
        Assert.Equal(nameof(BiologicalWasteDestruction), signature.EntityType);
        Assert.Equal(record.Id, signature.EntityId);

        Assert.Empty((await Service(w).GetEligibleAsync(null)).ThawedVials);
        var again = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(w).RecordAsync(Request(w, thaws: new() { first.Id }), w.User.Id, null));
        Assert.Contains("already been destroyed", again.Message);
    }

    [Fact]
    public async Task WithdrawnBatch_MakesEveryVialEligibleAtOnce_AndDestroyingTheRemainderEmptiesTheBatch()
    {
        var w = await SeedAsync();
        var thaw = await ThawAsync(w); // 4 vials left, thawed only minutes ago
        await TestServiceFactory.Cryovial(w.Db).DestroyAsync(w.Batch.Id);

        var eligible = await Service(w).GetEligibleAsync(null);
        var vial = Assert.Single(eligible.ThawedVials);
        Assert.Equal("Batch withdrawn", vial.EligibleBecause);
        var batch = Assert.Single(eligible.WithdrawnBatches);
        Assert.Equal(4, batch.VialsRemaining);

        var record = await Service(w).RecordAsync(Request(w, thaws: new() { thaw.Id }, batches: new() { w.Batch.Id }), w.User.Id, null);

        var unthawed = record.Items.Single(i => i.ItemType == nameof(BiologicalWasteItemType.UnthawedCryovials));
        Assert.Equal(4, unthawed.Quantity);
        Assert.Equal(0, (await w.Db.Cryovials.AsNoTracking().SingleAsync()).VialsRemaining);
        var after = await Service(w).GetEligibleAsync(null);
        Assert.Empty(after.ThawedVials);
        Assert.Empty(after.WithdrawnBatches);
    }

    [Fact]
    public async Task UnthawedVials_OfABatchStillInUse_CannotBeDestroyed()
    {
        var w = await SeedAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(w).RecordAsync(Request(w, batches: new() { w.Batch.Id }), w.User.Id, null));
        Assert.Contains("has not been withdrawn", ex.Message);
    }

    [Fact]
    public async Task WrongPassword_RecordsNothing()
    {
        var w = await SeedAsync();
        await TestServiceFactory.Cryovial(w.Db).DestroyAsync(w.Batch.Id);

        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            Service(w).RecordAsync(Request(w, batches: new() { w.Batch.Id }, password: "wrong"), w.User.Id, null));

        Assert.Empty(w.Db.BiologicalWasteDestructions);
        Assert.Equal(5, (await w.Db.Cryovials.AsNoTracking().SingleAsync()).VialsRemaining);
    }

    [Fact]
    public async Task OtherWasteOnly_IsRecorded_ButEachItemNeedsADescription()
    {
        var w = await SeedAsync();

        var bad = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(w).RecordAsync(Request(w, others: new() { new(" ", 1, "bag") }), w.User.Id, null));
        Assert.Contains("description", bad.Message);

        var record = await Service(w).RecordAsync(Request(w, others: new() { new("Contaminated broth tubes", 6, "tubes") }), w.User.Id, null);
        Assert.Equal(0, record.CryovialItemCount);
        Assert.Single(await Service(w).GetAllAsync(null));
    }

    [Fact]
    public async Task ProgramOfAnotherAutoclave_IsRefused()
    {
        var w = await SeedAsync();
        var other = new Equipment { SectionId = w.Autoclave.SectionId, Name = "Autoclave 3", Code = "AUT-03", Type = EquipmentType.Autoclave };
        w.Db.Equipment.Add(other);
        await w.Db.SaveChangesAsync();
        var foreign = new AutoclaveProgram { EquipmentId = other.Id, ProgramCode = "P01", ProgramName = "Media", LoadType = "Media", Temperature = 121m, CycleTimeMinutes = 15 };
        w.Db.AutoclavePrograms.Add(foreign);
        await w.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(w).RecordAsync(Request(w, others: new() { new("Plates", 1, "bag") }, programId: foreign.Id), w.User.Id, null));
        Assert.Contains("does not belong to autoclave", ex.Message);
    }

    [Fact]
    public async Task MyTasks_RemindsTheThawer_OnlyAfter48Hours_UntilTheVialIsDestroyed()
    {
        var w = await SeedAsync();
        var thaw = await ThawAsync(w);
        var tasks = new MyTasksService(w.Db, new UserSectionScopeService(w.Db), w.Clock);

        w.Clock.Advance(TimeSpan.FromHours(47));
        Assert.DoesNotContain(await tasks.GetMyTasksAsync(w.User.Id), t => t.TaskType == "VialDestruction");

        w.Clock.Advance(TimeSpan.FromHours(2));
        var reminder = Assert.Single(await tasks.GetMyTasksAsync(w.User.Id), t => t.TaskType == "VialDestruction");
        Assert.Equal(TaskUrgency.Overdue, reminder.Urgency);
        Assert.Contains("ECO/01/26", reminder.Title);

        await Service(w).RecordAsync(Request(w, thaws: new() { thaw.Id }), w.User.Id, null);
        Assert.DoesNotContain(await tasks.GetMyTasksAsync(w.User.Id), t => t.TaskType == "VialDestruction");
    }
}

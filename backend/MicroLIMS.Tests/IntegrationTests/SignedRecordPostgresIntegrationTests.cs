using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// A record that is signed when it is created - a biological waste
// destruction, a maintenance log entry, a calibration - must carry a
// signature whose EntityId is the record's real id. The in-memory tests
// cannot catch a wrong one: only PostgreSQL has the append-only trigger on
// ElectronicSignatures that rejects UPDATE (so the signature cannot be
// pointed at the record after the fact) and enforces the foreign keys.
[Collection("PostgresDatabaseCollection")]
public class SignedRecordPostgresIntegrationTests
{
    private const string Password = "Signer-Valid-Password-2026!";
    private readonly PostgresTestFixture _fixture;

    public SignedRecordPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private async Task<User> SeedAnalystAsync(MicroLimsDbContext db)
    {
        var role = await db.Roles.FirstAsync(r => r.Type == RoleType.Analyst);
        var user = new User
        {
            FullName = "Signing Analyst",
            Username = $"sig_{Suffix()}",
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return user;
    }

    private static BiologicalWasteDestructionService WasteService(MicroLimsDbContext db) =>
        new(db, new ElectronicSignatureService(db), new UserSectionScopeService(db), new DatabaseSequenceHelper(db));

    private static EquipmentWorkService WorkService(MicroLimsDbContext db) =>
        new(db, LabClock.Default, new ElectronicSignatureService(db), new DatabaseSequenceHelper(db));

    private async Task<(Equipment Autoclave, AutoclaveProgram Program)> SeedAutoclaveAsync(MicroLimsDbContext db)
    {
        var autoclave = new Equipment
        {
            SectionId = _fixture.SeededSectionId, Name = "Autoclave", Code = $"AUT-{Suffix()}", Type = EquipmentType.Autoclave
        };
        db.Equipment.Add(autoclave);
        await db.SaveChangesAsync();
        var program = new AutoclaveProgram
        {
            EquipmentId = autoclave.Id, ProgramCode = "P09", ProgramName = "Waste decontamination", LoadType = "Biohazard Waste",
            Temperature = 121m, CycleTimeMinutes = 30
        };
        db.AutoclavePrograms.Add(program);
        await db.SaveChangesAsync();
        return (autoclave, program);
    }

    private async Task<Cryovial> SeedWithdrawnBatchAsync(MicroLimsDbContext db)
    {
        var organism = new Organism { ScientificName = $"E. coli {Suffix()}" };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();
        var material = new Material
        {
            SectionId = _fixture.SeededSectionId, MaterialType = MaterialType.LyophilizedMicroorganism, MaterialName = "E. coli disk",
            ManufacturerName = "ATCC", BatchNumber = $"LB{Suffix()}", ReceivingDate = DateTime.UtcNow.AddDays(-5), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Location = "Freezer", QuantityReceived = 10, QuantityRemaining = 10, Unit = MaterialUnit.Disc, OrganismId = organism.Id
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        var batch = new Cryovial
        {
            Code = $"ECO/{Suffix()}", MaterialId = material.Id, OrganismId = organism.Id, OrganismNameSnapshot = "E. coli", ManufacturerName = "ATCC",
            ExpiryDate = DateTime.UtcNow.AddYears(1), NumberOfVialsPrepared = 5, VialsRemaining = 5,
            ApprovalStatus = ApprovalGateStatus.Approved, IsDestroyed = true
        };
        db.Cryovials.Add(batch);
        await db.SaveChangesAsync();
        return batch;
    }

    private static RecordBiologicalWasteDestructionRequest WasteRequest(
        Equipment autoclave, AutoclaveProgram program, List<int>? batches, string password) =>
        new(autoclave.Id, program.Id, "118", DateTime.UtcNow, null, batches,
            new() { new("Used TSA plates from GPT", 12, "plates") }, "Routine waste run", password);

    private async Task<EquipmentInventory> SeedInventoryAsync(MicroLimsDbContext db)
    {
        var inventory = new EquipmentInventory
        {
            Code = $"INC-{Suffix()}", InstrumentType = "Incubator", ManufacturerName = "Binder", Location = "Room 101",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 12, CalibrationIntervalUnit = IntervalUnit.Months,
            PmIntervalValue = 1, PmIntervalUnit = IntervalUnit.Months,
            SectionId = _fixture.SeededSectionId
        };
        db.EquipmentInventories.Add(inventory);
        await db.SaveChangesAsync();
        return inventory;
    }

    [PostgresFact]
    public async Task RecordWasteDestruction_SignsWithTheRealRecordId()
    {
        await using var db = _fixture.CreateDbContext();
        var user = await SeedAnalystAsync(db);
        var (autoclave, program) = await SeedAutoclaveAsync(db);

        var record = await WasteService(db).RecordAsync(WasteRequest(autoclave, program, null, Password), user.Id, "10.0.0.1");

        await using var check = _fixture.CreateDbContext();
        var destruction = await check.BiologicalWasteDestructions.SingleAsync(d => d.Id == record.Id);
        var signature = await check.ElectronicSignatures.SingleAsync(s =>
            s.EntityType == nameof(BiologicalWasteDestruction) && s.EntityId == destruction.Id);
        Assert.Equal(signature.Id, destruction.SignatureId);
        Assert.Equal(SignatureMeaning.BiologicalWasteDestroyed, signature.MeaningOfSignature);
        Assert.Equal(user.Id, signature.UserId);
    }

    [PostgresFact]
    public async Task RecordWasteDestruction_WithWrongPassword_SavesNoRecordAndKeepsTheFailureAudit()
    {
        await using var db = _fixture.CreateDbContext();
        var user = await SeedAnalystAsync(db);
        var (autoclave, program) = await SeedAutoclaveAsync(db);
        var batch = await SeedWithdrawnBatchAsync(db);

        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            WasteService(db).RecordAsync(WasteRequest(autoclave, program, new() { batch.Id }, "not-the-password"), user.Id, null));

        await using var check = _fixture.CreateDbContext();
        Assert.False(await check.BiologicalWasteDestructions.AnyAsync(d => d.AutoclaveEquipmentId == autoclave.Id));
        Assert.False(await check.ElectronicSignatures.AnyAsync(s => s.UserId == user.Id));
        Assert.Equal(5, (await check.Cryovials.SingleAsync(c => c.Id == batch.Id)).VialsRemaining);
        Assert.Equal(1, await check.AuditLogs.CountAsync(a =>
            a.UserId == user.Id && a.EntityName == "ElectronicSignature" && a.Action == "SignatureFailed"));
    }

    [PostgresFact]
    public async Task AddMaintenanceEntry_SignsWithTheRealRecordId()
    {
        await using var db = _fixture.CreateDbContext();
        var user = await SeedAnalystAsync(db);
        var inventory = await SeedInventoryAsync(db);

        var entry = await WorkService(db).AddLogEntryAsync(inventory.Id,
            new AddLogEntryRequest(MaintenanceEntryType.BreakdownRepair, LabClock.Default.LabToday, "Replaced the door gasket", null, null, null, Password),
            user.Id, "10.0.0.1");

        await using var check = _fixture.CreateDbContext();
        var saved = await check.MaintenanceLogEntries.SingleAsync(m => m.Id == entry.Id);
        var signature = await check.ElectronicSignatures.SingleAsync(s =>
            s.EntityType == nameof(MaintenanceLogEntry) && s.EntityId == saved.Id);
        Assert.Equal(signature.Id, saved.ElectronicSignatureId);
        Assert.Equal(SignatureMeaning.MaintenanceEntryRecorded, signature.MeaningOfSignature);
        Assert.Equal(user.Id, signature.UserId);
    }

    [PostgresFact]
    public async Task AddPreventiveMaintenanceEntry_CompletesTheOpenTask_AndLinksItsEventToTheSignature()
    {
        await using var db = _fixture.CreateDbContext();
        var user = await SeedAnalystAsync(db);
        var inventory = await SeedInventoryAsync(db);
        var today = LabClock.Default.LabToday;
        db.EquipmentTasks.AddRange(
            new EquipmentTask
            {
                EquipmentInventoryId = inventory.Id, TaskType = EquipmentTaskType.PreventiveMaintenance, Year = today.Year, Sequence = 1,
                OriginalDueDate = today, DueDate = today, IntervalSnapshot = "1 month"
            },
            new EquipmentTask
            {
                EquipmentInventoryId = inventory.Id, TaskType = EquipmentTaskType.PreventiveMaintenance, Year = today.Year, Sequence = 2,
                OriginalDueDate = today.AddDays(1), DueDate = today.AddDays(1), IntervalSnapshot = "1 month"
            });
        await db.SaveChangesAsync();

        var entry = await WorkService(db).AddLogEntryAsync(inventory.Id,
            new AddLogEntryRequest(MaintenanceEntryType.PreventiveMaintenance, today, "Monthly PM", null, null, null, Password),
            user.Id, null);

        await using var check = _fixture.CreateDbContext();
        var signature = await check.ElectronicSignatures.SingleAsync(s =>
            s.EntityType == nameof(MaintenanceLogEntry) && s.EntityId == entry.Id);
        var completed = await check.EquipmentTasks.SingleAsync(t => t.MaintenanceLogEntryId == entry.Id);
        var events = await check.EquipmentTaskEvents.Where(e => e.EquipmentTaskId == completed.Id).ToListAsync();
        Assert.Equal(EquipmentTaskEventType.Completed, Assert.Single(events).EventType);
        Assert.Equal(signature.Id, events[0].ElectronicSignatureId);
    }

    [PostgresFact]
    public async Task AddMaintenanceEntry_WithWrongPassword_SavesNoEntryAndKeepsTheFailureAudit()
    {
        await using var db = _fixture.CreateDbContext();
        var user = await SeedAnalystAsync(db);
        var inventory = await SeedInventoryAsync(db);

        await Assert.ThrowsAsync<SignatureVerificationException>(() => WorkService(db).AddLogEntryAsync(inventory.Id,
            new AddLogEntryRequest(MaintenanceEntryType.BreakdownRepair, LabClock.Default.LabToday, "Replaced the door gasket", null, null, null, "not-the-password"),
            user.Id, null));

        await using var check = _fixture.CreateDbContext();
        Assert.False(await check.MaintenanceLogEntries.AnyAsync(m => m.EquipmentInventoryId == inventory.Id));
        Assert.False(await check.ElectronicSignatures.AnyAsync(s => s.UserId == user.Id));
        Assert.Equal(1, await check.AuditLogs.CountAsync(a =>
            a.UserId == user.Id && a.EntityName == "ElectronicSignature" && a.Action == "SignatureFailed"));
    }

    [PostgresFact]
    public async Task RecordCalibration_LinksItsTaskEventToTheSignature()
    {
        await using var db = _fixture.CreateDbContext();
        var user = await SeedAnalystAsync(db);
        var inventory = await SeedInventoryAsync(db);
        var certificate = new EquipmentDocument
        {
            EquipmentInventoryId = inventory.Id, DocumentType = EquipmentDocumentType.CalibrationCertificate,
            OriginalFileName = "cert.pdf", StorageKey = $"equipment-documents/{inventory.Id}/cert.pdf", FileExtension = ".pdf",
            ContentType = "application/pdf", FileSizeBytes = 1024, ContentSha256 = Suffix(), UploadedByUserId = user.Id,
            Status = MaterialDocumentStatus.Current, UploadedAt = DateTime.UtcNow
        };
        db.EquipmentDocuments.Add(certificate);
        await db.SaveChangesAsync();

        var task = await WorkService(db).RecordCalibrationAsync(inventory.Id,
            new RecordCalibrationRequest(certificate.Id, LabClock.Default.LabToday, null, Password), user.Id, null);

        await using var check = _fixture.CreateDbContext();
        var signature = await check.ElectronicSignatures.SingleAsync(s =>
            s.EntityType == nameof(EquipmentTask) && s.EntityId == task.Id);
        var completed = await check.EquipmentTaskEvents.SingleAsync(e => e.EquipmentTaskId == task.Id && e.EventType == EquipmentTaskEventType.Completed);
        Assert.Equal(signature.Id, completed.ElectronicSignatureId);
    }
}

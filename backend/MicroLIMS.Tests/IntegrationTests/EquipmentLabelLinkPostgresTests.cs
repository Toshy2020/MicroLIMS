using System.Text.Json;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class EquipmentLabelLinkPostgresTests
{
    private static readonly DateTime FixedUtc = new(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);

    private readonly PostgresTestFixture _fixture;

    public EquipmentLabelLinkPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed record World(int SectionAId, int SectionBId, int HeadAId, int HeadBId);

    private async Task<World> ArrangeSectionsAndHeadsAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();
        db.CurrentUserId = _fixture.SeededUserId;

        var dept = new DocumentDepartment { Name = $"Dept {tag}", Code = $"D{tag}", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var labA = new DocumentSection { Name = $"Lab A {tag}", Code = $"A{tag}", DepartmentId = dept.Id, IsActive = true };
        var labB = new DocumentSection { Name = $"Lab B {tag}", Code = $"B{tag}", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(labA, labB);
        await db.SaveChangesAsync();

        var headRole = await db.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.SectionHead);
        var headA = new User { Username = $"eqHeadA_{tag}", FullName = $"Eq Head A {tag}", RoleId = headRole.Id, IsActive = true, PasswordHash = "x" };
        var headB = new User { Username = $"eqHeadB_{tag}", FullName = $"Eq Head B {tag}", RoleId = headRole.Id, IsActive = true, PasswordHash = "x" };
        db.Users.AddRange(headA, headB);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = headA.Id, DepartmentId = dept.Id, SectionId = labA.Id },
            new UserOrgMembership { UserId = headB.Id, DepartmentId = dept.Id, SectionId = labB.Id });
        await db.SaveChangesAsync();

        return new World(labA.Id, labB.Id, headA.Id, headB.Id);
    }

    private async Task<EquipmentInventory> SeedAssetAsync(int sectionId, string codePrefix, int userId, bool withPrintLog = false)
    {
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();
        db.CurrentUserId = userId;

        var asset = new EquipmentInventory
        {
            InstrumentType = "Incubator",
            ManufacturerName = "Binder",
            Code = $"{codePrefix}-{tag}",
            Location = "Micro Lab 1",
            Status = EquipmentOperationalStatus.InService,
            SectionId = sectionId,
            CreatedByUserId = userId,
            CreatedAt = FixedUtc,
            LastModifiedByUserId = userId,
            LastModifiedAt = FixedUtc
        };
        db.EquipmentInventories.Add(asset);
        await db.SaveChangesAsync();

        if (withPrintLog)
        {
            var format = await db.LabelFormats.AsNoTracking().SingleAsync(f => f.EntityType == LabelEntityType.EquipmentAsset);
            db.LabelPrintLogs.Add(new LabelPrintLog
            {
                EntityType = LabelEntityType.EquipmentAsset,
                EntityId = asset.Id,
                Code = asset.Code,
                PrintNumber = 1,
                IsReprint = false,
                Copies = 1,
                LabelFormatId = format.Id,
                LabelFormatVersion = format.Version,
                PrintedByUserId = userId,
                PrintedAt = FixedUtc,
                BatchId = Guid.NewGuid()
            });
            await db.SaveChangesAsync();
        }

        return asset;
    }

    private async Task<Equipment> SeedWorkflowEquipmentAsync(int sectionId, string codePrefix, int userId, int? equipmentInventoryId = null)
    {
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();
        db.CurrentUserId = userId;

        var equipment = new Equipment
        {
            Name = $"Incubator {tag}",
            Code = $"{codePrefix}-{tag}",
            Type = EquipmentType.Incubator,
            Location = "Micro Lab 1",
            SetPointTemperature = 32.5m,
            SectionId = sectionId,
            EquipmentInventoryId = equipmentInventoryId
        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();
        return equipment;
    }

    private static EquipmentInventoryService InventoryService(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db), new FixedTimeProvider(FixedUtc));

    private static EquipmentMasterDataService MasterDataService(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db));

    [PostgresFact]
    public async Task Asset_code_can_change_before_any_label_is_printed()
    {
        var w = await ArrangeSectionsAndHeadsAsync();
        var asset = await SeedAssetAsync(w.SectionAId, "AST-OLD", w.HeadAId);
        var newCode = $"AST-NEW-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedUtc)))
        {
            actDb.CurrentUserId = w.HeadAId;
            actDb.ExpectedVersion = asset.Version;
            await InventoryService(actDb).UpdateAsync(asset.Id, new SaveEquipmentInventoryRequest(
                asset.InstrumentType,
                asset.ManufacturerName,
                asset.SerialNumber,
                asset.FirmwareVersion,
                newCode,
                asset.Location,
                asset.CalibrationDueDate,
                asset.Status,
                SectionId: w.SectionAId), w.HeadAId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.EquipmentInventories.AsNoTracking().SingleAsync(e => e.Id == asset.Id);
        Assert.Equal(newCode, persisted.Code);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(EquipmentInventory) && a.EntityId == asset.Id.ToString() && a.Action == "Update")
            .SingleAsync();
        Assert.Equal(w.HeadAId, audit.UserId);
        Assert.Equal(asset.Code, JsonDocument.Parse(audit.PreviousValue!).RootElement.GetProperty("Code").GetString());
        Assert.Equal(newCode, JsonDocument.Parse(audit.NewValue!).RootElement.GetProperty("Code").GetString());
    }

    [PostgresFact]
    public async Task Asset_code_change_is_rejected_after_a_label_is_printed()
    {
        var w = await ArrangeSectionsAndHeadsAsync();
        var asset = await SeedAssetAsync(w.SectionAId, "AST-LCK", w.HeadAId, withPrintLog: true);
        var attemptedCode = $"AST-CHG-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedUtc)))
        {
            actDb.CurrentUserId = w.HeadAId;
            actDb.ExpectedVersion = asset.Version;
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                InventoryService(actDb).UpdateAsync(asset.Id, new SaveEquipmentInventoryRequest(
                    asset.InstrumentType,
                    asset.ManufacturerName,
                    asset.SerialNumber,
                    asset.FirmwareVersion,
                    attemptedCode,
                    asset.Location,
                    asset.CalibrationDueDate,
                    asset.Status,
                    SectionId: w.SectionAId), w.HeadAId));

            Assert.Equal("The code cannot be changed because a label has been printed for this instrument.", ex.Message);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.EquipmentInventories.AsNoTracking().SingleAsync(e => e.Id == asset.Id);
        Assert.Equal(asset.Code, persisted.Code);
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(
            a => a.EntityName == nameof(EquipmentInventory) && a.EntityId == asset.Id.ToString() && a.Action == "Update"));
    }

    [PostgresFact]
    public async Task Renaming_other_fields_is_still_allowed_after_a_label_is_printed()
    {
        var w = await ArrangeSectionsAndHeadsAsync();
        var asset = await SeedAssetAsync(w.SectionAId, "AST-RNM", w.HeadAId, withPrintLog: true);

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedUtc)))
        {
            actDb.CurrentUserId = w.HeadAId;
            actDb.ExpectedVersion = asset.Version;
            await InventoryService(actDb).UpdateAsync(asset.Id, new SaveEquipmentInventoryRequest(
                "CO2 Incubator",
                "Memmert",
                asset.SerialNumber,
                asset.FirmwareVersion,
                asset.Code,
                "Micro Lab 2",
                asset.CalibrationDueDate,
                asset.Status,
                SectionId: w.SectionAId), w.HeadAId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.EquipmentInventories.AsNoTracking().SingleAsync(e => e.Id == asset.Id);
        Assert.Equal(asset.Code, persisted.Code);
        Assert.Equal("CO2 Incubator", persisted.InstrumentType);
        Assert.Equal("Memmert", persisted.ManufacturerName);
        Assert.Equal("Micro Lab 2", persisted.Location);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(EquipmentInventory) && a.EntityId == asset.Id.ToString() && a.Action == "Update")
            .SingleAsync();
        Assert.Equal(w.HeadAId, audit.UserId);
        Assert.Equal("Micro Lab 1", JsonDocument.Parse(audit.PreviousValue!).RootElement.GetProperty("Location").GetString());
        Assert.Equal("Micro Lab 2", JsonDocument.Parse(audit.NewValue!).RootElement.GetProperty("Location").GetString());
    }

    [PostgresFact]
    public async Task Asset_response_reports_code_locked_only_after_a_print()
    {
        var w = await ArrangeSectionsAndHeadsAsync();
        var printed = await SeedAssetAsync(w.SectionAId, "AST-PRT", w.HeadAId, withPrintLog: true);
        var unprinted = await SeedAssetAsync(w.SectionAId, "AST-UNP", w.HeadAId, withPrintLog: false);

        // A print log for another entity type with the same numeric id must not lock the asset.
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var sampleFormat = await arrangeDb.LabelFormats.AsNoTracking().SingleAsync(f => f.EntityType == LabelEntityType.Sample);
            arrangeDb.LabelPrintLogs.Add(new LabelPrintLog
            {
                EntityType = LabelEntityType.Sample,
                EntityId = unprinted.Id,
                Code = $"S-{unprinted.Id}",
                PrintNumber = 1,
                IsReprint = false,
                Copies = 1,
                LabelFormatId = sampleFormat.Id,
                LabelFormatVersion = sampleFormat.Version,
                PrintedByUserId = w.HeadAId,
                PrintedAt = FixedUtc,
                BatchId = Guid.NewGuid()
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = InventoryService(actDb);
        var scope = new[] { w.SectionAId };

        var all = await service.GetAllAsync(scope);
        var forPrint = await service.GetForPrintAsync(scope);
        var printedDetail = await service.GetByIdAsync(printed.Id, w.HeadAId);
        var unprintedDetail = await service.GetByIdAsync(unprinted.Id, w.HeadAId);

        Assert.True(Assert.Single(all, x => x.Id == printed.Id).CodeLocked);
        Assert.False(Assert.Single(all, x => x.Id == unprinted.Id).CodeLocked);
        Assert.True(Assert.Single(forPrint, x => x.Id == printed.Id).CodeLocked);
        Assert.False(Assert.Single(forPrint, x => x.Id == unprinted.Id).CodeLocked);
        Assert.NotNull(printedDetail);
        Assert.True(printedDetail!.CodeLocked);
        Assert.NotNull(unprintedDetail);
        Assert.False(unprintedDetail!.CodeLocked);
    }

    [PostgresFact]
    public async Task Linking_an_asset_persists_and_is_audited()
    {
        var w = await ArrangeSectionsAndHeadsAsync();
        var asset = await SeedAssetAsync(w.SectionAId, "AST-LNK", w.HeadAId);
        var eq = await SeedWorkflowEquipmentAsync(w.SectionAId, "WF-LNK", w.HeadAId);

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedUtc)))
        {
            actDb.CurrentUserId = w.HeadAId;
            actDb.ExpectedVersion = eq.Version;
            var res = await MasterDataService(actDb).UpdateEquipmentAsync(w.HeadAId, eq.Id, new UpdateEquipmentRequest(
                eq.Name,
                eq.Code,
                eq.Type,
                eq.Location,
                eq.SetPointTemperature,
                eq.CalibrationDueDate,
                eq.Vendor,
                eq.CdsSoftware,
                eq.ConnectionSettings,
                w.SectionAId,
                EquipmentInventoryId: asset.Id));

            Assert.Equal(asset.Id, res.EquipmentInventoryId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Equipment.AsNoTracking().SingleAsync(e => e.Id == eq.Id);
        Assert.Equal(asset.Id, persisted.EquipmentInventoryId);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Equipment) && a.EntityId == eq.Id.ToString() && a.Action == "Update")
            .SingleAsync();
        Assert.Equal(w.HeadAId, audit.UserId);
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(audit.PreviousValue!).RootElement.GetProperty("EquipmentInventoryId").ValueKind);
        Assert.Equal(asset.Id, JsonDocument.Parse(audit.NewValue!).RootElement.GetProperty("EquipmentInventoryId").GetInt32());
    }

    [PostgresFact]
    public async Task Clearing_the_asset_link_persists_and_is_audited()
    {
        var w = await ArrangeSectionsAndHeadsAsync();
        var asset = await SeedAssetAsync(w.SectionAId, "AST-CLR", w.HeadAId);
        var eq = await SeedWorkflowEquipmentAsync(w.SectionAId, "WF-CLR", w.HeadAId, equipmentInventoryId: asset.Id);

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedUtc)))
        {
            actDb.CurrentUserId = w.HeadAId;
            actDb.ExpectedVersion = eq.Version;
            var res = await MasterDataService(actDb).UpdateEquipmentAsync(w.HeadAId, eq.Id, new UpdateEquipmentRequest(
                eq.Name,
                eq.Code,
                eq.Type,
                eq.Location,
                eq.SetPointTemperature,
                eq.CalibrationDueDate,
                eq.Vendor,
                eq.CdsSoftware,
                eq.ConnectionSettings,
                w.SectionAId,
                EquipmentInventoryId: null));

            Assert.Null(res.EquipmentInventoryId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Equipment.AsNoTracking().SingleAsync(e => e.Id == eq.Id);
        Assert.Null(persisted.EquipmentInventoryId);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Equipment) && a.EntityId == eq.Id.ToString() && a.Action == "Update")
            .SingleAsync();
        Assert.Equal(w.HeadAId, audit.UserId);
        Assert.Equal(asset.Id, JsonDocument.Parse(audit.PreviousValue!).RootElement.GetProperty("EquipmentInventoryId").GetInt32());
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(audit.NewValue!).RootElement.GetProperty("EquipmentInventoryId").ValueKind);
    }

    [PostgresFact]
    public async Task Linking_an_asset_already_linked_elsewhere_is_rejected()
    {
        var w = await ArrangeSectionsAndHeadsAsync();
        var asset = await SeedAssetAsync(w.SectionAId, "AST-DUP", w.HeadAId);
        await SeedWorkflowEquipmentAsync(w.SectionAId, "WF-1", w.HeadAId, equipmentInventoryId: asset.Id);
        var eq2 = await SeedWorkflowEquipmentAsync(w.SectionAId, "WF-2", w.HeadAId);

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedUtc)))
        {
            actDb.CurrentUserId = w.HeadAId;
            actDb.ExpectedVersion = eq2.Version;
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                MasterDataService(actDb).UpdateEquipmentAsync(w.HeadAId, eq2.Id, new UpdateEquipmentRequest(
                    eq2.Name,
                    eq2.Code,
                    eq2.Type,
                    eq2.Location,
                    eq2.SetPointTemperature,
                    eq2.CalibrationDueDate,
                    eq2.Vendor,
                    eq2.CdsSoftware,
                    eq2.ConnectionSettings,
                    w.SectionAId,
                    EquipmentInventoryId: asset.Id)));

            Assert.Equal("This asset is already linked to another instrument.", ex.Message);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Equipment.AsNoTracking().SingleAsync(e => e.Id == eq2.Id);
        Assert.Null(persisted.EquipmentInventoryId);
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(
            a => a.EntityName == nameof(Equipment) && a.EntityId == eq2.Id.ToString() && a.Action == "Update"));
    }

    [PostgresFact]
    public async Task Linking_an_asset_outside_callers_sections_is_rejected()
    {
        var w = await ArrangeSectionsAndHeadsAsync();
        var assetInB = await SeedAssetAsync(w.SectionBId, "AST-LABB", w.HeadBId);
        var eqInA = await SeedWorkflowEquipmentAsync(w.SectionAId, "WF-LABA", w.HeadAId);

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedUtc)))
        {
            actDb.CurrentUserId = w.HeadAId;
            actDb.ExpectedVersion = eqInA.Version;
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                MasterDataService(actDb).UpdateEquipmentAsync(w.HeadAId, eqInA.Id, new UpdateEquipmentRequest(
                    eqInA.Name,
                    eqInA.Code,
                    eqInA.Type,
                    eqInA.Location,
                    eqInA.SetPointTemperature,
                    eqInA.CalibrationDueDate,
                    eqInA.Vendor,
                    eqInA.CdsSoftware,
                    eqInA.ConnectionSettings,
                    w.SectionAId,
                    EquipmentInventoryId: assetInB.Id)));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Equipment.AsNoTracking().SingleAsync(e => e.Id == eqInA.Id);
        Assert.Null(persisted.EquipmentInventoryId);
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(
            a => a.EntityName == nameof(Equipment) && a.EntityId == eqInA.Id.ToString() && a.Action == "Update"));
    }
}

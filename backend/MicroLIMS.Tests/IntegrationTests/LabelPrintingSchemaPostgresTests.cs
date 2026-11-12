using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class LabelPrintingSchemaPostgresTests
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private static readonly DateTime FixedUtc = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    private readonly PostgresTestFixture _fixture;

    public LabelPrintingSchemaPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private async Task<LabelFormat> GetFormatAsync(LabelEntityType type)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.LabelFormats.AsNoTracking().SingleAsync(f => f.EntityType == type);
    }

    private LabelPrintLog NewPrintLog(
        LabelFormat format,
        int entityId,
        int printNumber = 1,
        string code = "S:FP1026001",
        int copies = 1,
        string? reason = null) => new()
    {
        EntityType = format.EntityType,
        EntityId = entityId,
        Code = code,
        PrintNumber = printNumber,
        IsReprint = printNumber > 1,
        Copies = copies,
        LabelFormatId = format.Id,
        LabelFormatVersion = format.Version,
        Reason = reason,
        PrintedByUserId = _fixture.SeededUserId,
        PrintedAt = FixedUtc,
        BatchId = Guid.NewGuid()
    };

    [PostgresFact]
    public async Task Duplicate_print_number_for_same_record_is_rejected()
    {
        var format = await GetFormatAsync(LabelEntityType.Sample);
        var entityId = Random.Shared.Next(100_000, 999_999);

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.LabelPrintLogs.Add(NewPrintLog(format, entityId, printNumber: 1, code: $"FP{entityId}"));
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        actDb.LabelPrintLogs.Add(NewPrintLog(format, entityId, printNumber: 1, code: $"FP{entityId}"));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => actDb.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(UniqueViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task Update_of_print_log_is_rejected_by_trigger()
    {
        var format = await GetFormatAsync(LabelEntityType.Sample);
        var entityId = Random.Shared.Next(100_000, 999_999);
        int logId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var log = NewPrintLog(format, entityId, printNumber: 1, code: $"FP{entityId}");
            arrangeDb.LabelPrintLogs.Add(log);
            await arrangeDb.SaveChangesAsync();
            logId = log.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync(
                    "UPDATE \"LabelPrintLogs\" SET \"Copies\" = 99 WHERE \"Id\" = {0}", logId));
            Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.LabelPrintLogs.AsNoTracking().SingleAsync(l => l.Id == logId);
        Assert.Equal(1, persisted.Copies);
    }

    [PostgresFact]
    public async Task Delete_of_print_log_is_rejected_by_trigger()
    {
        var format = await GetFormatAsync(LabelEntityType.Sample);
        var entityId = Random.Shared.Next(100_000, 999_999);
        int logId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var log = NewPrintLog(format, entityId, printNumber: 1, code: $"FP{entityId}");
            arrangeDb.LabelPrintLogs.Add(log);
            await arrangeDb.SaveChangesAsync();
            logId = log.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync(
                    "DELETE FROM \"LabelPrintLogs\" WHERE \"Id\" = {0}", logId));
            Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.True(await assertDb.LabelPrintLogs.AsNoTracking().AnyAsync(l => l.Id == logId));
    }

    [PostgresFact]
    public async Task Truncate_of_print_log_is_rejected_by_trigger()
    {
        await using var actDb = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"LabelPrintLogs\" CASCADE"));
        Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TRUNCATE", ex.Message);
    }

    [PostgresFact]
    public async Task Insert_of_print_log_is_allowed_and_audited()
    {
        var format = await GetFormatAsync(LabelEntityType.MediaLot);
        var entityId = Random.Shared.Next(100_000, 999_999);
        var batchId = Guid.NewGuid();
        int logId;

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = _fixture.SeededUserId;
            var log = new LabelPrintLog
            {
                EntityType = LabelEntityType.MediaLot,
                EntityId = entityId,
                Code = $"TSA/{entityId}/26",
                PrintNumber = 1,
                IsReprint = false,
                Copies = 3,
                LabelFormatId = format.Id,
                LabelFormatVersion = format.Version,
                PrintedByUserId = _fixture.SeededUserId,
                PrintedAt = FixedUtc,
                BatchId = batchId
            };
            actDb.LabelPrintLogs.Add(log);
            await actDb.SaveChangesAsync();
            logId = log.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.LabelPrintLogs.AsNoTracking().SingleAsync(l => l.Id == logId);
        Assert.Equal(LabelEntityType.MediaLot, persisted.EntityType);
        Assert.Equal(entityId, persisted.EntityId);
        Assert.Equal(3, persisted.Copies);
        Assert.Equal(batchId, persisted.BatchId);

        var audit = await assertDb.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(LabelPrintLog)
            && a.EntityId == logId.ToString()
            && a.Action == "Create");
        Assert.Equal(_fixture.SeededUserId, audit.UserId);
        Assert.Contains($"TSA/{entityId}/26", audit.NewValue ?? string.Empty);
    }

    [PostgresFact]
    public async Task One_asset_cannot_link_to_two_workflow_equipment_rows()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        int assetId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var asset = new EquipmentInventory
            {
                InstrumentType = "Incubator",
                ManufacturerName = "Binder",
                Code = $"EQ-AST-{tag}",
                Location = "Lab 1",
                Status = EquipmentOperationalStatus.InService,
                SectionId = _fixture.SeededSectionId,
                CreatedByUserId = _fixture.SeededUserId,
                LastModifiedByUserId = _fixture.SeededUserId
            };
            arrangeDb.EquipmentInventories.Add(asset);
            await arrangeDb.SaveChangesAsync();
            assetId = asset.Id;

            arrangeDb.Equipment.Add(new Equipment
            {
                Name = $"Incubator 1 {tag}",
                Code = $"INC1-{tag}",
                Type = EquipmentType.Incubator,
                SectionId = _fixture.SeededSectionId,
                EquipmentInventoryId = assetId
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        actDb.Equipment.Add(new Equipment
        {
            Name = $"Incubator 2 {tag}",
            Code = $"INC2-{tag}",
            Type = EquipmentType.Incubator,
            SectionId = _fixture.SeededSectionId,
            EquipmentInventoryId = assetId
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => actDb.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(UniqueViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task Six_label_formats_are_seeded_one_per_type()
    {
        await using var assertDb = _fixture.CreateDbContext();
        var formats = await assertDb.LabelFormats.AsNoTracking()
            .OrderBy(f => f.EntityType)
            .ToListAsync();

        Assert.Equal(6, formats.Count);

        var byType = formats.ToDictionary(f => f.EntityType);
        Assert.Equal((50m, 25m, LabelLayout.Standard, 1), (byType[LabelEntityType.Sample].WidthMm, byType[LabelEntityType.Sample].HeightMm, byType[LabelEntityType.Sample].Layout, byType[LabelEntityType.Sample].DefaultCopies));
        Assert.Equal((40m, 20m, LabelLayout.Standard, 1), (byType[LabelEntityType.MediaLot].WidthMm, byType[LabelEntityType.MediaLot].HeightMm, byType[LabelEntityType.MediaLot].Layout, byType[LabelEntityType.MediaLot].DefaultCopies));
        Assert.Equal((50m, 25m, LabelLayout.Standard, 1), (byType[LabelEntityType.SolutionPreparation].WidthMm, byType[LabelEntityType.SolutionPreparation].HeightMm, byType[LabelEntityType.SolutionPreparation].Layout, byType[LabelEntityType.SolutionPreparation].DefaultCopies));
        Assert.Equal((25m, 10m, LabelLayout.Small, 1), (byType[LabelEntityType.Cryovial].WidthMm, byType[LabelEntityType.Cryovial].HeightMm, byType[LabelEntityType.Cryovial].Layout, byType[LabelEntityType.Cryovial].DefaultCopies));
        Assert.Equal((50m, 25m, LabelLayout.Standard, 1), (byType[LabelEntityType.MaterialStockLot].WidthMm, byType[LabelEntityType.MaterialStockLot].HeightMm, byType[LabelEntityType.MaterialStockLot].Layout, byType[LabelEntityType.MaterialStockLot].DefaultCopies));
        Assert.Equal((50m, 25m, LabelLayout.Standard, 1), (byType[LabelEntityType.EquipmentAsset].WidthMm, byType[LabelEntityType.EquipmentAsset].HeightMm, byType[LabelEntityType.EquipmentAsset].Layout, byType[LabelEntityType.EquipmentAsset].DefaultCopies));
    }

    [PostgresFact]
    public async Task Label_permissions_exist_and_are_granted_to_section_head()
    {
        await using var assertDb = _fixture.CreateDbContext();

        var permissions = await assertDb.Permissions.AsNoTracking()
            .Where(p => p.Code == PermissionConstants.LabelsReprint || p.Code == PermissionConstants.LabelsConfigure)
            .ToListAsync();
        Assert.Equal(2, permissions.Count);
        Assert.All(permissions, p => Assert.False(string.IsNullOrWhiteSpace(p.Description)));

        var sectionHead = await assertDb.Roles.AsNoTracking()
            .SingleAsync(r => r.Type == RoleType.SectionHead && r.IsSystemRole);
        var sectionHeadCodes = await assertDb.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == sectionHead.Id)
            .Select(rp => rp.Permission!.Code)
            .ToListAsync();

        Assert.Contains(PermissionConstants.LabelsReprint, sectionHeadCodes);
        Assert.Contains(PermissionConstants.LabelsConfigure, sectionHeadCodes);
    }

    [PostgresFact]
    public async Task Label_format_rejects_out_of_range_size()
    {
        await using var actDb = _fixture.CreateDbContext();
        var format = await actDb.LabelFormats.SingleAsync(f => f.EntityType == LabelEntityType.Sample);
        format.WidthMm = 5m;

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => actDb.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(CheckViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task Label_format_update_is_audited_with_old_and_new_values()
    {
        int formatId;
        decimal originalWidth;
        int originalCopies;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var format = await arrangeDb.LabelFormats.AsNoTracking().SingleAsync(f => f.EntityType == LabelEntityType.Sample);
            formatId = format.Id;
            originalWidth = format.WidthMm;
            originalCopies = format.DefaultCopies;
        }

        try
        {
            await using (var actDb = _fixture.CreateDbContext())
            {
                actDb.CurrentUserId = _fixture.SeededUserId;
                var format = await actDb.LabelFormats.SingleAsync(f => f.Id == formatId);
                format.WidthMm = 60m;
                format.DefaultCopies = 2;
                await actDb.SaveChangesAsync();
            }

            await using var assertDb = _fixture.CreateDbContext();
            var persisted = await assertDb.LabelFormats.AsNoTracking().SingleAsync(f => f.Id == formatId);
            Assert.Equal(60m, persisted.WidthMm);
            Assert.Equal(2, persisted.DefaultCopies);

            var audit = await assertDb.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == nameof(LabelFormat)
                    && a.EntityId == formatId.ToString()
                    && a.Action == "Update")
                .OrderByDescending(a => a.Id)
                .FirstAsync();
            Assert.Equal(_fixture.SeededUserId, audit.UserId);
            Assert.Contains("\"WidthMm\":50", audit.PreviousValue ?? string.Empty);
            Assert.Contains("\"WidthMm\":60", audit.NewValue ?? string.Empty);
            Assert.Contains("\"DefaultCopies\":1", audit.PreviousValue ?? string.Empty);
            Assert.Contains("\"DefaultCopies\":2", audit.NewValue ?? string.Empty);
        }
        finally
        {
            await using var restoreDb = _fixture.CreateDbContext();
            var format = await restoreDb.LabelFormats.SingleAsync(f => f.Id == formatId);
            format.WidthMm = originalWidth;
            format.DefaultCopies = originalCopies;
            await restoreDb.SaveChangesAsync();
        }
    }

    [PostgresFact]
    public async Task Cryovial_code_of_250_characters_is_accepted()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var code250 = $"{tag}-{new string('A', 250 - tag.Length - 1)}";
        Assert.Equal(250, code250.Length);

        int materialId;
        int organismId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var organism = new Organism { ScientificName = $"Test organism {tag}" };
            arrangeDb.Organisms.Add(organism);

            var material = new Material
            {
                SectionId = _fixture.SeededSectionId,
                MaterialType = MaterialType.LyophilizedMicroorganism,
                MaterialName = $"Strain {tag}",
                ManufacturerName = "Test Mfr",
                BatchNumber = $"B-{tag}",
                ReceivingDate = FixedUtc,
                Location = "Freezer",
                QuantityReceived = 10,
                QuantityRemaining = 10,
                Unit = MaterialUnit.Vial,
                CreatedByUserId = _fixture.SeededUserId,
                LastModifiedByUserId = _fixture.SeededUserId
            };
            arrangeDb.Materials.Add(material);
            await arrangeDb.SaveChangesAsync();

            materialId = material.Id;
            organismId = organism.Id;
        }

        int cryovialId;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var cryovial = new Cryovial
            {
                Code = code250,
                MaterialId = materialId,
                OrganismId = organismId,
                OrganismNameSnapshot = $"Test organism {tag}",
                ManufacturerName = "Test Mfr",
                ExpiryDate = FixedUtc.AddYears(1),
                NumberOfVialsPrepared = 5,
                VialsRemaining = 5,
                StorageCondition = "-80C",
                PhysicalCheckConfirmed = true,
                PhysicalCheckText = "Intact",
                PreparedAt = FixedUtc,
                PreparedByUserId = _fixture.SeededUserId
            };
            actDb.Cryovials.Add(cryovial);
            await actDb.SaveChangesAsync();
            cryovialId = cryovial.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Cryovials.AsNoTracking().SingleAsync(c => c.Id == cryovialId);
        Assert.Equal(code250, persisted.Code);
    }
}

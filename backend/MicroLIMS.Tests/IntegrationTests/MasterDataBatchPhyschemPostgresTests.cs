using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.ArchitectureTests;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MasterDataBatchPhyschemPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public MasterDataBatchPhyschemPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static MaterialMasterService BuildMaterialService(MicroLimsDbContext db, TimeProvider? clock = null)
    {
        var seq = new DatabaseSequenceHelper(db);
        return new MaterialMasterService(
            db,
            new UserSectionScopeService(db),
            new ElectronicSignatureService(db),
            new AuditEventService(db, seq),
            clock,
            new MasterDataBatchService(db));
    }

    private static SolutionMasterService BuildSolutionService(MicroLimsDbContext db, TimeProvider? clock = null)
    {
        var seq = new DatabaseSequenceHelper(db);
        return new SolutionMasterService(
            db,
            new UserSectionScopeService(db),
            new AuditEventService(db, seq),
            clock,
            new MasterDataBatchService(db));
    }

    private static EquipmentMasterDataService BuildEquipmentService(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db), new MasterDataBatchService(db));

    private async Task<(int HeadUserId, int OwnSectionId, int OtherSectionId)> SeedScopedHeadAsync(string suffix)
    {
        await using var db = _fixture.CreateDbContext();

        var own = await db.DocumentSections.FirstAsync(s => s.Code == LaboratoryCodes.Microbiology);
        var other = await db.DocumentSections.FirstOrDefaultAsync(s => s.Id != own.Id && s.IsActive);
        if (other is null)
        {
            other = new DocumentSection
            {
                Code = $"OTH-{suffix}",
                Name = $"Other Lab {suffix}",
                DepartmentId = own.DepartmentId,
                IsActive = true
            };
            db.DocumentSections.Add(other);
            await db.SaveChangesAsync();
        }

        var role = await db.Roles.FirstAsync(r => r.Type == RoleType.SectionHead);
        var user = new User
        {
            Username = $"pc_batch_head_{suffix}",
            PasswordHash = "x",
            FullName = $"PC Batch Head {suffix}",
            Email = $"pc_batch_{suffix}@test.local",
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = own.DepartmentId,
            SectionId = own.Id
        });
        await db.SaveChangesAsync();

        return (user.Id, own.Id, other.Id);
    }

    // ---- Master Testing Materials ----

    [PostgresFact]
    public async Task MaterialMaster_AllValid_CreatesAll_AuditedLikeSingleCreate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var (userId, ownSectionId, _) = await SeedScopedHeadAsync(suffix);
        var fixedNow = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(fixedNow);

        MaterialMasterEntryResponse singleResult;
        await using (var singleDb = _fixture.CreateDbContext(clock))
        {
            singleDb.CurrentUserId = userId;
            var svc = BuildMaterialService(singleDb, clock);
            singleResult = await svc.CreateAsync(
                new SaveMaterialMasterEntryRequest(
                    $"RG-S-{suffix}", $"Single Reagent {suffix}", MaterialMasterCategory.Reagent,
                    "AR", "Merck", MaterialUnit.Gram, ownSectionId),
                userId);
        }

        IReadOnlyList<MaterialMasterEntryResponse> batchResults;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            actDb.CurrentUserId = userId;
            var svc = BuildMaterialService(actDb, clock);
            batchResults = await svc.CreateBatchAsync(
                new[]
                {
                    new SaveMaterialMasterEntryRequest(
                        $"RG-B1-{suffix}", $"Batch Reagent 1 {suffix}", MaterialMasterCategory.Reagent,
                        "HPLC", "Sigma", MaterialUnit.Milliliter, ownSectionId),
                    new SaveMaterialMasterEntryRequest(
                        $"IND-B2-{suffix}", $"Batch Indicator 2 {suffix}", MaterialMasterCategory.Indicator,
                        "ACS", "Merck", MaterialUnit.Gram, ownSectionId,
                        WorkingConcentration: "1%", Solvent: "Ethanol",
                        TransitionRangeFrom: 8.2m, TransitionRangeTo: 10.0m,
                        ColourChange: "Colorless to Pink", IndicatorUse: "Acid-Base")
                },
                userId);
        }

        Assert.Equal(2, batchResults.Count);

        await using var assertDb = _fixture.CreateDbContext();
        var batchIds = batchResults.Select(r => r.Id).ToList();
        var persisted = await assertDb.MaterialMasterEntries
            .AsNoTracking()
            .Where(e => batchIds.Contains(e.Id))
            .OrderBy(e => e.Id)
            .ToListAsync();

        Assert.Equal(2, persisted.Count);
        Assert.Equal($"RG-B1-{suffix}", persisted[0].Code);
        Assert.Equal($"Batch Reagent 1 {suffix}", persisted[0].Name);
        Assert.Equal(MaterialMasterCategory.Reagent, persisted[0].Category);
        Assert.Equal(ownSectionId, persisted[0].SectionId);
        Assert.True(persisted[0].IsActive);

        Assert.Equal($"IND-B2-{suffix}", persisted[1].Code);
        Assert.Equal(MaterialMasterCategory.Indicator, persisted[1].Category);
        Assert.Equal("1%", persisted[1].WorkingConcentration);
        Assert.Equal(8.2m, persisted[1].TransitionRangeFrom);

        var singleIdStr = singleResult.Id.ToString();
        var batchIdStrs = batchIds.Select(id => id.ToString()).ToList();

        var singleAudit = await assertDb.AuditLogs
            .AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(MaterialMasterEntry) && a.Action == "Create" && a.EntityId == singleIdStr);

        var batchAudits = await assertDb.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(MaterialMasterEntry) && a.Action == "Create" && batchIdStrs.Contains(a.EntityId))
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(2, batchAudits.Count);

        var singlePayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(singleAudit.NewValue!)!;
        for (var i = 0; i < batchAudits.Count; i++)
        {
            var batchAudit = batchAudits[i];
            var entry = persisted[i];
            Assert.Equal(singleAudit.UserId, batchAudit.UserId);
            Assert.Equal(singleAudit.Action, batchAudit.Action);
            Assert.Equal(singleAudit.EntityName, batchAudit.EntityName);
            Assert.Equal(singleAudit.PreviousValue, batchAudit.PreviousValue);
            Assert.Equal(singleAudit.Timestamp, batchAudit.Timestamp);

            var batchPayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(batchAudit.NewValue!)!;
            Assert.Equal(singlePayload.Keys.OrderBy(k => k), batchPayload.Keys.OrderBy(k => k));
            Assert.Equal(entry.Id, batchPayload["Id"].GetInt32());
            Assert.Equal(entry.Code, batchPayload["Code"].GetString());
            Assert.Equal(entry.Name, batchPayload["Name"].GetString());
            Assert.Equal(entry.SectionId, batchPayload["SectionId"].GetInt32());
        }
    }

    [PostgresFact]
    public async Task MaterialMaster_OneInvalid_NoneCreated_RowAndMessageReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var (userId, ownSectionId, _) = await SeedScopedHeadAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildMaterialService(actDb);
            var rows = new[]
            {
                new SaveMaterialMasterEntryRequest(
                    $"RG-OK-{suffix}", $"Valid {suffix}", MaterialMasterCategory.Reagent,
                    null, null, MaterialUnit.Gram, ownSectionId),
                // Prefix rule enforced only by single CreateAsync (Reagent requires RG-).
                new SaveMaterialMasterEntryRequest(
                    $"BAD-PREFIX-{suffix}", $"Wrong Prefix {suffix}", MaterialMasterCategory.Reagent,
                    null, null, MaterialUnit.Gram, ownSectionId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateBatchAsync(rows, userId));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("Reagent codes must start with \"RG-\".", err.Message);
            Assert.Empty(actDb.ChangeTracker.Entries<MaterialMasterEntry>());
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.MaterialMasterEntries.AsNoTracking().AnyAsync(e =>
            e.Code == $"RG-OK-{suffix}" || e.Code == $"BAD-PREFIX-{suffix}"));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(MaterialMasterEntry) && a.Action == "Create" && a.NewValue != null && a.NewValue.Contains($"RG-OK-{suffix}")));
    }

    [PostgresFact]
    public async Task MaterialMaster_DuplicateCodeInRequest_BothRowsReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var (userId, ownSectionId, _) = await SeedScopedHeadAsync(suffix);
        var dupCode = $"RG-DUP-{suffix}";

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildMaterialService(actDb);
            var rows = new[]
            {
                new SaveMaterialMasterEntryRequest(dupCode, "Mat 1", MaterialMasterCategory.Reagent, null, null, MaterialUnit.Gram, ownSectionId),
                new SaveMaterialMasterEntryRequest($"RG-UNIQ-{suffix}", "Mat 2", MaterialMasterCategory.Reagent, null, null, MaterialUnit.Gram, ownSectionId),
                new SaveMaterialMasterEntryRequest(dupCode.ToLowerInvariant(), "Mat 3", MaterialMasterCategory.Reagent, null, null, MaterialUnit.Gram, ownSectionId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateBatchAsync(rows, userId));
            Assert.Contains(ex.FieldErrors, e => e.Row == 0 && e.Field == "code");
            Assert.Contains(ex.FieldErrors, e => e.Row == 2 && e.Field == "code");
            Assert.DoesNotContain(ex.FieldErrors, e => e.Row == 1);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.MaterialMasterEntries.AsNoTracking().AnyAsync(e =>
            e.Code == dupCode || e.Code == $"RG-UNIQ-{suffix}"));
    }

    [PostgresFact]
    public async Task MaterialMaster_ExistingCode_Reported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var (userId, ownSectionId, _) = await SeedScopedHeadAsync(suffix);
        var existingCode = $"RG-EX-{suffix}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.MaterialMasterEntries.Add(new MaterialMasterEntry
            {
                Code = existingCode,
                Name = $"Existing {suffix}",
                Category = MaterialMasterCategory.Reagent,
                BaseUnit = MaterialUnit.Gram,
                SectionId = ownSectionId,
                IsActive = true,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = userId,
                LastModifiedAt = DateTime.UtcNow
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildMaterialService(actDb);
            var rows = new[]
            {
                new SaveMaterialMasterEntryRequest($"RG-FRESH-{suffix}", "Fresh", MaterialMasterCategory.Reagent, null, null, MaterialUnit.Gram, ownSectionId),
                new SaveMaterialMasterEntryRequest(existingCode.ToLowerInvariant(), "Collision", MaterialMasterCategory.Reagent, null, null, MaterialUnit.Gram, ownSectionId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateBatchAsync(rows, userId));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("code", err.Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.MaterialMasterEntries.AsNoTracking().AnyAsync(e => e.Code == $"RG-FRESH-{suffix}"));
    }

    [PostgresFact]
    public async Task MaterialMaster_201Rows_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var (userId, ownSectionId, _) = await SeedScopedHeadAsync(suffix);
        var rows = Enumerable.Range(0, 201)
            .Select(i => new SaveMaterialMasterEntryRequest(
                $"RG-201-{i}-{suffix}", $"Mat {i}", MaterialMasterCategory.Reagent, null, null, MaterialUnit.Gram, ownSectionId))
            .ToList();

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildMaterialService(actDb);
            await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateBatchAsync(rows, userId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.MaterialMasterEntries.AsNoTracking().AnyAsync(e => e.Code.Contains(suffix)));
    }

    [PostgresFact]
    public async Task MaterialMaster_SectionICannotConfigure_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var (userId, ownSectionId, otherSectionId) = await SeedScopedHeadAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildMaterialService(actDb);
            var rows = new[]
            {
                new SaveMaterialMasterEntryRequest($"RG-OWN-{suffix}", "Own", MaterialMasterCategory.Reagent, null, null, MaterialUnit.Gram, ownSectionId),
                new SaveMaterialMasterEntryRequest($"RG-OTH-{suffix}", "Other", MaterialMasterCategory.Reagent, null, null, MaterialUnit.Gram, otherSectionId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateBatchAsync(rows, userId));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("sectionId", err.Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.MaterialMasterEntries.AsNoTracking().AnyAsync(e =>
            e.Code == $"RG-OWN-{suffix}" || e.Code == $"RG-OTH-{suffix}"));
    }

    [PostgresFact]
    public async Task MaterialMaster_FailureInsideTransaction_RollsBackEarlierRows()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var (userId, ownSectionId, _) = await SeedScopedHeadAsync(suffix);

        var catPrefix = $"X{suffix[..4]}";
        int inactiveCategoryId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var cat = new MaterialCategory
            {
                Name = $"Inactive Cat {suffix}",
                CodePrefix = catPrefix,
                DefaultUnit = MaterialUnit.Gram,
                IsActive = false,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = userId,
                LastModifiedAt = DateTime.UtcNow
            };
            arrangeDb.MaterialCategories.Add(cat);
            await arrangeDb.SaveChangesAsync();
            inactiveCategoryId = cat.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildMaterialService(actDb);
            // Both rows pass pre-validation (MaterialCategory exists), then row 0 saves inside the
            // transaction and row 1 fails in ResolveMaterialCategoryAsync because the category is inactive.
            var rows = new[]
            {
                new SaveMaterialMasterEntryRequest($"RG-TX0-{suffix}", $"TX0 {suffix}", MaterialMasterCategory.Reagent, null, null, MaterialUnit.Gram, ownSectionId),
                new SaveMaterialMasterEntryRequest($"{catPrefix}-TX1-{suffix}", $"TX1 {suffix}", MaterialMasterCategory.Other, null, null, MaterialUnit.Gram, ownSectionId, MaterialCategoryId: inactiveCategoryId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateBatchAsync(rows, userId));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Empty(actDb.ChangeTracker.Entries<MaterialMasterEntry>());
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.MaterialMasterEntries.AsNoTracking().AnyAsync(e =>
            e.Code == $"RG-TX0-{suffix}" || e.Code == $"{catPrefix}-TX1-{suffix}"));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(MaterialMasterEntry)
            && a.Action == "Create"
            && a.NewValue != null
            && (a.NewValue.Contains($"RG-TX0-{suffix}") || a.NewValue.Contains($"{catPrefix}-TX1-{suffix}"))));
    }

    [PostgresFact]
    public async Task MaterialMaster_WithoutPermission_403()
    {
        var method = typeof(MaterialMasterController).GetMethod(nameof(MaterialMasterController.CreateBatch));
        Assert.NotNull(method);

        var post = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("batch", post!.Template);

        var auth = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(auth);
        Assert.Equal(PermissionConstants.MasterDataManage, auth!.Policy);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());

        var allowed = AuthorizationMatrixTests.AllowedRoles<MaterialMasterController>(nameof(MaterialMasterController.CreateBatch));
        var singleAllowed = AuthorizationMatrixTests.AllowedRoles<MaterialMasterController>(nameof(MaterialMasterController.Create));
        Assert.NotNull(allowed);
        Assert.Equal(singleAllowed, allowed);
        Assert.Contains(RoleType.SystemAdministrator, allowed);
        Assert.Contains(RoleType.SectionHead, allowed);
        Assert.DoesNotContain(RoleType.Analyst, allowed);
        Assert.DoesNotContain(RoleType.Reviewer, allowed);
        Assert.DoesNotContain(RoleType.Qa, allowed);

        await using var assertDb = _fixture.CreateDbContext();
        var perm = await assertDb.Permissions.AsNoTracking().SingleAsync(p => p.Code == PermissionConstants.MasterDataManage);
        Assert.True(perm.IsEnforced);
    }

    // ---- Solution Master ----

    private async Task<(int HeadUserId, int OwnSectionId, int OtherSectionId, int ActiveEntryId, int InactiveEntryId)> SeedSolutionScopeAsync(string suffix)
    {
        var (userId, ownSectionId, otherSectionId) = await SeedScopedHeadAsync(suffix);

        await using var db = _fixture.CreateDbContext();
        var active = new MaterialMasterEntry
        {
            Code = $"RG-ACT-{suffix}",
            Name = $"Active Reagent {suffix}",
            Category = MaterialMasterCategory.Reagent,
            BaseUnit = MaterialUnit.Gram,
            SectionId = ownSectionId,
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = userId,
            LastModifiedAt = DateTime.UtcNow
        };
        var inactive = new MaterialMasterEntry
        {
            Code = $"RG-INACT-{suffix}",
            Name = $"Inactive Reagent {suffix}",
            Category = MaterialMasterCategory.Reagent,
            BaseUnit = MaterialUnit.Gram,
            SectionId = ownSectionId,
            IsActive = false,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = userId,
            LastModifiedAt = DateTime.UtcNow
        };
        db.MaterialMasterEntries.AddRange(active, inactive);
        await db.SaveChangesAsync();

        return (userId, ownSectionId, otherSectionId, active.Id, inactive.Id);
    }

    private static SaveSolutionMasterRequest BuildSolutionRequest(string name, int sectionId, int entryId, int shelfLife = 30) =>
        new(
            Name: name,
            Type: SolutionType.Reagent,
            ShelfLifeValue: shelfLife,
            ShelfLifeUnit: ShelfLifeUnit.Days,
            StorageCondition: "2-8 C",
            FinalVolumeMl: 100m,
            Instructions: "Dissolve in purified water.",
            Components: new List<SolutionComponentInput>
            {
                new(entryId, 5m, SolutionComponentUnit.Gram)
            },
            SectionId: sectionId);

    [PostgresFact]
    public async Task SolutionMaster_AllValid_CreatesAll_AuditedLikeSingleCreate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, _, activeEntryId, _) = await SeedSolutionScopeAsync(suffix);
        var fixedNow = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(fixedNow);

        SolutionMasterResponse singleResult;
        await using (var singleDb = _fixture.CreateDbContext(clock))
        {
            singleDb.CurrentUserId = userId;
            var svc = BuildSolutionService(singleDb, clock);
            singleResult = await svc.CreateAsync(BuildSolutionRequest($"Sol-S-{suffix}", ownSectionId, activeEntryId), userId);
        }

        IReadOnlyList<SolutionMasterResponse> batchResults;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSolutionService(actDb, clock);
            batchResults = await svc.CreateBatchAsync(
                new[]
                {
                    BuildSolutionRequest($"Sol-B1-{suffix}", ownSectionId, activeEntryId),
                    BuildSolutionRequest($"Sol-B2-{suffix}", ownSectionId, activeEntryId) with { SterilizationRequired = true, FinalVolumeMl = 250m }
                },
                userId);
        }

        Assert.Equal(2, batchResults.Count);

        await using var assertDb = _fixture.CreateDbContext();
        var batchIds = batchResults.Select(r => r.Id).ToList();
        var persisted = await assertDb.SolutionMasters
            .AsNoTracking()
            .Include(s => s.Components)
            .Where(s => batchIds.Contains(s.Id))
            .OrderBy(s => s.Id)
            .ToListAsync();

        Assert.Equal(2, persisted.Count);
        Assert.Equal($"Sol-B1-{suffix}", persisted[0].Name);
        Assert.Equal(100m, persisted[0].FinalVolumeMl);
        Assert.Single(persisted[0].Components);
        Assert.Equal(activeEntryId, persisted[0].Components[0].MaterialMasterEntryId);

        Assert.Equal($"Sol-B2-{suffix}", persisted[1].Name);
        Assert.Equal(250m, persisted[1].FinalVolumeMl);
        Assert.True(persisted[1].SterilizationRequired);

        var singleIdStr = singleResult.Id.ToString();
        var batchIdStrs = batchIds.Select(id => id.ToString()).ToList();

        var singleAudit = await assertDb.AuditLogs
            .AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(SolutionMaster) && a.Action == "Create" && a.EntityId == singleIdStr);

        var batchAudits = await assertDb.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(SolutionMaster) && a.Action == "Create" && batchIdStrs.Contains(a.EntityId))
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(2, batchAudits.Count);

        var singlePayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(singleAudit.NewValue!)!;
        for (var i = 0; i < batchAudits.Count; i++)
        {
            var batchAudit = batchAudits[i];
            var sol = persisted[i];
            Assert.Equal(singleAudit.UserId, batchAudit.UserId);
            Assert.Equal(singleAudit.Action, batchAudit.Action);
            Assert.Equal(singleAudit.EntityName, batchAudit.EntityName);
            Assert.Equal(singleAudit.PreviousValue, batchAudit.PreviousValue);
            Assert.Equal(singleAudit.Timestamp, batchAudit.Timestamp);

            var batchPayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(batchAudit.NewValue!)!;
            Assert.Equal(singlePayload.Keys.OrderBy(k => k), batchPayload.Keys.OrderBy(k => k));
            Assert.Equal(sol.Id, batchPayload["Id"].GetInt32());
            Assert.Equal(sol.Name, batchPayload["Name"].GetString());
            Assert.Equal(sol.SectionId, batchPayload["SectionId"].GetInt32());
        }
    }

    [PostgresFact]
    public async Task SolutionMaster_OneInvalid_NoneCreated_RowAndMessageReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, _, activeEntryId, _) = await SeedSolutionScopeAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSolutionService(actDb);
            var rows = new[]
            {
                BuildSolutionRequest($"Sol-OK-{suffix}", ownSectionId, activeEntryId),
                BuildSolutionRequest($"Sol-BadLife-{suffix}", ownSectionId, activeEntryId, shelfLife: 0)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateBatchAsync(rows, userId));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("Shelf life must be greater than zero.", err.Message);
            Assert.Empty(actDb.ChangeTracker.Entries<SolutionMaster>());
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.SolutionMasters.AsNoTracking().AnyAsync(s => s.SectionId == ownSectionId && s.Name.Contains(suffix)));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(SolutionMaster) && a.Action == "Create" && a.NewValue != null && a.NewValue.Contains($"Sol-OK-{suffix}")));
    }

    // SolutionMaster has no Code field; uniqueness is per (SectionId, Name) enforced inside CreateAsync,
    // so a duplicate name within the same section fails on the later row inside the transaction and rolls back.
    [PostgresFact]
    public async Task SolutionMaster_DuplicateNameInRequest_LaterRowReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, _, activeEntryId, _) = await SeedSolutionScopeAsync(suffix);
        var dupName = $"Sol-Dup-{suffix}";

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSolutionService(actDb);
            var rows = new[]
            {
                BuildSolutionRequest(dupName, ownSectionId, activeEntryId),
                BuildSolutionRequest($"Sol-Mid-{suffix}", ownSectionId, activeEntryId),
                BuildSolutionRequest(dupName, ownSectionId, activeEntryId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateBatchAsync(rows, userId));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(2, err.Row);
            Assert.Equal($"A solution named \"{dupName}\" already exists.", err.Message);
            Assert.Empty(actDb.ChangeTracker.Entries<SolutionMaster>());
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.SolutionMasters.AsNoTracking().AnyAsync(s => s.SectionId == ownSectionId && s.Name.Contains(suffix)));
    }

    [PostgresFact]
    public async Task SolutionMaster_ExistingName_Reported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, _, activeEntryId, _) = await SeedSolutionScopeAsync(suffix);
        var existingName = $"Sol-Ex-{suffix}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.SolutionMasters.Add(new SolutionMaster
            {
                SectionId = ownSectionId,
                Name = existingName,
                Type = SolutionType.Reagent,
                ShelfLifeValue = 30,
                ShelfLifeUnit = ShelfLifeUnit.Days,
                StorageCondition = "RT",
                FinalVolumeMl = 100m,
                Instructions = "Mix",
                IsActive = true,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = userId,
                LastModifiedAt = DateTime.UtcNow
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSolutionService(actDb);
            var rows = new[]
            {
                BuildSolutionRequest($"Sol-Fresh-{suffix}", ownSectionId, activeEntryId),
                BuildSolutionRequest(existingName, ownSectionId, activeEntryId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateBatchAsync(rows, userId));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal($"A solution named \"{existingName}\" already exists.", err.Message);
            Assert.Empty(actDb.ChangeTracker.Entries<SolutionMaster>());
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.SolutionMasters.AsNoTracking().AnyAsync(s => s.Name == $"Sol-Fresh-{suffix}"));
        Assert.Equal(1, await assertDb.SolutionMasters.AsNoTracking().CountAsync(s => s.Name == existingName && s.SectionId == ownSectionId));
    }

    [PostgresFact]
    public async Task SolutionMaster_201Rows_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, _, activeEntryId, _) = await SeedSolutionScopeAsync(suffix);
        var rows = Enumerable.Range(0, 201)
            .Select(i => BuildSolutionRequest($"Sol-201-{i}-{suffix}", ownSectionId, activeEntryId))
            .ToList();

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSolutionService(actDb);
            await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateBatchAsync(rows, userId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.SolutionMasters.AsNoTracking().AnyAsync(s => s.Name.Contains(suffix)));
    }

    [PostgresFact]
    public async Task SolutionMaster_SectionICannotConfigure_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, otherSectionId, activeEntryId, _) = await SeedSolutionScopeAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSolutionService(actDb);
            var rows = new[]
            {
                BuildSolutionRequest($"Sol-Own-{suffix}", ownSectionId, activeEntryId),
                BuildSolutionRequest($"Sol-Oth-{suffix}", otherSectionId, activeEntryId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateBatchAsync(rows, userId));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("sectionId", err.Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.SolutionMasters.AsNoTracking().AnyAsync(s => s.Name.Contains(suffix)));
    }

    [PostgresFact]
    public async Task SolutionMaster_FailureInsideTransaction_RollsBackEarlierRows()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, _, activeEntryId, inactiveEntryId) = await SeedSolutionScopeAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSolutionService(actDb);
            // Both rows pass pre-validation (both MaterialMasterEntryIds exist); row 0 saves inside the
            // transaction and row 1 fails because inactiveEntryId is inactive.
            var rows = new[]
            {
                BuildSolutionRequest($"Sol-TX0-{suffix}", ownSectionId, activeEntryId),
                BuildSolutionRequest($"Sol-TX1-{suffix}", ownSectionId, inactiveEntryId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateBatchAsync(rows, userId));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Empty(actDb.ChangeTracker.Entries<SolutionMaster>());
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.SolutionMasters.AsNoTracking().AnyAsync(s => s.Name.Contains(suffix)));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(SolutionMaster)
            && a.Action == "Create"
            && a.NewValue != null
            && (a.NewValue.Contains($"Sol-TX0-{suffix}") || a.NewValue.Contains($"Sol-TX1-{suffix}"))));
    }

    [PostgresFact]
    public async Task SolutionMaster_WithoutPermission_403()
    {
        var method = typeof(SolutionMasterController).GetMethod(nameof(SolutionMasterController.CreateBatch));
        Assert.NotNull(method);

        var post = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("batch", post!.Template);

        var auth = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(auth);
        Assert.Equal(PermissionConstants.MasterDataManage, auth!.Policy);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());

        var allowed = AuthorizationMatrixTests.AllowedRoles<SolutionMasterController>(nameof(SolutionMasterController.CreateBatch));
        var singleAllowed = AuthorizationMatrixTests.AllowedRoles<SolutionMasterController>(nameof(SolutionMasterController.Create));
        Assert.NotNull(allowed);
        Assert.Equal(singleAllowed, allowed);
        Assert.Contains(RoleType.SystemAdministrator, allowed);
        Assert.Contains(RoleType.SectionHead, allowed);
        Assert.DoesNotContain(RoleType.Analyst, allowed);
        Assert.DoesNotContain(RoleType.Reviewer, allowed);
        Assert.DoesNotContain(RoleType.Qa, allowed);

        await using var assertDb = _fixture.CreateDbContext();
        var perm = await assertDb.Permissions.AsNoTracking().SingleAsync(p => p.Code == PermissionConstants.MasterDataManage);
        Assert.True(perm.IsEnforced);
    }

    // ---- Equipment Master ----

    [PostgresFact]
    public async Task Equipment_AllValid_CreatesAll_AuditedLikeSingleCreate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, _) = await SeedScopedHeadAsync(suffix);
        var fixedNow = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(fixedNow);

        EquipmentResponse singleResult;
        await using (var singleDb = _fixture.CreateDbContext(clock))
        {
            singleDb.CurrentUserId = userId;
            var svc = BuildEquipmentService(singleDb);
            singleResult = await svc.CreateEquipmentAsync(
                userId,
                new CreateEquipmentRequest(
                    $"Single Inc {suffix}", $"EQ-S-{suffix}", EquipmentType.Incubator,
                    "Lab A", 35.0m, null, "Memmert", null, null, ownSectionId));
        }

        IReadOnlyList<EquipmentResponse> batchResults;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEquipmentService(actDb);
            batchResults = await svc.CreateEquipmentBatchAsync(
                userId,
                new[]
                {
                    new CreateEquipmentRequest(
                        $"Batch Inc 1 {suffix}", $"EQ-B1-{suffix}", EquipmentType.Incubator,
                        "Lab B", 22.5m, null, "Binder", null, null, ownSectionId),
                    new CreateEquipmentRequest(
                        $"Batch HPLC 2 {suffix}", $"EQ-B2-{suffix}", EquipmentType.Hplc,
                        "Chem Lab", null, null, "Shimadzu", CdsSoftware.ShimadzuLabSolutions, null, ownSectionId)
                });
        }

        Assert.Equal(2, batchResults.Count);

        await using var assertDb = _fixture.CreateDbContext();
        var batchIds = batchResults.Select(r => r.Id).ToList();
        var persisted = await assertDb.Equipment
            .AsNoTracking()
            .Where(e => batchIds.Contains(e.Id))
            .OrderBy(e => e.Id)
            .ToListAsync();

        Assert.Equal(2, persisted.Count);
        Assert.Equal($"EQ-B1-{suffix}", persisted[0].Code);
        Assert.Equal($"Batch Inc 1 {suffix}", persisted[0].Name);
        Assert.Equal(35.0m, singleResult.SetPointTemperature);
        Assert.Equal(22.5m, persisted[0].SetPointTemperature);
        Assert.Equal(ownSectionId, persisted[0].SectionId);

        Assert.Equal($"EQ-B2-{suffix}", persisted[1].Code);
        Assert.Equal(EquipmentType.Hplc, persisted[1].Type);
        Assert.Equal(CdsSoftware.ShimadzuLabSolutions, persisted[1].CdsSoftware);

        var singleIdStr = singleResult.Id.ToString();
        var batchIdStrs = batchIds.Select(id => id.ToString()).ToList();

        var singleAudit = await assertDb.AuditLogs
            .AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(Equipment) && a.Action == "Create" && a.EntityId == singleIdStr);

        var batchAudits = await assertDb.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(Equipment) && a.Action == "Create" && batchIdStrs.Contains(a.EntityId))
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(2, batchAudits.Count);

        var singlePayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(singleAudit.NewValue!)!;
        for (var i = 0; i < batchAudits.Count; i++)
        {
            var batchAudit = batchAudits[i];
            var eq = persisted[i];
            Assert.Equal(singleAudit.UserId, batchAudit.UserId);
            Assert.Equal(singleAudit.Action, batchAudit.Action);
            Assert.Equal(singleAudit.EntityName, batchAudit.EntityName);
            Assert.Equal(singleAudit.PreviousValue, batchAudit.PreviousValue);
            Assert.Equal(singleAudit.Timestamp, batchAudit.Timestamp);

            var batchPayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(batchAudit.NewValue!)!;
            Assert.Equal(singlePayload.Keys.OrderBy(k => k), batchPayload.Keys.OrderBy(k => k));
            Assert.Equal(eq.Id, batchPayload["Id"].GetInt32());
            Assert.Equal(eq.Code, batchPayload["Code"].GetString());
            Assert.Equal(eq.Name, batchPayload["Name"].GetString());
            Assert.Equal(eq.SectionId, batchPayload["SectionId"].GetInt32());
        }
    }

    [PostgresFact]
    public async Task Equipment_OneInvalid_NoneCreated_RowAndMessageReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, _) = await SeedScopedHeadAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEquipmentService(actDb);
            var rows = new[]
            {
                new CreateEquipmentRequest($"EQ OK {suffix}", $"EQ-OK-{suffix}", EquipmentType.Incubator, "Lab", 35m, null, null, null, null, ownSectionId),
                // Rule enforced only by single CreateEquipmentAsync: HPLC requires CdsSoftware.
                new CreateEquipmentRequest($"EQ Bad HPLC {suffix}", $"EQ-BAD-{suffix}", EquipmentType.Hplc, "Lab", null, null, null, CdsSoftware: null, null, ownSectionId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateEquipmentBatchAsync(userId, rows));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("CDS Software is required for HPLC equipment.", err.Message);
            Assert.Empty(actDb.ChangeTracker.Entries<Equipment>());
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Equipment.AsNoTracking().AnyAsync(e =>
            e.Code == $"EQ-OK-{suffix}" || e.Code == $"EQ-BAD-{suffix}"));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(Equipment) && a.Action == "Create" && a.NewValue != null && a.NewValue.Contains($"EQ-OK-{suffix}")));
    }

    [PostgresFact]
    public async Task Equipment_DuplicateCodeInRequest_BothRowsReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, _) = await SeedScopedHeadAsync(suffix);
        var dupCode = $"EQ-DUP-{suffix}";

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEquipmentService(actDb);
            var rows = new[]
            {
                new CreateEquipmentRequest("Eq 1", dupCode, EquipmentType.Incubator, null, null, null, null, null, null, ownSectionId),
                new CreateEquipmentRequest("Eq 2", $"EQ-UNIQ-{suffix}", EquipmentType.Incubator, null, null, null, null, null, null, ownSectionId),
                new CreateEquipmentRequest("Eq 3", dupCode.ToLowerInvariant(), EquipmentType.Incubator, null, null, null, null, null, null, ownSectionId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateEquipmentBatchAsync(userId, rows));
            Assert.Contains(ex.FieldErrors, e => e.Row == 0 && e.Field == "code");
            Assert.Contains(ex.FieldErrors, e => e.Row == 2 && e.Field == "code");
            Assert.DoesNotContain(ex.FieldErrors, e => e.Row == 1);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Equipment.AsNoTracking().AnyAsync(e =>
            e.Code == dupCode || e.Code == $"EQ-UNIQ-{suffix}"));
    }

    [PostgresFact]
    public async Task Equipment_ExistingCode_Reported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, _) = await SeedScopedHeadAsync(suffix);
        var existingCode = $"EQ-EX-{suffix}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.Equipment.Add(new Equipment
            {
                Name = $"Existing {suffix}",
                Code = existingCode,
                Type = EquipmentType.Incubator,
                SectionId = ownSectionId
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEquipmentService(actDb);
            var rows = new[]
            {
                new CreateEquipmentRequest("Fresh", $"EQ-FRESH-{suffix}", EquipmentType.Incubator, null, null, null, null, null, null, ownSectionId),
                new CreateEquipmentRequest("Collision", existingCode, EquipmentType.Incubator, null, null, null, null, null, null, ownSectionId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateEquipmentBatchAsync(userId, rows));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("code", err.Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Equipment.AsNoTracking().AnyAsync(e => e.Code == $"EQ-FRESH-{suffix}"));
    }

    [PostgresFact]
    public async Task Equipment_201Rows_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, _) = await SeedScopedHeadAsync(suffix);
        var rows = Enumerable.Range(0, 201)
            .Select(i => new CreateEquipmentRequest(
                $"Eq {i}", $"EQ-201-{i}-{suffix}", EquipmentType.Incubator, null, null, null, null, null, null, ownSectionId))
            .ToList();

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEquipmentService(actDb);
            await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateEquipmentBatchAsync(userId, rows));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Equipment.AsNoTracking().AnyAsync(e => e.Code.Contains(suffix)));
    }

    [PostgresFact]
    public async Task Equipment_SectionICannotConfigure_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, otherSectionId) = await SeedScopedHeadAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEquipmentService(actDb);
            var rows = new[]
            {
                new CreateEquipmentRequest("Own", $"EQ-OWN-{suffix}", EquipmentType.Incubator, null, null, null, null, null, null, ownSectionId),
                new CreateEquipmentRequest("Other", $"EQ-OTH-{suffix}", EquipmentType.Incubator, null, null, null, null, null, null, otherSectionId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateEquipmentBatchAsync(userId, rows));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("sectionId", err.Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Equipment.AsNoTracking().AnyAsync(e =>
            e.Code == $"EQ-OWN-{suffix}" || e.Code == $"EQ-OTH-{suffix}"));
    }

    [PostgresFact]
    public async Task Equipment_FailureInsideTransaction_RollsBackEarlierRows()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, ownSectionId, _) = await SeedScopedHeadAsync(suffix);

        int assetId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var asset = new EquipmentInventory
            {
                InstrumentType = "Incubator",
                ManufacturerName = "Memmert",
                Code = $"INV-{suffix}",
                Location = "Lab",
                SectionId = ownSectionId,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = userId,
                LastModifiedAt = DateTime.UtcNow
            };
            arrangeDb.EquipmentInventories.Add(asset);
            await arrangeDb.SaveChangesAsync();
            assetId = asset.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEquipmentService(actDb);
            // Both rows pass pre-validation; row 0 links assetId inside the transaction,
            // then row 1 fails because the same asset cannot be linked to two instruments.
            var rows = new[]
            {
                new CreateEquipmentRequest($"EQ TX0 {suffix}", $"EQ-TX0-{suffix}", EquipmentType.Incubator, null, null, null, null, null, null, ownSectionId, assetId),
                new CreateEquipmentRequest($"EQ TX1 {suffix}", $"EQ-TX1-{suffix}", EquipmentType.Incubator, null, null, null, null, null, null, ownSectionId, assetId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateEquipmentBatchAsync(userId, rows));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("This asset is already linked to another instrument.", err.Message);
            Assert.Empty(actDb.ChangeTracker.Entries<Equipment>());
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Equipment.AsNoTracking().AnyAsync(e =>
            e.Code == $"EQ-TX0-{suffix}" || e.Code == $"EQ-TX1-{suffix}"));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(Equipment)
            && a.Action == "Create"
            && a.NewValue != null
            && (a.NewValue.Contains($"EQ-TX0-{suffix}") || a.NewValue.Contains($"EQ-TX1-{suffix}"))));
    }

    [PostgresFact]
    public async Task Equipment_WithoutPermission_403()
    {
        var method = typeof(EquipmentMasterDataController).GetMethod(nameof(EquipmentMasterDataController.CreateEquipmentBatch));
        Assert.NotNull(method);

        var post = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("equipment/batch", post!.Template);

        var auth = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(auth);
        Assert.Equal(PermissionConstants.MasterDataManage, auth!.Policy);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());

        var allowed = AuthorizationMatrixTests.AllowedRoles<EquipmentMasterDataController>(nameof(EquipmentMasterDataController.CreateEquipmentBatch));
        var singleAllowed = AuthorizationMatrixTests.AllowedRoles<EquipmentMasterDataController>(nameof(EquipmentMasterDataController.CreateEquipment));
        Assert.NotNull(allowed);
        Assert.Equal(singleAllowed, allowed);
        Assert.Contains(RoleType.SystemAdministrator, allowed);
        Assert.Contains(RoleType.SectionHead, allowed);
        Assert.DoesNotContain(RoleType.Analyst, allowed);
        Assert.DoesNotContain(RoleType.Reviewer, allowed);
        Assert.DoesNotContain(RoleType.Qa, allowed);

        await using var assertDb = _fixture.CreateDbContext();
        var perm = await assertDb.Permissions.AsNoTracking().SingleAsync(p => p.Code == PermissionConstants.MasterDataManage);
        Assert.True(perm.IsEnforced);
    }
}

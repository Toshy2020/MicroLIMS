using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MasterDataCopyPostgresTests
{
    private const string TestPassword = "TestPassword!123";
    private readonly PostgresTestFixture _fixture;

    public MasterDataCopyPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static string Tag() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static MasterDataCopyService BuildCopyService(MicroLimsDbContext db)
    {
        var scope = new UserSectionScopeService(db);
        var specService = new SpecificationService(db);
        var specMasterService = new SpecificationMasterDataService(db, scope, specService);
        var itemService = new ItemService(db);
        return new MasterDataCopyService(db, scope, specService, specMasterService, itemService);
    }

    private async Task<int> SeedAdminUserAsync(MicroLimsDbContext db)
    {
        var roleId = await db.Roles
            .AsNoTracking()
            .Where(r => r.Type == RoleType.SystemAdministrator)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();

        var tag = Tag();
        var user = new User
        {
            FullName = "Copy Admin " + tag,
            Username = "copy_admin_" + tag,
            Email = $"copy_admin_{tag}@example.test",
            PasswordHash = TestPasswords.Hash(TestPassword),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<int> SeedSectionHeadAsync(MicroLimsDbContext db, DocumentSection allowedSection)
    {
        var roleId = await db.Roles
            .AsNoTracking()
            .Where(r => r.Type == RoleType.SectionHead)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();

        var tag = Tag();
        var user = new User
        {
            FullName = "Copy SectionHead " + tag,
            Username = "copy_sh_" + tag,
            Email = $"copy_sh_{tag}@example.test",
            PasswordHash = TestPasswords.Hash(TestPassword),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = allowedSection.DepartmentId,
            SectionId = allowedSection.Id
        });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static DocumentSection EnsurePhyschemSection(MicroLimsDbContext db)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var phys = db.DocumentSections.FirstOrDefault(s =>
            s.Id != micro.Id
            && (s.Code == "PHYS" || s.Code == "PHYSCHEM" || s.Name == "Physicochemical Laboratory"));
        if (phys is null)
        {
            phys = new DocumentSection
            {
                Name = "Physicochemical Laboratory",
                Code = "PHYS",
                DepartmentId = micro.DepartmentId,
                IsActive = true
            };
            db.DocumentSections.Add(phys);
            db.SaveChanges();
        }
        return phys;
    }

    private static TestDefinition SeedTestDef(MicroLimsDbContext db, int sectionId, string prefix)
    {
        var code = $"{prefix}_{Tag()}";
        var td = new TestDefinition
        {
            Code = code,
            DisplayName = $"Test {code}",
            SectionId = sectionId,
            WorkflowType = WorkflowType.Measurement
        };
        db.TestDefinitions.Add(td);
        db.SaveChanges();
        return td;
    }

    private static Specification MakeRangeSpec(
        int itemId,
        string testCode,
        string paramName,
        decimal lower = 10m,
        decimal upper = 50m,
        ProductionStageRole? stageRole = null,
        string? grade = null,
        int displayOrder = 0)
    {
        var spec = new Specification
        {
            ItemId = itemId,
            TestCode = testCode,
            ParameterName = paramName,
            DisplayOrder = displayOrder,
            LimitType = LimitType.Range,
            LowerLimit = lower,
            UpperLimit = upper,
            LowerInclusive = true,
            UpperInclusive = true,
            Unit = "%",
            ProductionStageRole = stageRole,
            Grade = grade
        };
        SpecificationService.ApplyCanonicalSpecLimit(spec);
        return spec;
    }

    [PostgresFact]
    public async Task Preview_MarksOkAlreadyPresentInvalid_SavesNothing()
    {
        var tag = Tag();
        int userId, sourceId, targetId;
        int okSpecId, dupSpecId, invSpecId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            userId = await SeedAdminUserAsync(arrangeDb);
            var micro = TestServiceFactory.EnsureMicroSection(arrangeDb);

            var tOk = SeedTestDef(arrangeDb, micro.Id, "COPY_OK");
            var tDup = SeedTestDef(arrangeDb, micro.Id, "COPY_DUP");
            var tInv = SeedTestDef(arrangeDb, micro.Id, "COPY_INV");

            var source = new Item
            {
                Code = "SRC-" + tag,
                Name = "Source " + tag,
                Category = SampleCategory.FinishedProduct,
                AssignedTests =
                {
                    new SampleTest { TestCode = tOk.Code, DisplayName = tOk.DisplayName },
                    new SampleTest { TestCode = tDup.Code, DisplayName = tDup.DisplayName },
                    new SampleTest { TestCode = tInv.Code, DisplayName = tInv.DisplayName }
                }
            };
            var target = new Item
            {
                Code = "TGT-" + tag,
                Name = "Target " + tag,
                Category = SampleCategory.RawMaterial,
                AssignedTests =
                {
                    new SampleTest { TestCode = tOk.Code, DisplayName = tOk.DisplayName },
                    new SampleTest { TestCode = tDup.Code, DisplayName = tDup.DisplayName },
                    new SampleTest { TestCode = tInv.Code, DisplayName = tInv.DisplayName }
                }
            };
            arrangeDb.Items.AddRange(source, target);
            await arrangeDb.SaveChangesAsync();

            var sOk = MakeRangeSpec(source.Id, tOk.Code, "Assay Ok");
            var sDup = MakeRangeSpec(source.Id, tDup.Code, "Moisture Dup");
            // Stage-specific rows are valid on FinishedProduct source, invalid on RawMaterial target.
            var sInv = MakeRangeSpec(source.Id, tInv.Code, "Stage Inv", stageRole: ProductionStageRole.Finished);
            var existingOnTarget = MakeRangeSpec(target.Id, tDup.Code, "Moisture Dup");
            arrangeDb.Specifications.AddRange(sOk, sDup, sInv, existingOnTarget);
            await arrangeDb.SaveChangesAsync();

            sourceId = source.Id;
            targetId = target.Id;
            okSpecId = sOk.Id;
            dupSpecId = sDup.Id;
            invSpecId = sInv.Id;
        }

        IReadOnlyList<CopySpecificationsPreviewRow> preview;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildCopyService(actDb);
            preview = await svc.PreviewAsync(
                new CopySpecificationsRequest(sourceId, targetId, [okSpecId, dupSpecId, invSpecId]),
                userId);
        }

        Assert.Equal(3, preview.Count);
        Assert.Equal("Ok", preview.Single(r => r.SourceSpecificationId == okSpecId).Status);
        Assert.Null(preview.Single(r => r.SourceSpecificationId == okSpecId).Message);

        var dupRow = preview.Single(r => r.SourceSpecificationId == dupSpecId);
        Assert.Equal("AlreadyPresent", dupRow.Status);
        Assert.False(string.IsNullOrWhiteSpace(dupRow.Message));

        var invRow = preview.Single(r => r.SourceSpecificationId == invSpecId);
        Assert.Equal("Invalid", invRow.Status);
        Assert.False(string.IsNullOrWhiteSpace(invRow.Message));

        await using var assertDb = _fixture.CreateDbContext();
        var targetSpecs = await assertDb.Specifications
            .AsNoTracking()
            .Where(s => s.ItemId == targetId)
            .ToListAsync();
        Assert.Single(targetSpecs);
    }

    [PostgresFact]
    public async Task Copy_CreatesOkRows_SkipsAlreadyPresent_AllOrNothingOnInvalid()
    {
        var tag = Tag();
        int userId, sourceId, targetId;
        int okSpecId, dupSpecId, invSpecId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            userId = await SeedAdminUserAsync(arrangeDb);
            var micro = TestServiceFactory.EnsureMicroSection(arrangeDb);

            var tOk = SeedTestDef(arrangeDb, micro.Id, "BATCH_OK");
            var tDup = SeedTestDef(arrangeDb, micro.Id, "BATCH_DUP");
            var tInv = SeedTestDef(arrangeDb, micro.Id, "BATCH_INV");

            var source = new Item
            {
                Code = "BSRC-" + tag,
                Name = "Batch Source " + tag,
                Category = SampleCategory.FinishedProduct,
                AssignedTests =
                {
                    new SampleTest { TestCode = tOk.Code, DisplayName = tOk.DisplayName },
                    new SampleTest { TestCode = tDup.Code, DisplayName = tDup.DisplayName },
                    new SampleTest { TestCode = tInv.Code, DisplayName = tInv.DisplayName }
                }
            };
            var target = new Item
            {
                Code = "BTGT-" + tag,
                Name = "Batch Target " + tag,
                Category = SampleCategory.RawMaterial,
                AssignedTests =
                {
                    new SampleTest { TestCode = tOk.Code, DisplayName = tOk.DisplayName },
                    new SampleTest { TestCode = tDup.Code, DisplayName = tDup.DisplayName },
                    new SampleTest { TestCode = tInv.Code, DisplayName = tInv.DisplayName }
                }
            };
            arrangeDb.Items.AddRange(source, target);
            await arrangeDb.SaveChangesAsync();

            var sOk = MakeRangeSpec(source.Id, tOk.Code, "Ok Param");
            var sDup = MakeRangeSpec(source.Id, tDup.Code, "Dup Param");
            var sInv = MakeRangeSpec(source.Id, tInv.Code, "Inv Stage Param", stageRole: ProductionStageRole.Finished);
            var existingOnTarget = MakeRangeSpec(target.Id, tDup.Code, "Dup Param", lower: 5m, upper: 15m);
            arrangeDb.Specifications.AddRange(sOk, sDup, sInv, existingOnTarget);
            await arrangeDb.SaveChangesAsync();

            sourceId = source.Id;
            targetId = target.Id;
            okSpecId = sOk.Id;
            dupSpecId = sDup.Id;
            invSpecId = sInv.Id;
        }

        // First attempt includes invalid row -> must roll back S1 as well.
        await using (var actRollbackDb = _fixture.CreateDbContext())
        {
            var svc = BuildCopyService(actRollbackDb);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.CopyAsync(new CopySpecificationsRequest(sourceId, targetId, [okSpecId, dupSpecId, invSpecId]), userId));
        }

        await using (var assertAfterRollbackDb = _fixture.CreateDbContext())
        {
            var specsAfterRollback = await assertAfterRollbackDb.Specifications
                .AsNoTracking()
                .Where(s => s.ItemId == targetId)
                .ToListAsync();
            Assert.Single(specsAfterRollback);
        }

        // Second attempt with Ok + AlreadyPresent -> creates 1 row and skips AlreadyPresent cleanly.
        int createdCount;
        await using (var actSuccessDb = _fixture.CreateDbContext())
        {
            var svc = BuildCopyService(actSuccessDb);
            createdCount = await svc.CopyAsync(
                new CopySpecificationsRequest(sourceId, targetId, [okSpecId, dupSpecId]),
                userId);
        }

        Assert.Equal(1, createdCount);

        await using var assertDb = _fixture.CreateDbContext();
        var specs = await assertDb.Specifications
            .AsNoTracking()
            .Where(s => s.ItemId == targetId && !s.IsRetired)
            .OrderBy(s => s.Id)
            .ToListAsync();
        Assert.Equal(2, specs.Count);
        Assert.Contains(specs, s => s.ParameterName == "Ok Param");
        // Existing target spec was kept untouched (not overwritten).
        var keptDup = specs.Single(s => s.ParameterName == "Dup Param");
        Assert.Equal(5m, keptDup.LowerLimit);
        Assert.Equal(15m, keptDup.UpperLimit);
    }

    [PostgresFact]
    public async Task Copy_AuditNotesSource()
    {
        var tag = Tag();
        int userId, sourceId, targetId, srcSpecId;
        string sourceCode = "AUD-SRC-" + tag;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            userId = await SeedAdminUserAsync(arrangeDb);
            var micro = TestServiceFactory.EnsureMicroSection(arrangeDb);
            var testDef = SeedTestDef(arrangeDb, micro.Id, "AUD_T");

            var source = new Item
            {
                Code = sourceCode,
                Name = "Audit Source " + tag,
                Category = SampleCategory.FinishedProduct,
                AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } }
            };
            var target = new Item
            {
                Code = "AUD-TGT-" + tag,
                Name = "Audit Target " + tag,
                Category = SampleCategory.FinishedProduct,
                AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } }
            };
            arrangeDb.Items.AddRange(source, target);
            await arrangeDb.SaveChangesAsync();

            var spec = MakeRangeSpec(source.Id, testDef.Code, "Bioburden");
            arrangeDb.Specifications.Add(spec);
            await arrangeDb.SaveChangesAsync();

            sourceId = source.Id;
            targetId = target.Id;
            srcSpecId = spec.Id;
        }

        int createdCount;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildCopyService(actDb);
            createdCount = await svc.CopyAsync(
                new CopySpecificationsRequest(sourceId, targetId, [srcSpecId]),
                userId);
        }

        Assert.Equal(1, createdCount);

        await using var assertDb = _fixture.CreateDbContext();
        var copiedSpec = await assertDb.Specifications
            .AsNoTracking()
            .SingleAsync(s => s.ItemId == targetId && !s.IsRetired);

        var audit = await assertDb.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(Specification)
                        && a.EntityId == copiedSpec.Id.ToString()
                        && a.Action == "Create")
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal(userId, audit.UserId);
        Assert.Equal("Create", audit.Action);
        Assert.Equal(copiedSpec.Id.ToString(), audit.EntityId);
        Assert.Null(audit.PreviousValue);
        Assert.False(string.IsNullOrWhiteSpace(audit.NewValue));
        Assert.Equal($"Copied from specification {srcSpecId} of item {sourceCode}", audit.SourceContext);
    }

    [PostgresFact]
    public async Task Copy_TargetInSectionICannotConfigure_Refused()
    {
        var tag = Tag();
        int sectionHeadId, sourceId, targetId, physSpecId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = TestServiceFactory.EnsureMicroSection(arrangeDb);
            var phys = EnsurePhyschemSection(arrangeDb);
            sectionHeadId = await SeedSectionHeadAsync(arrangeDb, micro);

            var physTest = SeedTestDef(arrangeDb, phys.Id, "PHYS_OWN");
            var source = new Item
            {
                Code = "OWN-SRC-" + tag,
                Name = "Owned Source " + tag,
                Category = SampleCategory.FinishedProduct,
                AssignedTests = { new SampleTest { TestCode = physTest.Code, DisplayName = physTest.DisplayName } }
            };
            var target = new Item
            {
                Code = "OWN-TGT-" + tag,
                Name = "Owned Target " + tag,
                Category = SampleCategory.FinishedProduct,
                AssignedTests = { new SampleTest { TestCode = physTest.Code, DisplayName = physTest.DisplayName } }
            };
            arrangeDb.Items.AddRange(source, target);
            await arrangeDb.SaveChangesAsync();

            var physSpec = MakeRangeSpec(source.Id, physTest.Code, "HPLC Assay");
            arrangeDb.Specifications.Add(physSpec);
            await arrangeDb.SaveChangesAsync();

            sourceId = source.Id;
            targetId = target.Id;
            physSpecId = physSpec.Id;
        }

        await using (var actPreviewDb = _fixture.CreateDbContext())
        {
            var svc = BuildCopyService(actPreviewDb);
            var preview = await svc.PreviewAsync(
                new CopySpecificationsRequest(sourceId, targetId, [physSpecId]),
                sectionHeadId);
            Assert.Single(preview);
            Assert.Equal("Invalid", preview[0].Status);
        }

        await using (var actCopyDb = _fixture.CreateDbContext())
        {
            var svc = BuildCopyService(actCopyDb);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.CopyAsync(new CopySpecificationsRequest(sourceId, targetId, [physSpecId]), sectionHeadId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Specifications.AsNoTracking().AnyAsync(s => s.ItemId == targetId));
    }

    [PostgresFact]
    public async Task Copy_SourceIJustRead_Allowed()
    {
        var tag = Tag();
        int sectionHeadId, sourceId, targetId, microSpecId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = TestServiceFactory.EnsureMicroSection(arrangeDb);
            var phys = EnsurePhyschemSection(arrangeDb);
            sectionHeadId = await SeedSectionHeadAsync(arrangeDb, micro);

            var microTest = SeedTestDef(arrangeDb, micro.Id, "READ_MIC");
            var physTest = SeedTestDef(arrangeDb, phys.Id, "READ_PHY");

            // Source has tests from both sections (owned/created independently); caller only configures Micro.
            var source = new Item
            {
                Code = "RD-SRC-" + tag,
                Name = "Readable Source " + tag,
                Category = SampleCategory.FinishedProduct,
                AssignedTests =
                {
                    new SampleTest { TestCode = microTest.Code, DisplayName = microTest.DisplayName },
                    new SampleTest { TestCode = physTest.Code, DisplayName = physTest.DisplayName }
                }
            };
            var target = new Item
            {
                Code = "RD-TGT-" + tag,
                Name = "Micro Target " + tag,
                Category = SampleCategory.FinishedProduct,
                AssignedTests = { new SampleTest { TestCode = microTest.Code, DisplayName = microTest.DisplayName } }
            };
            arrangeDb.Items.AddRange(source, target);
            await arrangeDb.SaveChangesAsync();

            var microSpec = MakeRangeSpec(source.Id, microTest.Code, "TAMC");
            var physSpec = MakeRangeSpec(source.Id, physTest.Code, "pH");
            arrangeDb.Specifications.AddRange(microSpec, physSpec);
            await arrangeDb.SaveChangesAsync();

            sourceId = source.Id;
            targetId = target.Id;
            microSpecId = microSpec.Id;
        }

        int created;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildCopyService(actDb);
            created = await svc.CopyAsync(
                new CopySpecificationsRequest(sourceId, targetId, [microSpecId]),
                sectionHeadId);
        }

        Assert.Equal(1, created);

        await using var assertDb = _fixture.CreateDbContext();
        var copied = await assertDb.Specifications
            .AsNoTracking()
            .SingleAsync(s => s.ItemId == targetId && !s.IsRetired);
        Assert.Equal("TAMC", copied.ParameterName);
    }

    [PostgresFact]
    public async Task RetiredSourceRows_NotOffered()
    {
        var tag = Tag();
        int userId, sourceId, targetId, activeSpecId, retiredSpecId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            userId = await SeedAdminUserAsync(arrangeDb);
            var micro = TestServiceFactory.EnsureMicroSection(arrangeDb);
            var test1 = SeedTestDef(arrangeDb, micro.Id, "RET_ACT");
            var test2 = SeedTestDef(arrangeDb, micro.Id, "RET_OLD");

            var source = new Item
            {
                Code = "RSRC-" + tag,
                Name = "Retired Source " + tag,
                Category = SampleCategory.FinishedProduct,
                AssignedTests =
                {
                    new SampleTest { TestCode = test1.Code, DisplayName = test1.DisplayName },
                    new SampleTest { TestCode = test2.Code, DisplayName = test2.DisplayName }
                }
            };
            var target = new Item
            {
                Code = "RTGT-" + tag,
                Name = "Retired Target " + tag,
                Category = SampleCategory.FinishedProduct,
                AssignedTests =
                {
                    new SampleTest { TestCode = test1.Code, DisplayName = test1.DisplayName },
                    new SampleTest { TestCode = test2.Code, DisplayName = test2.DisplayName }
                }
            };
            arrangeDb.Items.AddRange(source, target);
            await arrangeDb.SaveChangesAsync();

            var activeSpec = MakeRangeSpec(source.Id, test1.Code, "Active Param");
            var retiredSpec = MakeRangeSpec(source.Id, test2.Code, "Retired Param");
            retiredSpec.IsRetired = true;
            retiredSpec.RetiredAt = DateTime.UtcNow;
            retiredSpec.RetiredByUserId = userId;
            arrangeDb.Specifications.AddRange(activeSpec, retiredSpec);
            await arrangeDb.SaveChangesAsync();

            sourceId = source.Id;
            targetId = target.Id;
            activeSpecId = activeSpec.Id;
            retiredSpecId = retiredSpec.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildCopyService(actDb);

            var allOffered = await svc.PreviewAsync(new CopySpecificationsRequest(sourceId, targetId, []), userId);
            Assert.Single(allOffered);
            Assert.Equal(activeSpecId, allOffered[0].SourceSpecificationId);

            var explicitOffered = await svc.PreviewAsync(
                new CopySpecificationsRequest(sourceId, targetId, [activeSpecId, retiredSpecId]),
                userId);
            Assert.Single(explicitOffered);
            Assert.Equal(activeSpecId, explicitOffered[0].SourceSpecificationId);

            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.CopyAsync(new CopySpecificationsRequest(sourceId, targetId, [retiredSpecId]), userId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Specifications.AsNoTracking().AnyAsync(s => s.ItemId == targetId));
    }

    [PostgresFact]
    public async Task CopiedRow_EqualsSourceInEveryConfiguredField_ExceptItemId()
    {
        var tag = Tag();
        int userId, sourceId, targetId, srcSpecId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            userId = await SeedAdminUserAsync(arrangeDb);
            var micro = TestServiceFactory.EnsureMicroSection(arrangeDb);
            var testDef = SeedTestDef(arrangeDb, micro.Id, "FULL_EQ");

            var source = new Item
            {
                Code = "EQ-SRC-" + tag,
                Name = "Equal Source " + tag,
                Category = SampleCategory.FinishedProduct,
                AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } }
            };
            var target = new Item
            {
                Code = "EQ-TGT-" + tag,
                Name = "Equal Target " + tag,
                Category = SampleCategory.FinishedProduct,
                AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } }
            };
            arrangeDb.Items.AddRange(source, target);
            await arrangeDb.SaveChangesAsync();

            var srcSpec = new Specification
            {
                ItemId = source.Id,
                TestCode = testDef.Code,
                ParameterName = "Microbial Limit " + tag,
                DisplayOrder = 4,
                LimitType = LimitType.TargetWithTolerance,
                ReferenceStandard = "USP <61>",
                Target = 100.5m,
                Tolerance = 5.25m,
                ToleranceMode = ToleranceMode.Absolute,
                LowerInclusive = true,
                UpperInclusive = false,
                SampleQuantity = 10m,
                SampleQuantityUnit = "g",
                AlertLimit = "Alert <= 50",
                ActionLimit = "Action <= 80",
                Unit = "CFU/g",
                ProductionStageRole = ProductionStageRole.Finished,
                Grade = "USP",
                Stages =
                {
                    new SpecificationStage { StageNumber = 1, StageLabel = "L1", AcceptanceCriteriaText = "NLT 80%" },
                    new SpecificationStage { StageNumber = 2, StageLabel = "L2", AcceptanceCriteriaText = "Mean >= 80%" }
                }
            };
            SpecificationService.ApplyCanonicalSpecLimit(srcSpec);
            arrangeDb.Specifications.Add(srcSpec);
            await arrangeDb.SaveChangesAsync();

            sourceId = source.Id;
            targetId = target.Id;
            srcSpecId = srcSpec.Id;
        }

        int created;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildCopyService(actDb);
            created = await svc.CopyAsync(
                new CopySpecificationsRequest(sourceId, targetId, [srcSpecId]),
                userId);
        }

        Assert.Equal(1, created);

        await using var assertDb = _fixture.CreateDbContext();
        var sourceRow = await assertDb.Specifications
            .AsNoTracking()
            .Include(s => s.Stages.OrderBy(st => st.StageNumber))
            .SingleAsync(s => s.Id == srcSpecId);

        var copiedRow = await assertDb.Specifications
            .AsNoTracking()
            .Include(s => s.Stages.OrderBy(st => st.StageNumber))
            .SingleAsync(s => s.ItemId == targetId && !s.IsRetired);

        // Explicit exclusion list for mapped scalar properties that legitimately differ on copy:
        // - Id: database identity PK of the newly inserted row.
        // - ItemId: destination item FK (must equal targetId rather than source.ItemId).
        // - Version: PostgreSQL xmin concurrency token assigned by Postgres on insert.
        // - IsRetired / RetiredAt / RetiredByUserId: retirement lifecycle audit state;
        //   retired source rows are never copied, so a new copy always starts active (false / null / null).
        var excludedProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(Specification.Id),
            nameof(Specification.ItemId),
            nameof(Specification.Version),
            nameof(Specification.IsRetired),
            nameof(Specification.RetiredAt),
            nameof(Specification.RetiredByUserId)
        };

        var entityType = assertDb.Model.FindEntityType(typeof(Specification));
        Assert.NotNull(entityType);

        foreach (var efProperty in entityType.GetProperties())
        {
            if (excludedProperties.Contains(efProperty.Name))
                continue;

            var clrProperty = typeof(Specification).GetProperty(efProperty.Name);
            Assert.NotNull(clrProperty);

            var expected = clrProperty.GetValue(sourceRow);
            var actual = clrProperty.GetValue(copiedRow);
            Assert.True(
                Equals(expected, actual),
                $"Mapped property Specification.{efProperty.Name} differed on copy: expected <{expected ?? "null"}>, actual <{actual ?? "null"}>.");
        }

        Assert.NotEqual(sourceRow.Id, copiedRow.Id);
        Assert.Equal(targetId, copiedRow.ItemId);
        Assert.False(copiedRow.IsRetired);
        Assert.Null(copiedRow.RetiredAt);
        Assert.Null(copiedRow.RetiredByUserId);

        Assert.Equal(sourceRow.Stages.Count, copiedRow.Stages.Count);
        foreach (var (srcStage, copiedStage) in sourceRow.Stages.Zip(copiedRow.Stages))
        {
            Assert.Equal(srcStage.StageNumber, copiedStage.StageNumber);
            Assert.Equal(srcStage.StageLabel, copiedStage.StageLabel);
            Assert.Equal(srcStage.AcceptanceCriteriaText, copiedStage.AcceptanceCriteriaText);
        }
    }

    [PostgresFact]
    public async Task ItemCopy_WithBothOptions_CreatesItemSpecsAndAutoAssignedTests_InOneTransaction()
    {
        var tag = Tag();
        int userId, sourceId;
        string t1Code, t2Code, srcCode = "IC-SRC-" + tag;
        string copiedItemCode = "IC-NEW-" + tag;
        string rolledBackItemCode = "IC-RB-" + tag;
        int s1Id, s2Id;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            userId = await SeedAdminUserAsync(arrangeDb);
            var micro = TestServiceFactory.EnsureMicroSection(arrangeDb);
            var t1 = SeedTestDef(arrangeDb, micro.Id, "IC_T1");
            var t2 = SeedTestDef(arrangeDb, micro.Id, "IC_T2");
            t1Code = t1.Code;
            t2Code = t2.Code;

            var source = new Item
            {
                Code = srcCode,
                Name = "Item Copy Source " + tag,
                Category = SampleCategory.FinishedProduct,
                SopNumber = "SOP-IC-1",
                AssignedTests =
                {
                    new SampleTest { TestCode = t1.Code, DisplayName = t1.DisplayName },
                    new SampleTest { TestCode = t2.Code, DisplayName = t2.DisplayName }
                }
            };
            arrangeDb.Items.Add(source);
            await arrangeDb.SaveChangesAsync();

            var s1 = MakeRangeSpec(source.Id, t1.Code, "Count 1", stageRole: ProductionStageRole.Finished);
            var s2 = MakeRangeSpec(source.Id, t2.Code, "Count 2");
            var sRetired = MakeRangeSpec(source.Id, t2.Code, "Retired Row");
            sRetired.IsRetired = true;
            sRetired.RetiredAt = DateTime.UtcNow;
            sRetired.RetiredByUserId = userId;
            arrangeDb.Specifications.AddRange(s1, s2, sRetired);
            await arrangeDb.SaveChangesAsync();

            sourceId = source.Id;
            s1Id = s1.Id;
            s2Id = s2.Id;
        }

        // 1) Happy path: creates Item + both AssignedTests + both active Specifications in one transaction.
        int createdItemId;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildCopyService(actDb);
            var created = await svc.CreateItemAsync(
                new ItemSaveRequest(
                    Name: "Copied Item " + tag,
                    Code: copiedItemCode,
                    Category: SampleCategory.FinishedProduct,
                    SopNumber: "SOP-IC-NEW",
                    AssignedTests: null,
                    CopyFrom: new CopyItemOptions(sourceId, Specifications: true, AutoAssignedTests: true)),
                userId);
            createdItemId = created.Id;
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var createdItem = await assertDb.Items
                .AsNoTracking()
                .Include(i => i.AssignedTests)
                .Include(i => i.Specifications)
                .SingleAsync(i => i.Id == createdItemId);

            Assert.Equal([t1Code, t2Code], createdItem.AssignedTests.Select(t => t.TestCode).OrderBy(c => c).ToArray());
            var activeSpecs = createdItem.Specifications.Where(s => !s.IsRetired).ToList();
            Assert.Equal(2, activeSpecs.Count);
            Assert.Contains(activeSpecs, s => s.ParameterName == "Count 1");
            Assert.Contains(activeSpecs, s => s.ParameterName == "Count 2");
            Assert.DoesNotContain(createdItem.Specifications, s => s.ParameterName == "Retired Row");

            var itemAudit = await assertDb.AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityName == nameof(Item)
                            && a.EntityId == createdItemId.ToString()
                            && a.Action == "Create")
                .FirstOrDefaultAsync();
            Assert.NotNull(itemAudit);
            Assert.Equal(userId, itemAudit.UserId);
            Assert.Equal("Create", itemAudit.Action);
            Assert.Equal(createdItemId.ToString(), itemAudit.EntityId);
            Assert.Null(itemAudit.PreviousValue);
            Assert.False(string.IsNullOrWhiteSpace(itemAudit.NewValue));
            Assert.Equal($"Copied from item {srcCode}", itemAudit.SourceContext);

            var activeSpecEntityIds = activeSpecs.Select(s => s.Id.ToString()).ToList();
            var specAudits = await assertDb.AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityName == nameof(Specification)
                            && activeSpecEntityIds.Contains(a.EntityId)
                            && a.Action == "Create")
                .ToListAsync();
            Assert.Equal(2, specAudits.Count);
            Assert.Contains(specAudits, a => a.SourceContext == $"Copied from specification {s1Id} of item {srcCode}" && a.UserId == userId);
            Assert.Contains(specAudits, a => a.SourceContext == $"Copied from specification {s2Id} of item {srcCode}" && a.UserId == userId);
        }

        // 2) Transaction atomicity: copying FinishedProduct stage-specific spec onto a RawMaterial target fails mid-way
        // and must roll back BOTH the new Item and its AssignedTests / earlier specs.
        await using (var actRollbackDb = _fixture.CreateDbContext())
        {
            var svc = BuildCopyService(actRollbackDb);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.CreateItemAsync(
                    new ItemSaveRequest(
                        Name: "Rolled Back " + tag,
                        Code: rolledBackItemCode,
                        Category: SampleCategory.RawMaterial,
                        SopNumber: "SOP-RB",
                        AssignedTests: null,
                        CopyFrom: new CopyItemOptions(sourceId, Specifications: true, AutoAssignedTests: true)),
                    userId));
        }

        await using var assertRollbackDb = _fixture.CreateDbContext();
        Assert.False(await assertRollbackDb.Items.AsNoTracking().AnyAsync(i => i.Code == rolledBackItemCode));
    }

    [PostgresFact]
    public async Task ItemCopy_DoesNotCopyCodeNameSopNumberOrDocuments()
    {
        var tag = Tag();
        int userId, sourceId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            userId = await SeedAdminUserAsync(arrangeDb);
            var micro = TestServiceFactory.EnsureMicroSection(arrangeDb);
            var testDef = SeedTestDef(arrangeDb, micro.Id, "DOC_T");

            var source = new Item
            {
                Code = "ORIG-CODE-" + tag,
                Name = "Original Name " + tag,
                Category = SampleCategory.FinishedProduct,
                SopNumber = "SOP-ORIG-" + tag,
                AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } }
            };
            arrangeDb.Items.Add(source);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.ItemDocuments.Add(new ItemDocument
            {
                ItemId = source.Id,
                DocumentType = ItemDocumentType.Sop,
                OriginalFileName = "orig-stm.pdf",
                StorageKey = "items/orig-stm-" + tag + ".pdf",
                FileExtension = ".pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 1234,
                ContentSha256 = new string('a', 64),
                Version = "1.0",
                UploadedByUserId = userId,
                UploadedAt = DateTime.UtcNow,
                Status = MaterialDocumentStatus.Current
            });
            arrangeDb.Specifications.Add(MakeRangeSpec(source.Id, testDef.Code, "Doc Test Param"));
            await arrangeDb.SaveChangesAsync();

            sourceId = source.Id;
        }

        int createdItemId;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildCopyService(actDb);
            var created = await svc.CreateItemAsync(
                new ItemSaveRequest(
                    Name: "Fresh Name " + tag,
                    Code: "FRESH-CODE-" + tag,
                    Category: SampleCategory.FinishedProduct,
                    SopNumber: "SOP-FRESH-" + tag,
                    AssignedTests: null,
                    CopyFrom: new CopyItemOptions(sourceId, Specifications: true, AutoAssignedTests: true)),
                userId);
            createdItemId = created.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var copied = await assertDb.Items
            .AsNoTracking()
            .SingleAsync(i => i.Id == createdItemId);

        Assert.Equal("Fresh Name " + tag, copied.Name);
        Assert.Equal("FRESH-CODE-" + tag, copied.Code);
        Assert.Equal("SOP-FRESH-" + tag, copied.SopNumber);

        Assert.False(await assertDb.ItemDocuments.AsNoTracking().AnyAsync(d => d.ItemId == createdItemId));
        Assert.True(await assertDb.ItemDocuments.AsNoTracking().AnyAsync(d => d.ItemId == sourceId));
    }
}

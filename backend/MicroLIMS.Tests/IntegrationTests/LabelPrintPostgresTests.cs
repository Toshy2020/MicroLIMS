using System.Globalization;
using System.Text.Json;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Labels;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Labels;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class LabelPrintPostgresTests
{
    private const string Password = "Correct-Horse-9";
    private static readonly DateTime FixedUtc = new(2026, 11, 12, 22, 30, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo CairoZone = LabClock.ResolveTimeZone("Africa/Cairo");

    private readonly PostgresTestFixture _fixture;

    public LabelPrintPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static IReadOnlySet<string> Perms(params string[] codes) =>
        new HashSet<string>(codes, StringComparer.Ordinal);

    private ILabelPrintService CreateService(MicroLimsDbContext db, DateTime? utcNow = null)
    {
        var time = new FixedTimeProvider(new DateTimeOffset(utcNow ?? FixedUtc, TimeSpan.Zero));
        var clock = new LabClock(time, CairoZone);
        var dataSource = new LabelDataSource(db, new UserSectionScopeService(db), clock);
        var renderer = new LabelPdfRenderer(new ZXingBarcodeEncoder());
        var audit = new AuditEventService(db, _fixture.CreateSequenceHelper(db), time);
        return new LabelPrintService(db, dataSource, renderer, audit, time, clock);
    }

    private static async Task<User> CreateUserInSectionAsync(
        MicroLimsDbContext db,
        string username,
        int deptId,
        int sectionId,
        RoleType roleType = RoleType.Analyst)
    {
        var role = new Role { Type = roleType, Name = $"Role-{username}", IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User
        {
            FullName = $"User {username}",
            Username = username.ToLowerInvariant(),
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = deptId,
            SectionId = sectionId
        });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<(int MicroUserId, int FpUserId, int SampleId, string SampleCode)> SeedSampleWorldAsync(
        MicroLimsDbContext db)
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var micro = await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");
        var fp = await db.DocumentSections.FirstAsync(s => s.Code == "FP");

        var microUser = await CreateUserInSectionAsync(db, $"lp_micro_{uid}", micro.DepartmentId, micro.Id);
        var fpUser = await CreateUserInSectionAsync(db, $"lp_fp_{uid}", fp.DepartmentId, fp.Id);

        var testCode = $"TD-LP-{uid}";
        db.TestDefinitions.Add(new TestDefinition
        {
            Code = testCode,
            DisplayName = $"Test {uid}",
            SectionId = micro.Id,
            IsActive = true
        });
        var item = new Item
        {
            Code = $"ITM-LP-{uid}",
            Name = $"Product {uid}",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        item.AssignedTests.Add(new SampleTest { TestCode = testCode, DisplayName = $"Test {uid}" });
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var cause = await db.CausesOfTesting.FirstAsync();
        var sample = new Sample
        {
            ReferenceNumber = $"FP{uid[..6]}",
            Category = SampleCategory.FinishedProduct,
            ItemId = item.Id,
            CauseOfTestingId = cause.Id,
            SampledBy = "Sampler",
            BatchNumber = $"BAT-{uid}",
            ControlNumber = $"CTRL-{uid}",
            ReceivedByUserId = microUser.Id,
            ReceivedAt = FixedUtc,
            LabSectionId = micro.Id,
            Status = SampleStatus.Received
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        return (microUser.Id, fpUser.Id, sample.Id, sample.ReferenceNumber);
    }

    private static async Task<int> SeedMediaLotAsync(MicroLimsDbContext db, int userId, int sectionId, string uid)
    {
        var mediaSource = new Material
        {
            SectionId = sectionId,
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = $"Agar Base {uid}",
            ManufacturerName = "MediaCorp",
            BatchNumber = $"MB-{uid}",
            ReceivingDate = FixedUtc,
            ExpiryDate = FixedUtc.AddYears(1),
            StockId = $"STK-LP-{uid}",
            Location = "Media Room",
            QuantityReceived = 500m,
            QuantityRemaining = 400m,
            Unit = MaterialUnit.Gram,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId
        };
        db.Materials.Add(mediaSource);
        await db.SaveChangesAsync();

        var media = new Media
        {
            MaterialId = mediaSource.Id,
            LotNumber = $"MED-LP-{uid}",
            ManufacturerLot = $"ML-{uid}",
            ManufacturerName = "MediaCorp",
            TotalWeight = 10m,
            TotalVolume = "500 ml",
            AutoclaveProgram = "P1",
            LoadType = "liquid",
            Temperature = 121m,
            CycleTime = 15,
            CycleNumber = 1,
            Ph = 7.2m,
            PreparedAt = FixedUtc,
            ExpiryDate = FixedUtc.AddDays(30),
            PreparedByUserId = userId,
            Status = MediaStatus.Active,
            ApprovalStatus = ApprovalGateStatus.Approved,
            IsReleasedForUse = true
        };
        db.Media.Add(media);
        await db.SaveChangesAsync();
        return media.Id;
    }

    [PostgresFact]
    public async Task First_print_writes_log_row_and_LABEL_PRINTED_audit_event()
    {
        int userId;
        int sampleId;
        string sampleCode;
        LabelFormat format;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (userId, _, sampleId, sampleCode) = await SeedSampleWorldAsync(arrangeDb);
            format = await arrangeDb.LabelFormats.AsNoTracking()
                .SingleAsync(f => f.EntityType == LabelEntityType.Sample);
        }

        LabelPrintResult result;
        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(new DateTimeOffset(FixedUtc, TimeSpan.Zero))))
        {
            var service = CreateService(actDb);
            result = await service.PrintAsync(
                new PrintLabelsRequest(
                    new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, Copies: 3) },
                    Reason: null),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab),
                CancellationToken.None);
        }

        Assert.NotEmpty(result.Pdf);
        Assert.NotEqual(Guid.Empty, result.BatchId);

        await using var assertDb = _fixture.CreateDbContext();
        var log = await assertDb.LabelPrintLogs.AsNoTracking()
            .SingleAsync(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleId);
        Assert.Equal(1, log.PrintNumber);
        Assert.False(log.IsReprint);
        Assert.Equal(3, log.Copies);
        Assert.Equal(userId, log.PrintedByUserId);
        Assert.Equal(FixedUtc, log.PrintedAt);
        Assert.Equal(sampleCode, log.Code);
        Assert.Equal(format.Id, log.LabelFormatId);
        Assert.Equal(format.Version, log.LabelFormatVersion);
        Assert.Equal(result.BatchId, log.BatchId);
        Assert.Null(log.Reason);

        var entityIdText = sampleId.ToString(CultureInfo.InvariantCulture);
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Include(a => a.Changes)
            .SingleAsync(a => a.ActionCode == "LABEL_PRINTED"
                && a.EntityName == nameof(LabelEntityType.Sample)
                && a.EntityId == entityIdText);
        Assert.Equal(AuditActionCategory.Labelling, audit.ActionCategory);
        Assert.Equal(userId, audit.UserId);
        Assert.Equal(sampleId, audit.SampleId);
        Assert.Contains(audit.Changes, c => c.FieldName == "Copies" && c.NewValue == "3");
        Assert.Contains(audit.Changes, c => c.FieldName == "Code" && c.NewValue == sampleCode);
        Assert.Contains(audit.Changes, c => c.FieldName == "PrintNumber" && c.NewValue == "1");
        Assert.Contains(audit.Changes, c => c.FieldName == "LabelFormatVersion"
            && c.NewValue == format.Version.ToString(CultureInfo.InvariantCulture));
    }

    [PostgresFact]
    public async Task Returned_pdf_has_one_page_per_requested_label()
    {
        int userId;
        int sampleId;
        int mediaId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (userId, _, sampleId, _) = await SeedSampleWorldAsync(arrangeDb);
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            mediaId = await SeedMediaLotAsync(arrangeDb, userId, micro.Id, Guid.NewGuid().ToString("N")[..8].ToUpperInvariant());
        }

        LabelPrintResult result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = CreateService(actDb);
            result = await service.PrintAsync(
                new PrintLabelsRequest(
                    new[]
                    {
                        new PrintLabelItem(LabelEntityType.Sample, sampleId, Copies: 2),
                        new PrintLabelItem(LabelEntityType.MediaLot, mediaId, Copies: 3)
                    },
                    Reason: null),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab, PermissionConstants.MediaPrepare),
                CancellationToken.None);
        }

        using var doc = PdfDocument.Open(result.Pdf);
        Assert.Equal(5, doc.NumberOfPages);
    }

    [PostgresFact]
    public async Task Reprint_without_permission_is_forbidden_and_writes_nothing()
    {
        int userId;
        int sampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (userId, _, sampleId, _) = await SeedSampleWorldAsync(arrangeDb);
            var firstPrintService = CreateService(arrangeDb);
            await firstPrintService.PrintAsync(
                new PrintLabelsRequest(new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, 1) }, null),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab),
                CancellationToken.None);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = CreateService(actDb);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PrintAsync(
                new PrintLabelsRequest(
                    new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, 1) },
                    Reason: "Damaged label on tube"),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab),
                CancellationToken.None));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var logs = await assertDb.LabelPrintLogs.AsNoTracking()
            .Where(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleId)
            .ToListAsync();
        Assert.Single(logs);
        Assert.Equal(1, logs[0].PrintNumber);

        var entityIdText = sampleId.ToString(CultureInfo.InvariantCulture);
        Assert.False(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.ActionCode == "LABEL_REPRINTED" && a.EntityId == entityIdText));
    }

    [PostgresFact]
    public async Task Reprint_without_reason_or_with_short_reason_is_rejected()
    {
        int userId;
        int sampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (userId, _, sampleId, _) = await SeedSampleWorldAsync(arrangeDb);
            await CreateService(arrangeDb).PrintAsync(
                new PrintLabelsRequest(new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, 1) }, null),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab),
                CancellationToken.None);
        }

        var permissions = Perms(PermissionConstants.SamplesReceiveOwnLab, PermissionConstants.LabelsReprint);

        foreach (var badReason in new string?[] { null, "   ", "too short", "  123456789  " })
        {
            await using var actDb = _fixture.CreateDbContext();
            await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(actDb).PrintAsync(
                new PrintLabelsRequest(
                    new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, 1) },
                    badReason),
                userId,
                permissions,
                CancellationToken.None));
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            await CreateService(actDb).PrintAsync(
                new PrintLabelsRequest(
                    new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, 1) },
                    "  1234567890  "),
                userId,
                permissions,
                CancellationToken.None);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var logs = await assertDb.LabelPrintLogs.AsNoTracking()
            .Where(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleId)
            .OrderBy(l => l.PrintNumber)
            .ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Equal("1234567890", logs[1].Reason);
    }

    [PostgresFact]
    public async Task Reprint_with_permission_and_reason_writes_print_number_2_and_LABEL_REPRINTED()
    {
        int userId;
        int sampleId;
        string sampleCode;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (userId, _, sampleId, sampleCode) = await SeedSampleWorldAsync(arrangeDb);
            await CreateService(arrangeDb).PrintAsync(
                new PrintLabelsRequest(new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, 1) }, null),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab),
                CancellationToken.None);
        }

        const string reason = "Original label smudged in incubator";
        LabelPrintResult result;
        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(new DateTimeOffset(FixedUtc.AddMinutes(5), TimeSpan.Zero))))
        {
            result = await CreateService(actDb, FixedUtc.AddMinutes(5)).PrintAsync(
                new PrintLabelsRequest(
                    new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, Copies: 2) },
                    reason),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab, PermissionConstants.LabelsReprint),
                CancellationToken.None);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var reprintLog = await assertDb.LabelPrintLogs.AsNoTracking()
            .SingleAsync(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleId && l.PrintNumber == 2);
        Assert.True(reprintLog.IsReprint);
        Assert.Equal(2, reprintLog.Copies);
        Assert.Equal(reason, reprintLog.Reason);
        Assert.Equal(result.BatchId, reprintLog.BatchId);

        var entityIdText = sampleId.ToString(CultureInfo.InvariantCulture);
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Include(a => a.Changes)
            .SingleAsync(a => a.ActionCode == "LABEL_REPRINTED" && a.EntityId == entityIdText);
        Assert.Equal(AuditActionCategory.Labelling, audit.ActionCategory);
        Assert.Equal(userId, audit.UserId);
        Assert.Equal(sampleId, audit.SampleId);
        Assert.Equal(reason, audit.Reason);
        Assert.Contains(audit.Changes, c => c.FieldName == "Code" && c.NewValue == sampleCode);
        Assert.Contains(audit.Changes, c => c.FieldName == "PrintNumber" && c.NewValue == "2");
        Assert.Contains(audit.Changes, c => c.FieldName == "Copies" && c.NewValue == "2");
    }

    [PostgresFact]
    public async Task Mixed_first_print_and_reprint_request_follows_reprint_rules()
    {
        int userId;
        int sampleAId;
        int sampleBId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (userId, _, sampleBId, _) = await SeedSampleWorldAsync(arrangeDb);
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var item = await arrangeDb.Items.FirstAsync();
            var cause = await arrangeDb.CausesOfTesting.FirstAsync();
            var sampleA = new Sample
            {
                ReferenceNumber = $"FM{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                Category = SampleCategory.FinishedProduct,
                ItemId = item.Id,
                CauseOfTestingId = cause.Id,
                SampledBy = "Sampler",
                BatchNumber = "BAT-MIX",
                ControlNumber = "CTRL-MIX",
                ReceivedByUserId = userId,
                ReceivedAt = FixedUtc,
                LabSectionId = micro.Id,
                Status = SampleStatus.Received
            };
            arrangeDb.Samples.Add(sampleA);
            await arrangeDb.SaveChangesAsync();
            sampleAId = sampleA.Id;

            await CreateService(arrangeDb).PrintAsync(
                new PrintLabelsRequest(new[] { new PrintLabelItem(LabelEntityType.Sample, sampleBId, 1) }, null),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab),
                CancellationToken.None);
        }

        // Without Labels.Reprint -> forbidden even though sampleA is a first print
        await using (var noPermDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService(noPermDb).PrintAsync(
                new PrintLabelsRequest(
                    new[]
                    {
                        new PrintLabelItem(LabelEntityType.Sample, sampleAId, 1),
                        new PrintLabelItem(LabelEntityType.Sample, sampleBId, 1)
                    },
                    "Reprinting damaged label"),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab),
                CancellationToken.None));
        }

        // With Labels.Reprint but short reason -> rejected
        await using (var badReasonDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(badReasonDb).PrintAsync(
                new PrintLabelsRequest(
                    new[]
                    {
                        new PrintLabelItem(LabelEntityType.Sample, sampleAId, 1),
                        new PrintLabelItem(LabelEntityType.Sample, sampleBId, 1)
                    },
                    "short"),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab, PermissionConstants.LabelsReprint),
                CancellationToken.None));
        }

        // Neither attempt wrote anything to sampleA or a second row to sampleB
        await using (var midAssertDb = _fixture.CreateDbContext())
        {
            Assert.False(await midAssertDb.LabelPrintLogs.AsNoTracking()
                .AnyAsync(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleAId));
            Assert.Equal(1, await midAssertDb.LabelPrintLogs.AsNoTracking()
                .CountAsync(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleBId));
        }

        // With Labels.Reprint + valid reason -> succeeds with LABEL_PRINTED for A and LABEL_REPRINTED for B
        LabelPrintResult result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            result = await CreateService(actDb).PrintAsync(
                new PrintLabelsRequest(
                    new[]
                    {
                        new PrintLabelItem(LabelEntityType.Sample, sampleAId, 2),
                        new PrintLabelItem(LabelEntityType.Sample, sampleBId, 1)
                    },
                    "Batch relabelling after storage move"),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab, PermissionConstants.LabelsReprint),
                CancellationToken.None);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var logA = await assertDb.LabelPrintLogs.AsNoTracking()
            .SingleAsync(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleAId);
        Assert.Equal(1, logA.PrintNumber);
        Assert.False(logA.IsReprint);
        Assert.Equal(result.BatchId, logA.BatchId);

        var logB2 = await assertDb.LabelPrintLogs.AsNoTracking()
            .SingleAsync(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleBId && l.PrintNumber == 2);
        Assert.True(logB2.IsReprint);
        Assert.Equal(result.BatchId, logB2.BatchId);

        Assert.True(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.ActionCode == "LABEL_PRINTED" && a.EntityId == sampleAId.ToString(CultureInfo.InvariantCulture)));
        Assert.True(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.ActionCode == "LABEL_REPRINTED" && a.EntityId == sampleBId.ToString(CultureInfo.InvariantCulture)));
    }

    [PostgresFact]
    public async Task Copies_0_and_51_are_rejected_and_nothing_is_written()
    {
        int userId;
        int sampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (userId, _, sampleId, _) = await SeedSampleWorldAsync(arrangeDb);
        }

        foreach (var badCopies in new[] { 0, 51 })
        {
            await using var actDb = _fixture.CreateDbContext();
            await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(actDb).PrintAsync(
                new PrintLabelsRequest(new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, badCopies) }, null),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab),
                CancellationToken.None));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.LabelPrintLogs.AsNoTracking()
            .AnyAsync(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleId));
    }

    [PostgresFact]
    public async Task Duplicate_record_in_one_request_is_rejected()
    {
        int userId;
        int sampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (userId, _, sampleId, _) = await SeedSampleWorldAsync(arrangeDb);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(actDb).PrintAsync(
                new PrintLabelsRequest(
                    new[]
                    {
                        new PrintLabelItem(LabelEntityType.Sample, sampleId, 1),
                        new PrintLabelItem(LabelEntityType.Sample, sampleId, 2)
                    },
                    null),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab),
                CancellationToken.None));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.LabelPrintLogs.AsNoTracking()
            .AnyAsync(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleId));
    }

    [PostgresFact]
    public async Task Record_in_another_section_gives_not_found_and_writes_nothing()
    {
        int fpUserId;
        int microSampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (_, fpUserId, microSampleId, _) = await SeedSampleWorldAsync(arrangeDb);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<NotFoundException>(() => CreateService(actDb).PrintAsync(
                new PrintLabelsRequest(new[] { new PrintLabelItem(LabelEntityType.Sample, microSampleId, 1) }, null),
                fpUserId,
                Perms(PermissionConstants.SamplesReceiveOwnLab),
                CancellationToken.None));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.LabelPrintLogs.AsNoTracking()
            .AnyAsync(l => l.EntityType == LabelEntityType.Sample && l.EntityId == microSampleId));
    }

    [PostgresFact]
    public async Task User_without_the_create_permission_is_forbidden()
    {
        int userId;
        int sampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (userId, _, sampleId, _) = await SeedSampleWorldAsync(arrangeDb);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService(actDb).PrintAsync(
                new PrintLabelsRequest(new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, 1) }, null),
                userId,
                Perms(PermissionConstants.LabelsReprint),
                CancellationToken.None));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.LabelPrintLogs.AsNoTracking()
            .AnyAsync(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleId));
    }

    [PostgresFact]
    public async Task Solution_in_progress_cannot_be_printed()
    {
        int userId;
        int solutionId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var user = await CreateUserInSectionAsync(arrangeDb, $"lp_sol_{uid}", micro.DepartmentId, micro.Id);
            userId = user.Id;

            var solMaster = new SolutionMaster
            {
                SectionId = micro.Id,
                Name = $"Diluent {uid}",
                Type = SolutionType.Diluent,
                ShelfLifeValue = 7,
                ShelfLifeUnit = ShelfLifeUnit.Days,
                FinalVolumeMl = 500m,
                Instructions = "Mix",
                IsActive = true,
                CreatedByUserId = userId,
                CreatedAt = FixedUtc,
                LastModifiedByUserId = userId,
                LastModifiedAt = FixedUtc
            };
            arrangeDb.SolutionMasters.Add(solMaster);
            await arrangeDb.SaveChangesAsync();

            var prep = new SolutionPreparation
            {
                SectionId = micro.Id,
                Code = null,
                SolutionMasterId = solMaster.Id,
                Type = SolutionType.Diluent,
                RecipeSnapshotJson = JsonSerializer.Serialize(SolutionMasterResponse.From(solMaster), SnapshotJson.Options),
                Status = SolutionPreparationStatus.InProgress,
                StartedByUserId = userId,
                StartedAt = FixedUtc
            };
            arrangeDb.SolutionPreparations.Add(prep);
            await arrangeDb.SaveChangesAsync();
            solutionId = prep.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(actDb).PrintAsync(
                new PrintLabelsRequest(new[] { new PrintLabelItem(LabelEntityType.SolutionPreparation, solutionId, 1) }, null),
                userId,
                Perms(PermissionConstants.SolutionsPrepare),
                CancellationToken.None));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.LabelPrintLogs.AsNoTracking()
            .AnyAsync(l => l.EntityType == LabelEntityType.SolutionPreparation && l.EntityId == solutionId));
    }

    [PostgresFact]
    public async Task Label_too_small_fails_before_anything_is_logged()
    {
        int userId;
        int sampleId;
        LabelFormat original;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (userId, _, sampleId, _) = await SeedSampleWorldAsync(arrangeDb);
            original = await arrangeDb.LabelFormats.AsNoTracking()
                .SingleAsync(f => f.EntityType == LabelEntityType.Sample);
        }

        try
        {
            await using (var shrinkDb = _fixture.CreateDbContext())
            {
                var format = await shrinkDb.LabelFormats.SingleAsync(f => f.Id == original.Id);
                format.WidthMm = 10m;
                format.HeightMm = 10m;
                await shrinkDb.SaveChangesAsync();
            }

            await using (var actDb = _fixture.CreateDbContext())
            {
                await Assert.ThrowsAsync<LabelDoesNotFitException>(() => CreateService(actDb).PrintAsync(
                    new PrintLabelsRequest(new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, 1) }, null),
                    userId,
                    Perms(PermissionConstants.SamplesReceiveOwnLab),
                    CancellationToken.None));
            }

            await using var assertDb = _fixture.CreateDbContext();
            Assert.False(await assertDb.LabelPrintLogs.AsNoTracking()
                .AnyAsync(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleId));

            var entityIdText = sampleId.ToString(CultureInfo.InvariantCulture);
            Assert.False(await assertDb.AuditLogs.AsNoTracking()
                .AnyAsync(a => a.ActionCode == "LABEL_PRINTED" && a.EntityId == entityIdText));
        }
        finally
        {
            await using var restoreDb = _fixture.CreateDbContext();
            var format = await restoreDb.LabelFormats.SingleAsync(f => f.Id == original.Id);
            format.WidthMm = original.WidthMm;
            format.HeightMm = original.HeightMm;
            format.Layout = original.Layout;
            format.DefaultCopies = original.DefaultCopies;
            await restoreDb.SaveChangesAsync();
        }
    }

    [PostgresFact]
    public async Task Two_concurrent_first_prints_yield_print_numbers_1_and_2_or_one_conflict()
    {
        int userId;
        int sampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (userId, _, sampleId, _) = await SeedSampleWorldAsync(arrangeDb);
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<(LabelPrintResult? Result, Exception? Error)> RunConcurrentPrintAsync()
        {
            await using var actDb = _fixture.CreateDbContext(new FixedTimeProvider(new DateTimeOffset(FixedUtc, TimeSpan.Zero)));
            var service = CreateService(actDb);
            await gate.Task;
            try
            {
                var result = await service.PrintAsync(
                    new PrintLabelsRequest(new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, 1) }, "Concurrent print test"),
                    userId,
                    Perms(PermissionConstants.SamplesReceiveOwnLab, PermissionConstants.LabelsReprint),
                    CancellationToken.None);
                return (result, null);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return (null, ex);
            }
        }

        var t1 = Task.Run(RunConcurrentPrintAsync);
        var t2 = Task.Run(RunConcurrentPrintAsync);
        await Task.Delay(50);
        gate.SetResult();

        var outcomes = await Task.WhenAll(t1, t2);
        var successes = outcomes.Count(o => o.Result is not null);
        var conflicts = outcomes.Count(o => o.Error is DbUpdateConcurrencyException);

        Assert.Equal(2, successes + conflicts);
        Assert.InRange(successes, 1, 2);

        await using var assertDb = _fixture.CreateDbContext();
        var printNumbers = await assertDb.LabelPrintLogs.AsNoTracking()
            .Where(l => l.EntityType == LabelEntityType.Sample && l.EntityId == sampleId)
            .OrderBy(l => l.PrintNumber)
            .Select(l => l.PrintNumber)
            .ToListAsync();

        Assert.Equal(successes, printNumbers.Count);
        Assert.Equal(1, printNumbers.Count(n => n == 1));
        if (successes == 2)
        {
            Assert.Equal(new[] { 1, 2 }, printNumbers);
        }
    }

    [PostgresFact]
    public async Task Status_reports_print_count_default_copies_and_blocked_reason()
    {
        int userId;
        int sampleId;
        int inProgressSolutionId;
        int sampleDefaultCopies;
        int solutionDefaultCopies;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (userId, _, sampleId, _) = await SeedSampleWorldAsync(arrangeDb);
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");

            var solMaster = new SolutionMaster
            {
                SectionId = micro.Id,
                Name = $"Diluent Status {Guid.NewGuid():N}"[..20],
                Type = SolutionType.Diluent,
                ShelfLifeValue = 7,
                ShelfLifeUnit = ShelfLifeUnit.Days,
                FinalVolumeMl = 250m,
                Instructions = "Mix",
                IsActive = true,
                CreatedByUserId = userId,
                CreatedAt = FixedUtc,
                LastModifiedByUserId = userId,
                LastModifiedAt = FixedUtc
            };
            arrangeDb.SolutionMasters.Add(solMaster);
            await arrangeDb.SaveChangesAsync();

            var prep = new SolutionPreparation
            {
                SectionId = micro.Id,
                Code = null,
                SolutionMasterId = solMaster.Id,
                Type = SolutionType.Diluent,
                RecipeSnapshotJson = JsonSerializer.Serialize(SolutionMasterResponse.From(solMaster), SnapshotJson.Options),
                Status = SolutionPreparationStatus.InProgress,
                StartedByUserId = userId,
                StartedAt = FixedUtc
            };
            arrangeDb.SolutionPreparations.Add(prep);
            await arrangeDb.SaveChangesAsync();
            inProgressSolutionId = prep.Id;

            sampleDefaultCopies = await arrangeDb.LabelFormats.AsNoTracking()
                .Where(f => f.EntityType == LabelEntityType.Sample)
                .Select(f => f.DefaultCopies)
                .SingleAsync();
            solutionDefaultCopies = await arrangeDb.LabelFormats.AsNoTracking()
                .Where(f => f.EntityType == LabelEntityType.SolutionPreparation)
                .Select(f => f.DefaultCopies)
                .SingleAsync();

            // Print sample twice
            var printSvc = CreateService(arrangeDb);
            await printSvc.PrintAsync(
                new PrintLabelsRequest(new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, 1) }, null),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab),
                CancellationToken.None);
            await printSvc.PrintAsync(
                new PrintLabelsRequest(new[] { new PrintLabelItem(LabelEntityType.Sample, sampleId, 1) }, "Second print for status"),
                userId,
                Perms(PermissionConstants.SamplesReceiveOwnLab, PermissionConstants.LabelsReprint),
                CancellationToken.None);
        }

        IReadOnlyList<LabelPrintStatusDto> statuses;
        await using (var actDb = _fixture.CreateDbContext())
        {
            statuses = await CreateService(actDb).GetStatusAsync(
                new[]
                {
                    (LabelEntityType.Sample, sampleId),
                    (LabelEntityType.SolutionPreparation, inProgressSolutionId)
                },
                userId,
                CancellationToken.None);
        }

        Assert.Equal(2, statuses.Count);

        var sampleStatus = statuses[0];
        Assert.Equal(LabelEntityType.Sample, sampleStatus.EntityType);
        Assert.Equal(sampleId, sampleStatus.EntityId);
        Assert.True(sampleStatus.CanPrint);
        Assert.Null(sampleStatus.BlockedReason);
        Assert.Equal(2, sampleStatus.PrintCount);
        Assert.Equal(sampleDefaultCopies, sampleStatus.DefaultCopies);

        var solutionStatus = statuses[1];
        Assert.Equal(LabelEntityType.SolutionPreparation, solutionStatus.EntityType);
        Assert.Equal(inProgressSolutionId, solutionStatus.EntityId);
        Assert.False(solutionStatus.CanPrint);
        Assert.Equal("In progress.", solutionStatus.BlockedReason);
        Assert.Equal(0, solutionStatus.PrintCount);
        Assert.Equal(solutionDefaultCopies, solutionStatus.DefaultCopies);
    }
}

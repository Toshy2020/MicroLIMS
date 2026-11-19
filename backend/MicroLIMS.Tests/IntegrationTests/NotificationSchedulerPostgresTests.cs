using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.BackgroundServices;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.BackgroundJobs;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class NotificationSchedulerPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public NotificationSchedulerPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 12, 30)];

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class NoOpEmailSender : IEmailSender
    {
        public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
    }

    private static IConfiguration DefaultConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:Origin"] = "https://lims.example.test",
                ["Lab:TimeZoneId"] = "Africa/Cairo"
            })
            .Build();

    private static NotificationScheduler CreateScheduler(MicroLimsDbContext db, TimeProvider time, IConfiguration? config = null)
    {
        var cfg = config ?? DefaultConfig();
        var clock = new LabClock(time, LabClock.ResolveTimeZone(cfg["Lab:TimeZoneId"] ?? "Africa/Cairo"));
        var publisher = new NotificationPublisher(db, time, cfg, clock);
        var recipients = new NotificationRecipients(db, new SegregationOfDutiesGuard(db));
        return new NotificationScheduler(db, publisher, recipients, clock, settings: null, cfg);
    }

    private async Task<DocumentSection> CreateSectionAsync(MicroLimsDbContext db, string tag)
    {
        var section = new DocumentSection
        {
            DepartmentId = _fixture.SeededDepartmentId,
            Code = UniqueCode($"NS{tag}_"),
            Name = $"Scheduler Sec {tag} {Guid.NewGuid():N}"[..36],
            IsActive = true
        };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        return section;
    }

    private async Task<Role> CreateRoleWithPermissionsAsync(MicroLimsDbContext db, RoleType roleType, params string[] permissionCodes)
    {
        var role = new Role
        {
            Name = UniqueCode($"R_{roleType}_"),
            Type = roleType,
            IsActive = true
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        if (permissionCodes.Length > 0)
        {
            var permissions = await db.Permissions.Where(p => permissionCodes.Contains(p.Code)).ToListAsync();
            foreach (var perm in permissions)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            }
            await db.SaveChangesAsync();
        }

        return role;
    }

    private async Task<User> CreateUserInSectionAsync(
        MicroLimsDbContext db,
        int roleId,
        int sectionId,
        string prefix,
        string? email = null)
    {
        var user = new User
        {
            FullName = $"{prefix} {Guid.NewGuid():N}"[..30],
            Username = UniqueCode($"{prefix}_").ToLowerInvariant(),
            PasswordHash = TestPasswords.Hash("Sched-Pass-123!"),
            RoleId = roleId,
            Email = email,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = _fixture.SeededDepartmentId,
            SectionId = sectionId
        });
        await db.SaveChangesAsync();
        return user;
    }

    [PostgresFact]
    public async Task Run_CreatesExpiryNotification_ForUserWithNoSessionOrLogin()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);

        int preparerId;
        int headId;
        int eqHolderId;
        int mediaId;
        int solutionId;
        int cryovialId;
        int equipmentId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var section = await CreateSectionAsync(arrangeDb, "EXP");
            var roles = await arrangeDb.Roles.AsNoTracking().ToListAsync();
            var analystRole = roles.First(r => r.Type == RoleType.Analyst);
            var headRole = roles.First(r => r.Type == RoleType.SectionHead);
            var eqRole = await CreateRoleWithPermissionsAsync(arrangeDb, RoleType.Analyst, PermissionConstants.EquipmentWorkRecord);

            // Users have zero UserSessions / RefreshTokens / login records.
            var preparer = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, section.Id, "prep", "prep@lab.test");
            var head = await CreateUserInSectionAsync(arrangeDb, headRole.Id, section.Id, "head", "head@lab.test");
            var eqHolder = await CreateUserInSectionAsync(arrangeDb, eqRole.Id, section.Id, "eq", "eq@lab.test");
            preparerId = preparer.Id;
            headId = head.Id;
            eqHolderId = eqHolder.Id;

            var mediaMat = new Material
            {
                MaterialType = MaterialType.DehydratedMedia,
                MaterialName = "Sabouraud Agar",
                ManufacturerName = "Oxoid",
                BatchNumber = UniqueCode("LOT-"),
                ReceivingDate = now.UtcDateTime.AddDays(-20),
                Code = UniqueCode("SDA"),
                Location = "Shelf A",
                QuantityReceived = 500,
                QuantityRemaining = 500,
                Unit = MaterialUnit.Gram,
                SectionId = section.Id
            };
            var strainMat = new Material
            {
                MaterialType = MaterialType.LyophilizedMicroorganism,
                MaterialName = "E. coli",
                ManufacturerName = "ATCC",
                BatchNumber = UniqueCode("ST-"),
                ReceivingDate = now.UtcDateTime.AddDays(-20),
                Code = UniqueCode("EC"),
                Location = "Freezer",
                QuantityReceived = 5,
                QuantityRemaining = 5,
                Unit = MaterialUnit.Vial,
                SectionId = section.Id
            };
            arrangeDb.Materials.AddRange(mediaMat, strainMat);
            await arrangeDb.SaveChangesAsync();

            var media = new Media
            {
                MaterialId = mediaMat.Id,
                LotNumber = UniqueCode("MED/"),
                ExpiryDate = now.UtcDateTime.AddDays(3),
                PreparedByUserId = preparer.Id,
                Status = MediaStatus.Active,
                ApprovalStatus = ApprovalGateStatus.Approved,
                IsReleasedForUse = true
            };
            arrangeDb.Media.Add(media);

            var solMaster = new SolutionMaster
            {
                SectionId = section.Id,
                Name = UniqueCode("Buffer "),
                Type = SolutionType.Diluent,
                ShelfLifeValue = 7,
                ShelfLifeUnit = ShelfLifeUnit.Days,
                StorageCondition = "15-25 C",
                FinalVolumeMl = 500m,
                Instructions = "Mix",
                IsActive = true,
                CreatedByUserId = preparer.Id,
                CreatedAt = now.UtcDateTime,
                LastModifiedByUserId = preparer.Id,
                LastModifiedAt = now.UtcDateTime
            };
            arrangeDb.SolutionMasters.Add(solMaster);
            var organism = new Organism { ScientificName = $"E. coli {UniqueCode("O")}", AtccNumber = "8739" };
            arrangeDb.Organisms.Add(organism);
            await arrangeDb.SaveChangesAsync();

            var solution = new SolutionPreparation
            {
                SectionId = section.Id,
                Code = UniqueCode("SOL-"),
                SolutionMasterId = solMaster.Id,
                Type = SolutionType.Diluent,
                RecipeSnapshotJson = "{}",
                Status = SolutionPreparationStatus.Prepared,
                StartedByUserId = preparer.Id,
                StartedAt = now.UtcDateTime.AddDays(-3),
                PreparedByUserId = preparer.Id,
                PreparedAt = now.UtcDateTime.AddDays(-3),
                ExpiresAt = now.UtcDateTime.AddDays(2),
                FinalVolumeMl = 500m
            };
            arrangeDb.SolutionPreparations.Add(solution);

            var cryovial = new Cryovial
            {
                Code = UniqueCode("CRYO-"),
                MaterialId = strainMat.Id,
                OrganismId = organism.Id,
                OrganismNameSnapshot = organism.ScientificName,
                ManufacturerName = "ATCC",
                PreparedAt = now.UtcDateTime.AddDays(-30),
                ExpiryDate = now.UtcDateTime.AddDays(4),
                NumberOfVialsPrepared = 5,
                VialsRemaining = 3,
                StorageCondition = "-70 C",
                PhysicalCheckConfirmed = true,
                PhysicalCheckText = "OK",
                PreparedByUserId = preparer.Id,
                ApprovalStatus = ApprovalGateStatus.Approved
            };
            arrangeDb.Cryovials.Add(cryovial);

            var equipment = new EquipmentInventory
            {
                Code = UniqueCode("BAL-"),
                InstrumentType = "Analytical Balance",
                ManufacturerName = "Mettler",
                Location = "Room 2",
                Status = EquipmentOperationalStatus.InService,
                SectionId = section.Id,
                CalibrationDueDate = now.UtcDateTime.AddDays(5),
                CreatedByUserId = preparer.Id,
                LastModifiedByUserId = preparer.Id
            };
            arrangeDb.EquipmentInventories.Add(equipment);
            await arrangeDb.SaveChangesAsync();

            mediaId = media.Id;
            solutionId = solution.Id;
            cryovialId = cryovial.Id;
            equipmentId = equipment.Id;

            Assert.Empty(await arrangeDb.RefreshTokens.AsNoTracking().Where(s => s.UserId == preparerId).ToListAsync());
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var scheduler = CreateScheduler(actDb, clock);
            await scheduler.RunAsync();
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var mediaKey = $"MediaExpiry:media:{mediaId}";
            var solKey = $"SolutionExpiry:solution:{solutionId}";
            var strainKey = $"StrainExpiry:cryovial:{cryovialId}";
            var calKey = $"CalibrationDue:equipment:{equipmentId}";

            var logs = await assertDb.NotificationLogs.AsNoTracking()
                .Where(n => n.DedupeKey == mediaKey || n.DedupeKey == solKey || n.DedupeKey == strainKey || n.DedupeKey == calKey)
                .ToListAsync();

            Assert.Contains(logs, n => n.DedupeKey == mediaKey && n.UserId == preparerId && n.Type == "MediaExpiry");
            Assert.Contains(logs, n => n.DedupeKey == mediaKey && n.UserId == headId && n.Type == "MediaExpiry");
            Assert.Contains(logs, n => n.DedupeKey == solKey && n.UserId == preparerId && n.Type == "SolutionExpiry");
            Assert.Contains(logs, n => n.DedupeKey == solKey && n.UserId == headId && n.Type == "SolutionExpiry");
            Assert.Contains(logs, n => n.DedupeKey == strainKey && n.UserId == preparerId && n.Type == "StrainExpiry");
            Assert.Contains(logs, n => n.DedupeKey == strainKey && n.UserId == headId && n.Type == "StrainExpiry");
            Assert.Contains(logs, n => n.DedupeKey == calKey && n.UserId == eqHolderId && n.Type == "CalibrationDue");
        }
    }

    [PostgresFact]
    public async Task CalibrationDue_NotifiesPermissionHolderInEquipmentSection_ExcludesOtherSection()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);

        int inSectionUserId;
        int otherSectionUserId;
        int equipmentId;
        int otherEquipmentId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var eqSection = await CreateSectionAsync(arrangeDb, "CALA");
            var otherSection = await CreateSectionAsync(arrangeDb, "CALB");
            var eqRole = await CreateRoleWithPermissionsAsync(arrangeDb, RoleType.Analyst, PermissionConstants.EquipmentWorkRecord);

            var inSectionUser = await CreateUserInSectionAsync(arrangeDb, eqRole.Id, eqSection.Id, "calin", "calin@lab.test");
            var otherSectionUser = await CreateUserInSectionAsync(arrangeDb, eqRole.Id, otherSection.Id, "calout", "calout@lab.test");
            inSectionUserId = inSectionUser.Id;
            otherSectionUserId = otherSectionUser.Id;

            var equipment = new EquipmentInventory
            {
                Code = UniqueCode("BAL-SECA-"),
                InstrumentType = "Analytical Balance",
                ManufacturerName = "Mettler",
                Location = "Room 1",
                Status = EquipmentOperationalStatus.InService,
                SectionId = eqSection.Id,
                CalibrationDueDate = now.UtcDateTime.AddDays(3),
                CreatedByUserId = inSectionUserId,
                LastModifiedByUserId = inSectionUserId
            };
            var otherEquipment = new EquipmentInventory
            {
                Code = UniqueCode("BAL-SECB-"),
                InstrumentType = "Microbalance",
                ManufacturerName = "Sartorius",
                Location = "Room 2",
                Status = EquipmentOperationalStatus.InService,
                SectionId = otherSection.Id,
                CalibrationDueDate = now.UtcDateTime.AddDays(2),
                CreatedByUserId = otherSectionUserId,
                LastModifiedByUserId = otherSectionUserId
            };
            arrangeDb.EquipmentInventories.AddRange(equipment, otherEquipment);
            await arrangeDb.SaveChangesAsync();
            equipmentId = equipment.Id;
            otherEquipmentId = otherEquipment.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var scheduler = CreateScheduler(actDb, clock);
            await scheduler.RunAsync();
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var calKey = $"CalibrationDue:equipment:{equipmentId}";
            var otherCalKey = $"CalibrationDue:equipment:{otherEquipmentId}";
            var logs = await assertDb.NotificationLogs.AsNoTracking()
                .Where(n => n.DedupeKey == calKey || n.DedupeKey == otherCalKey)
                .ToListAsync();

            Assert.Contains(logs, n => n.DedupeKey == calKey && n.UserId == inSectionUserId && n.Type == "CalibrationDue");
            Assert.DoesNotContain(logs, n => n.DedupeKey == calKey && n.UserId == otherSectionUserId);
            Assert.Contains(logs, n => n.DedupeKey == otherCalKey && n.UserId == otherSectionUserId && n.Type == "CalibrationDue");
            Assert.DoesNotContain(logs, n => n.DedupeKey == otherCalKey && n.UserId == inSectionUserId);
        }
    }

    [PostgresFact]
    public async Task Run_Twice_NoDuplicates()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        int preparerId;
        string mediaKey;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var section = await CreateSectionAsync(arrangeDb, "TWO");
            var analystRole = await arrangeDb.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
            var preparer = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, section.Id, "twice", "twice@lab.test");
            preparerId = preparer.Id;

            var mat = new Material
            {
                MaterialType = MaterialType.DehydratedMedia,
                MaterialName = "MacConkey",
                ManufacturerName = "Oxoid",
                BatchNumber = UniqueCode("LOT-"),
                ReceivingDate = now.UtcDateTime.AddDays(-5),
                Code = UniqueCode("MAC"),
                Location = "Lab",
                QuantityReceived = 100,
                QuantityRemaining = 100,
                Unit = MaterialUnit.Gram,
                SectionId = section.Id
            };
            arrangeDb.Materials.Add(mat);
            await arrangeDb.SaveChangesAsync();

            var media = new Media
            {
                MaterialId = mat.Id,
                LotNumber = UniqueCode("M2/"),
                ExpiryDate = now.UtcDateTime.AddDays(2),
                PreparedByUserId = preparerId,
                Status = MediaStatus.Active,
                ApprovalStatus = ApprovalGateStatus.Approved,
                IsReleasedForUse = true
            };
            arrangeDb.Media.Add(media);
            await arrangeDb.SaveChangesAsync();
            mediaKey = $"MediaExpiry:media:{media.Id}";
        }

        await using (var actDb1 = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDb1, clock).RunAsync();
        }
        await using (var actDb2 = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDb2, clock).RunAsync();
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var rows = await assertDb.NotificationLogs.AsNoTracking()
                .Where(n => n.UserId == preparerId && n.DedupeKey == mediaKey)
                .ToListAsync();
            Assert.Single(rows);

            var outboxRows = await assertDb.EmailOutbox.AsNoTracking()
                .Where(e => e.NotificationLogId == rows[0].Id)
                .ToListAsync();
            Assert.Single(outboxRows);
        }
    }

    [PostgresFact]
    public async Task TwoSchedulersConcurrently_NoDuplicates()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        int preparerId;
        string mediaKey;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var section = await CreateSectionAsync(arrangeDb, "CON");
            var analystRole = await arrangeDb.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
            var preparer = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, section.Id, "conc", "conc@lab.test");
            preparerId = preparer.Id;

            var mat = new Material
            {
                MaterialType = MaterialType.DehydratedMedia,
                MaterialName = "VRBGA",
                ManufacturerName = "Oxoid",
                BatchNumber = UniqueCode("LOT-"),
                ReceivingDate = now.UtcDateTime.AddDays(-5),
                Code = UniqueCode("VRB"),
                Location = "Lab",
                QuantityReceived = 100,
                QuantityRemaining = 100,
                Unit = MaterialUnit.Gram,
                SectionId = section.Id
            };
            arrangeDb.Materials.Add(mat);
            await arrangeDb.SaveChangesAsync();

            var media = new Media
            {
                MaterialId = mat.Id,
                LotNumber = UniqueCode("MC/"),
                ExpiryDate = now.UtcDateTime.AddDays(1),
                PreparedByUserId = preparerId,
                Status = MediaStatus.Active,
                ApprovalStatus = ApprovalGateStatus.Approved,
                IsReleasedForUse = true
            };
            arrangeDb.Media.Add(media);
            await arrangeDb.SaveChangesAsync();
            mediaKey = $"MediaExpiry:media:{media.Id}";
        }

        await using var actDb1 = _fixture.CreateDbContext();
        await using var actDb2 = _fixture.CreateDbContext();
        await Task.WhenAll(
            CreateScheduler(actDb1, clock).RunAsync(),
            CreateScheduler(actDb2, clock).RunAsync());

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var rows = await assertDb.NotificationLogs.AsNoTracking()
                .Where(n => n.UserId == preparerId && n.DedupeKey == mediaKey)
                .ToListAsync();
            Assert.Single(rows);
        }
    }

    [PostgresFact]
    public async Task LotNoLongerExpiring_Resolved()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        int preparerId;
        int mediaId;
        string mediaKey;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var section = await CreateSectionAsync(arrangeDb, "RES");
            var analystRole = await arrangeDb.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
            var preparer = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, section.Id, "res", "res@lab.test");
            preparerId = preparer.Id;

            var mat = new Material
            {
                MaterialType = MaterialType.DehydratedMedia,
                MaterialName = "PCA",
                ManufacturerName = "Oxoid",
                BatchNumber = UniqueCode("LOT-"),
                ReceivingDate = now.UtcDateTime.AddDays(-5),
                Code = UniqueCode("PCA"),
                Location = "Lab",
                QuantityReceived = 100,
                QuantityRemaining = 100,
                Unit = MaterialUnit.Gram,
                SectionId = section.Id
            };
            arrangeDb.Materials.Add(mat);
            await arrangeDb.SaveChangesAsync();

            var media = new Media
            {
                MaterialId = mat.Id,
                LotNumber = UniqueCode("MR/"),
                ExpiryDate = now.UtcDateTime.AddDays(2),
                PreparedByUserId = preparerId,
                Status = MediaStatus.Active,
                ApprovalStatus = ApprovalGateStatus.Approved,
                IsReleasedForUse = true
            };
            arrangeDb.Media.Add(media);
            await arrangeDb.SaveChangesAsync();
            mediaId = media.Id;
            mediaKey = $"MediaExpiry:media:{mediaId}";
        }

        await using (var actDb1 = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDb1, clock).RunAsync();
        }

        await using (var updateDb = _fixture.CreateDbContext())
        {
            var media = await updateDb.Media.FirstAsync(m => m.Id == mediaId);
            media.Status = MediaStatus.Destroyed;
            await updateDb.SaveChangesAsync();
        }

        await using (var actDb2 = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDb2, clock).RunAsync();
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var log = await assertDb.NotificationLogs.AsNoTracking()
                .SingleAsync(n => n.UserId == preparerId && n.DedupeKey == mediaKey);
            Assert.NotNull(log.ResolvedAt);
        }
    }

    [PostgresFact]
    public async Task IncubationRead_ResolvesIncubationReady()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        var refNo = UniqueCode("FP01_");
        int analystId;
        int starterId;
        int incubationId;
        int sampleId;
        int testOrderId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var section = await CreateSectionAsync(arrangeDb, "INC");
            var analystRole = await arrangeDb.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
            var analyst = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, section.Id, "incan", null);
            var starter = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, section.Id, "incst", null);
            analystId = analyst.Id;
            starterId = starter.Id;

            var cause = new CauseOfTesting { Name = UniqueCode("Routine ") };
            arrangeDb.CausesOfTesting.Add(cause);
            await arrangeDb.SaveChangesAsync();

            var sample = new Sample
            {
                ReferenceNumber = refNo,
                Category = SampleCategory.FinishedProduct,
                Status = SampleStatus.InTesting,
                ReceivedAt = now.UtcDateTime.AddHours(-12),
                ReceivedByUserId = _fixture.SeededUserId,
                CauseOfTestingId = cause.Id
            };
            var order = new TestOrder
            {
                TestCode = "TAMC",
                SectionId = section.Id,
                AssignedAnalystId = analystId,
                Status = ApprovalStatus.InProgress,
                CurrentStep = WorkflowStep.Incubating
            };
            sample.TestOrders.Add(order);
            arrangeDb.Samples.Add(sample);
            await arrangeDb.SaveChangesAsync();

            var incubation = new Incubation
            {
                TestOrderId = order.Id,
                StepNumber = 1,
                StepName = "Stage 1",
                StartedAt = now.UtcDateTime.AddDays(-2),
                ExpectedReadingAt = now.UtcDateTime.AddMinutes(-10),
                StartedByUserId = starterId,
                StageNumber = 1
            };
            arrangeDb.Incubations.Add(incubation);
            await arrangeDb.SaveChangesAsync();

            incubationId = incubation.Id;
            sampleId = sample.Id;
            testOrderId = order.Id;
        }

        await using (var actDb1 = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDb1, clock).RunAsync();
        }

        var incKey = $"IncubationReady:incubation:{incubationId}";

        await using (var midAssertDb = _fixture.CreateDbContext())
        {
            var rows = await midAssertDb.NotificationLogs.AsNoTracking()
                .Where(n => n.DedupeKey == incKey)
                .ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r =>
            {
                Assert.Equal("IncubationReady", r.Type);
                Assert.Equal($"TAMC (Stage 1) for sample {refNo} is ready.", r.Message);
                Assert.Equal(sampleId, r.SampleId);
                Assert.Equal(testOrderId, r.TestOrderId);
                Assert.Null(r.ResolvedAt);
            });
            Assert.Contains(rows, r => r.UserId == analystId);
            Assert.Contains(rows, r => r.UserId == starterId);
        }

        // Analyst reads the incubation.
        await using (var readDb = _fixture.CreateDbContext())
        {
            var inc = await readDb.Incubations.FirstAsync(i => i.Id == incubationId);
            inc.CompletedAt = now.UtcDateTime.AddMinutes(5);
            inc.CompletedByUserId = analystId;
            await readDb.SaveChangesAsync();
        }

        await using (var actDb2 = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDb2, clock).RunAsync();
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var rows = await assertDb.NotificationLogs.AsNoTracking()
                .Where(n => n.DedupeKey == incKey)
                .ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.NotNull(r.ResolvedAt));
        }
    }

    [PostgresFact]
    public async Task ItemOverdue_At24h01_Created_At23h59_Not()
    {
        var baseline = new DateTimeOffset(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(baseline.AddHours(23).AddMinutes(59));

        int analystId;
        int reviewerId;
        int approverId;
        int unrelatedAnalystId;
        int testOrderId;
        int reviewSampleId;
        int approvalSampleId;
        int sectionId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var section = await CreateSectionAsync(arrangeDb, "OVR");
            sectionId = section.Id;

            var analystRole = await arrangeDb.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
            var reviewerRole = await CreateRoleWithPermissionsAsync(arrangeDb, RoleType.Reviewer, PermissionConstants.SamplesReview);
            var approverRole = await CreateRoleWithPermissionsAsync(arrangeDb, RoleType.SectionHead, PermissionConstants.SamplesApprove);

            var analyst = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, sectionId, "ova", "ova@lab.test");
            var unrelatedAnalyst = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, sectionId, "ovu", "ovu@lab.test");
            var reviewer = await CreateUserInSectionAsync(arrangeDb, reviewerRole.Id, sectionId, "ovr", "ovr@lab.test");
            var approver = await CreateUserInSectionAsync(arrangeDb, approverRole.Id, sectionId, "ovp", "ovp@lab.test");

            analystId = analyst.Id;
            unrelatedAnalystId = unrelatedAnalyst.Id;
            reviewerId = reviewer.Id;
            approverId = approver.Id;

            var cause = new CauseOfTesting { Name = UniqueCode("Routine ") };
            arrangeDb.CausesOfTesting.Add(cause);
            await arrangeDb.SaveChangesAsync();

            var testingSample = new Sample
            {
                ReferenceNumber = UniqueCode("OV-TST-"),
                Category = SampleCategory.FinishedProduct,
                Status = SampleStatus.InTesting,
                ReceivedAt = baseline.UtcDateTime,
                ReceivedByUserId = _fixture.SeededUserId,
                CauseOfTestingId = cause.Id
            };
            var testOrder = new TestOrder
            {
                TestCode = "TAMC",
                SectionId = sectionId,
                AssignedAnalystId = analystId,
                Status = ApprovalStatus.InProgress,
                CurrentStep = WorkflowStep.Running
            };
            testingSample.TestOrders.Add(testOrder);

            var reviewSample = new Sample
            {
                ReferenceNumber = UniqueCode("OV-REV-"),
                Category = SampleCategory.FinishedProduct,
                Status = SampleStatus.UnderReview,
                ReceivedAt = baseline.UtcDateTime.AddHours(-2),
                ReceivedByUserId = _fixture.SeededUserId,
                CauseOfTestingId = cause.Id
            };
            reviewSample.TestOrders.Add(new TestOrder
            {
                TestCode = "TAMC",
                SectionId = sectionId,
                AssignedAnalystId = analystId,
                Status = ApprovalStatus.ResultEntered,
                CurrentStep = WorkflowStep.Ready
            });

            var approvalSample = new Sample
            {
                ReferenceNumber = UniqueCode("OV-APP-"),
                Category = SampleCategory.FinishedProduct,
                Status = SampleStatus.UnderApproval,
                ReceivedAt = baseline.UtcDateTime.AddHours(-2),
                ReviewedAt = baseline.UtcDateTime,
                ReceivedByUserId = _fixture.SeededUserId,
                CauseOfTestingId = cause.Id
            };
            approvalSample.TestOrders.Add(new TestOrder
            {
                TestCode = "TAMC",
                SectionId = sectionId,
                AssignedAnalystId = analystId,
                Status = ApprovalStatus.Reviewed,
                CurrentStep = WorkflowStep.Reviewed
            });

            arrangeDb.Samples.AddRange(testingSample, reviewSample, approvalSample);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.ReviewWorkflowEvents.Add(new ReviewWorkflowEvent
            {
                EntityType = ReviewEntityTypes.Sample,
                EntityId = reviewSample.Id,
                EventType = ReviewWorkflowEventType.SubmittedForReview,
                PerformedByUserId = analystId,
                PerformedByNameSnapshot = "Analyst",
                Timestamp = baseline.UtcDateTime
            });
            await arrangeDb.SaveChangesAsync();

            testOrderId = testOrder.Id;
            reviewSampleId = reviewSample.Id;
            approvalSampleId = approvalSample.Id;
        }

        var testKey = $"ItemOverdue:test:{testOrderId}";
        var revKey = $"ItemOverdue:review:{reviewSampleId}:section:{sectionId}";
        var appKey = $"ItemOverdue:approval:{approvalSampleId}:section:{sectionId}";

        // At 23h59: no overdue notifications created.
        await using (var actDb23 = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDb23, clock).RunAsync();
        }

        await using (var assertDb23 = _fixture.CreateDbContext())
        {
            var before = await assertDb23.NotificationLogs.AsNoTracking()
                .Where(n => n.DedupeKey == testKey || n.DedupeKey == revKey || n.DedupeKey == appKey)
                .ToListAsync();
            Assert.Empty(before);
        }

        // Advance clock to 24h01: created for assignee / reviewers / approvers, NOT unrelated analyst.
        clock.UtcNow = baseline.AddHours(24).AddMinutes(1);

        await using (var actDb24 = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDb24, clock).RunAsync();
        }

        await using (var assertDb24 = _fixture.CreateDbContext())
        {
            var after = await assertDb24.NotificationLogs.AsNoTracking()
                .Where(n => n.DedupeKey == testKey || n.DedupeKey == revKey || n.DedupeKey == appKey)
                .ToListAsync();

            Assert.Contains(after, n => n.DedupeKey == testKey && n.UserId == analystId && n.Type == "ItemOverdue");
            Assert.Contains(after, n => n.DedupeKey == revKey && n.UserId == reviewerId && n.Type == "ItemOverdue");
            Assert.Contains(after, n => n.DedupeKey == appKey && n.UserId == approverId && n.Type == "ItemOverdue");
            Assert.DoesNotContain(after, n => n.UserId == unrelatedAnalystId);
            Assert.DoesNotContain(after, n => (n.DedupeKey == revKey || n.DedupeKey == appKey) && n.UserId == analystId);
        }
    }

    [PostgresFact]
    public async Task RetentionPurge_RemovesOldRows_KeepsRecent()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        int userId;
        int oldId;
        int recentId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var section = await CreateSectionAsync(arrangeDb, "RET");
            var role = await arrangeDb.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
            var user = await CreateUserInSectionAsync(arrangeDb, role.Id, section.Id, "ret", null);
            userId = user.Id;

            var oldRow = new NotificationLog
            {
                UserId = userId,
                Type = "DirectMessage",
                Title = "Old",
                Message = "Old message",
                CreatedAt = now.UtcDateTime.AddDays(-91)
            };
            var recentRow = new NotificationLog
            {
                UserId = userId,
                Type = "DirectMessage",
                Title = "Recent",
                Message = "Recent message",
                CreatedAt = now.UtcDateTime.AddDays(-10)
            };
            arrangeDb.NotificationLogs.AddRange(oldRow, recentRow);
            await arrangeDb.SaveChangesAsync();
            oldId = oldRow.Id;
            recentId = recentRow.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDb, clock).RunAsync();
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Null(await assertDb.NotificationLogs.AsNoTracking().FirstOrDefaultAsync(n => n.Id == oldId));
            Assert.NotNull(await assertDb.NotificationLogs.AsNoTracking().FirstOrDefaultAsync(n => n.Id == recentId));
        }
    }

    [PostgresFact]
    public async Task Digest_OneEmailPerUserPerLabDay_SkippedWhenNothingUnread_SentAtConfiguredLocalHour()
    {
        // Africa/Cairo is UTC+2 in November. Default notifications.digestHourLocal is 7 (07:00 local = 05:00 UTC).
        var beforeDigestHourUtc = new DateTimeOffset(2026, 11, 15, 4, 30, 0, TimeSpan.Zero); // 06:30 Cairo
        var atDigestHourUtc = new DateTimeOffset(2026, 11, 15, 5, 0, 0, TimeSpan.Zero);     // 07:00 Cairo
        var nextDayDigestUtc = new DateTimeOffset(2026, 11, 16, 5, 5, 0, TimeSpan.Zero);    // 07:05 Cairo next day

        var clock = new MutableTimeProvider(beforeDigestHourUtc);
        int userId;
        int unreadLogId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var section = await CreateSectionAsync(arrangeDb, "DIG");
            var role = await arrangeDb.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
            var user = await CreateUserInSectionAsync(arrangeDb, role.Id, section.Id, "dig", "digest@lab.test");
            userId = user.Id;

            arrangeDb.UserNotificationPreferences.Add(new UserNotificationPreference
            {
                UserId = userId,
                Type = "MediaExpiry",
                EmailMode = EmailMode.Digest
            });

            var log = new NotificationLog
            {
                UserId = userId,
                Type = "MediaExpiry",
                Title = "Media lot expiring or expired",
                Message = "TSA lot D1 expires tomorrow.",
                Severity = "warning",
                IsRead = false,
                CreatedAt = beforeDigestHourUtc.UtcDateTime.AddHours(-1)
            };
            arrangeDb.NotificationLogs.Add(log);
            await arrangeDb.SaveChangesAsync();
            unreadLogId = log.Id;
        }

        // Before configured local hour: no digest row.
        await using (var actDbBefore = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDbBefore, clock).RunAsync();
        }
        await using (var assertDbBefore = _fixture.CreateDbContext())
        {
            Assert.Empty(await assertDbBefore.EmailOutbox.AsNoTracking()
                .Where(e => e.UserId == userId && e.Kind == EmailOutboxKind.Digest)
                .ToListAsync());
        }

        // At configured local hour: one Digest outbox row created.
        clock.UtcNow = atDigestHourUtc;
        await using (var actDbAtHour = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDbAtHour, clock).RunAsync();
        }
        await using (var assertDbAtHour = _fixture.CreateDbContext())
        {
            var digests = await assertDbAtHour.EmailOutbox.AsNoTracking()
                .Where(e => e.UserId == userId && e.Kind == EmailOutboxKind.Digest)
                .ToListAsync();
            var digest = Assert.Single(digests);
            Assert.Equal("digest@lab.test", digest.ToAddress);
            Assert.Contains("TSA lot D1 expires tomorrow.", digest.Body);
            Assert.Contains("https://lims.example.test", digest.Body);
        }

        // Second run same lab date: guarded — still only one Digest row.
        clock.UtcNow = atDigestHourUtc.AddHours(3);
        await using (var actDbRepeat = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDbRepeat, clock).RunAsync();
        }
        await using (var assertDbRepeat = _fixture.CreateDbContext())
        {
            Assert.Single(await assertDbRepeat.EmailOutbox.AsNoTracking()
                .Where(e => e.UserId == userId && e.Kind == EmailOutboxKind.Digest)
                .ToListAsync());
        }

        // Next lab day after marking the notification read: skipped when nothing unread.
        await using (var readDb = _fixture.CreateDbContext())
        {
            var log = await readDb.NotificationLogs.FirstAsync(n => n.Id == unreadLogId);
            log.IsRead = true;
            log.ReadAt = clock.UtcNow.UtcDateTime;
            await readDb.SaveChangesAsync();
        }

        clock.UtcNow = nextDayDigestUtc;
        await using (var actDbNextDay = _fixture.CreateDbContext())
        {
            await CreateScheduler(actDbNextDay, clock).RunAsync();
        }
        await using (var assertDbNextDay = _fixture.CreateDbContext())
        {
            Assert.Single(await assertDbNextDay.EmailOutbox.AsNoTracking()
                .Where(e => e.UserId == userId && e.Kind == EmailOutboxKind.Digest)
                .ToListAsync());
        }
    }

    [PostgresFact]
    public async Task GetNotifications_NoLongerComputes()
    {
        var now = DateTimeOffset.UtcNow;
        int userId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var section = await CreateSectionAsync(arrangeDb, "NOC");
            var role = await arrangeDb.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
            var user = await CreateUserInSectionAsync(arrangeDb, role.Id, section.Id, "nocomp", "nocomp@lab.test");
            userId = user.Id;

            var mat = new Material
            {
                MaterialType = MaterialType.DehydratedMedia,
                MaterialName = "TSB",
                ManufacturerName = "Oxoid",
                BatchNumber = UniqueCode("LOT-"),
                ReceivingDate = now.UtcDateTime.AddDays(-5),
                Code = UniqueCode("TSB"),
                Location = "Lab",
                QuantityReceived = 100,
                QuantityRemaining = 100,
                Unit = MaterialUnit.Gram,
                SectionId = section.Id
            };
            arrangeDb.Materials.Add(mat);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.Media.Add(new Media
            {
                MaterialId = mat.Id,
                LotNumber = UniqueCode("NC/"),
                ExpiryDate = now.UtcDateTime.AddDays(1),
                PreparedByUserId = userId,
                Status = MediaStatus.Active,
                ApprovalStatus = ApprovalGateStatus.Approved,
                IsReleasedForUse = true
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = new DashboardNotificationService(actDb);
            var list = await service.GetNotificationsAsync(RoleType.Analyst, userId);
            Assert.Empty(list);
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Empty(await assertDb.NotificationLogs.AsNoTracking().Where(n => n.UserId == userId).ToListAsync());
        }
    }

    [PostgresFact]
    public async Task WorkerIsListedInBackgroundJobs_AndRecordsRuns()
    {
        var def = BackgroundJobCatalog.Find(NotificationWorker.ProcessName);
        Assert.NotNull(def);
        Assert.Equal(BackgroundJobClassification.Gxp, def!.Classification);

        var services = new ServiceCollection();
        services.AddScoped<IMicroLimsDbContext>(_ => _fixture.CreateDbContext());
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<ILabClock>(_ => LabClock.Default);
        services.AddSingleton(DefaultConfig());
        services.AddSingleton<IEmailSender, NoOpEmailSender>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();
        services.AddScoped<SegregationOfDutiesGuard>();
        services.AddScoped<NotificationRecipients>();
        services.AddScoped<NotificationScheduler>();
        services.AddScoped<EmailOutboxSender>();

        await using var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var recorder = new BackgroundJobRunRecorder(scopeFactory, TimeProvider.System, NullLogger<BackgroundJobRunRecorder>.Instance);
        var worker = new NotificationWorker(scopeFactory, NullLogger<NotificationWorker>.Instance, recorder);

        using var cts = new CancellationTokenSource();
        var run = await recorder.RunAsync(
            NotificationWorker.ProcessName,
            BackgroundJobTrigger.Schedule,
            null,
            null,
            ct => worker.RunCycleAsync(ct).ContinueWith(t => (int?)t.Result, ct),
            cts.Token);

        Assert.Equal(BackgroundJobOutcome.Succeeded, run.Outcome);

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.BackgroundJobRuns.AsNoTracking()
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(r => r.JobName == NotificationWorker.ProcessName);
        Assert.NotNull(persisted);
        Assert.Equal(BackgroundJobOutcome.Succeeded, persisted!.Outcome);
    }
}

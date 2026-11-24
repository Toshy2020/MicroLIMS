using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class QualitySchemaPostgresTests
{
    private static readonly TimeSpan Blocked = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(30);
    private const string AdminPassword = "QualityAdminP@ss1!";
    private const string PreQualityMigration = "20261124182545_AddCompetencyOverrides";

    private static readonly string[] QualityCodes =
    [
        PermissionConstants.QualityApprove,
        PermissionConstants.QualityInvestigate,
        PermissionConstants.QualityLabConclude,
        PermissionConstants.QualityRaise,
        PermissionConstants.QualityView
    ];

    private readonly PostgresTestFixture _fixture;

    public QualitySchemaPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static FixedClock ClockInYear(int year) =>
        new(new DateTimeOffset(year, 6, 15, 12, 0, 0, TimeSpan.Zero));

    private static QualityRecord NewQualityRecord(
        string number,
        QualityRecordType type,
        int? raisedByUserId,
        DateTime raisedAtUtc,
        string? raisedBySystemProcess = null) => new()
    {
        Number = number,
        Type = type,
        Title = $"Test {type} {number}",
        Description = "Schema integration test record",
        RaisedByUserId = raisedByUserId,
        RaisedBySystemProcess = raisedBySystemProcess,
        RaisedAtUtc = raisedAtUtc
    };

    [PostgresFact]
    public async Task Concurrent_creation_gets_distinct_numbers_per_type()
    {
        var clock = ClockInYear(2081);
        var firstHasNumber = new TaskCompletionSource();
        var letFirstFinish = new TaskCompletionSource();

        var first = Task.Run(async () =>
        {
            await using var actDb = _fixture.CreateDbContext();
            await using var tx = await actDb.Database.BeginTransactionAsync();
            var number = await new ReferenceNumberGenerator(actDb, clock)
                .GenerateQualityNumberAsync(QualityRecordType.Deviation);
            firstHasNumber.SetResult();
            await letFirstFinish.Task.WaitAsync(Generous);
            actDb.QualityRecords.Add(NewQualityRecord(number, QualityRecordType.Deviation, _fixture.SeededUserId, clock.GetUtcNow().UtcDateTime));
            await actDb.SaveChangesAsync();
            await tx.CommitAsync();
            return number;
        });

        await firstHasNumber.Task.WaitAsync(Generous);

        var second = Task.Run(async () =>
        {
            await using var actDb = _fixture.CreateDbContext();
            await using var tx = await actDb.Database.BeginTransactionAsync();
            var number = await new ReferenceNumberGenerator(actDb, clock)
                .GenerateQualityNumberAsync(QualityRecordType.Deviation);
            actDb.QualityRecords.Add(NewQualityRecord(number, QualityRecordType.Deviation, _fixture.SeededUserId, clock.GetUtcNow().UtcDateTime));
            await actDb.SaveChangesAsync();
            await tx.CommitAsync();
            return number;
        });

        // Second transaction must wait for the first transaction's advisory lock.
        var finishedEarly = await Task.WhenAny(second, Task.Delay(Blocked));
        Assert.NotSame(second, finishedEarly);

        letFirstFinish.SetResult();
        var firstNumber = await first.WaitAsync(Generous);
        var secondNumber = await second.WaitAsync(Generous);

        Assert.Equal("DEV-2081-0001", firstNumber);
        Assert.Equal("DEV-2081-0002", secondNumber);

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.QualityRecords.AsNoTracking()
            .Where(r => r.Number.StartsWith("DEV-2081-"))
            .OrderBy(r => r.Number)
            .Select(r => r.Number)
            .ToListAsync();
        Assert.Equal(["DEV-2081-0001", "DEV-2081-0002"], persisted);
    }

    [PostgresFact]
    public async Task Number_restarts_per_year_and_type()
    {
        var clock2082 = ClockInYear(2082);
        var clock2083 = ClockInYear(2083);

        string dev2082First, dev2082Second, capa2082First, cc2082First, cmp2082First, dev2083First;

        await using (var actDb = _fixture.CreateDbContext())
        {
            await using var tx = await actDb.Database.BeginTransactionAsync();
            var gen2082 = new ReferenceNumberGenerator(actDb, clock2082);

            dev2082First = await gen2082.GenerateQualityNumberAsync(QualityRecordType.Deviation);
            actDb.QualityRecords.Add(NewQualityRecord(dev2082First, QualityRecordType.Deviation, _fixture.SeededUserId, clock2082.GetUtcNow().UtcDateTime));

            // Second in same transaction must see Local tracked row and increment.
            dev2082Second = await gen2082.GenerateQualityNumberAsync(QualityRecordType.Deviation);
            actDb.QualityRecords.Add(NewQualityRecord(dev2082Second, QualityRecordType.Deviation, _fixture.SeededUserId, clock2082.GetUtcNow().UtcDateTime));

            capa2082First = await gen2082.GenerateQualityNumberAsync(QualityRecordType.Capa);
            actDb.QualityRecords.Add(NewQualityRecord(capa2082First, QualityRecordType.Capa, _fixture.SeededUserId, clock2082.GetUtcNow().UtcDateTime));

            cc2082First = await gen2082.GenerateQualityNumberAsync(QualityRecordType.ChangeControl);
            actDb.QualityRecords.Add(NewQualityRecord(cc2082First, QualityRecordType.ChangeControl, _fixture.SeededUserId, clock2082.GetUtcNow().UtcDateTime));

            cmp2082First = await gen2082.GenerateQualityNumberAsync(QualityRecordType.Complaint);
            actDb.QualityRecords.Add(NewQualityRecord(cmp2082First, QualityRecordType.Complaint, _fixture.SeededUserId, clock2082.GetUtcNow().UtcDateTime));

            await actDb.SaveChangesAsync();
            await tx.CommitAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            await using var tx = await actDb.Database.BeginTransactionAsync();
            var gen2083 = new ReferenceNumberGenerator(actDb, clock2083);
            dev2083First = await gen2083.GenerateQualityNumberAsync(QualityRecordType.Deviation);
            actDb.QualityRecords.Add(NewQualityRecord(dev2083First, QualityRecordType.Deviation, _fixture.SeededUserId, clock2083.GetUtcNow().UtcDateTime));
            await actDb.SaveChangesAsync();
            await tx.CommitAsync();
        }

        Assert.Equal("DEV-2082-0001", dev2082First);
        Assert.Equal("DEV-2082-0002", dev2082Second);
        Assert.Equal("CAPA-2082-0001", capa2082First);
        Assert.Equal("CC-2082-0001", cc2082First);
        Assert.Equal("CMP-2082-0001", cmp2082First);
        Assert.Equal("DEV-2083-0001", dev2083First);
    }

    [PostgresFact]
    public async Task Step_and_answer_rows_reject_update_and_delete()
    {
        int stepId;
        int answerId;
        var now = DateTime.UtcNow;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var record = NewQualityRecord($"DEV-TRIG-{Guid.NewGuid():N}"[..20], QualityRecordType.Deviation, _fixture.SeededUserId, now);
            arrangeDb.QualityRecords.Add(record);
            await arrangeDb.SaveChangesAsync();

            var sig = new ElectronicSignature
            {
                UserId = _fixture.SeededUserId,
                UserFullNameSnapshot = "Seeded User",
                UsernameSnapshot = "seeded",
                RoleSnapshot = "Qa",
                MeaningOfSignature = SignatureMeaning.DeviationAssessed,
                EntityType = nameof(QualityRecord),
                EntityId = record.Id,
                SignedAt = now,
                Comment = "Test signature"
            };
            arrangeDb.ElectronicSignatures.Add(sig);
            await arrangeDb.SaveChangesAsync();

            var step = new QualityRecordStep
            {
                RecordId = record.Id,
                Kind = QualityStepKind.DeviationAssessed,
                SignedByUserId = _fixture.SeededUserId,
                SignedAtUtc = now,
                SignatureId = sig.Id,
                Comment = "Original",
                SnapshotJson = "{}"
            };
            arrangeDb.QualityRecordSteps.Add(step);

            var answer = new QualityChecklistAnswer
            {
                RecordId = record.Id,
                ItemKey = "Q1",
                ItemText = "Is impact assessed?",
                Answer = QualityChecklistAnswerValue.Yes,
                Comment = "Original answer",
                AnsweredByUserId = _fixture.SeededUserId,
                AnsweredAtUtc = now
            };
            arrangeDb.QualityChecklistAnswers.Add(answer);
            await arrangeDb.SaveChangesAsync();

            stepId = step.Id;
            answerId = answer.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"QualityRecordSteps\" SET \"Comment\" = 'Tampered' WHERE \"Id\" = {stepId}"));
        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"QualityRecordSteps\" WHERE \"Id\" = {stepId}"));
        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"QualityRecordSteps\""));

        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"QualityChecklistAnswers\" SET \"Comment\" = 'Tampered' WHERE \"Id\" = {answerId}"));
        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"QualityChecklistAnswers\" WHERE \"Id\" = {answerId}"));
        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"QualityChecklistAnswers\""));
    }

    [PostgresFact]
    public async Task Record_delete_rejected()
    {
        int recordId;
        int actionId;
        int linkId;
        int attachmentId;
        var now = DateTime.UtcNow;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var record = NewQualityRecord($"DEV-DEL-{Guid.NewGuid():N}"[..20], QualityRecordType.Deviation, _fixture.SeededUserId, now);
            arrangeDb.QualityRecords.Add(record);
            await arrangeDb.SaveChangesAsync();

            var sig = new ElectronicSignature
            {
                UserId = _fixture.SeededUserId,
                UserFullNameSnapshot = "Seeded User",
                UsernameSnapshot = "seeded",
                RoleSnapshot = "Qa",
                MeaningOfSignature = SignatureMeaning.DeviationAssessed,
                EntityType = nameof(QualityRecord),
                EntityId = record.Id,
                SignedAt = now,
                Comment = "Test signature"
            };
            arrangeDb.ElectronicSignatures.Add(sig);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.QualityRecordSteps.Add(new QualityRecordStep
            {
                RecordId = record.Id,
                Kind = QualityStepKind.DeviationAssessed,
                SignedByUserId = _fixture.SeededUserId,
                SignedAtUtc = now,
                SignatureId = sig.Id,
                SnapshotJson = "{}"
            });

            var action = new QualityAction
            {
                RecordId = record.Id,
                Kind = QualityActionKind.Corrective,
                Description = "Action",
                ResponsibleUserId = _fixture.SeededUserId,
                DueDate = DateOnly.FromDateTime(now.AddDays(7))
            };
            arrangeDb.QualityActions.Add(action);

            var link = new QualityRecordLink
            {
                RecordId = record.Id,
                TargetType = QualityLinkTargetType.Item,
                TargetId = 1,
                AddedByUserId = _fixture.SeededUserId,
                AddedAtUtc = now
            };
            arrangeDb.QualityRecordLinks.Add(link);

            var attachment = new QualityAttachment
            {
                RecordId = record.Id,
                OriginalFileName = "evidence.pdf",
                StorageKey = $"quality/{record.Id}/{Guid.NewGuid():N}_evidence.pdf",
                ContentType = "application/pdf",
                FileExtension = ".pdf",
                FileSizeBytes = 1024,
                ContentSha256 = new string('a', 64),
                UploadedByUserId = _fixture.SeededUserId,
                UploadedAtUtc = now
            };
            arrangeDb.QualityAttachments.Add(attachment);

            await arrangeDb.SaveChangesAsync();

            recordId = record.Id;
            actionId = action.Id;
            linkId = link.Id;
            attachmentId = attachment.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"QualityRecords\" WHERE \"Id\" = {recordId}"));
        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"QualityRecords\" CASCADE"));

        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"QualityActions\" WHERE \"Id\" = {actionId}"));
        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"QualityActions\" CASCADE"));

        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"QualityRecordLinks\" WHERE \"Id\" = {linkId}"));
        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"QualityRecordLinks\" CASCADE"));

        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"QualityAttachments\" WHERE \"Id\" = {attachmentId}"));
        await Assert.ThrowsAsync<PostgresException>(() =>
            actDb.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"QualityAttachments\""));
    }

    [PostgresFact]
    public async Task Classifications_seeded()
    {
        await using var assertDb = _fixture.CreateDbContext();
        var classifications = await assertDb.QualityClassifications.AsNoTracking()
            .OrderBy(c => c.Order)
            .Select(c => new { c.Name, c.Order, c.IsActive })
            .ToListAsync();

        Assert.Contains(classifications, c => c.Name == "Minor" && c.IsActive);
        Assert.Contains(classifications, c => c.Name == "Major" && c.IsActive);
        Assert.Contains(classifications, c => c.Name == "Critical" && c.IsActive);
    }

    [PostgresFact]
    public async Task Null_raiser_allowed_with_raised_by_system_process()
    {
        int recordId;
        var now = DateTime.UtcNow;

        await using (var actDb = _fixture.CreateDbContext())
        {
            var record = NewQualityRecord(
                $"DEV-SYS-{Guid.NewGuid():N}"[..20],
                QualityRecordType.Deviation,
                raisedByUserId: null,
                now,
                raisedBySystemProcess: "MonitoringDeviationEscalation");
            actDb.QualityRecords.Add(record);
            await actDb.SaveChangesAsync();
            recordId = record.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.QualityRecords.AsNoTracking().SingleAsync(r => r.Id == recordId);
        Assert.Null(persisted.RaisedByUserId);
        Assert.Equal("MonitoringDeviationEscalation", persisted.RaisedBySystemProcess);
    }

    [PostgresFact]
    public async Task Unique_link_index_rejects_duplicate_including_null_fields()
    {
        int recordId;
        var now = DateTime.UtcNow;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var record = NewQualityRecord($"CC-LNK-{Guid.NewGuid():N}"[..20], QualityRecordType.ChangeControl, _fixture.SeededUserId, now);
            arrangeDb.QualityRecords.Add(record);
            await arrangeDb.SaveChangesAsync();
            recordId = record.Id;

            arrangeDb.QualityRecordLinks.Add(new QualityRecordLink
            {
                RecordId = recordId,
                TargetType = QualityLinkTargetType.Specification,
                TargetId = 42,
                NewRecordCode = null,
                AddedByUserId = _fixture.SeededUserId,
                AddedAtUtc = now
            });
            arrangeDb.QualityRecordLinks.Add(new QualityRecordLink
            {
                RecordId = recordId,
                TargetType = QualityLinkTargetType.Specification,
                TargetId = null,
                NewRecordCode = "SPEC-NEW-01",
                AddedByUserId = _fixture.SeededUserId,
                AddedAtUtc = now
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.QualityRecordLinks.Add(new QualityRecordLink
            {
                RecordId = recordId,
                TargetType = QualityLinkTargetType.Specification,
                TargetId = 42,
                NewRecordCode = null,
                AddedByUserId = _fixture.SeededUserId,
                AddedAtUtc = now
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => actDb.SaveChangesAsync());
        }

        await using (var actDb2 = _fixture.CreateDbContext())
        {
            actDb2.QualityRecordLinks.Add(new QualityRecordLink
            {
                RecordId = recordId,
                TargetType = QualityLinkTargetType.Specification,
                TargetId = null,
                NewRecordCode = "SPEC-NEW-01",
                AddedByUserId = _fixture.SeededUserId,
                AddedAtUtc = now
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => actDb2.SaveChangesAsync());
        }
    }

    [PostgresFact]
    public async Task Upgrade_grants_and_audits_exactly_the_expected_quality_permissions()
    {
        await ScratchPostgresDatabase.RunAsync(async connection =>
        {
            await ArrangeUpgradeAsync(connection);

            await using var beforeDb = ScratchPostgresDatabase.NewContext(connection);
            var before = await RolesAsync(beforeDb);
            var lastAuditId = await beforeDb.AuditLogs.AsNoTracking().MaxAsync(log => (int?)log.Id) ?? 0;

            await using (var migrateDb = ScratchPostgresDatabase.NewContext(connection))
                await migrateDb.Database.MigrateAsync();

            await using var afterDb = ScratchPostgresDatabase.NewContext(connection);
            var catalog = await CatalogAsync(afterDb);
            Assert.Equal(QualityCodes, catalog.Select(p => p.Code));
            Assert.All(catalog, p => Assert.True(p.IsEnforced));

            var after = await RolesAsync(afterDb);
            Assert.Equal(before.Select(r => r.Id), after.Select(r => r.Id));
            var audits = await afterDb.AuditLogs.AsNoTracking()
                .Where(log => log.Id > lastAuditId && log.EntityName == "Role")
                .ToListAsync();
            Assert.Equal(before.Count(r => ExpectedGrants(r).Length > 0), audits.Count);

            foreach (var (original, updated) in before.Zip(after))
            {
                var added = ExpectedGrants(original);
                var expected = Sort(original.Codes.Concat(added));
                Assert.Equal(expected, updated.Codes);
                Assert.Equal(original.PermissionVersion + (added.Length == 0 ? 0 : 1), updated.PermissionVersion);

                if (added.Length == 0) continue;

                var audit = Assert.Single(audits, log => log.EntityId == original.Id.ToString());
                Assert.Equal("ROLE_PERMISSIONS_UPDATED", audit.Action);
                Assert.Equal("RolePermissionsUpdated", audit.ActionCode);
                Assert.Equal(AuditActionCategory.Security, audit.ActionCategory);
                Assert.Equal(ActorType.System, audit.ActorType);
                Assert.Equal(0, audit.UserId);
                Assert.Contains("AddQualityPermissions", audit.SystemProcessName);
                Assert.False(string.IsNullOrWhiteSpace(audit.Reason));
                using var previous = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
                using var current = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
                Assert.Equal(original.Codes, ReadCodes(previous, "PermissionCodes"));
                Assert.Equal(expected, ReadCodes(current, "PermissionCodes"));
                Assert.Equal(added, ReadCodes(current, "Granted"));
                Assert.Empty(ReadCodes(current, "Revoked"));
            }
        });
    }

    [PostgresFact]
    public async Task Qa_role_cannot_be_granted_investigate_or_lab_conclude()
    {
        int adminId;
        int qaRoleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            adminId = await CreateActingAdminAsync(arrangeDb);
            var qaRole = new Role
            {
                Name = $"QA_Test_{Guid.NewGuid():N}",
                Type = RoleType.Qa,
                IsSystemRole = false,
                IsActive = true
            };
            arrangeDb.Roles.Add(qaRole);
            await arrangeDb.SaveChangesAsync();
            qaRoleId = qaRole.Id;
        }

        foreach (var forbidden in new[] { PermissionConstants.QualityInvestigate, PermissionConstants.QualityLabConclude })
        {
            await using var actDb = _fixture.CreateDbContext();
            var service = new RoleService(actDb, new PermissionService(actDb));
            var requested = PermissionConstants.QaGrants
                .Where(c => !PermissionConstants.Hidden.Contains(c))
                .Append(forbidden)
                .ToList();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.UpdatePermissionsAsync(qaRoleId, requested, adminId, $"Attempt {forbidden}", AdminPassword));

            Assert.Equal($"A Quality Assurance role cannot hold: {forbidden}.", ex.Message);
        }
    }

    [PostgresFact]
    public async Task Non_qa_role_cannot_be_granted_approve_with_laboratory_codes()
    {
        int adminId;
        int sectionHeadCustomId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            adminId = await CreateActingAdminAsync(arrangeDb);
            var role = new Role
            {
                Name = $"Custom_SH_{Guid.NewGuid():N}",
                Type = RoleType.SectionHead,
                IsSystemRole = false,
                IsActive = true
            };
            arrangeDb.Roles.Add(role);
            await arrangeDb.SaveChangesAsync();
            sectionHeadCustomId = role.Id;

            var perm = await arrangeDb.Permissions.AsNoTracking()
                .SingleAsync(p => p.Code == PermissionConstants.SamplesReview);
            arrangeDb.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = new RoleService(actDb, new PermissionService(actDb));
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdatePermissionsAsync(
                sectionHeadCustomId,
                new List<string> { PermissionConstants.SamplesReview, PermissionConstants.QualityApprove },
                adminId,
                "Attempt to grant Quality.Approve with lab code",
                AdminPassword));

        Assert.Equal("A Quality Assurance role cannot hold: Samples.Review.", ex.Message);
    }

    private static async Task<int> CreateActingAdminAsync(MicroLimsDbContext db)
    {
        var adminRole = await db.Roles.AsNoTracking()
            .FirstAsync(r => r.Type == RoleType.SystemAdministrator && r.IsSystemRole);
        var actor = new User
        {
            FullName = "Quality Schema Admin",
            Username = $"qual_admin_{Guid.NewGuid():N}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword, 4),
            RoleId = adminRole.Id,
            IsActive = true
        };
        db.Users.Add(actor);
        await db.SaveChangesAsync();
        return actor.Id;
    }

    private static async Task ArrangeUpgradeAsync(string connection)
    {
        await using var db = ScratchPostgresDatabase.NewContext(connection);
        await db.Database.MigrateAsync(PreQualityMigration);
        db.Roles.AddRange(Enum.GetValues<RoleType>()
            .Select(type => new Role { Type = type, Name = type.ToString(), IsSystemRole = true }));
        db.Roles.Add(new Role { Type = RoleType.Analyst, Name = "Custom analyst" });
        await db.SaveChangesAsync();
    }

    private static string[] ExpectedGrants(RoleState role) => (role.IsSystemRole, role.Type) switch
    {
        (true, RoleType.SectionHead) =>
        [
            PermissionConstants.QualityInvestigate,
            PermissionConstants.QualityLabConclude,
            PermissionConstants.QualityRaise,
            PermissionConstants.QualityView
        ],
        (true, RoleType.Reviewer or RoleType.Analyst) =>
        [
            PermissionConstants.QualityRaise,
            PermissionConstants.QualityView
        ],
        (true, RoleType.QcManager) =>
        [
            PermissionConstants.QualityLabConclude,
            PermissionConstants.QualityRaise,
            PermissionConstants.QualityView
        ],
        (true, RoleType.Qa) =>
        [
            PermissionConstants.QualityApprove,
            PermissionConstants.QualityRaise,
            PermissionConstants.QualityView
        ],
        _ => []
    };

    private static async Task<RoleState[]> RolesAsync(MicroLimsDbContext db)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Type).ThenBy(r => r.Id)
            .Select(r => new { r.Id, r.Type, r.IsSystemRole, r.PermissionVersion }).ToListAsync();
        var grants = await db.RolePermissions.AsNoTracking()
            .Select(g => new { g.RoleId, Code = g.Permission!.Code }).ToListAsync();
        var byRole = grants.ToLookup(g => g.RoleId, g => g.Code);
        return roles.Select(r => new RoleState(r.Id, r.Type, r.IsSystemRole, r.PermissionVersion, Sort(byRole[r.Id]))).ToArray();
    }

    private static async Task<PermissionState[]> CatalogAsync(MicroLimsDbContext db) =>
        (await db.Permissions.AsNoTracking().Where(p => QualityCodes.Contains(p.Code))
            .Select(p => new PermissionState(p.Code, p.Description, p.IsEnforced))
            .ToListAsync()).OrderBy(p => p.Code, StringComparer.Ordinal).ToArray();

    private static string[] ReadCodes(JsonDocument json, string property) =>
        Sort(json.RootElement.GetProperty(property).EnumerateArray().Select(e => Assert.IsType<string>(e.GetString())));

    private static string[] Sort(IEnumerable<string> codes) => codes.Order(StringComparer.Ordinal).ToArray();

    private sealed record RoleState(int Id, RoleType Type, bool IsSystemRole, int PermissionVersion, string[] Codes);
    private sealed record PermissionState(string Code, string? Description, bool IsEnforced);
}

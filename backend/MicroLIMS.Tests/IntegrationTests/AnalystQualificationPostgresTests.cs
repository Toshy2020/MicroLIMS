using System.Security.Cryptography;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Competency;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class AnalystQualificationPostgresTests(PostgresTestFixture fixture)
{
    [PostgresFact]
    public async Task Migration_adds_qualification_and_evidence_tables()
    {
        await using var db = fixture.CreateDbContext();
        Assert.Equal(0L, await db.Database.SqlQueryRaw<long>(
            """SELECT count(*) AS "Value" FROM "AnalystQualifications" WHERE false""").SingleAsync());
        Assert.Equal(0L, await db.Database.SqlQueryRaw<long>(
            """SELECT count(*) AS "Value" FROM "QualificationAttachments" WHERE false""").SingleAsync());
    }

    [PostgresFact]
    public async Task Migration_seeds_competency_permissions()
    {
        await using var db = fixture.CreateDbContext();
        var codes = await db.Permissions.AsNoTracking().Where(p => p.Code.StartsWith("Competency."))
            .OrderBy(p => p.Code).Select(p => p.Code).ToListAsync();
        Assert.Equal(new[] { "Competency.Manage", "Competency.Override", "Competency.View" }, codes);
    }

    [PostgresFact]
    public async Task Migration_grants_competency_to_the_intended_existing_roles()
    {
        await using var isolated = new PostgresTestFixture();
        await isolated.InitializeAsync();
        await using (var before = isolated.CreateDbContext())
        {
            await before.GetService<IMigrator>().MigrateAsync("20261120111739_AddOosInvestigations");
            Assert.False(await before.Permissions.AsNoTracking().AnyAsync(p => p.Code.StartsWith("Competency.")));
            before.Roles.AddRange(Enum.GetValues<RoleType>().Select(type => new Role
            {
                Type = type, Name = $"Existing {type}", IsActive = true, IsSystemRole = true
            }));
            before.Roles.Add(new Role { Type = RoleType.SectionHead, Name = "Restricted custom lead", IsActive = true });
            await before.SaveChangesAsync();
        }

        await using (var migrate = isolated.CreateDbContext())
            await migrate.Database.MigrateAsync();

        await using var verify = isolated.CreateDbContext();
        var grants = await verify.RolePermissions.AsNoTracking()
            .Where(r => r.Role!.Name.StartsWith("Existing ") && r.Permission!.Code.StartsWith("Competency."))
            .Select(r => new { r.Role!.Type, r.Permission!.Code }).ToListAsync();
        Assert.Equal(new[] { RoleType.SectionHead, RoleType.Reviewer, RoleType.Analyst, RoleType.QcManager, RoleType.Qa },
            grants.Where(r => r.Code == "Competency.View").Select(r => r.Type).Order());
        Assert.Equal(new[] { RoleType.SectionHead, RoleType.QcManager },
            grants.Where(r => r.Code == "Competency.Manage").Select(r => r.Type).Order());
        Assert.Equal(RoleType.SectionHead, Assert.Single(grants, r => r.Code == "Competency.Override").Type);
        Assert.False(await verify.RolePermissions.AsNoTracking().AnyAsync(r =>
            r.Role!.Name == "Restricted custom lead" && r.Permission!.Code.StartsWith("Competency.")));
    }

    [PostgresFact]
    public async Task Signed_tables_reject_truncate()
    {
        await using var db = fixture.CreateDbContext();
        await RejectAsync(db, """TRUNCATE TABLE "AnalystQualifications" CASCADE""");
        await RejectAsync(db, """TRUNCATE TABLE "QualificationAttachments" """);
    }

    private const string Password = "qualification-password";
    private static readonly DateTimeOffset Now = new(2037, 1, 30, 23, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly LabToday = new(2037, 1, 31);
    private readonly FixedTimeProvider _clock = new(Now);
    private readonly InMemoryFileStorageService _storage = new();

    [PostgresFact]
    public async Task Expiry_uses_lab_date_and_qualification_persists_signed_evidence_and_audit()
    {
        var (request, head) = await ArrangeAsync(months: 1);
        var bytes = "%PDF-evidence"u8.ToArray();
        var id = await QualifyAsync(request with { Attachments = [new("evidence.pdf", bytes)] }, head);

        await using var verify = fixture.CreateDbContext();
        var q = await verify.AnalystQualifications.AsNoTracking().Include(x => x.Signature)
            .Include(x => x.Attachments).SingleAsync(x => x.Id == id);
        Assert.Equal(request.UserId, q.UserId);
        Assert.Equal(request.TestDefinitionId, q.TestDefinitionId);
        Assert.Equal(request.Basis, q.Basis);
        Assert.Equal(request.Evidence, q.Evidence);
        Assert.Null(q.EvidenceSampleId);
        Assert.Equal(LabToday, q.QualifiedOn);
        Assert.Equal(new DateOnly(2037, 2, 28), q.ExpiresOn);
        Assert.True(q.IsCurrentOn(new DateOnly(2037, 2, 28)));
        Assert.False(q.IsCurrentOn(new DateOnly(2037, 3, 1)));
        Assert.Null(q.WithdrawnOn);
        Assert.Equal(head, q.SignedByUserId);
        Assert.Equal(head, q.Signature.UserId);
        Assert.Equal("Signing head", q.Signature.UserFullNameSnapshot);
        Assert.Equal(Now.UtcDateTime, q.Signature.SignedAt);
        Assert.Equal(id, q.Signature.EntityId);
        Assert.Equal(nameof(AnalystQualification), q.Signature.EntityType);
        Assert.Equal(SignatureMeaning.AnalystQualified, q.Signature.MeaningOfSignature);
        var attachment = Assert.Single(q.Attachments);
        Assert.Equal(head, attachment.UploadedByUserId);
        Assert.Equal(Now.UtcDateTime, attachment.UploadedAt);
        Assert.Equal("evidence.pdf", attachment.OriginalFileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(bytes.LongLength, attachment.FileSizeBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), attachment.ContentSha256);
        Assert.Equal(bytes, await _storage.ReadAsync(attachment.StorageKey));

        var audit = await GetAuditAsync(verify, id, "Create");
        Assert.Equal(head, audit.UserId);
        Assert.Equal(request.Evidence, audit.Reason);
        Assert.Equal(Now.UtcDateTime, audit.Timestamp);
        Assert.Null(audit.PreviousValue);
        Assert.Contains(request.Evidence, audit.NewValue);
        Assert.Contains("2037-02-28", audit.NewValue);
        Assert.Contains(nameof(AnalystQualification.SignatureId), audit.NewValue);
    }

    [PostgresFact]
    public async Task No_interval_has_no_expiry_and_qualification_remains_valid_after_test_moves()
    {
        var (request, head) = await ArrangeAsync();
        var id = await QualifyAsync(request, head);
        await using (var move = fixture.CreateDbContext())
        {
            var section = NewSection();
            move.DocumentSections.Add(section);
            var test = await move.TestDefinitions.SingleAsync(t => t.Id == request.TestDefinitionId);
            test.Section = section;
            await move.SaveChangesAsync();
        }

        await using var verify = fixture.CreateDbContext();
        var testSection = await verify.TestDefinitions.AsNoTracking().Where(t => t.Id == request.TestDefinitionId)
            .Select(t => t.SectionId).SingleAsync();
        Assert.NotEqual(fixture.SeededSectionId, testSection);
        var q = await verify.AnalystQualifications.AsNoTracking().SingleAsync(x =>
            x.UserId == request.UserId && x.TestDefinitionId == request.TestDefinitionId);
        Assert.Equal(id, q.Id);
        Assert.Null(q.ExpiresOn);
        Assert.True(q.IsCurrentOn(LabToday.AddYears(1)));
    }

    [PostgresFact]
    public async Task Existing_test_master_update_sets_preserves_clears_and_audits_interval()
    {
        var (request, head) = await ArrangeAsync();
        string code;
        await using (var arrange = fixture.CreateDbContext())
            code = await arrange.TestDefinitions.AsNoTracking().Where(t => t.Id == request.TestDefinitionId)
                .Select(t => t.Code).SingleAsync();

        var update = new UpdateTestDefinitionRequest(code, "Qualification test");
        await using (var act = fixture.CreateDbContext(_clock))
        {
            var service = CreateTestMaster(act);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.UpdateTestDefinitionAsync(head, request.TestDefinitionId, update with { RequalificationMonths = 121 }));
        }
        await using (var act = fixture.CreateDbContext(_clock))
            await CreateTestMaster(act).UpdateTestDefinitionAsync(head, request.TestDefinitionId, update with { RequalificationMonths = 12 });
        await using (var act = fixture.CreateDbContext(_clock))
            await CreateTestMaster(act).UpdateTestDefinitionAsync(head, request.TestDefinitionId, update);
        await using (var check = fixture.CreateDbContext())
            Assert.Equal(12, await check.TestDefinitions.AsNoTracking().Where(t => t.Id == request.TestDefinitionId)
                .Select(t => t.RequalificationMonths).SingleAsync());

        var clear = System.Text.Json.JsonSerializer.Deserialize<UpdateTestDefinitionRequest>(
            $$"""{"code":"{{code}}","displayName":"Qualification test","requalificationMonths":null}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        await using (var act = fixture.CreateDbContext(_clock))
            await CreateTestMaster(act).UpdateTestDefinitionAsync(head, request.TestDefinitionId, Assert.IsType<UpdateTestDefinitionRequest>(clear));

        await using var verify = fixture.CreateDbContext();
        Assert.Null(await verify.TestDefinitions.AsNoTracking().Where(t => t.Id == request.TestDefinitionId)
            .Select(t => t.RequalificationMonths).SingleAsync());
        var updates = await verify.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(TestDefinition) &&
            a.EntityId == request.TestDefinitionId.ToString() && a.Action == "Update").OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, updates.Count);
        Assert.All(updates, a => Assert.Equal(head, a.UserId));
        Assert.Contains("\"RequalificationMonths\":12", updates[0].NewValue);
        Assert.Contains("\"RequalificationMonths\":12", updates[1].PreviousValue);
    }

    [PostgresFact]
    public async Task Qualification_refuses_self_nonmember_out_of_scope_wrong_password_and_no_permission()
    {
        var (request, head) = await ArrangeAsync();
        int nonmemberId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var nonmember = new User
            {
                RoleId = await arrange.Roles.AsNoTracking().Where(r => r.Type == RoleType.Analyst).Select(r => r.Id).FirstAsync(),
                FullName = "Analyst without membership", Username = Guid.NewGuid().ToString("N"), IsActive = true
            };
            arrange.Users.Add(nonmember);
            await arrange.SaveChangesAsync();
            nonmemberId = nonmember.Id;
        }

        await using (var act = fixture.CreateDbContext(_clock))
        {
            var service = CreateService(act);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.QualifyAsync(request with { UserId = head }, head));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.QualifyAsync(request with { UserId = nonmemberId }, head));
            await Assert.ThrowsAsync<CodedAccessDeniedException>(() => service.QualifyAsync(request, request.UserId));
            await Assert.ThrowsAsync<SignatureVerificationException>(() => service.QualifyAsync(request with { Password = "wrong" }, head));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.QualifyAsync(request with { QualifiedOn = LabToday.AddDays(1) }, head));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.QualifyAsync(request with
            {
                Attachments = [new("evidence.pdf", "invalid PDF"u8.ToArray())]
            }, head));
        }

        await using (var move = fixture.CreateDbContext())
        {
            var test = await move.TestDefinitions.SingleAsync(t => t.Id == request.TestDefinitionId);
            test.Section = NewSection();
            await move.SaveChangesAsync();
        }

        await using (var act = fixture.CreateDbContext(_clock))
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService(act).QualifyAsync(request, head));

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.AnalystQualifications.AsNoTracking().AnyAsync(q => q.TestDefinitionId == request.TestDefinitionId));
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.UserId == head));
        Assert.Contains(await verify.AuditLogs.AsNoTracking().Where(a => a.UserId == head).ToListAsync(),
            a => a.Action == "SignatureFailed");
        Assert.Empty(_storage.Files);
    }

    [PostgresFact]
    public async Task Withdraw_records_signature_state_and_audit_once_and_requalification_keeps_history()
    {
        var (request, head) = await ArrangeAsync();
        var id = await QualifyAsync(request, head);
        uint originalVersion;
        await using (var before = fixture.CreateDbContext())
            originalVersion = await before.AnalystQualifications.AsNoTracking().Where(q => q.Id == id).Select(q => q.Version).SingleAsync();

        const string reason = "Training evidence corrected";
        await using (var act = fixture.CreateDbContext(_clock))
        {
            var service = CreateService(act);
            await Assert.ThrowsAsync<CodedAccessDeniedException>(() => service.WithdrawAsync(id, reason, Password, request.UserId));
            act.ExpectedVersion = originalVersion - 1;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.WithdrawAsync(id, reason, Password, head));
            act.ExpectedVersion = null;
            await Assert.ThrowsAsync<SignatureVerificationException>(() => service.WithdrawAsync(id, reason, "wrong", head));
        }

        await using (var blocked = fixture.CreateDbContext())
            Assert.Null(await blocked.AnalystQualifications.AsNoTracking().Where(q => q.Id == id).Select(q => q.WithdrawnOn).SingleAsync());

        await using (var act = fixture.CreateDbContext(_clock))
            await CreateService(act).WithdrawAsync(id, reason, Password, head);
        await using (var repeat = fixture.CreateDbContext(_clock))
            await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(repeat).WithdrawAsync(id, "Second withdrawal", Password, head));

        var nextId = await QualifyAsync(request, head);
        Assert.NotEqual(id, nextId);
        await using var verify = fixture.CreateDbContext();
        var q = await verify.AnalystQualifications.AsNoTracking().Include(x => x.WithdrawnSignature).SingleAsync(x => x.Id == id);
        Assert.Equal(head, q.WithdrawnByUserId);
        Assert.Equal(reason, q.WithdrawnReason);
        Assert.Equal(Now.UtcDateTime, q.WithdrawnOn);
        Assert.NotEqual(originalVersion, q.Version);
        Assert.False(q.IsCurrentOn(LabToday));
        Assert.True((await verify.AnalystQualifications.AsNoTracking().SingleAsync(x => x.Id == nextId)).IsCurrentOn(LabToday));
        var signature = Assert.IsType<ElectronicSignature>(q.WithdrawnSignature);
        Assert.Equal(head, signature.UserId);
        Assert.Equal(id, signature.EntityId);
        Assert.Equal(Now.UtcDateTime, signature.SignedAt);
        Assert.Equal(SignatureMeaning.AnalystQualificationWithdrawn, signature.MeaningOfSignature);
        Assert.Equal(reason, signature.Comment);
        Assert.Equal(1, await verify.ElectronicSignatures.AsNoTracking().CountAsync(s =>
            s.EntityType == nameof(AnalystQualification) && s.EntityId == id &&
            s.MeaningOfSignature == SignatureMeaning.AnalystQualificationWithdrawn));

        var audit = await GetAuditAsync(verify, id, "Update");
        Assert.Equal(head, audit.UserId);
        Assert.Equal(reason, audit.Reason);
        Assert.Equal(Now.UtcDateTime, audit.Timestamp);
        Assert.Contains(request.Evidence, audit.PreviousValue);
        Assert.DoesNotContain(reason, audit.PreviousValue);
        Assert.Contains(reason, audit.NewValue);
        Assert.Contains(nameof(AnalystQualification.WithdrawnSignatureId), audit.NewValue);
    }

    [PostgresFact]
    public async Task Database_rejects_edits_deletes_and_adding_evidence_after_signing()
    {
        var (request, head) = await ArrangeAsync();
        var id = await QualifyAsync(request with { Attachments = [new("signed.txt", "evidence"u8.ToArray())] }, head);
        await using var db = fixture.CreateDbContext();
        await RejectAsync(db, """UPDATE "AnalystQualifications" SET "Evidence" = 'changed' WHERE "Id" = {0}""", id);
        await RejectAsync(db, """UPDATE "AnalystQualifications" SET "WithdrawnReason" = 'partial withdrawal' WHERE "Id" = {0}""", id);
        await RejectAsync(db, """DELETE FROM "AnalystQualifications" WHERE "Id" = {0}""", id);
        await RejectAsync(db, """UPDATE "QualificationAttachments" SET "StorageKey" = 'changed' WHERE "AnalystQualificationId" = {0}""", id);
        await RejectAsync(db, """DELETE FROM "QualificationAttachments" WHERE "AnalystQualificationId" = {0}""", id);
        await RejectAsync(db, """
            INSERT INTO "QualificationAttachments" ("AnalystQualificationId", "OriginalFileName", "StorageKey",
                "ContentType", "FileExtension", "FileSizeBytes", "ContentSha256", "UploadedByUserId", "UploadedAt")
            VALUES ({0}, 'unsigned.txt', 'unsigned', 'text/plain', '.txt', 1, 'x', {1}, now())
            """, id, head);
    }

    private async Task<int> QualifyAsync(QualifyRequest request, int head)
    {
        await using var act = fixture.CreateDbContext(_clock);
        return (await CreateService(act).QualifyAsync(request, head)).Id;
    }

    private TestDefinitionMasterDataService CreateTestMaster(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db), new AuditEventService(db, fixture.CreateSequenceHelper(db), _clock));

    private QualificationService CreateService(MicroLimsDbContext db) =>
        new(db, new PermissionService(db), new UserSectionScopeService(db),
            new LabClock(_clock, LabClock.ResolveTimeZone("Africa/Cairo")), fixture.CreateSequenceHelper(db),
            new ElectronicSignatureService(db, _clock), _storage);

    private async Task<(QualifyRequest Request, int Head)> ArrangeAsync(int? months = null)
    {
        await using var db = fixture.CreateDbContext();
        var key = Guid.NewGuid().ToString("N");
        var role = new Role { Type = RoleType.SectionHead, Name = key, IsActive = true };
        var head = new User
        {
            Role = role, FullName = "Signing head", Username = $"head-{key}", IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
        };
        var analyst = new User
        {
            RoleId = await db.Roles.AsNoTracking().Where(r => r.Type == RoleType.Analyst).Select(r => r.Id).FirstAsync(),
            FullName = "Qualified analyst", Username = $"analyst-{key}", IsActive = true
        };
        var test = new TestDefinition
        {
            Code = key, DisplayName = "Qualification test", SectionId = fixture.SeededSectionId,
            RequalificationMonths = months
        };
        db.Users.AddRange(head, analyst);
        db.TestDefinitions.Add(test);
        await db.SaveChangesAsync();
        var permissionId = await db.Permissions.Where(p => p.Code == "Competency.Manage").Select(p => p.Id).SingleAsync();
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
        foreach (var member in new[] { head, analyst })
            db.UserOrgMemberships.Add(new UserOrgMembership
            {
                UserId = member.Id, DepartmentId = fixture.SeededDepartmentId, SectionId = fixture.SeededSectionId
            });
        await db.SaveChangesAsync();
        return (new(analyst.Id, test.Id, QualificationBasis.ObservedPractice, "Observed the method", null, LabToday, Password), head.Id);
    }

    private DocumentSection NewSection()
    {
        var code = Guid.NewGuid().ToString("N")[..12];
        return new DocumentSection { Name = $"Destination laboratory {code}", Code = code, DepartmentId = fixture.SeededDepartmentId };
    }

    private static Task<AuditLog> GetAuditAsync(MicroLimsDbContext db, int id, string action) =>
        db.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(AnalystQualification) && a.EntityId == id.ToString() && a.Action == action);

    private static async Task RejectAsync(MicroLimsDbContext db, string sql, params object[] parameters)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, parameters));
        Assert.Contains("append-only:", error.Message);
    }
}

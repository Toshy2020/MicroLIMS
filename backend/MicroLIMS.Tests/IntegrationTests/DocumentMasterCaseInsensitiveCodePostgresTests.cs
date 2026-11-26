using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentMasterCaseInsensitiveCodePostgresTests(PostgresTestFixture fixture)
{
    private static DocumentMasterService CreateService(MicroLimsDbContext db)
    {
        var seqHelper = new DatabaseSequenceHelper(db);
        var auditService = new AuditEventService(db, seqHelper);
        var authService = new DocumentAuthorizationService(db);
        return new DocumentMasterService(db, seqHelper, auditService, authService);
    }

    private RegisterDocumentMasterRequest NewRegisterRequest(string code, string title = "CONF-212 Test Doc") =>
        new(
            CompanyDocumentCode: code,
            Title: title,
            DocumentTypeId: fixture.SeededDocTypeId,
            DepartmentId: fixture.SeededDepartmentId,
            SectionId: fixture.SeededSectionId,
            DocumentOwnerUserId: fixture.SeededControllerUserId,
            Confidentiality: DocumentConfidentiality.Internal);

    [PostgresFact]
    public async Task Create_WithCaseVariantOfActiveCode_RefusedWithCodedMessage()
    {
        var code = $"SOP-A-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        await using (var arrange = fixture.CreateDbContext())
        {
            await CreateService(arrange).RegisterDocumentMasterAsync(NewRegisterRequest(code), fixture.SeededControllerUserId);
        }

        await using (var act = fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                CreateService(act).RegisterDocumentMasterAsync(
                    NewRegisterRequest($"  {code.ToLowerInvariant()}  "),
                    fixture.SeededControllerUserId));

            Assert.Equal(MessageCodes.DocumentCompanyCodeAlreadyExists, ex.Code);
            Assert.Equal(code.ToLowerInvariant(), ex.Parameters!["companyDocumentCode"]);
        }

        await using var assert = fixture.CreateDbContext();
        Assert.Equal(1, await assert.DocumentMasters.AsNoTracking()
            .CountAsync(m => m.CompanyDocumentCode.ToLower() == code.ToLowerInvariant()));
    }

    [PostgresFact]
    public async Task Create_WithCaseVariantOfVoidedCode_RefusedWithCodedMessage()
    {
        var code = $"SOP-B-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        await using (var arrange = fixture.CreateDbContext())
        {
            var svc = CreateService(arrange);
            var created = await svc.RegisterDocumentMasterAsync(NewRegisterRequest(code), fixture.SeededControllerUserId);
            await svc.VoidMasterAsync(created.Id, new VoidDocumentMasterRequest("Voided for CONF-212 test"), fixture.SeededControllerUserId);
        }

        await using (var act = fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                CreateService(act).RegisterDocumentMasterAsync(
                    NewRegisterRequest(code.ToLowerInvariant()),
                    fixture.SeededControllerUserId));

            Assert.Equal(MessageCodes.DocumentCompanyCodeAlreadyExists, ex.Code);
            Assert.Equal(code.ToLowerInvariant(), ex.Parameters!["companyDocumentCode"]);
        }

        await using var assert = fixture.CreateDbContext();
        Assert.Equal(1, await assert.DocumentMasters.AsNoTracking()
            .CountAsync(m => m.CompanyDocumentCode.ToLower() == code.ToLowerInvariant()));
    }

    [PostgresFact]
    public async Task UpdateDraftMetadata_CodeChangeToCaseVariantOfAnotherMaster_Refused_AndOwnCaseChangeAudited()
    {
        var codeA = $"SOP-CA-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        var codeB = $"SOP-CB-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        var codeVoid = $"SOP-CV-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        int idB;

        await using (var arrange = fixture.CreateDbContext())
        {
            var svc = CreateService(arrange);
            await svc.RegisterDocumentMasterAsync(NewRegisterRequest(codeA, "Doc A"), fixture.SeededControllerUserId);
            var b = await svc.RegisterDocumentMasterAsync(NewRegisterRequest(codeB, "Doc B"), fixture.SeededControllerUserId);
            idB = b.Id;
            var c = await svc.RegisterDocumentMasterAsync(NewRegisterRequest(codeVoid, "Doc Void"), fixture.SeededControllerUserId);
            await svc.VoidMasterAsync(c.Id, new VoidDocumentMasterRequest("Voided for CONF-212 test"), fixture.SeededControllerUserId);
        }

        // Negative 1: cannot rename B to case-variant of active A
        await using (var actActive = fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                CreateService(actActive).UpdateDraftMetadataAsync(
                    idB,
                    new UpdateDocumentMasterDraftRequest(
                        codeA.ToLowerInvariant(), "Doc B", fixture.SeededDocTypeId,
                        fixture.SeededDepartmentId, fixture.SeededSectionId,
                        fixture.SeededControllerUserId, DocumentConfidentiality.Internal),
                    fixture.SeededControllerUserId));

            Assert.Equal(MessageCodes.DocumentCompanyCodeInUse, ex.Code);
            Assert.Equal(codeA.ToLowerInvariant(), ex.Parameters!["companyDocumentCode"]);
        }

        // Negative 2: cannot rename B to case-variant of voided C
        await using (var actVoid = fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                CreateService(actVoid).UpdateDraftMetadataAsync(
                    idB,
                    new UpdateDocumentMasterDraftRequest(
                        codeVoid.ToLowerInvariant(), "Doc B", fixture.SeededDocTypeId,
                        fixture.SeededDepartmentId, fixture.SeededSectionId,
                        fixture.SeededControllerUserId, DocumentConfidentiality.Internal),
                    fixture.SeededControllerUserId));

            Assert.Equal(MessageCodes.DocumentCompanyCodeInUse, ex.Code);
            Assert.Equal(codeVoid.ToLowerInvariant(), ex.Parameters!["companyDocumentCode"]);
        }

        // Positive (Rule 4.4 triad): renaming own code's casing succeeds, persists, and writes audit trail
        var newCaseB = codeB.ToLowerInvariant();
        await using (var actOwn = fixture.CreateDbContext())
        {
            await CreateService(actOwn).UpdateDraftMetadataAsync(
                idB,
                new UpdateDocumentMasterDraftRequest(
                    newCaseB, "Doc B Updated", fixture.SeededDocTypeId,
                    fixture.SeededDepartmentId, fixture.SeededSectionId,
                    fixture.SeededControllerUserId, DocumentConfidentiality.Internal),
                fixture.SeededControllerUserId);
        }

        await using var assert = fixture.CreateDbContext();
        var persisted = await assert.DocumentMasters.AsNoTracking().SingleAsync(m => m.Id == idB);
        Assert.Equal(newCaseB, persisted.CompanyDocumentCode);
        Assert.Equal("Doc B Updated", persisted.Title);

        var audit = await assert.AuditLogs.AsNoTracking()
            .Include(a => a.Changes)
            .FirstOrDefaultAsync(a => a.ActionCode == "DocumentMasterMetadataUpdated" && a.DocumentMasterId == idB);
        Assert.NotNull(audit);
        Assert.Equal(fixture.SeededControllerUserId, audit.UserId);
        Assert.Contains(audit.Changes, c => c.FieldName == "CompanyDocumentCode" && c.PreviousValue == codeB && c.NewValue == newCaseB);
    }

    [PostgresFact]
    public async Task Create_ExactReuseOfVoidedCode_RefusedWithCodedMessage()
    {
        var code = $"SOP-D-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        await using (var arrange = fixture.CreateDbContext())
        {
            var svc = CreateService(arrange);
            var created = await svc.RegisterDocumentMasterAsync(NewRegisterRequest(code), fixture.SeededControllerUserId);
            await svc.VoidMasterAsync(created.Id, new VoidDocumentMasterRequest("Voided for CONF-212 reuse test"), fixture.SeededControllerUserId);
        }

        await using (var act = fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                CreateService(act).RegisterDocumentMasterAsync(NewRegisterRequest(code), fixture.SeededControllerUserId));

            Assert.Equal(MessageCodes.DocumentCompanyCodeAlreadyExists, ex.Code);
            Assert.Equal(code, ex.Parameters!["companyDocumentCode"]);
        }

        await using var assert = fixture.CreateDbContext();
        Assert.Equal(1, await assert.DocumentMasters.AsNoTracking()
            .CountAsync(m => m.CompanyDocumentCode == code));
    }

    [PostgresFact]
    public async Task DbLevel_RawInsertOfCaseVariant_ViolatesUniqueIndex23505()
    {
        var code = $"SOP-E-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        await using (var arrange = fixture.CreateDbContext())
        {
            var svc = CreateService(arrange);
            var created = await svc.RegisterDocumentMasterAsync(NewRegisterRequest(code), fixture.SeededControllerUserId);
            await svc.VoidMasterAsync(created.Id, new VoidDocumentMasterRequest("Voided for raw DB index test"), fixture.SeededControllerUserId);
        }

        await using var act = fixture.CreateDbContext();
        var conn = (NpgsqlConnection)act.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO "DocumentMasters"
            ("MicroLimsDocumentId", "CompanyDocumentCode", "Title", "DocumentTypeId", "DepartmentId", "SectionId",
             "DocumentOwnerUserId", "Confidentiality", "RecordOrigin", "RecordStatus", "CreatedAt", "CreatedByUserId")
            VALUES
            (@mlId, @code, 'Raw Insert Clash', @typeId, @deptId, @secId, @userId, 1, 1, 1, NOW(), @userId);
            """;
        cmd.Parameters.AddWithValue("mlId", $"DOC-RAW-{Guid.NewGuid():N}"[..18]);
        cmd.Parameters.AddWithValue("code", code.ToLowerInvariant());
        cmd.Parameters.AddWithValue("typeId", fixture.SeededDocTypeId);
        cmd.Parameters.AddWithValue("deptId", fixture.SeededDepartmentId);
        cmd.Parameters.AddWithValue("secId", fixture.SeededSectionId);
        cmd.Parameters.AddWithValue("userId", fixture.SeededControllerUserId);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }
}

// Own throw-away databases for migration Up guard and Down rollback verification.
public class EnforceCaseInsensitiveDocumentCodesMigrationPostgresTests
{
    private const string Before = "20261126135039_AddOosRetestPlanReportableRetest";
    private const string MigrationSuffix = "_EnforceCaseInsensitiveDocumentCodes";

    [PostgresFact]
    public async Task Migration_Up_RefusesClashingCodesAndListsEveryGroup()
    {
        var admin = PostgresTestConfiguration.AdminConnectionString!;
        var name = PostgresTestConfiguration.NewTestDatabaseName();
        var cs = PostgresTestConfiguration.BuildTestConnectionString(admin, name);
        await Exec(admin, $"CREATE DATABASE \"{name}\"");
        try
        {
            await using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>().UseNpgsql(cs).Options);
            var target = db.Database.GetMigrations().Single(m => m.EndsWith(MigrationSuffix, StringComparison.Ordinal));
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(Before);

            await using var c = new NpgsqlConnection(cs);
            await c.OpenAsync();
            // Seed two clashing groups: active/active case-variant and active/void exact/case-variant.
            await InsertMasterAsync(c, "DOC-M-1", "CLASH-01", 1);
            await InsertMasterAsync(c, "DOC-M-2", "clash-01", 1);
            await InsertMasterAsync(c, "DOC-M-3", "DUP-02", 1);
            await InsertMasterAsync(c, "DOC-M-4", "dup-02", 2);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(target));
            Assert.Contains("CLASH-01", ex.MessageText);
            Assert.Contains("clash-01", ex.MessageText);
            Assert.Contains("DUP-02", ex.MessageText);
            Assert.Contains("dup-02", ex.MessageText);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            PostgresTestConfiguration.EnsureSafeToDrop(name, admin);
            await Exec(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    [PostgresFact]
    public async Task Migration_Down_RestoresOldFilteredUniqueIndex()
    {
        var admin = PostgresTestConfiguration.AdminConnectionString!;
        var name = PostgresTestConfiguration.NewTestDatabaseName();
        var cs = PostgresTestConfiguration.BuildTestConnectionString(admin, name);
        await Exec(admin, $"CREATE DATABASE \"{name}\"");
        try
        {
            await using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>().UseNpgsql(cs).Options);
            var target = db.Database.GetMigrations().Single(m => m.EndsWith(MigrationSuffix, StringComparison.Ordinal));
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(target);
            await migrator.MigrateAsync(Before);

            await using var c = new NpgsqlConnection(cs);
            await c.OpenAsync();

            // Old filtered index restored: case-variant active codes and duplicate void codes are allowed again,
            // while exact active duplicates are rejected.
            await InsertMasterAsync(c, "DOC-D-1", "DOWN-01", 1);
            await InsertMasterAsync(c, "DOC-D-2", "down-01", 1);
            await InsertMasterAsync(c, "DOC-D-3", "DOWN-VOID", 2);
            await InsertMasterAsync(c, "DOC-D-4", "DOWN-VOID", 2);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => InsertMasterAsync(c, "DOC-D-5", "DOWN-01", 1));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            PostgresTestConfiguration.EnsureSafeToDrop(name, admin);
            await Exec(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    private static async Task InsertMasterAsync(NpgsqlConnection c, string microLimsId, string companyCode, int recordStatus)
    {
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO "DocumentMasters"
            ("MicroLimsDocumentId", "CompanyDocumentCode", "Title", "DocumentTypeId", "DepartmentId", "SectionId",
             "DocumentOwnerUserId", "Confidentiality", "RecordOrigin", "RecordStatus", "CreatedAt", "CreatedByUserId")
            VALUES
            (@mlId, @code, @title,
             (SELECT "Id" FROM "DocumentTypes" LIMIT 1),
             (SELECT "Id" FROM "DocumentDepartments" LIMIT 1),
             (SELECT "Id" FROM "DocumentSections" LIMIT 1),
             (SELECT "Id" FROM "Users" LIMIT 1),
             1, 1, @status, NOW(),
             (SELECT "Id" FROM "Users" LIMIT 1));
            """, c);
        cmd.Parameters.AddWithValue("mlId", microLimsId);
        cmd.Parameters.AddWithValue("code", companyCode);
        cmd.Parameters.AddWithValue("title", companyCode);
        cmd.Parameters.AddWithValue("status", recordStatus);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task Exec(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }
}

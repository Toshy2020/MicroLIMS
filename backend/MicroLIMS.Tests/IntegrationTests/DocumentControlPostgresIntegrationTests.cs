using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[CollectionDefinition("PostgresDatabaseCollection")]
public class PostgresDatabaseCollection : ICollectionFixture<PostgresTestFixture> { }

[Collection("PostgresDatabaseCollection")]
public class DocumentControlPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentControlPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task AuditLogs_Update_ThrowsPostgresException()
    {
        await using var db = _fixture.CreateDbContext();

        var log = new AuditLog
        {
            EntityName = "TestEntity",
            EntityId = "1",
            Action = "Create",
            UserId = _fixture.SeededUserId,
            Timestamp = DateTime.UtcNow
        };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            var conn = (NpgsqlConnection)db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE \"AuditLogs\" SET \"Action\" = 'Tampered' WHERE \"Id\" = {log.Id};";
            await cmd.ExecuteNonQueryAsync();
        });

        Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prohibited", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task AuditLogs_Delete_ThrowsPostgresException()
    {
        await using var db = _fixture.CreateDbContext();

        var log = new AuditLog
        {
            EntityName = "TestEntity",
            EntityId = "2",
            Action = "Create",
            UserId = _fixture.SeededUserId,
            Timestamp = DateTime.UtcNow
        };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            var conn = (NpgsqlConnection)db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM \"AuditLogs\" WHERE \"Id\" = {log.Id};";
            await cmd.ExecuteNonQueryAsync();
        });

        Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task AuditEventChanges_Update_ThrowsPostgresException()
    {
        await using var db = _fixture.CreateDbContext();

        var log = new AuditLog
        {
            EntityName = "TestEntity",
            EntityId = "3",
            Action = "Create",
            UserId = _fixture.SeededUserId,
            Timestamp = DateTime.UtcNow
        };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();

        var change = new AuditEventChange
        {
            AuditLogId = log.Id,
            FieldName = "Title",
            PreviousValue = "Old",
            NewValue = "New"
        };
        db.AuditEventChanges.Add(change);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            var conn = (NpgsqlConnection)db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE \"AuditEventChanges\" SET \"NewValue\" = 'Tampered' WHERE \"Id\" = {change.Id};";
            await cmd.ExecuteNonQueryAsync();
        });

        Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task AuditEventChanges_Delete_ThrowsPostgresException()
    {
        await using var db = _fixture.CreateDbContext();

        var log = new AuditLog
        {
            EntityName = "TestEntity",
            EntityId = "4",
            Action = "Create",
            UserId = _fixture.SeededUserId,
            Timestamp = DateTime.UtcNow
        };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();

        var change = new AuditEventChange
        {
            AuditLogId = log.Id,
            FieldName = "Status",
            PreviousValue = "Draft",
            NewValue = "Active"
        };
        db.AuditEventChanges.Add(change);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            var conn = (NpgsqlConnection)db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM \"AuditEventChanges\" WHERE \"Id\" = {change.Id};";
            await cmd.ExecuteNonQueryAsync();
        });

        Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task ElectronicSignatures_Update_ThrowsPostgresException()
    {
        await using var db = _fixture.CreateDbContext();

        var sig = new ElectronicSignature
        {
            EntityType = "Sample",
            EntityId = 1,
            MeaningOfSignature = SignatureMeaning.Reviewed,
            UserId = _fixture.SeededUserId,
            SignedAt = DateTime.UtcNow,
            UserFullNameSnapshot = "QA Document Admin",
            UsernameSnapshot = "qa_admin",
            RoleSnapshot = "System Administrator"
        };
        db.ElectronicSignatures.Add(sig);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            var conn = (NpgsqlConnection)db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE \"ElectronicSignatures\" SET \"Comment\" = 'Tampered' WHERE \"Id\" = {sig.Id};";
            await cmd.ExecuteNonQueryAsync();
        });

        Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task ElectronicSignatures_Delete_ThrowsPostgresException()
    {
        await using var db = _fixture.CreateDbContext();

        var sig = new ElectronicSignature
        {
            EntityType = "Sample",
            EntityId = 2,
            MeaningOfSignature = SignatureMeaning.Approved,
            UserId = _fixture.SeededUserId,
            SignedAt = DateTime.UtcNow,
            UserFullNameSnapshot = "QA Document Admin",
            UsernameSnapshot = "qa_admin",
            RoleSnapshot = "System Administrator"
        };
        db.ElectronicSignatures.Add(sig);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            var conn = (NpgsqlConnection)db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM \"ElectronicSignatures\" WHERE \"Id\" = {sig.Id};";
            await cmd.ExecuteNonQueryAsync();
        });

        Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task DocumentNumberSequence_ProducesGaplessValuesUnderNormalLoad()
    {
        await using var db = _fixture.CreateDbContext();
        var helper = _fixture.CreateSequenceHelper(db);

        var values = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var val = await helper.GetNextSequenceValueAsync("document_number_seq");
            values.Add(val);
        }

        Assert.Equal(10, values.Count);
        for (int i = 1; i < values.Count; i++)
        {
            Assert.Equal(values[i - 1] + 1, values[i]);
        }
    }

    [PostgresFact]
    public async Task DocumentNumberSequence_PreservesGapsOnRolledBackTransaction()
    {
        await using var db = _fixture.CreateDbContext();
        var helper = _fixture.CreateSequenceHelper(db);

        long val1;
        // Transaction 1: draws a value, then rolls back
        await using (var tx1 = await db.Database.BeginTransactionAsync())
        {
            val1 = await helper.GetNextSequenceValueAsync("document_number_seq");
            await tx1.RollbackAsync();
        }

        long val2;
        // Transaction 2: draws next value and commits
        await using (var tx2 = await db.Database.BeginTransactionAsync())
        {
            val2 = await helper.GetNextSequenceValueAsync("document_number_seq");
            await tx2.CommitAsync();
        }

        // Under GMP requirements: sequence consumed in aborted transaction is NEVER reissued
        Assert.True(val2 > val1, $"Expected val2 ({val2}) to be strictly greater than val1 ({val1})");
    }

    [PostgresFact]
    public async Task UniqueCompanyCode_EnforcedAcrossActive_ReleasedAcrossVoid()
    {
        await using var db = _fixture.CreateDbContext();

        var companyCode = $"SOP-QC-{Guid.NewGuid():N}".Substring(0, 20);

        var doc1 = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-{Guid.NewGuid():N}".Substring(0, 15),
            CompanyDocumentCode = companyCode,
            Title = "First Active Document",
            DocumentTypeId = _fixture.SeededDocTypeId,
            DepartmentId = _fixture.SeededDepartmentId,
            SectionId = _fixture.SeededSectionId,
            DocumentOwnerUserId = _fixture.SeededUserId,
            Confidentiality = DocumentConfidentiality.Internal,
            RecordOrigin = RecordOrigin.Native,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAt = DateTime.UtcNow
        };
        db.DocumentMasters.Add(doc1);
        await db.SaveChangesAsync();

        // Attempt second active document with duplicate CompanyDocumentCode -> Must fail
        var doc2 = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-{Guid.NewGuid():N}".Substring(0, 15),
            CompanyDocumentCode = companyCode,
            Title = "Duplicate Active Document",
            DocumentTypeId = _fixture.SeededDocTypeId,
            DepartmentId = _fixture.SeededDepartmentId,
            SectionId = _fixture.SeededSectionId,
            DocumentOwnerUserId = _fixture.SeededUserId,
            Confidentiality = DocumentConfidentiality.Internal,
            RecordOrigin = RecordOrigin.Native,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAt = DateTime.UtcNow
        };
        db.DocumentMasters.Add(doc2);

        var dbEx = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await db.SaveChangesAsync();
        });
        Assert.IsType<PostgresException>(dbEx.InnerException);

        // Detach failed entity
        db.Entry(doc2).State = EntityState.Detached;

        // Void the first document
        doc1.RecordStatus = DocumentRecordStatus.Void;
        doc1.VoidedAt = DateTime.UtcNow;
        doc1.VoidedByUserId = _fixture.SeededUserId;
        doc1.VoidReason = "Superseded by new revision flow";
        await db.SaveChangesAsync();

        // Now inserting doc2 with the same company code must succeed
        var doc3 = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-{Guid.NewGuid():N}".Substring(0, 15),
            CompanyDocumentCode = companyCode,
            Title = "New Active Document Reusing Code",
            DocumentTypeId = _fixture.SeededDocTypeId,
            DepartmentId = _fixture.SeededDepartmentId,
            SectionId = _fixture.SeededSectionId,
            DocumentOwnerUserId = _fixture.SeededUserId,
            Confidentiality = DocumentConfidentiality.Internal,
            RecordOrigin = RecordOrigin.Native,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAt = DateTime.UtcNow
        };
        db.DocumentMasters.Add(doc3);
        await db.SaveChangesAsync();

        Assert.True(doc3.Id > 0);
    }

    [PostgresFact]
    public async Task Postgres_RegisterDocumentMaster_DrawsSequenceAndCreatesAuditedDraft()
    {
        await using var db = _fixture.CreateDbContext();
        var seqHelper = new DatabaseSequenceHelper(db);
        var auditService = new AuditEventService(db, seqHelper);
        var authService = new DocumentAuthorizationService(db);
        var masterService = new DocumentMasterService(db, seqHelper, auditService, authService);

        var request = new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-PG-{Guid.NewGuid():N}".Substring(0, 15),
            Title: "Postgres End-to-End Test SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: _fixture.SeededUserId,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Validation",
            Keywords: new List<string> { "Postgres", "Integration" },
            InitialRevisionNumber: "01"
        );

        var existingNum = await db.DocumentNumberingConfigurations.FirstOrDefaultAsync();
        if (existingNum != null && existingNum.Prefix != "DOC-")
        {
            existingNum.Prefix = "DOC-";
            existingNum.NumberFormat = "0000000";
            await db.SaveChangesAsync();
        }

        var result = await masterService.RegisterDocumentMasterAsync(request, _fixture.SeededUserId);

        Assert.NotNull(result);
        Assert.StartsWith("DOC-", result.MicroLimsDocumentId);
        Assert.Equal(DocumentRecordStatus.Active, result.RecordStatus);

        // Verify direct Postgres query on AuditLogs & AuditEventChanges
        var auditLog = await db.AuditLogs
            .Include(a => a.Changes)
            .FirstOrDefaultAsync(a => a.ActionCode == "DocumentMasterRegistered" && a.DocumentMasterId == result.Id);

        Assert.NotNull(auditLog);
        Assert.True(auditLog.Changes.Count > 0);
        Assert.Contains(auditLog.Changes, c => c.FieldName == "CompanyDocumentCode" && c.NewValue == request.CompanyDocumentCode);
    }

    [PostgresFact]
    public async Task Postgres_UploadAndVerifyFile_ComputesSha256AndDetectsIntegrityFailure()
    {
        await using var db = _fixture.CreateDbContext();
        var seqHelper = new DatabaseSequenceHelper(db);
        var auditService = new AuditEventService(db, seqHelper);
        var authService = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var masterService = new DocumentMasterService(db, seqHelper, auditService, authService);
        var fileService = new DocumentFileService(db, storage, auditService, authService);

        var registered = await masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-FILE-{Guid.NewGuid():N}".Substring(0, 15),
            Title: "File Integrity SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: _fixture.SeededUserId
        ), _fixture.SeededUserId);

        var revId = registered.Revisions[0].Id;
        var validContent = Encoding.UTF8.GetBytes("%PDF-1.7 Authentic Controlled Content");
        var expectedHash = Convert.ToHexString(SHA256.HashData(validContent));

        var uploaded = await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.ControlledPdf,
            "sop_authentic.pdf",
            "application/pdf",
            validContent,
            _fixture.SeededUserId);

        Assert.Equal(expectedHash, uploaded.ContentSha256);

        // Successful retrieval
        var (meta, content) = await fileService.GetFileContentAsync(uploaded.Id, _fixture.SeededUserId);
        Assert.Equal(validContent.Length, content.Length);

        // Tamper storage
        var fileInDb = await db.RevisionFiles.FindAsync(uploaded.Id);
        storage.Files[fileInDb!.StorageKey] = Encoding.UTF8.GetBytes("%PDF-1.7 TAMPERED BIT FLIP");

        // Retrieval must detect integrity failure and record security audit log in Postgres
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await fileService.GetFileContentAsync(uploaded.Id, _fixture.SeededUserId);
        });
        Assert.Contains("integrity check failed", ex.Message, StringComparison.OrdinalIgnoreCase);

        var securityAudit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "FileIntegrityVerificationFailed" && a.EntityId == uploaded.Id.ToString());
        Assert.NotNull(securityAudit);
        Assert.Equal(AuditActionCategory.Security, securityAudit.ActionCategory);
    }

    [PostgresFact]
    public async Task Postgres_VoidDocumentMaster_EnforcesControllerRoleAndUpdatesAudit()
    {
        await using var db = _fixture.CreateDbContext();
        var seqHelper = new DatabaseSequenceHelper(db);
        var auditService = new AuditEventService(db, seqHelper);
        var authService = new DocumentAuthorizationService(db);
        var masterService = new DocumentMasterService(db, seqHelper, auditService, authService);

        var registered = await masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-VOID-{Guid.NewGuid():N}".Substring(0, 15),
            Title: "Doc To Void",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: _fixture.SeededUserId
        ), _fixture.SeededUserId);

        // Attempt voiding as regular user -> Must fail with 403
        var voidRequest = new VoidDocumentMasterRequest("Document registered in error by project team.");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await masterService.VoidMasterAsync(registered.Id, voidRequest, _fixture.SeededUserId);
        });

        // Void as Document Controller -> Must succeed
        var voided = await masterService.VoidMasterAsync(registered.Id, voidRequest, _fixture.SeededControllerUserId);
        Assert.Equal(DocumentRecordStatus.Void, voided.RecordStatus);
        Assert.Equal(voidRequest.Reason, voided.VoidReason);

        var voidAudit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "DocumentMasterVoided" && a.DocumentMasterId == registered.Id);
        Assert.NotNull(voidAudit);
        Assert.Equal(_fixture.SeededControllerUserId, voidAudit.UserId);
    }

    private async Task<User> CreateTestUserAsync(MicroLimsDbContext db, string username, RoleType roleType)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Type == roleType);
        if (role == null)
        {
            role = new Role
            {
                Type = roleType,
                Name = roleType.ToString(),
                IsSystemRole = false,
                IsActive = true
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        var user = new User
        {
            FullName = $"Test User {username}",
            Username = username,
            PasswordHash = "hashed_pw",
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [PostgresFact]
    public async Task Postgres_DraftMetadataUpdate_RecordsSemanticFieldChangesAndRejectsUnauthorizedUser()
    {
        await using var db = _fixture.CreateDbContext();
        var seqHelper = new DatabaseSequenceHelper(db);
        var auditService = new AuditEventService(db, seqHelper);
        var authService = new DocumentAuthorizationService(db);
        var masterService = new DocumentMasterService(db, seqHelper, auditService, authService);

        var docOwner = await CreateTestUserAsync(db, $"owner_{Guid.NewGuid():N}".Substring(0, 15), RoleType.Analyst);
        var unauthorizedUser = await CreateTestUserAsync(db, $"unauth_{Guid.NewGuid():N}".Substring(0, 15), RoleType.Analyst);

        var registered = await masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-META-{Guid.NewGuid():N}".Substring(0, 15),
            Title: "Original Meta Title",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: docOwner.Id
        ), docOwner.Id);

        var updateReq = new UpdateDocumentMasterDraftRequest(
            CompanyDocumentCode: registered.CompanyDocumentCode,
            Title: "Updated Meta Title by Owner",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: docOwner.Id,
            Confidentiality: DocumentConfidentiality.Restricted,
            Category: "Environmental Monitoring",
            Keywords: new List<string> { "Sterility", "Autoclave" }
        );

        // Unauthorized user attempt -> Throws
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await masterService.UpdateDraftMetadataAsync(registered.Id, updateReq, unauthorizedUser.Id);
        });

        // Authorized owner update -> Succeeds
        var updated = await masterService.UpdateDraftMetadataAsync(registered.Id, updateReq, docOwner.Id);
        Assert.Equal("Updated Meta Title by Owner", updated.Title);
        Assert.Equal(DocumentConfidentiality.Restricted, updated.Confidentiality);
        Assert.Equal("Environmental Monitoring", updated.Category);
        Assert.Equal(2, updated.Keywords.Count);

        // Audit Trail verification in Postgres
        var auditLog = await db.AuditLogs
            .Include(a => a.Changes)
            .FirstOrDefaultAsync(a => a.ActionCode == "DocumentMasterMetadataUpdated" && a.DocumentMasterId == registered.Id);
        Assert.NotNull(auditLog);
        Assert.Contains(auditLog.Changes, c => c.FieldName == "Title" && c.PreviousValue == "Original Meta Title" && c.NewValue == "Updated Meta Title by Owner");
        Assert.Contains(auditLog.Changes, c => c.FieldName == "Confidentiality" && c.NewValue == "Restricted");
    }

    [PostgresFact]
    public async Task Postgres_WorkflowRoleAssignments_GrantsAuthorPermissionsAndRecordsAudit()
    {
        await using var db = _fixture.CreateDbContext();
        var seqHelper = new DatabaseSequenceHelper(db);
        var auditService = new AuditEventService(db, seqHelper);
        var authService = new DocumentAuthorizationService(db);
        var masterService = new DocumentMasterService(db, seqHelper, auditService, authService);

        var owner = await CreateTestUserAsync(db, $"own_{Guid.NewGuid():N}".Substring(0, 15), RoleType.Analyst);
        var authorCandidate = await CreateTestUserAsync(db, $"auth_{Guid.NewGuid():N}".Substring(0, 15), RoleType.Analyst);

        var registered = await masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-ASSIGN-{Guid.NewGuid():N}".Substring(0, 15),
            Title: "Assignment Test SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: owner.Id
        ), owner.Id);

        var updateReq = new UpdateDocumentMasterDraftRequest(
            CompanyDocumentCode: registered.CompanyDocumentCode,
            Title: "Title Modified by Assigned Author",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: owner.Id,
            Confidentiality: DocumentConfidentiality.Internal
        );

        // Before assignment: authorCandidate cannot edit -> UnauthorizedAccessException
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await masterService.UpdateDraftMetadataAsync(registered.Id, updateReq, authorCandidate.Id);
        });

        // Owner assigns Author role to authorCandidate
        var assignment = await masterService.AddOrUpdateAssignmentAsync(
            registered.Id,
            new CreateAssignmentRequest(authorCandidate.Id, AssignmentRole.Author),
            owner.Id);

        Assert.Equal(AssignmentRole.Author, assignment.AssignmentRole);
        Assert.Equal(authorCandidate.Id, assignment.UserId);

        // Now authorCandidate CAN edit
        var updated = await masterService.UpdateDraftMetadataAsync(registered.Id, updateReq, authorCandidate.Id);
        Assert.Equal("Title Modified by Assigned Author", updated.Title);

        // Remove assignment
        await masterService.RemoveAssignmentAsync(registered.Id, assignment.Id, owner.Id);

        // After removal: authorCandidate cannot edit again
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await masterService.UpdateDraftMetadataAsync(registered.Id, updateReq, authorCandidate.Id);
        });

        // Audit Trail verification in Postgres
        var assignLog = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "WorkflowAssignmentAdded" && a.DocumentMasterId == registered.Id);
        Assert.NotNull(assignLog);

        var removeLog = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "WorkflowAssignmentRemoved" && a.DocumentMasterId == registered.Id);
        Assert.NotNull(removeLog);
    }

    [PostgresFact]
    public async Task Postgres_DraftFileReplacement_DeactivatesPreviousFileAndPreservesHistoricalLink()
    {
        await using var db = _fixture.CreateDbContext();
        var seqHelper = new DatabaseSequenceHelper(db);
        var auditService = new AuditEventService(db, seqHelper);
        var authService = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var masterService = new DocumentMasterService(db, seqHelper, auditService, authService);
        var fileService = new DocumentFileService(db, storage, auditService, authService);

        var registered = await masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-REP-{Guid.NewGuid():N}".Substring(0, 15),
            Title: "File Replacement SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: _fixture.SeededUserId
        ), _fixture.SeededUserId);

        var revId = registered.Revisions[0].Id;
        var fileBytes1 = Encoding.UTF8.GetBytes("%PDF-1.7 Original Draft File Content v1");
        var fileBytes2 = Encoding.UTF8.GetBytes("%PDF-1.7 Replaced Draft File Content v2");

        var file1 = await fileService.UploadRevisionFileAsync(
            revId, FileRole.ControlledPdf, "rev1_v1.pdf", "application/pdf", fileBytes1, _fixture.SeededUserId);
        Assert.True(file1.IsActive);
        Assert.Null(file1.SupersededByFileId);

        var file2 = await fileService.UploadRevisionFileAsync(
            revId, FileRole.ControlledPdf, "rev1_v2.pdf", "application/pdf", fileBytes2, _fixture.SeededUserId);
        Assert.True(file2.IsActive);
        Assert.Null(file2.SupersededByFileId);

        // Verify file1 is deactivated in DB
        var reloadedFile1 = await db.RevisionFiles.FindAsync(file1.Id);
        Assert.NotNull(reloadedFile1);
        Assert.False(reloadedFile1.IsActive);
        Assert.Equal(file2.Id, reloadedFile1.SupersededByFileId);

        // Historical retrieval of superseded file1 is still bit-exact
        var (meta1, content1) = await fileService.GetFileContentAsync(file1.Id, _fixture.SeededUserId);
        Assert.Equal(fileBytes1.Length, content1.Length);

        // Audit log in Postgres
        var replaceAudit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "RevisionFileReplaced" && a.DocumentRevisionId == revId);
        Assert.NotNull(replaceAudit);
        Assert.Equal(file2.Id.ToString(), replaceAudit.EntityId);
    }

    [PostgresFact]
    public async Task Postgres_CancelDraftRevision_EnforcesReasonLengthAndDeactivatesFiles()
    {
        await using var db = _fixture.CreateDbContext();
        var seqHelper = new DatabaseSequenceHelper(db);
        var auditService = new AuditEventService(db, seqHelper);
        var authService = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var masterService = new DocumentMasterService(db, seqHelper, auditService, authService);
        var fileService = new DocumentFileService(db, storage, auditService, authService);

        var registered = await masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-CNC-{Guid.NewGuid():N}".Substring(0, 15),
            Title: "Cancel Draft SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: _fixture.SeededUserId
        ), _fixture.SeededUserId);

        var revId = registered.Revisions[0].Id;
        var fileBytes = Encoding.UTF8.GetBytes("%PDF-1.7 Content to be cancelled");
        var uploaded = await fileService.UploadRevisionFileAsync(
            revId, FileRole.ControlledPdf, "to_cancel.pdf", "application/pdf", fileBytes, _fixture.SeededUserId);

        // Short reason (< 10 chars) -> Throws ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await masterService.CancelDraftRevisionAsync(revId, new CancelDraftRevisionRequest("Short"), _fixture.SeededUserId);
        });

        // Valid reason (>= 10 chars) -> Cancels
        var cancelReason = "Project scope changed; procedure no longer required.";
        var cancelled = await masterService.CancelDraftRevisionAsync(revId, new CancelDraftRevisionRequest(cancelReason), _fixture.SeededUserId);

        Assert.Equal(DocumentRevisionStatus.Cancelled, cancelled.RevisionStatus);
        Assert.Equal(cancelReason, cancelled.CancelReason);
        Assert.NotNull(cancelled.CancelledAt);

        // Verify attached files are deactivated
        var fileInDb = await db.RevisionFiles.FindAsync(uploaded.Id);
        Assert.False(fileInDb!.IsActive);

        // Attempt upload to cancelled revision -> Throws UnauthorizedAccessException
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await fileService.UploadRevisionFileAsync(
                revId, FileRole.ControlledPdf, "another.pdf", "application/pdf", fileBytes, _fixture.SeededUserId);
        });

        // Audit Trail verification in Postgres
        var cancelAudit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "DraftRevisionCancelled" && a.DocumentRevisionId == revId);
        Assert.NotNull(cancelAudit);
        Assert.Equal(cancelReason, cancelAudit.Reason);
    }

    [PostgresFact]
    public async Task Postgres_ConfigurationService_EnforcesSoftDeactivationAndAuditsSettings()
    {
        await using var db = _fixture.CreateDbContext();
        var seqHelper = new DatabaseSequenceHelper(db);
        var auditService = new AuditEventService(db, seqHelper);
        var authService = new DocumentAuthorizationService(db);
        var configService = new DocumentConfigurationService(db, auditService, authService);

        // Create Type
        var typeCode = $"VAL_{Guid.NewGuid():N}".Substring(0, 6).ToUpper();
        var createdType = await configService.CreateDocumentTypeAsync(
            new CreateDocumentTypeRequest(typeCode, "Validation Protocol", 36), _fixture.SeededUserId);
        Assert.True(createdType.IsActive);

        // Soft deactivate Type
        var updatedType = await configService.UpdateDocumentTypeAsync(
            createdType.Id,
            new UpdateDocumentTypeRequest("Validation Protocol", 36, false),
            _fixture.SeededUserId);
        Assert.False(updatedType.IsActive);

        // Active list excludes inactive type
        var activeTypes = await configService.GetDocumentTypesAsync(includeInactive: false);
        Assert.DoesNotContain(activeTypes, t => t.Id == createdType.Id);

        // All list includes inactive type
        var allTypes = await configService.GetDocumentTypesAsync(includeInactive: true);
        Assert.Contains(allTypes, t => t.Id == createdType.Id);

        // Ensure baseline prefix is DOC-
        var existingNum = await db.DocumentNumberingConfigurations.FirstOrDefaultAsync();
        if (existingNum != null && existingNum.Prefix != "DOC-")
        {
            existingNum.Prefix = "DOC-";
            existingNum.NumberFormat = "0000000";
            await db.SaveChangesAsync();
        }

        try
        {
            // Update Numbering Configuration
            var updatedNum = await configService.UpdateNumberingConfigAsync(
                new UpdateDocumentNumberingConfigRequest("QAD-", "00000", true), _fixture.SeededUserId);
            Assert.Equal("QAD-", updatedNum.Prefix);
            Assert.Equal("00000", updatedNum.NumberFormat);
            Assert.StartsWith("QAD-", updatedNum.SampleNextId);

            var numAudit = await db.AuditLogs
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(a => a.ActionCode == "DocumentNumberingConfigUpdated");
            Assert.NotNull(numAudit);

            // Update System Setting
            var setting = await configService.UpdateConfigurationSettingAsync(
                "DocumentControl.ControlledCopy.DownloadPermitted",
                new UpdateConfigurationSettingRequest("false"),
                _fixture.SeededUserId);
            Assert.Equal("false", setting.SettingValue);

            var settingAudit = await db.AuditLogs
                .Include(a => a.Changes)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(a => a.ActionCode == "ConfigurationSettingUpdated");
            Assert.NotNull(settingAudit);
        }
        finally
        {
            // Revert numbering config to avoid affecting other tests
            await configService.UpdateNumberingConfigAsync(
                new UpdateDocumentNumberingConfigRequest("DOC-", "0000000", true), _fixture.SeededUserId);
        }
    }

    [PostgresFact]
    public async Task Postgres_DocumentLibrary_FiltersByStatusAndExcludesVoidByDefault()
    {
        await using var db = _fixture.CreateDbContext();
        var seqHelper = new DatabaseSequenceHelper(db);
        var auditService = new AuditEventService(db, seqHelper);
        var authService = new DocumentAuthorizationService(db);
        var masterService = new DocumentMasterService(db, seqHelper, auditService, authService);

        var uniquePrefix = $"LIB_{Guid.NewGuid():N}".Substring(0, 8);
        var doc1 = await masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"{uniquePrefix}-DOC-1",
            Title: "Active Library Doc 1",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: _fixture.SeededUserId
        ), _fixture.SeededUserId);

        var doc2 = await masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"{uniquePrefix}-DOC-2",
            Title: "Voided Library Doc 2",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: _fixture.SeededUserId
        ), _fixture.SeededUserId);

        // Void doc2
        await masterService.VoidMasterAsync(doc2.Id, new VoidDocumentMasterRequest("Voided for library filter testing."), _fixture.SeededControllerUserId);

        // Query default: Voided is excluded
        var resDefault = await masterService.GetLibraryAsync(new DocumentLibraryFilterRequest
        {
            SearchTerm = uniquePrefix,
            IncludeCancelledAndVoided = false
        }, _fixture.SeededUserId);
        Assert.Single(resDefault.Items);
        Assert.Equal(doc1.Id, resDefault.Items[0].Id);

        // Query with IncludeCancelledAndVoided = true: Returns both
        var resAll = await masterService.GetLibraryAsync(new DocumentLibraryFilterRequest
        {
            SearchTerm = uniquePrefix,
            IncludeCancelledAndVoided = true
        }, _fixture.SeededUserId);
        Assert.Equal(2, resAll.Items.Count);
        Assert.Contains(resAll.Items, d => d.Id == doc1.Id && d.RecordStatus == DocumentRecordStatus.Active);
        Assert.Contains(resAll.Items, d => d.Id == doc2.Id && d.RecordStatus == DocumentRecordStatus.Void);
    }

    [PostgresFact]
    public async Task Postgres_DocumentAuditService_FiltersLogsAndEmitsSelfAuditedCsvExport()
    {
        await using var db = _fixture.CreateDbContext();
        var seqHelper = new DatabaseSequenceHelper(db);
        var auditService = new AuditEventService(db, seqHelper);
        var authService = new DocumentAuthorizationService(db);
        var masterService = new DocumentMasterService(db, seqHelper, auditService, authService);
        var docAuditService = new DocumentAuditService(db, auditService, authService);

        var registered = await masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-AUD-{Guid.NewGuid():N}".Substring(0, 15),
            Title: "Audit Query Test SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: _fixture.SeededUserId
        ), _fixture.SeededUserId);

        // Query record audit
        var recordAudit = await docAuditService.GetRecordAuditHistoryAsync(registered.Id, null, _fixture.SeededUserId);
        Assert.NotEmpty(recordAudit);
        Assert.Contains(recordAudit, a => a.ActionCode == "DocumentMasterRegistered");

        // Export CSV
        var (csvBytes, contentType, fileName) = await docAuditService.ExportAuditTrailAsync(new DocumentAuditFilterRequest
        {
            DocumentMasterId = registered.Id
        }, _fixture.SeededUserId);

        Assert.NotEmpty(csvBytes);
        var csvText = Encoding.UTF8.GetString(csvBytes);
        Assert.Contains("EventUid,TimestampUTC,ActorType", csvText);
        Assert.Contains("DocumentMasterRegistered", csvText);

        // Verify 21 CFR Part 11 Self-Audit Log: AuditTrailExported was written to Postgres
        var exportLog = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "AuditTrailExported" && a.UserId == _fixture.SeededUserId);
        Assert.NotNull(exportLog);
        Assert.Equal(AuditActionCategory.Security, exportLog.ActionCategory);
    }
}

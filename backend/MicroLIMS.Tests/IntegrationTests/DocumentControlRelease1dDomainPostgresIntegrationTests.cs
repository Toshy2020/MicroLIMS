using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentControlRelease1dDomainPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentControlRelease1dDomainPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(int MasterId, int RevisionId)> SeedDocumentAsync(MicroLimsDbContext db)
    {
        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-R1D-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            CompanyDocumentCode = $"SOP-R1D-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            Title = "Release 1d Lineage Test SOP",
            DocumentTypeId = _fixture.SeededDocTypeId,
            DepartmentId = _fixture.SeededDepartmentId,
            SectionId = _fixture.SeededSectionId,
            DocumentOwnerUserId = _fixture.SeededUserId,
            CreatedByUserId = _fixture.SeededUserId,
            RecordStatus = DocumentRecordStatus.Active
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var rev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Draft,
            CreatedByUserId = _fixture.SeededUserId
        };
        db.DocumentRevisions.Add(rev);
        await db.SaveChangesAsync();

        return (master.Id, rev.Id);
    }

    private RevisionFile CreateFile(int revisionId, FileRole role, int version, bool isApprovedFinal = false, int? generatedFromId = null)
    {
        var ext = role == FileRole.SourceFile ? "docx" : "pdf";
        var mime = role == FileRole.SourceFile
            ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            : "application/pdf";

        return new RevisionFile
        {
            DocumentRevisionId = revisionId,
            FileRole = role,
            FileName = $"SOP-R1D_v{version}.{ext}",
            ContentType = mime,
            SizeBytes = 2048,
            ContentSha256 = $"sha256-{Guid.NewGuid():N}",
            StorageKey = $"documents/{revisionId}/{Guid.NewGuid():N}_{role}.{ext}",
            FileVersion = version,
            IsApprovedFinalSource = isApprovedFinal,
            GeneratedFromSourceFileId = generatedFromId,
            IsActive = true,
            UploadedByUserId = _fixture.SeededUserId,
            UploadedAt = DateTime.UtcNow
        };
    }

    [PostgresFact]
    public async Task Postgres_PersistsExplicitFileVersions_AndBlocksDuplicates()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, revisionId) = await SeedDocumentAsync(db);

        var fileV1 = CreateFile(revisionId, FileRole.SourceFile, version: 1);
        fileV1.IsActive = false;
        db.RevisionFiles.Add(fileV1);
        await db.SaveChangesAsync();

        var fileV2 = CreateFile(revisionId, FileRole.SourceFile, version: 2);
        db.RevisionFiles.Add(fileV2);
        await db.SaveChangesAsync();

        fileV1.SupersededByFileId = fileV2.Id;
        await db.SaveChangesAsync();

        Assert.True(fileV1.Id > 0);
        Assert.True(fileV2.Id > 0);
        Assert.Equal(1, fileV1.FileVersion);
        Assert.Equal(2, fileV2.FileVersion);

        // Attempt duplicate (revisionId, SourceFile, version 2)
        var duplicateVersion = CreateFile(revisionId, FileRole.SourceFile, version: 2);
        db.RevisionFiles.Add(duplicateVersion);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    [PostgresFact]
    public async Task Postgres_FilteredUniqueIndex_AllowsOnlyOneApprovedFinalSourcePerRevision()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, revisionId) = await SeedDocumentAsync(db);

        var fileV1 = CreateFile(revisionId, FileRole.SourceFile, version: 1, isApprovedFinal: true);
        db.RevisionFiles.Add(fileV1);
        await db.SaveChangesAsync();

        // Attempting to mark a second file as IsApprovedFinalSource = true on the same revision must fail
        var fileV2 = CreateFile(revisionId, FileRole.SourceFile, version: 2, isApprovedFinal: true);
        db.RevisionFiles.Add(fileV2);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    [PostgresFact]
    public async Task Postgres_CheckConstraint_ApprovedFinalSourceRole_RejectsControlledPdf()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, revisionId) = await SeedDocumentAsync(db);

        // IsApprovedFinalSource can ONLY be true for FileRole.SourceFile (2), never ControlledPdf (1)
        var invalidPdf = CreateFile(revisionId, FileRole.ControlledPdf, version: 1, isApprovedFinal: true);
        db.RevisionFiles.Add(invalidPdf);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
        Assert.Contains("CK_RevisionFiles_ApprovedFinalSourceRole", ex.InnerException.Message);
    }

    [PostgresFact]
    public async Task Postgres_CheckConstraint_GeneratedFromSourceRole_RejectsSourceFile()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, revisionId) = await SeedDocumentAsync(db);

        var sourceV1 = CreateFile(revisionId, FileRole.SourceFile, version: 1);
        db.RevisionFiles.Add(sourceV1);
        await db.SaveChangesAsync();

        // GeneratedFromSourceFileId can ONLY be populated for ControlledPdf (1), never SourceFile (2)
        var invalidSource = CreateFile(revisionId, FileRole.SourceFile, version: 2, generatedFromId: sourceV1.Id);
        db.RevisionFiles.Add(invalidSource);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
        Assert.Contains("CK_RevisionFiles_GeneratedFromSourceRole", ex.InnerException.Message);
    }

    [PostgresFact]
    public async Task Postgres_CheckConstraint_NotSelfGenerated_BlocksSelfReference()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, revisionId) = await SeedDocumentAsync(db);

        var pdf = CreateFile(revisionId, FileRole.ControlledPdf, version: 1);
        db.RevisionFiles.Add(pdf);
        await db.SaveChangesAsync();

        // Attempting to set GeneratedFromSourceFileId == Id violates CK_RevisionFiles_NotSelfGenerated
        pdf.GeneratedFromSourceFileId = pdf.Id;

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
        Assert.Contains("CK_RevisionFiles_NotSelfGenerated", ex.InnerException.Message);
    }

    [PostgresFact]
    public async Task Postgres_ControlledPdf_SuccessfullyLinksToExactWordSourceProvenance()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, revisionId) = await SeedDocumentAsync(db);

        var wordSource = CreateFile(revisionId, FileRole.SourceFile, version: 1, isApprovedFinal: true);
        db.RevisionFiles.Add(wordSource);
        await db.SaveChangesAsync();

        var controlledPdf = CreateFile(revisionId, FileRole.ControlledPdf, version: 1, generatedFromId: wordSource.Id);
        db.RevisionFiles.Add(controlledPdf);
        await db.SaveChangesAsync();

        Assert.True(controlledPdf.Id > 0);

        var retrieved = await db.RevisionFiles
            .Include(f => f.GeneratedFromSourceFile)
            .FirstOrDefaultAsync(f => f.Id == controlledPdf.Id);

        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.GeneratedFromSourceFile);
        Assert.Equal(wordSource.Id, retrieved.GeneratedFromSourceFileId);
        Assert.Equal(wordSource.FileName, retrieved.GeneratedFromSourceFile.FileName);
        Assert.True(retrieved.GeneratedFromSourceFile.IsApprovedFinalSource);
    }

    [PostgresFact]
    public async Task Postgres_DocumentRevision_LinksApprovedSourceAndControlledPdf()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, revisionId) = await SeedDocumentAsync(db);

        var wordSource = CreateFile(revisionId, FileRole.SourceFile, version: 1, isApprovedFinal: true);
        db.RevisionFiles.Add(wordSource);
        await db.SaveChangesAsync();

        var controlledPdf = CreateFile(revisionId, FileRole.ControlledPdf, version: 1, generatedFromId: wordSource.Id);
        db.RevisionFiles.Add(controlledPdf);
        await db.SaveChangesAsync();

        var revision = await db.DocumentRevisions.FindAsync(revisionId);
        Assert.NotNull(revision);

        revision.ApprovedSourceFileId = wordSource.Id;
        revision.ControlledPdfFileId = controlledPdf.Id;
        await db.SaveChangesAsync();

        var retrieved = await db.DocumentRevisions
            .Include(r => r.ApprovedSourceFile)
            .Include(r => r.ControlledPdfFile)
            .FirstOrDefaultAsync(r => r.Id == revisionId);

        Assert.NotNull(retrieved);
        Assert.Equal(wordSource.Id, retrieved.ApprovedSourceFileId);
        Assert.Equal(controlledPdf.Id, retrieved.ControlledPdfFileId);
        Assert.Equal("docx", retrieved.ApprovedSourceFile!.FileName.Split('.').Last());
        Assert.Equal("pdf", retrieved.ControlledPdfFile!.FileName.Split('.').Last());
    }

    [PostgresFact]
    public async Task Postgres_DocumentApprovalTask_LinksApprovedSourceAndControlledPdf()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, revisionId) = await SeedDocumentAsync(db);

        var wordSource = CreateFile(revisionId, FileRole.SourceFile, version: 1, isApprovedFinal: true);
        db.RevisionFiles.Add(wordSource);
        await db.SaveChangesAsync();

        var controlledPdf = CreateFile(revisionId, FileRole.ControlledPdf, version: 1, generatedFromId: wordSource.Id);
        db.RevisionFiles.Add(controlledPdf);
        await db.SaveChangesAsync();

        var task = new DocumentApprovalTask
        {
            DocumentRevisionId = revisionId,
            AssignedApproverUserId = _fixture.SeededControllerUserId,
            AssignedByUserId = _fixture.SeededUserId,
            Status = DocumentApprovalTaskStatus.Approved,
            Decision = DocumentApprovalDecision.Approve,
            DecisionAt = DateTime.UtcNow,
            ApprovedSourceFileId = wordSource.Id,
            GeneratedControlledPdfId = controlledPdf.Id
        };
        db.DocumentApprovalTasks.Add(task);
        await db.SaveChangesAsync();

        var retrieved = await db.DocumentApprovalTasks
            .Include(t => t.ApprovedSourceFile)
            .Include(t => t.GeneratedControlledPdf)
            .FirstOrDefaultAsync(t => t.Id == task.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(wordSource.Id, retrieved.ApprovedSourceFileId);
        Assert.Equal(controlledPdf.Id, retrieved.GeneratedControlledPdfId);
    }

    [PostgresFact]
    public async Task Postgres_DocumentReviewFinding_LinksToSpecificSourceFileVersion()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, revisionId) = await SeedDocumentAsync(db);

        var wordSourceV1 = CreateFile(revisionId, FileRole.SourceFile, version: 1);
        db.RevisionFiles.Add(wordSourceV1);
        await db.SaveChangesAsync();

        var reviewTask = new DocumentReviewTask
        {
            DocumentRevisionId = revisionId,
            AssignedReviewerUserId = _fixture.SeededControllerUserId,
            AssignedByUserId = _fixture.SeededUserId,
            ReviewCycleNumber = 1,
            ReviewedSourceFileId = wordSourceV1.Id,
            Status = ReviewTaskStatus.Pending
        };
        db.DocumentReviewTasks.Add(reviewTask);
        await db.SaveChangesAsync();

        var finding = new DocumentReviewFinding
        {
            DocumentReviewTaskId = reviewTask.Id,
            CreatedByUserId = _fixture.SeededControllerUserId,
            CommentText = "Ambiguity in Section 3.1",
            SectionNumber = "3.1",
            PageNumber = 2,
            IsMandatory = true,
            Status = ReviewFindingStatus.Open,
            RevisionFileId = wordSourceV1.Id,
            SourceFileVersion = wordSourceV1.FileVersion
        };
        db.DocumentReviewFindings.Add(finding);
        await db.SaveChangesAsync();

        var retrieved = await db.DocumentReviewFindings
            .Include(f => f.RevisionFile)
            .FirstOrDefaultAsync(f => f.Id == finding.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(wordSourceV1.Id, retrieved.RevisionFileId);
        Assert.Equal(1, retrieved.SourceFileVersion);
        Assert.Equal(wordSourceV1.FileName, retrieved.RevisionFile!.FileName);
    }

    [PostgresFact]
    public async Task Postgres_DocumentReviewTask_SupportsMultiCycleAndReviewedSource()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, revisionId) = await SeedDocumentAsync(db);

        var wordSourceV1 = CreateFile(revisionId, FileRole.SourceFile, version: 1);
        wordSourceV1.IsActive = false;
        db.RevisionFiles.Add(wordSourceV1);
        await db.SaveChangesAsync();

        var wordSourceV2 = CreateFile(revisionId, FileRole.SourceFile, version: 2);
        db.RevisionFiles.Add(wordSourceV2);
        await db.SaveChangesAsync();

        wordSourceV1.SupersededByFileId = wordSourceV2.Id;
        await db.SaveChangesAsync();

        var cycle1 = new DocumentReviewTask
        {
            DocumentRevisionId = revisionId,
            AssignedReviewerUserId = _fixture.SeededControllerUserId,
            AssignedByUserId = _fixture.SeededUserId,
            ReviewCycleNumber = 1,
            ReviewedSourceFileId = wordSourceV1.Id,
            Status = ReviewTaskStatus.ReturnedForCorrection,
            Decision = ReviewDecision.ReturnForCorrection,
            DecisionAt = DateTime.UtcNow
        };
        db.DocumentReviewTasks.Add(cycle1);
        await db.SaveChangesAsync();

        var cycle2 = new DocumentReviewTask
        {
            DocumentRevisionId = revisionId,
            AssignedReviewerUserId = _fixture.SeededControllerUserId,
            AssignedByUserId = _fixture.SeededUserId,
            ReviewCycleNumber = 2,
            ReviewedSourceFileId = wordSourceV2.Id,
            Status = ReviewTaskStatus.Pending
        };
        db.DocumentReviewTasks.Add(cycle2);
        await db.SaveChangesAsync();

        var cycles = await db.DocumentReviewTasks
            .Where(t => t.DocumentRevisionId == revisionId)
            .OrderBy(t => t.ReviewCycleNumber)
            .ToListAsync();

        Assert.Equal(2, cycles.Count);
        Assert.Equal(1, cycles[0].ReviewCycleNumber);
        Assert.Equal(wordSourceV1.Id, cycles[0].ReviewedSourceFileId);
        Assert.Equal(2, cycles[1].ReviewCycleNumber);
        Assert.Equal(wordSourceV2.Id, cycles[1].ReviewedSourceFileId);
    }

    [PostgresFact]
    public async Task Postgres_DeleteBehaviorRestrict_PreventsDeletionOfReferencedFile()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, revisionId) = await SeedDocumentAsync(db);

        var wordSource = CreateFile(revisionId, FileRole.SourceFile, version: 1, isApprovedFinal: true);
        db.RevisionFiles.Add(wordSource);
        await db.SaveChangesAsync();

        var controlledPdf = CreateFile(revisionId, FileRole.ControlledPdf, version: 1, generatedFromId: wordSource.Id);
        db.RevisionFiles.Add(controlledPdf);
        await db.SaveChangesAsync();

        // In an isolated context, attempting to delete wordSource when controlledPdf references it via GeneratedFromSourceFileId
        // triggers PostgreSQL foreign key constraint violation (ON DELETE RESTRICT)
        await using var deleteDb = _fixture.CreateDbContext();
        var toDelete = await deleteDb.RevisionFiles.FindAsync(wordSource.Id);
        Assert.NotNull(toDelete);
        deleteDb.RevisionFiles.Remove(toDelete);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => deleteDb.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }
}

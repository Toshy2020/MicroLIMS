using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlRelease1dDomainUnitTests
{
    [Fact]
    public void RevisionFile_ConstructsWithRelease1dDefaults()
    {
        var file = new RevisionFile
        {
            DocumentRevisionId = 10,
            FileName = "SOP-QC-001.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            SizeBytes = 1024,
            ContentSha256 = "abc123def456",
            StorageKey = "documents/10/1_sourcefile.docx",
            UploadedByUserId = 1
        };

        Assert.Equal(1, file.FileVersion);
        Assert.False(file.IsApprovedFinalSource);
        Assert.Null(file.GeneratedFromSourceFileId);
        Assert.Null(file.GeneratedFromSourceFile);
        Assert.True(file.IsActive);
    }

    [Fact]
    public void RevisionFile_SupportsSequentialLineageAndSuperseding()
    {
        var sourceV1 = new RevisionFile
        {
            Id = 1,
            DocumentRevisionId = 10,
            FileRole = FileRole.SourceFile,
            FileName = "SOP-QC-001_v1.docx",
            FileVersion = 1,
            IsActive = false,
            SupersededByFileId = 2
        };

        var sourceV2 = new RevisionFile
        {
            Id = 2,
            DocumentRevisionId = 10,
            FileRole = FileRole.SourceFile,
            FileName = "SOP-QC-001_v2.docx",
            FileVersion = 2,
            IsActive = true
        };

        sourceV1.SupersededByFile = sourceV2;

        Assert.Equal(1, sourceV1.FileVersion);
        Assert.Equal(2, sourceV2.FileVersion);
        Assert.False(sourceV1.IsActive);
        Assert.True(sourceV2.IsActive);
        Assert.Equal(2, sourceV1.SupersededByFileId);
        Assert.Equal(sourceV2, sourceV1.SupersededByFile);
    }

    [Fact]
    public void RevisionFile_ControlledPdf_ReferencesSourceFileProvenance()
    {
        var approvedWordSource = new RevisionFile
        {
            Id = 101,
            DocumentRevisionId = 25,
            FileRole = FileRole.SourceFile,
            FileName = "SOP-QC-005_Approved.docx",
            FileVersion = 2,
            IsApprovedFinalSource = true,
            IsActive = true
        };

        var generatedPdf = new RevisionFile
        {
            Id = 102,
            DocumentRevisionId = 25,
            FileRole = FileRole.ControlledPdf,
            FileName = "SOP-QC-005_Controlled.pdf",
            FileVersion = 1,
            GeneratedFromSourceFileId = approvedWordSource.Id,
            GeneratedFromSourceFile = approvedWordSource,
            IsActive = true
        };

        Assert.Equal(approvedWordSource.Id, generatedPdf.GeneratedFromSourceFileId);
        Assert.Equal(approvedWordSource, generatedPdf.GeneratedFromSourceFile);
        Assert.True(approvedWordSource.IsApprovedFinalSource);
        Assert.Equal(FileRole.ControlledPdf, generatedPdf.FileRole);
        Assert.Equal(FileRole.SourceFile, generatedPdf.GeneratedFromSourceFile.FileRole);
    }

    [Fact]
    public void DocumentRevision_SupportsApprovedSourceAndControlledPdfPointers()
    {
        var revision = new DocumentRevision
        {
            Id = 50,
            DocumentMasterId = 5,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective
        };

        var approvedWord = new RevisionFile
        {
            Id = 501,
            DocumentRevisionId = revision.Id,
            FileRole = FileRole.SourceFile,
            FileVersion = 2,
            IsApprovedFinalSource = true
        };

        var controlledPdf = new RevisionFile
        {
            Id = 502,
            DocumentRevisionId = revision.Id,
            FileRole = FileRole.ControlledPdf,
            FileVersion = 1,
            GeneratedFromSourceFileId = approvedWord.Id
        };

        revision.ApprovedSourceFileId = approvedWord.Id;
        revision.ApprovedSourceFile = approvedWord;
        revision.ControlledPdfFileId = controlledPdf.Id;
        revision.ControlledPdfFile = controlledPdf;

        Assert.Equal(501, revision.ApprovedSourceFileId);
        Assert.Equal(approvedWord, revision.ApprovedSourceFile);
        Assert.Equal(502, revision.ControlledPdfFileId);
        Assert.Equal(controlledPdf, revision.ControlledPdfFile);
    }

    [Fact]
    public void DocumentApprovalTask_PreservesApprovalDossierLineage()
    {
        var task = new DocumentApprovalTask
        {
            Id = 60,
            DocumentRevisionId = 50,
            AssignedApproverUserId = 3,
            AssignedByUserId = 1,
            Status = DocumentApprovalTaskStatus.Approved,
            Decision = DocumentApprovalDecision.Approve,
            DecisionAt = DateTime.UtcNow
        };

        var sourceFile = new RevisionFile
        {
            Id = 501,
            DocumentRevisionId = 50,
            FileRole = FileRole.SourceFile,
            FileVersion = 2,
            IsApprovedFinalSource = true
        };

        var pdfFile = new RevisionFile
        {
            Id = 502,
            DocumentRevisionId = 50,
            FileRole = FileRole.ControlledPdf,
            FileVersion = 1,
            GeneratedFromSourceFileId = 501
        };

        task.ApprovedSourceFileId = sourceFile.Id;
        task.ApprovedSourceFile = sourceFile;
        task.GeneratedControlledPdfId = pdfFile.Id;
        task.GeneratedControlledPdf = pdfFile;

        Assert.Equal(501, task.ApprovedSourceFileId);
        Assert.Equal(sourceFile, task.ApprovedSourceFile);
        Assert.Equal(502, task.GeneratedControlledPdfId);
        Assert.Equal(pdfFile, task.GeneratedControlledPdf);
    }

    [Fact]
    public void DocumentReviewTask_ConstructsWithCycleNumberDefaultAndReviewedSource()
    {
        var reviewTask = new DocumentReviewTask
        {
            Id = 70,
            DocumentRevisionId = 50,
            AssignedReviewerUserId = 2,
            AssignedByUserId = 1,
            Status = ReviewTaskStatus.Pending
        };

        Assert.Equal(1, reviewTask.ReviewCycleNumber);
        Assert.Null(reviewTask.ReviewedSourceFileId);

        var reviewedSource = new RevisionFile
        {
            Id = 500,
            DocumentRevisionId = 50,
            FileRole = FileRole.SourceFile,
            FileVersion = 1
        };

        reviewTask.ReviewedSourceFileId = reviewedSource.Id;
        reviewTask.ReviewedSourceFile = reviewedSource;

        Assert.Equal(500, reviewTask.ReviewedSourceFileId);
        Assert.Equal(reviewedSource, reviewTask.ReviewedSourceFile);
    }

    [Fact]
    public void DocumentReviewFinding_LinksToSpecificSourceFileVersion()
    {
        var finding = new DocumentReviewFinding
        {
            Id = 80,
            DocumentReviewTaskId = 70,
            CreatedByUserId = 2,
            CommentText = "Please clarify procedure in Section 4.2",
            IsMandatory = true,
            SectionNumber = "4.2",
            PageNumber = 3,
            Status = ReviewFindingStatus.Open
        };

        var sourceFile = new RevisionFile
        {
            Id = 500,
            DocumentRevisionId = 50,
            FileRole = FileRole.SourceFile,
            FileVersion = 1
        };

        finding.RevisionFileId = sourceFile.Id;
        finding.RevisionFile = sourceFile;
        finding.SourceFileVersion = sourceFile.FileVersion;

        Assert.Equal(500, finding.RevisionFileId);
        Assert.Equal(sourceFile, finding.RevisionFile);
        Assert.Equal(1, finding.SourceFileVersion);
    }
}

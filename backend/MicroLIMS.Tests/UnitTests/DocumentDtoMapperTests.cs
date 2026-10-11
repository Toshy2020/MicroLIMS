using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentDtoMapperTests
{
    private static DocumentRevision Revision()
    {
        var author = new User { Id = 3, FullName = "Mazen Asharaf" };
        var canceller = new User { Id = 4, FullName = "Sara Lead" };
        return new DocumentRevision
        {
            Id = 7,
            DocumentMasterId = 2,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.Cancelled,
            EffectiveDate = new DateTime(2026, 1, 2),
            NextReviewDate = new DateTime(2028, 1, 2),
            ReviewCycleMonths = 24,
            RevisionType = RevisionType.Minor,
            ReasonForRevision = "Wording",
            ChangeReference = "CC-9",
            RecordOrigin = RecordOrigin.Native,
            CreatedAt = new DateTime(2025, 12, 1),
            CreatedByUserId = 3,
            CreatedByUser = author,
            CancelledAt = new DateTime(2026, 1, 5),
            CancelledByUserId = 4,
            CancelledByUser = canceller,
            CancelReason = "Superseded",
            Files =
            {
                new RevisionFile { Id = 1, DocumentRevisionId = 7, FileRole = FileRole.SourceFile, FileName = "old.docx", UploadedAt = new DateTime(2025, 12, 2), UploadedByUserId = 3, UploadedByUser = author },
                new RevisionFile
                {
                    Id = 2, DocumentRevisionId = 7, FileRole = FileRole.ControlledPdf, FileName = "new.pdf", ContentType = "application/pdf",
                    SizeBytes = 1234, ContentSha256 = "abc", IsActive = true, SupersededByFileId = 9,
                    UploadedAt = new DateTime(2025, 12, 3), UploadedByUserId = 3, UploadedByUser = author,
                    FileVersion = 4, IsApprovedFinalSource = true, GeneratedFromSourceFileId = 1
                }
            }
        };
    }

    [Fact]
    public void MapRevision_CopiesEveryField_AndOrdersFilesNewestFirst()
    {
        var dto = DocumentDtoMapper.MapRevision(Revision());

        Assert.Equal(7, dto.Id);
        Assert.Equal(2, dto.DocumentMasterId);
        Assert.Equal("02", dto.RevisionNumber);
        Assert.Equal(2, dto.RevisionSequence);
        Assert.Equal(DocumentRevisionStatus.Cancelled, dto.RevisionStatus);
        Assert.Equal(new DateTime(2026, 1, 2), dto.EffectiveDate);
        Assert.Equal(new DateTime(2028, 1, 2), dto.NextReviewDate);
        Assert.Equal(24, dto.ReviewCycleMonths);
        Assert.Equal(RevisionType.Minor, dto.RevisionType);
        Assert.Equal("Wording", dto.ReasonForRevision);
        Assert.Equal("CC-9", dto.ChangeReference);
        Assert.Equal(RecordOrigin.Native, dto.RecordOrigin);
        Assert.Equal(new DateTime(2025, 12, 1), dto.CreatedAt);
        Assert.Equal(3, dto.CreatedByUserId);
        Assert.Equal("Mazen Asharaf", dto.CreatedByName);
        Assert.Equal(new DateTime(2026, 1, 5), dto.CancelledAt);
        Assert.Equal(4, dto.CancelledByUserId);
        Assert.Equal("Sara Lead", dto.CancelledByName);
        Assert.Equal("Superseded", dto.CancelReason);
        Assert.Equal(new[] { 2, 1 }, dto.Files.Select(f => f.Id));
    }

    [Fact]
    public void MapFile_CopiesEveryField()
    {
        var f = Revision().Files.Single(x => x.Id == 2);

        var dto = DocumentDtoMapper.MapFile(f);

        Assert.Equal(2, dto.Id);
        Assert.Equal(7, dto.DocumentRevisionId);
        Assert.Equal(FileRole.ControlledPdf, dto.FileRole);
        Assert.Equal("new.pdf", dto.FileName);
        Assert.Equal("application/pdf", dto.ContentType);
        Assert.Equal(1234, dto.SizeBytes);
        Assert.Equal("abc", dto.ContentSha256);
        Assert.True(dto.IsActive);
        Assert.Equal(9, dto.SupersededByFileId);
        Assert.Equal(new DateTime(2025, 12, 3), dto.UploadedAt);
        Assert.Equal(3, dto.UploadedByUserId);
        Assert.Equal("Mazen Asharaf", dto.UploadedByName);
        Assert.Equal(4, dto.FileVersion);
        Assert.True(dto.IsApprovedFinalSource);
        Assert.Equal(1, dto.GeneratedFromSourceFileId);
    }

    [Fact]
    public void MapRevision_WithoutLoadedUsers_FallsBackToEmptyCreatorAndNullCanceller()
    {
        var r = Revision();
        r.CreatedByUser = null!;
        r.CancelledByUser = null;

        var dto = DocumentDtoMapper.MapRevision(r);

        Assert.Equal("", dto.CreatedByName);
        Assert.Null(dto.CancelledByName);
    }
}

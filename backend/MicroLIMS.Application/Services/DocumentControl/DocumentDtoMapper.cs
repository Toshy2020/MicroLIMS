using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services.DocumentControl;

// One place that turns a loaded DocumentRevision (with CreatedByUser,
// CancelledByUser, Files and each file's UploadedByUser) into its DTO.
// Callers must Include those navigations; a missing creator maps to "".
public static class DocumentDtoMapper
{
    public static DocumentRevisionDto MapRevision(DocumentRevision r)
    {
        return new DocumentRevisionDto(
            r.Id,
            r.DocumentMasterId,
            r.RevisionNumber,
            r.RevisionSequence,
            r.RevisionStatus,
            r.EffectiveDate,
            r.NextReviewDate,
            r.ReviewCycleMonths,
            r.RevisionType,
            r.ReasonForRevision,
            r.ChangeReference,
            r.RecordOrigin,
            r.CreatedAt,
            r.CreatedByUserId,
            r.CreatedByUser?.FullName ?? "",
            r.CancelledAt,
            r.CancelledByUserId,
            r.CancelledByUser?.FullName,
            r.CancelReason,
            r.Files.OrderByDescending(f => f.UploadedAt).Select(MapFile).ToList()
        );
    }

    public static RevisionFileDto MapFile(RevisionFile f)
    {
        return new RevisionFileDto(
            f.Id,
            f.DocumentRevisionId,
            f.FileRole,
            f.FileName,
            f.ContentType,
            f.SizeBytes,
            f.ContentSha256,
            f.IsActive,
            f.SupersededByFileId,
            f.UploadedAt,
            f.UploadedByUserId,
            f.UploadedByUser?.FullName ?? "",
            f.FileVersion,
            f.IsApprovedFinalSource,
            f.GeneratedFromSourceFileId
        );
    }
}

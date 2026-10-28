using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services.DocumentControl;

// One place that turns a loaded DocumentRevision (with CreatedByUser,
// CancelledByUser, Files and each file's UploadedByUser) into its DTO.
// Callers must Include those navigations; a missing creator maps to "".
public static class DocumentDtoMapper
{
    // What a deactivated custodian or assignee shows as. Their name is deliberately not exposed to
    // ordinary viewers; the reassignment queue shows it to the people who hand the work over.
    public const string InactiveHolderLabel = "Inactive user \u2014 needs reassignment";

    public static string HolderName(User? user) =>
        user is { IsActive: false } ? InactiveHolderLabel : user?.FullName ?? "";

    public static DocumentMasterAssignmentDto MapAssignment(DocumentMasterAssignment a) => new(
        a.Id,
        a.DocumentMasterId,
        a.UserId,
        a.User is { IsActive: false } ? "" : a.User?.Username ?? "",
        HolderName(a.User),
        a.AssignmentRole,
        a.IsActive,
        a.AssignedAt,
        a.AssignedByUserId,
        a.AssignedByUser?.FullName ?? "");

    // The master's sections, primary first then by name. Falls back to the primary
    // alone when Sections (and Sections.Section) were not loaded.
    public static (IReadOnlyList<int> Ids, IReadOnlyList<string> Names) MapSections(DocumentMaster m)
    {
        var rows = m.Sections
            .Where(s => s.SectionId != m.SectionId)
            .Select(s => (s.SectionId, Name: s.Section?.Name ?? ""))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Prepend((m.SectionId, Name: m.Section?.Name ?? ""))
            .ToList();
        return (rows.Select(r => r.SectionId).ToList(), rows.Select(r => r.Name).ToList());
    }

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

    public static RevisionChangeItemDto MapChangeItem(RevisionChangeItem i, string username, string fullName)
    {
        return new RevisionChangeItemDto(
            i.Id,
            i.DocumentRevisionId,
            i.SectionNumber,
            i.SectionTitle,
            i.DescriptionOfChange,
            i.ChangeRationale,
            i.ChangeCategory,
            i.Status,
            i.OriginatingReviewFindingId,
            i.CreatedByUserId,
            username,
            fullName,
            i.CreatedAt,
            i.ModifiedAt
        ) { Version = i.Version };
    }

    public static RevisionImpactAssessmentDto MapImpact(RevisionImpactAssessment a, string username, string fullName)
    {
        return new RevisionImpactAssessmentDto(
            a.Id,
            a.DocumentRevisionId,
            a.ProcedureOrMethodImpact,
            a.ProcedureOrMethodDetails,
            a.TrainingImpact,
            a.TrainingDetails,
            a.FormsOrTemplatesImpact,
            a.FormsOrTemplatesDetails,
            a.SpecificationsImpact,
            a.SpecificationsDetails,
            a.EquipmentImpact,
            a.EquipmentDetails,
            a.MaterialsOrMediaImpact,
            a.MaterialsOrMediaDetails,
            a.ValidationImpact,
            a.ValidationDetails,
            a.RegulatoryCommitmentImpact,
            a.RegulatoryCommitmentDetails,
            a.RelatedDocumentsImpact,
            a.RelatedDocumentsDetails,
            a.IsComplete,
            a.CompletedByUserId,
            username,
            fullName,
            a.CompletedAt
        );
    }
}

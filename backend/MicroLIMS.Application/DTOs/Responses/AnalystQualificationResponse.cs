using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

public sealed record QualificationAttachmentResponse(int Id, string FileName, string ContentType, long FileSizeBytes);

public sealed record AnalystQualificationResponse(
    int Id, uint Version, int UserId, int TestDefinitionId, QualificationBasis Basis, string Evidence,
    int? EvidenceSampleId, DateOnly QualifiedOn, DateOnly? ExpiresOn, SignatureDto Signature,
    DateTime? WithdrawnOn, string? WithdrawnReason, SignatureDto? WithdrawalSignature,
    IReadOnlyList<QualificationAttachmentResponse> Attachments)
{
    internal static AnalystQualificationResponse From(AnalystQualification qualification) =>
        new(qualification.Id, qualification.Version, qualification.UserId, qualification.TestDefinitionId,
            qualification.Basis, qualification.Evidence, qualification.EvidenceSampleId, qualification.QualifiedOn,
            qualification.ExpiresOn, Manifest(qualification.Signature), qualification.WithdrawnOn,
            qualification.WithdrawnReason, qualification.WithdrawnSignature is { } withdrawal ? Manifest(withdrawal) : null,
            qualification.Attachments.Select(a => new QualificationAttachmentResponse(
                a.Id, a.OriginalFileName, a.ContentType, a.FileSizeBytes)).ToArray());

    private static SignatureDto Manifest(ElectronicSignature signature) =>
        new(signature.UserFullNameSnapshot, signature.UsernameSnapshot, signature.RoleNameSnapshot ?? signature.RoleSnapshot,
            signature.MeaningOfSignature.ToString(), signature.SignedAt, signature.Comment);
}

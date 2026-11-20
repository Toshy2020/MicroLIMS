using System.Security.Cryptography;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Services.Competency;

public sealed record QualificationFile(string FileName, byte[] Data);

public sealed record QualifyRequest(
    int UserId, int TestDefinitionId, QualificationBasis Basis, string Evidence,
    int? EvidenceSampleId, DateOnly QualifiedOn, string Password,
    IReadOnlyList<QualificationFile>? Attachments = null);

public sealed class QualificationService(
    IMicroLimsDbContext db,
    PermissionService permissions,
    IUserSectionScopeService scope,
    ILabClock clock,
    IDatabaseSequenceHelper sequences,
    IElectronicSignatureService signatures,
    IFileStorageService storage)
{
    public async Task<AnalystQualificationResponse> QualifyAsync(
        QualifyRequest request, int actingUserId, CancellationToken cancellationToken = default)
    {
        await RequireManageAsync(actingUserId, cancellationToken);
        if (actingUserId == request.UserId)
            throw new BusinessRuleException(MessageCodes.CompetencyQualificationSelfSign, "You cannot qualify yourself.");

        var test = await GetTestAsync(request.TestDefinitionId, actingUserId, cancellationToken);
        var memberSections = await scope.GetMemberSectionIdsAsync(request.UserId, cancellationToken);
        if (!memberSections.Contains(test.SectionId) ||
            !await db.Users.AsNoTracking().AnyAsync(u => u.Id == request.UserId && u.IsActive, cancellationToken))
            throw new BusinessRuleException(MessageCodes.CompetencyQualificationIneligible, "The analyst must be active and belong to the test's laboratory.");

        if (string.IsNullOrWhiteSpace(request.Evidence) || !Enum.IsDefined(request.Basis) ||
            request.QualifiedOn == default || request.QualifiedOn > clock.LabToday)
            throw new BusinessRuleException(MessageCodes.CompetencyQualificationInvalid, "Provide a qualification basis, evidence and a qualified-on date no later than today.");

        if (request.EvidenceSampleId is int sampleId)
            await scope.EnsureSampleAccessAsync(actingUserId, sampleId, cancellationToken, SectionAccess.Read);

        var files = request.Attachments ?? Array.Empty<QualificationFile>();
        var attachmentProblem = DiscussionAttachmentPolicy.Validate(files.Select(f => (f.FileName, f.Data)).ToList());
        if (attachmentProblem is not null)
            throw new BusinessRuleException(MessageCodes.CompetencyAttachmentInvalid, attachmentProblem,
                new Dictionary<string, string> { ["detail"] = attachmentProblem });
        if (files.Any(f => Path.GetFileName(f.FileName).Length > 255))
            throw new BusinessRuleException(MessageCodes.CompetencyAttachmentNameTooLong, "Attachment names may contain at most 255 characters.");

        return await UnitOfWork.RunAsync(db, async () =>
        {
            var id = await sequences.ReserveIdAsync<AnalystQualification>(cancellationToken);
            var evidence = request.Evidence.Trim();
            var signature = await signatures.SignAsync(actingUserId, request.Password ?? string.Empty,
                SignatureMeaning.AnalystQualified, nameof(AnalystQualification), id, evidence, null);
            signature.SignedAt = clock.UtcNow.UtcDateTime;

            var qualification = new AnalystQualification
            {
                Id = id,
                UserId = request.UserId,
                TestDefinitionId = request.TestDefinitionId,
                Basis = request.Basis,
                Evidence = evidence,
                EvidenceSampleId = request.EvidenceSampleId,
                QualifiedOn = request.QualifiedOn,
                ExpiresOn = test.RequalificationMonths is int months ? request.QualifiedOn.AddMonths(months) : null,
                SignedByUserId = actingUserId,
                Signature = signature
            };

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileName(file.FileName);
                var key = await storage.SaveAsync($"competency/{id}/{Guid.NewGuid()}_{name}", file.Data);
                qualification.Attachments.Add(new QualificationAttachment
                {
                    OriginalFileName = name,
                    StorageKey = key,
                    FileExtension = Path.GetExtension(name).ToLowerInvariant(),
                    ContentType = DiscussionAttachmentPolicy.ContentTypeFor(name),
                    FileSizeBytes = file.Data.LongLength,
                    ContentSha256 = Convert.ToHexString(SHA256.HashData(file.Data)),
                    UploadedByUserId = actingUserId,
                    UploadedAt = signature.SignedAt
                });
            }

            db.AnalystQualifications.Add(qualification);
            await SaveAsActorAsync(actingUserId, evidence, cancellationToken);
            return AnalystQualificationResponse.From(qualification);
        });
    }

    public async Task<AnalystQualificationResponse> WithdrawAsync(
        int qualificationId, string reason, string password, int actingUserId, CancellationToken cancellationToken = default)
    {
        await RequireManageAsync(actingUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException(MessageCodes.CompetencyWithdrawalReasonRequired, "A withdrawal reason is required.");

        return await UnitOfWork.RunAsync(db, async () =>
        {
            var qualification = await db.AnalystQualifications.Include(q => q.Signature).Include(q => q.Attachments)
                .SingleOrDefaultAsync(q => q.Id == qualificationId, cancellationToken)
                ?? throw new NotFoundException(MessageCodes.CompetencyQualificationNotFound, "Qualification not found.");
            await GetTestAsync(qualification.TestDefinitionId, actingUserId, cancellationToken);
            RecordVersion.EnsureCurrent(db, qualification);
            if (qualification.WithdrawnOn is not null)
                throw new BusinessRuleException(MessageCodes.CompetencyQualificationAlreadyWithdrawn, "The qualification has already been withdrawn.");

            var signature = await signatures.SignAsync(actingUserId, password ?? string.Empty,
                SignatureMeaning.AnalystQualificationWithdrawn, nameof(AnalystQualification), qualificationId, reason.Trim(), null);
            signature.SignedAt = clock.UtcNow.UtcDateTime;
            qualification.WithdrawnOn = signature.SignedAt;
            qualification.WithdrawnByUserId = actingUserId;
            qualification.WithdrawnReason = reason.Trim();
            qualification.WithdrawnSignature = signature;

            await SaveAsActorAsync(actingUserId, qualification.WithdrawnReason, cancellationToken);

            return AnalystQualificationResponse.From(qualification);
        });
    }

    private async Task RequireManageAsync(int actingUserId, CancellationToken cancellationToken)
    {
        if (!await permissions.UserHasPermissionAsync(actingUserId, PermissionConstants.CompetencyManage, cancellationToken))
            throw new CodedAccessDeniedException(MessageCodes.CompetencyManageRequired, "You do not have permission to manage analyst qualifications.");
    }

    private async Task<TestDefinition> GetTestAsync(int id, int actor, CancellationToken cancellationToken)
    {
        var test = await db.TestDefinitions.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(MessageCodes.TestDefinitionRecordNotFound, "Test definition not found.");
        await TestConfigurationScope.EnsureCanConfigureAsync(scope, actor, test.SectionId, cancellationToken);
        return test;
    }

    private async Task SaveAsActorAsync(int actor, string reason, CancellationToken cancellationToken)
    {
        var previousActor = db.CurrentUserId;
        var previousReason = db.CurrentReason;
        db.CurrentUserId = actor;
        db.CurrentReason = reason;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            db.CurrentUserId = previousActor;
            db.CurrentReason = previousReason;
        }
    }
}

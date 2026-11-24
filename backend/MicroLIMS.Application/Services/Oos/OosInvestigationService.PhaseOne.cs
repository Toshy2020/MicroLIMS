using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Oos;

public sealed record SignPhaseOneRequest(OosPhaseOneAssessment Assessment, int? CauseCategoryId, string? CauseDescription, string Password);

public sealed partial class OosInvestigationService
{
    public Task AddAnalystStatementAsync(int investigationId, string text, int userId, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            var investigation = await OpenForEditAsync(investigationId, userId, PermissionConstants.OosContribute, ct);
            EnsureOosKind(investigation);
            EnsurePhaseOne(investigation);
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4000)
                throw new BusinessRuleException(MessageCodes.OosStatementInvalid, "Enter an analyst statement of 1 to 4000 characters.");

            var analyst = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Role != null && u.Role.Type == RoleType.Analyst, ct);
            if (!analyst || !await PerformedAnyAsync(investigationId, userId, ct))
                throw new BusinessRuleException(MessageCodes.OosStatementPerformerRequired,
                    "Only an analyst who performed a test in this investigation can add a statement.");

            await AppendAsync(investigation, 1, OosEntryKind.AnalystStatement, null, null, text.Trim(), userId, ct);
        }, ct);

    public Task AnswerChecklistAsync(int investigationId, int phase, string itemKey, OosChecklistAnswer answer,
        string? comment, int userId, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            var investigation = await OpenForEditAsync(investigationId, userId, PermissionConstants.OosContribute, ct);
            if (phase != 1)
                EnsureOosKind(investigation);
            if (phase == 1)
                EnsurePhaseOne(investigation);
            if (!Enum.IsDefined(answer) || comment?.Length > 1000 ||
                (phase != 1 && (phase != 2 || investigation.Status != OosInvestigationStatus.PhaseTwo)) ||
                !ReadChecklistKeys(investigation, phase).Contains(itemKey))
                throw new BusinessRuleException(MessageCodes.OosChecklistAnswerInvalid,
                    "Choose an active checklist item and a valid answer. Comments may have up to 1000 characters.");

            await AppendAsync(investigation, phase, OosEntryKind.ChecklistAnswer, itemKey, answer, comment?.Trim() ?? "", userId, ct);
        }, ct);

    public Task<bool> SignPhaseOneAsync(int investigationId, SignPhaseOneRequest request, uint expectedVersion, int userId,
        string? ip, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            var investigation = await OpenForEditAsync(investigationId, userId, PermissionConstants.OosManage, ct);
            EnsureOosKind(investigation);
            EnsurePhaseOne(investigation);
            if (investigation.Version != expectedVersion)
                throw OosTemplateService.VersionConflict();
            if (await PerformedAnyAsync(investigationId, userId, ct))
                throw new BusinessRuleException(MessageCodes.OosSignerPerformedTest,
                    "You performed a test in this investigation, so you cannot sign it.");

            var keys = ReadChecklistKeys(investigation, 1);
            if (keys.Count == 0)
                throw new BusinessRuleException(MessageCodes.OosPhaseOneTemplateEmpty, "No Phase I checklist is configured for this laboratory.");

            var entries = db.OosInvestigationEntrys.AsNoTracking().Where(e => e.InvestigationId == investigationId && e.Phase == 1);
            if (!await entries.AnyAsync(e => e.Kind == OosEntryKind.AnalystStatement, ct))
                throw new BusinessRuleException(MessageCodes.OosPhaseOneStatementRequired, "Add an analyst statement before signing Phase I.");

            var latestAnswers = entries.Where(e => e.Kind == OosEntryKind.ChecklistAnswer && e.ChecklistItemKey != null)
                .GroupBy(e => e.ChecklistItemKey).Select(g => g.Max(e => e.Id));
            var answered = await entries.Where(e => latestAnswers.Contains(e.Id) && e.Answer != null)
                .Select(e => e.ChecklistItemKey).ToListAsync(ct);
            if (!keys.IsSubsetOf(answered.OfType<string>()))
                throw new BusinessRuleException(MessageCodes.OosPhaseOneChecklistIncomplete, "Answer every active Phase I checklist item before signing.");

            var description = request.CauseDescription?.Trim();
            if (!Enum.IsDefined(request.Assessment) || request.CauseDescription?.Length > 4000)
                throw new BusinessRuleException(MessageCodes.OosPhaseOneAssessmentInvalid,
                    "Select a valid Phase I assessment and enter any cause description within 4000 characters.");

            var confirmed = request.Assessment == OosPhaseOneAssessment.LaboratoryErrorConfirmed;
            var categoryId = request.CauseCategoryId;
            if (confirmed && (categoryId is null || string.IsNullOrEmpty(description)))
                throw AssignableCauseRequired();
            if (categoryId is int id)
            {
                // Keep the category active through the signing transaction.
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""SELECT 1 FROM "OosCauseCategories" WHERE "Id" = {id} FOR SHARE""", ct);
                var category = await db.OosCauseCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct)
                    ?? throw new BusinessRuleException(MessageCodes.OosCategoryNotFound, "The OOS cause category was not found.");
                if (!category.IsActive)
                    throw new BusinessRuleException(MessageCodes.OosPhaseOneCauseInactive, "The selected cause category is no longer active.");
                if (confirmed && category.IsNoCauseFound)
                    throw AssignableCauseRequired();
            }

            ElectronicSignature signature;
            try
            {
                signature = await signatures.SignAsync(userId, request.Password ?? "",
                    SignatureMeaning.OosPhaseOneConcluded, nameof(OosInvestigation), investigationId, description, ip, ct);
            }
            catch (SignatureVerificationException error)
            {
                throw new BusinessRuleException(MessageCodes.OosPhaseOnePasswordFailed, error.Message);
            }

            db.CurrentUserId = userId;
            var now = time.GetUtcNow().UtcDateTime;
            signature.SignedAt = now;
            await db.SaveChangesAsync(ct);

            investigation.PhaseOneAssessment = request.Assessment;
            investigation.PhaseOneCauseCategoryId = categoryId;
            investigation.PhaseOneCauseDescription = description;
            investigation.PhaseOneSignatureId = signature.Id;
            investigation.PhaseOneSignedByUserId = userId;
            investigation.PhaseOneSignedAtUtc = now;
            investigation.Status = confirmed ? OosInvestigationStatus.AwaitingRetest : OosInvestigationStatus.PhaseTwo;

            if (confirmed)
            {
                var results = await db.OosInvestigationResults.Where(r => r.InvestigationId == investigationId).ToListAsync(ct);
                foreach (var link in results)
                {
                    link.IsInvalidated = true;
                    link.InvalidatedAtUtc = now;
                }
            }

            await db.SaveChangesAsync(ct);
            return !confirmed;
        }, ct);

    internal async Task<OosInvestigation> OpenForEditAsync(int id, int actor, string permission,
        CancellationToken ct = default, Func<BusinessRuleException>? closed = null, string? alternativePermission = null)
    {
        await EnsureInvestigationPermissionAsync(actor, permission, alternativePermission, ct);

        var sectionId = await db.OosInvestigations.AsNoTracking().Where(i => i.Id == id).Select(i => (int?)i.SectionId).SingleOrDefaultAsync(ct)
            ?? throw InvestigationNotFound();
        await EnsureScopeAsync(actor, sectionId, SectionAccess.Write, ct);

        return await LockOpenAsync(db.OosInvestigations.Where(i => i.Id == id), ct)
            ?? throw (closed ?? Closed)();
    }

    private async Task EnsureInvestigationPermissionAsync(int actor, string permission, string? alternative, CancellationToken ct)
    {
        if (!await permissions.UserHasPermissionAsync(actor, permission, ct) &&
            (alternative is null || !await permissions.UserHasPermissionAsync(actor, alternative, ct)))
            throw new CodedAccessDeniedException(MessageCodes.OosInvestigationAccessDenied, "You do not have permission for this investigation action.");
    }

    private static NotFoundException InvestigationNotFound() =>
        new(MessageCodes.OosInvestigationNotFound, "The OOS investigation was not found.");

    private async Task EnsureScopeAsync(int actor, int sectionId, SectionAccess access, CancellationToken ct = default)
    {
        try
        {
            await scope.EnsureSectionsAccessAsync(actor, [sectionId], ct, access);
        }
        catch (UnauthorizedAccessException)
        {
            throw new CodedAccessDeniedException(MessageCodes.OosSectionAccessDenied, "You do not have access to this laboratory section.");
        }
    }

    private async Task<bool> PerformedAnyAsync(int id, int actor, CancellationToken ct = default)
    {
        var orders = await db.OosInvestigationResults.AsNoTracking().Where(r => r.InvestigationId == id)
            .Select(r => r.TestOrderId).ToListAsync(ct);
        return (await duties.GetTestOrderIdsPerformedByAsync(actor, orders, ct)).Count != 0;
    }

    private async Task AppendAsync(OosInvestigation investigation, int phase, OosEntryKind kind, string? key,
        OosChecklistAnswer? answer, string text, int actor, CancellationToken ct = default, int? parentEntryId = null)
    {
        if (db.ExpectedVersion is uint expected && expected != investigation.Version)
            throw OosTemplateService.VersionConflict();

        db.CurrentUserId = actor;
        db.OosInvestigationEntrys.Add(new OosInvestigationEntry
        {
            InvestigationId = investigation.Id,
            Phase = phase,
            Kind = kind,
            ChecklistItemKey = key,
            ParentEntryId = parentEntryId,
            Answer = answer,
            Text = text,
            RecordedByUserId = actor,
            RecordedAtUtc = time.GetUtcNow().UtcDateTime
        });
        // A submitted entry changes the version of the phase the client reviewed.
        db.Entry(investigation).Property(nameof(OosInvestigation.Status)).IsModified = true;
        await db.SaveChangesAsync(ct);
    }

    private static HashSet<string> ReadChecklistKeys(OosInvestigation investigation, int phase)
    {
        using var snapshot = JsonDocument.Parse(investigation.TemplateSnapshotJson);
        return snapshot.RootElement.TryGetProperty("items", out var items)
            ? items.EnumerateArray().Where(i => i.GetProperty("phase").GetInt32() == phase)
                .Select(i => i.GetProperty("key").GetString()).OfType<string>().ToHashSet(StringComparer.Ordinal)
            : [];
    }

    private static void EnsureOosKind(OosInvestigation investigation)
    {
        if (investigation.Kind != OosInvestigationKind.Oos)
            throw new BusinessRuleException(MessageCodes.OosInvestigationKindNotOos, "This action is only available for an OOS investigation.");
    }

    private static void EnsurePhaseOne(OosInvestigation investigation)
    {
        if (investigation.Status != OosInvestigationStatus.PhaseOne || investigation.PhaseOneSignatureId is not null)
            throw Closed();
    }

    private static BusinessRuleException Closed() =>
        new(MessageCodes.OosPhaseOneClosed, "Phase I is no longer open for this investigation.");

    private static BusinessRuleException AssignableCauseRequired() =>
        new(MessageCodes.OosPhaseOneAssignableCauseRequired, "A confirmed laboratory error requires an assignable cause and a description.");
}

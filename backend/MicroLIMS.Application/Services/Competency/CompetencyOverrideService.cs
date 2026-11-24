using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Services.Competency;

public sealed class CompetencyOverrideService(
    IMicroLimsDbContext db, PermissionService permissions, IUserSectionScopeService scope,
    ILabClock clock, IDatabaseSequenceHelper sequences, IElectronicSignatureService signatures,
    ICompetencyGuard guard)
{
    public async Task<int> GrantAsync(int testOrderId, int analystUserId, string reason, string password,
        int actingUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await UnitOfWork.RunAsync(db, async () =>
            {
                if (!await permissions.UserHasPermissionAsync(actingUserId, PermissionConstants.CompetencyOverride, cancellationToken))
                    throw new CodedAccessDeniedException(MessageCodes.CompetencyOverridePermissionRequired,
                        "You do not have permission to grant a competency override.");

                await scope.EnsureTestOrderAccessAsync(actingUserId, testOrderId, cancellationToken);
                if (actingUserId == analystUserId)
                    throw new BusinessRuleException(MessageCodes.CompetencyOverrideSelf, "You cannot override your own competency.");
                var reasonText = reason?.Trim();
                if (reasonText is null || reasonText.Length is < 1 or > 2000)
                    throw new BusinessRuleException(MessageCodes.CompetencyOverrideReasonRequired,
                        "Enter an override reason of 1 to 2000 characters.");

                var order = await db.TestOrders.SingleOrDefaultAsync(t => t.Id == testOrderId, cancellationToken)
                    ?? throw new NotFoundException(MessageCodes.TestOrderNotFound, $"Test order {testOrderId} not found.",
                        new Dictionary<string, string> { ["testOrderId"] = testOrderId.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                if (order.IsSuperseded || order.OutsourcedToLab is not null ||
                    order.Status is not (ApprovalStatus.Pending or ApprovalStatus.InProgress) ||
                    await db.OosRetestPlanSamples.AsNoTracking().AnyAsync(s => s.SampleId == order.SampleId, cancellationToken))
                    throw new BusinessRuleException(MessageCodes.CompetencyOverrideOrderClosed,
                        "This test order is not open for a competency override.");

                var (assignable, _) = await AnalystAssignmentRules.CheckAssignableAsync(db, scope, analystUserId,
                    actingUserId, [order.SectionId], clock.UtcNow.UtcDateTime, cancellationToken);
                if (!assignable.Allowed)
                    throw new BusinessRuleException(MessageCodes.CompetencyOverrideAnalystIneligible,
                        "This analyst is not eligible for assignment to the test order.");

                if (await db.CompetencyOverrides.AsNoTracking().AnyAsync(o => o.TestOrderId == testOrderId && o.EndedAtUtc == null, cancellationToken))
                    throw new BusinessRuleException(MessageCodes.CompetencyOverrideAlreadyActive,
                        "This test order already has an active competency override.");
                if ((await guard.EvaluateAsync(analystUserId, testOrderId, cancellationToken)).Allowed)
                    throw new BusinessRuleException(MessageCodes.CompetencyOverrideNotNeeded, "A competency override is not needed.");

                var id = await sequences.ReserveIdAsync<CompetencyOverride>(cancellationToken);
                ElectronicSignature signature;
                try
                {
                    signature = await signatures.SignAsync(actingUserId, password ?? string.Empty,
                        SignatureMeaning.CompetencyOverride, nameof(CompetencyOverride), id, reasonText, null, cancellationToken);
                }
                catch (SignatureVerificationException)
                {
                    throw new BusinessRuleException(MessageCodes.CompetencyOverridePasswordFailed,
                        "Signature password verification failed.");
                }

                signature.SignedAt = clock.UtcNow.UtcDateTime;
                var priorActor = db.CurrentUserId;
                var priorReason = db.CurrentReason;
                db.CurrentUserId = actingUserId;
                db.CurrentReason = reasonText;
                try
                {
                    // Serialize the grant with competing review, assignment and grant writes on the same order.
                    db.Entry(order).Property(nameof(TestOrder.Status)).IsModified = true;
                    await db.SaveChangesAsync(cancellationToken);

                    db.CompetencyOverrides.Add(new CompetencyOverride
                    {
                        Id = id, TestOrderId = order.Id, SampleId = order.SampleId, AnalystUserId = analystUserId,
                        GrantedByUserId = actingUserId, GrantedAtUtc = signature.SignedAt, Reason = reasonText,
                        Signature = signature
                    });
                    await db.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    db.CurrentUserId = priorActor;
                    db.CurrentReason = priorReason;
                }

                return id;
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException(MessageCodes.CompetencyOverrideConcurrencyConflict,
                "The test changed. Refresh and try again.");
        }
    }
}

using System.Globalization;
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

public sealed record CompetencySettingDto(
    int SectionId, bool GateEnabled, int? ReminderDaysBeforeExpiry,
    DateTime? ChangedAt, string? ChangedByName, uint Version);

public sealed record ImpactOrderDto(
    int TestOrderId, string TestCode, string SampleReference, IReadOnlyList<CompetencyReason> Reasons);

public sealed record AnalystImpactDto(
    int AnalystUserId, string AnalystName, IReadOnlyList<ImpactOrderDto> Orders);

public sealed record GateImpactDto(
    int SectionId, IReadOnlyList<AnalystImpactDto> Analysts, int TotalRefusedOrders);

// Per-laboratory switch. An absent row means the gate is off. Changes need a reason and
// an electronic signature, and are audited with the business row in one transaction.
public sealed class CompetencySettingsService(
    IMicroLimsDbContext db,
    PermissionService permissions,
    IUserSectionScopeService scope,
    ILabClock clock,
    IElectronicSignatureService signatures,
    ICompetencyGuard guard)
{
    public async Task<CompetencySettingDto> GetAsync(
        int sectionId, int actingUserId, CancellationToken cancellationToken = default)
    {
        await RequireManageAsync(actingUserId, cancellationToken);
        await RequireSectionAsync(actingUserId, sectionId, cancellationToken);
        return await ReadAsync(sectionId, cancellationToken);
    }

    // Impact preview: open, assigned orders in the section evaluated as if the gate were on.
    public async Task<GateImpactDto> GetImpactAsync(
        int sectionId, int actingUserId, CancellationToken cancellationToken = default)
    {
        await RequireManageAsync(actingUserId, cancellationToken);
        await RequireSectionAsync(actingUserId, sectionId, cancellationToken);

        var orders = await db.TestOrders.AsNoTracking()
            .Where(order => order.SectionId == sectionId
                && !order.IsSuperseded
                && order.AssignedAnalystId != null
                && (order.Status == ApprovalStatus.Pending || order.Status == ApprovalStatus.InProgress
                    || order.Status == ApprovalStatus.RetestRequested))
            .Select(order => new
            {
                order.Id,
                order.TestCode,
                AnalystId = order.AssignedAnalystId!.Value,
                SampleReference = order.Sample == null ? string.Empty : order.Sample.ReferenceNumber
            })
            .ToListAsync(cancellationToken);

        var analysts = new List<AnalystImpactDto>();
        foreach (var group in orders.GroupBy(order => order.AnalystId).OrderBy(group => group.Key))
        {
            var decisions = await guard.EvaluateIgnoringGateAsync(
                group.Key, group.Select(order => order.Id).ToArray(), cancellationToken);
            var refused = group
                .Where(order => !decisions[order.Id].Allowed)
                .Select(order => new ImpactOrderDto(order.Id, order.TestCode, order.SampleReference, decisions[order.Id].Reasons))
                .ToList();
            if (refused.Count == 0)
                continue;

            var name = await db.Users.AsNoTracking().Where(user => user.Id == group.Key)
                .Select(user => user.FullName)
                .SingleOrDefaultAsync(cancellationToken) ?? group.Key.ToString(CultureInfo.InvariantCulture);
            analysts.Add(new AnalystImpactDto(group.Key, name, refused));
        }

        return new GateImpactDto(sectionId, analysts, analysts.Sum(analyst => analyst.Orders.Count));
    }

    public async Task<CompetencySettingDto> SetGateAsync(
        int sectionId, bool enabled, string reason, string password, uint? expectedVersion,
        int actingUserId, CancellationToken cancellationToken = default)
    {
        await RequireManageAsync(actingUserId, cancellationToken);
        await RequireSectionAsync(actingUserId, sectionId, cancellationToken);

        var reasonText = reason?.Trim();
        if (string.IsNullOrEmpty(reasonText))
            throw new BusinessRuleException(MessageCodes.CompetencyGateReasonRequired,
                "A reason is required to change the competency gate.");

        return await UnitOfWork.RunAsync(db, async () =>
        {
            var row = await db.CompetencySectionSettings
                .FirstOrDefaultAsync(setting => setting.SectionId == sectionId, cancellationToken);
            // The version the caller loaded must be the current one; no row reads as version 0.
            if ((expectedVersion ?? 0u) != (row?.Version ?? 0u))
                throw StaleVersion();
            if (row is null)
            {
                row = new CompetencySectionSetting { SectionId = sectionId };
                db.CompetencySectionSettings.Add(row);
            }

            var signature = await signatures.SignAsync(actingUserId, password ?? string.Empty,
                SignatureMeaning.CompetencyGateChanged, nameof(CompetencySectionSetting), sectionId,
                reasonText, null);
            signature.SignedAt = clock.UtcNow.UtcDateTime;

            row.GateEnabled = enabled;
            row.ChangedAt = signature.SignedAt;
            row.ChangedByUserId = actingUserId;

            var previousActor = db.CurrentUserId;
            var previousReason = db.CurrentReason;
            db.CurrentUserId = actingUserId;
            db.CurrentReason = reasonText;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                db.CurrentUserId = previousActor;
                db.CurrentReason = previousReason;
            }

            var changedBy = await db.Users.AsNoTracking().Where(user => user.Id == actingUserId)
                .Select(user => (string?)user.FullName)
                .SingleOrDefaultAsync(cancellationToken);
            return new CompetencySettingDto(sectionId, row.GateEnabled, row.ReminderDaysBeforeExpiry,
                row.ChangedAt, changedBy, row.Version);
        });
    }

    private static BusinessRuleException StaleVersion() =>
        new(MessageCodes.CompetencyGateVersionStale,
            "The competency settings changed after they were loaded. Reload and try again.");

    private async Task<CompetencySettingDto> ReadAsync(int sectionId, CancellationToken cancellationToken)
    {
        var setting = await db.CompetencySectionSettings.AsNoTracking()
            .Where(setting => setting.SectionId == sectionId)
            .Select(setting => new CompetencySettingDto(
                setting.SectionId, setting.GateEnabled, setting.ReminderDaysBeforeExpiry, setting.ChangedAt,
                setting.ChangedByUser == null ? null : setting.ChangedByUser.FullName, setting.Version))
            .SingleOrDefaultAsync(cancellationToken);
        return setting ?? new CompetencySettingDto(sectionId, false, null, null, null, 0u);
    }

    private async Task RequireManageAsync(int actingUserId, CancellationToken cancellationToken)
    {
        if (!await permissions.UserHasPermissionAsync(actingUserId, PermissionConstants.CompetencyManage, cancellationToken))
            throw new CodedAccessDeniedException(MessageCodes.CompetencyManageRequired,
                "You do not have permission to manage analyst qualifications.");
    }

    private async Task RequireSectionAsync(int actingUserId, int sectionId, CancellationToken cancellationToken)
    {
        var configurable = await scope.GetConfigurableSectionIdsAsync(actingUserId, cancellationToken);
        if (configurable is not null && !configurable.Contains(sectionId))
            throw new CodedAccessDeniedException(MessageCodes.CompetencySectionScopeDenied,
                "You do not have access to competency records for this laboratory section.");
    }
}

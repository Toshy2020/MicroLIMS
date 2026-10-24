using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services;

// Live figures for the cards on a laboratory's section page ("3 overdue",
// "1 expiring within 7 days"), so the page says what needs doing instead of
// only describing each page.
//
// Every figure reuses a rule that already exists elsewhere - the workspace
// work queues, the media expiry warning window, Material.Status, the
// calibration-overdue check, the working-standard due-soon window - rather
// than defining a second version of a GMP rule here. A card with nothing to
// report returns no counters, and a figure of zero is still returned so
// "0 overdue" can read as good news.
public class SectionCountersService
{
    // Same window as DashboardNotificationService's media expiry warning and
    // MediaExpiryService's default.
    public static readonly TimeSpan MediaExpiryWarningWindow = TimeSpan.FromDays(7);

    private readonly IMicroLimsDbContext _db;
    private readonly DashboardLabScopeService _labScope;
    private readonly IUserSectionScopeService _sectionScope;
    private readonly ITestWorkspaceService _workspace;
    private readonly ILabClock _clock;
    private readonly TimeProvider _time;

    public SectionCountersService(
        IMicroLimsDbContext db,
        DashboardLabScopeService labScope,
        IUserSectionScopeService sectionScope,
        ITestWorkspaceService workspace,
        ILabClock clock,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _labScope = labScope;
        _sectionScope = sectionScope;
        _workspace = workspace;
        _clock = clock;
        _time = timeProvider ?? TimeProvider.System;
    }

    // Labels are read after the number ("1 open run", "3 open runs").
    private static string Plural(int value, string one, string many) => value == 1 ? one : many;

    public async Task<IReadOnlyList<SectionCardCountersDto>> GetAsync(
        int userId, string labCode, SectionCardAccess access, CancellationToken ct = default)
    {
        // Throws for an unknown laboratory or one the user does not belong to.
        var scope = await _labScope.ResolveAsync(userId, labCode, ct);
        var sectionIds = scope.SectionIds ?? Array.Empty<int>();
        if (sectionIds.Count == 0) return Array.Empty<SectionCardCountersDto>();

        var cards = new List<SectionCardCountersDto>();
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        // Material.Status and EquipmentInventory.IsCalibrationOverdue compare
        // against the UTC calendar date; the figures follow the same rule so a
        // card never disagrees with the list it links to.
        var todayUtc = nowUtc.Date;

        if (scope.IsMicrobiology)
        {
            cards.Add(await WorkspaceCardAsync(SectionCardKeys.Workspace, userId, sectionIds[0], null));
            if (access.Media) cards.Add(await MediaCardAsync(sectionIds, nowUtc, ct));
            if (access.Cryovials) cards.Add(await CryovialCardAsync(sectionIds, todayUtc, ct));
        }
        else
        {
            // Only the areas this user works in have a workspace card.
            var areas = await _sectionScope.GetPhyschemAreasAsync(userId, ct);
            if (areas.Contains(WorkspaceArea.Fp))
                cards.Add(await WorkspaceCardAsync(SectionCardKeys.FpWorkspace, userId, sectionIds[0], "fp"));
            if (areas.Contains(WorkspaceArea.RmPm))
                cards.Add(await WorkspaceCardAsync(SectionCardKeys.RmPmWorkspace, userId, sectionIds[0], "rmpm"));
            if (access.Solutions) cards.Add(await SolutionCardAsync(sectionIds, ct));
            cards.Add(await WorkingStandardCardAsync(sectionIds, ct));
            cards.Add(await ChromatographyCardAsync(SectionCardKeys.Hplc, HplcTechnique.Hplc, sectionIds, ct));
            cards.Add(await ChromatographyCardAsync(SectionCardKeys.Gc, HplcTechnique.Gc, sectionIds, ct));
            cards.Add(await IcpCardAsync(sectionIds, ct));
        }

        if (access.Materials) cards.Add(await MaterialCardAsync(sectionIds, todayUtc, ct));
        if (access.Equipment) cards.Add(await EquipmentCardAsync(sectionIds, todayUtc, ct));

        return cards;
    }

    // The Document Control section page: the caller's own reading list. Every
    // signed-in user has one, so no laboratory scope or page policy applies.
    public async Task<IReadOnlyList<SectionCardCountersDto>> GetDocumentControlAsync(int userId, CancellationToken ct = default)
    {
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var mine = _db.DocumentTrainingAssignments.AsNoTracking().Where(a => a.AssignedUserId == userId);

        // My Reading List's tabs (categoryOf in MyReadingListPage.tsx, per
        // ML-DC-FRS-1C-001 section 2): Completed and Superseded first, then
        // Overdue - past its due date (TrainingAssignmentService's isOverdue)
        // or marked Overdue - and only then To read, so nothing counts twice.
        var open = mine.Where(a =>
            a.Status != TrainingAssignmentStatus.Acknowledged
            && a.Status != TrainingAssignmentStatus.CompletedPassed
            && a.Status != TrainingAssignmentStatus.SupersededIncomplete
            && a.Status != TrainingAssignmentStatus.TrainedOnSupersededOnly);
        var overdue = await open.CountAsync(a =>
            a.Status == TrainingAssignmentStatus.Overdue
            || (a.DueDateUtc < nowUtc && a.Status != TrainingAssignmentStatus.Cancelled), ct);
        var toRead = await open.CountAsync(a =>
            (a.Status == TrainingAssignmentStatus.Assigned || a.Status == TrainingAssignmentStatus.Reading)
            && a.DueDateUtc >= nowUtc, ct);

        return new[]
        {
            new SectionCardCountersDto(SectionCardKeys.ReadingList, new[]
            {
                new SectionCounterDto("to read", toRead, toRead > 0 ? "info" : "pending"),
                new SectionCounterDto("overdue", overdue, overdue > 0 ? "detected" : "pending")
            })
        };
    }

    // The same queues as the workspace's own tabs (GET /api/testorders/counts).
    // One active section exists per laboratory code, so the first is the lab.
    private async Task<SectionCardCountersDto> WorkspaceCardAsync(string key, int userId, int labSectionId, string? area)
    {
        var counts = await _workspace.GetWorkloadCountsAsync(userId, labSectionId, area);
        return new SectionCardCountersDto(key, new[]
        {
            new SectionCounterDto("overdue", counts.Overdue, counts.Overdue > 0 ? "detected" : "pending"),
            new SectionCounterDto("awaiting review", counts.AwaitingReview, counts.AwaitingReview > 0 ? "action" : "pending")
        });
    }

    private async Task<SectionCardCountersDto> MediaCardAsync(IReadOnlyList<int> sectionIds, DateTime nowUtc, CancellationToken ct)
    {
        var lots = _db.Media.AsNoTracking().Where(m => sectionIds.Contains(m.Material!.SectionId));
        var horizon = nowUtc.Add(MediaExpiryWarningWindow);
        var active = await lots.CountAsync(m => m.Status == MediaStatus.Active, ct);
        var expiring = await lots.CountAsync(m => m.Status == MediaStatus.Active && m.ExpiryDate <= horizon, ct);
        var awaitingRelease = await lots.CountAsync(m => m.Status == MediaStatus.Prepared, ct);
        return new SectionCardCountersDto(SectionCardKeys.Media, new[]
        {
            new SectionCounterDto(Plural(active, "active lot", "active lots"), active, "pending"),
            new SectionCounterDto("expiring within 7 days", expiring, expiring > 0 ? "action" : "pending"),
            new SectionCounterDto("awaiting release", awaitingRelease, awaitingRelease > 0 ? "info" : "pending")
        });
    }

    // Usable vials: an approved, not destroyed batch whose expiry date has not
    // passed - the condition CryovialThaw checks before a vial may be thawed.
    private async Task<SectionCardCountersDto> CryovialCardAsync(IReadOnlyList<int> sectionIds, DateTime todayUtc, CancellationToken ct)
    {
        var batches = _db.Cryovials.AsNoTracking()
            .Where(c => sectionIds.Contains(c.Material!.SectionId) && !c.IsDestroyed);
        var available = await batches
            .Where(c => c.ApprovalStatus == ApprovalGateStatus.Approved && c.ExpiryDate >= todayUtc)
            .SumAsync(c => (int?)c.VialsRemaining, ct) ?? 0;
        var awaitingApproval = await batches.CountAsync(c => c.ApprovalStatus == ApprovalGateStatus.PendingReview, ct);
        return new SectionCardCountersDto(SectionCardKeys.Cryovials, new[]
        {
            new SectionCounterDto(Plural(available, "vial available", "vials available"), available, "pending"),
            new SectionCounterDto("awaiting approval", awaitingApproval, awaitingApproval > 0 ? "action" : "pending")
        });
    }

    // Material.Status: Expired once the expiry date has passed; low stock is
    // the lot's own optional MinimumStockLevel, for lots still in stock.
    private async Task<SectionCardCountersDto> MaterialCardAsync(IReadOnlyList<int> sectionIds, DateTime todayUtc, CancellationToken ct)
    {
        var lots = _db.Materials.AsNoTracking().Where(m => sectionIds.Contains(m.SectionId));
        var expired = await lots.CountAsync(m => m.ExpiryDate.HasValue && m.ExpiryDate.Value < todayUtc, ct);
        var lowStock = await lots.CountAsync(m =>
            m.MinimumStockLevel.HasValue
            && m.QuantityRemaining > 0
            && m.QuantityRemaining <= m.MinimumStockLevel.Value
            && !(m.ExpiryDate.HasValue && m.ExpiryDate.Value < todayUtc), ct);
        return new SectionCardCountersDto(SectionCardKeys.Materials, new[]
        {
            new SectionCounterDto("expired", expired, expired > 0 ? "detected" : "pending"),
            new SectionCounterDto("low stock", lowStock, lowStock > 0 ? "action" : "pending")
        });
    }

    // EquipmentInventory.IsCalibrationOverdue, for equipment not retired.
    private async Task<SectionCardCountersDto> EquipmentCardAsync(IReadOnlyList<int> sectionIds, DateTime todayUtc, CancellationToken ct)
    {
        var items = _db.EquipmentInventories.AsNoTracking()
            .Where(e => e.SectionId.HasValue && sectionIds.Contains(e.SectionId.Value) && e.Status != EquipmentOperationalStatus.Retired);
        var calibrationOverdue = await items.CountAsync(e => e.CalibrationDueDate.HasValue && e.CalibrationDueDate.Value < todayUtc, ct);
        var outOfService = await items.CountAsync(e => e.Status == EquipmentOperationalStatus.OutOfService, ct);
        return new SectionCardCountersDto(SectionCardKeys.Equipment, new[]
        {
            new SectionCounterDto("calibration overdue", calibrationOverdue, calibrationOverdue > 0 ? "detected" : "pending"),
            new SectionCounterDto("out of service", outOfService, outOfService > 0 ? "action" : "pending")
        });
    }

    private async Task<SectionCardCountersDto> SolutionCardAsync(IReadOnlyList<int> sectionIds, CancellationToken ct)
    {
        var inProgress = await _db.SolutionPreparations.AsNoTracking()
            .CountAsync(p => sectionIds.Contains(p.SectionId) && p.Status == SolutionPreparationStatus.InProgress, ct);
        return new SectionCardCountersDto(SectionCardKeys.Solutions, new[]
        {
            new SectionCounterDto("in progress", inProgress, inProgress > 0 ? "info" : "pending")
        });
    }

    // WorkingStandardService's lot status: Expired before today, DueSoon within
    // WorkingStandardRules.DueSoonDays, on the laboratory's calendar; a
    // depleted lot is neither.
    private async Task<SectionCardCountersDto> WorkingStandardCardAsync(IReadOnlyList<int> sectionIds, CancellationToken ct)
    {
        var today = _clock.LabToday;
        var expiries = await _db.Materials.AsNoTracking()
            .Where(m => m.MaterialType == MaterialType.WorkingStandard
                        && sectionIds.Contains(m.SectionId)
                        && m.QuantityRemaining > 0
                        && m.ExpiryDate.HasValue)
            .Select(m => m.ExpiryDate!.Value)
            .ToListAsync(ct);
        var days = expiries.Select(DateOnly.FromDateTime).ToList();
        var expired = days.Count(d => d < today);
        var dueSoon = days.Count(d => d >= today && d <= today.AddDays(WorkingStandardRules.DueSoonDays));
        return new SectionCardCountersDto(SectionCardKeys.WorkingStandards, new[]
        {
            new SectionCounterDto("expired", expired, expired > 0 ? "detected" : "pending"),
            new SectionCounterDto($"due within {WorkingStandardRules.DueSoonDays} days", dueSoon, dueSoon > 0 ? "action" : "pending")
        });
    }

    private async Task<SectionCardCountersDto> ChromatographyCardAsync(string key, HplcTechnique technique, IReadOnlyList<int> sectionIds, CancellationToken ct)
    {
        var open = await _db.HplcRuns.AsNoTracking()
            .CountAsync(r => sectionIds.Contains(r.SectionId) && r.Status == HplcRunStatus.Open && r.HplcMethod!.Technique == technique, ct);
        return new SectionCardCountersDto(key, new[] { new SectionCounterDto(Plural(open, "open run", "open runs"), open, open > 0 ? "info" : "pending") });
    }

    private async Task<SectionCardCountersDto> IcpCardAsync(IReadOnlyList<int> sectionIds, CancellationToken ct)
    {
        var open = await _db.IcpRuns.AsNoTracking()
            .CountAsync(r => sectionIds.Contains(r.SectionId) && r.Status == IcpRunStatus.Open, ct);
        return new SectionCardCountersDto(SectionCardKeys.Icp, new[] { new SectionCounterDto(Plural(open, "open run", "open runs"), open, open > 0 ? "info" : "pending") });
    }
}

using System.Text.Json;
using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

public record StartPreparationRequest(int SolutionMasterId, int? HplcMethodId);
public record PreparationComponentInput(int ComponentId, int? MaterialId, decimal? QuantityUsed);
public record SavePreparationRequest(List<PreparationComponentInput> Components, decimal? FinalVolumeMl, decimal? MeasuredPh);
public record CompletePreparationRequest(string Password, string? Comment);
public record LotOption(int MaterialId, string BatchNumber, DateTime? ExpiryDate, decimal QuantityRemaining, MaterialUnit Unit, bool Usable, string? Reason);

// Solution Preparation area (HPLC chain S4, spec 4): prepares Mobile
// Phases, Diluents and Titrants from the Solution master against real
// Material stock lots. Start snapshots the master (D7); Save records the
// picked lots and quantities; Complete is one signed SaveChangesAsync that
// deducts stock (MaterialService.ConsumeAsync), allocates a code
// (SolutionPreparationCode) and sets an expiry. Cancel/Discard close the
// record without restoring stock. ExpireDueAsync is called by
// SolutionPreparationExpiryWorker (API layer) every 5 minutes.
public class SolutionPreparationService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly MaterialService _materials;
    private readonly IElectronicSignatureService _signatures;
    private readonly ILabClock _clock;

    public SolutionPreparationService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        MaterialService materials,
        IElectronicSignatureService signatures,
        ILabClock? clock = null)
    {
        _db = db;
        _scope = scope;
        _materials = materials;
        _signatures = signatures;
        _clock = clock ?? LabClock.Default;
    }

    public async Task<List<SolutionPreparationListItem>> GetAllAsync(int userId, SolutionPreparationStatus? status, SolutionType? type, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        var query = ListQuery();

        if (scope != null) query = query.Where(p => scope.Contains(p.SectionId));
        if (type.HasValue) query = query.Where(p => p.Type == type.Value);

        var rows = await query.OrderByDescending(p => p.StartedAt).ToListAsync(ct);
        var items = await ToListItemsAsync(rows, ct);

        return status.HasValue ? items.Where(i => i.EffectiveStatus == status.Value).ToList() : items;
    }

    public async Task<SolutionPreparationResponse> GetByIdAsync(int id, int userId, CancellationToken ct = default)
    {
        var prep = await LoadPrepAsync(id, ct);
        await EnsureAccessAsync(prep, userId, ct);
        return await BuildResponseAsync(prep, ct);
    }

    public async Task<SolutionPreparationResponse> StartAsync(StartPreparationRequest r, int userId, CancellationToken ct = default)
    {
        var solution = await LoadSolutionForSnapshotAsync(r.SolutionMasterId, ct)
            ?? throw new NotFoundException($"Solution master {r.SolutionMasterId} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(solution.SectionId))
            throw new NotFoundException($"Solution master {r.SolutionMasterId} not found.");

        if (!solution.IsActive)
            throw new InvalidOperationException($"Solution \"{solution.Name}\" is inactive.");

        HplcMethod? method = null;
        if (solution.Type == SolutionType.MobilePhase)
        {
            if (!r.HplcMethodId.HasValue)
                throw new InvalidOperationException("An HPLC method is required to start a mobile phase preparation.");

            method = await _db.HplcMethods.Include(m => m.MobilePhases)
                .FirstOrDefaultAsync(m => m.Id == r.HplcMethodId.Value, ct)
                ?? throw new NotFoundException($"HPLC method {r.HplcMethodId} not found.");

            if (method.SectionId != solution.SectionId)
                throw new InvalidOperationException("The HPLC method belongs to another laboratory.");

            if (!method.IsActive)
                throw new InvalidOperationException($"HPLC method \"{method.Name}\" is inactive.");

            if (!method.MobilePhases.Any(mp => mp.SolutionMasterId == solution.Id))
                throw new InvalidOperationException("That method does not use this mobile phase.");
        }
        else if (r.HplcMethodId.HasValue)
        {
            throw new InvalidOperationException("An HPLC method only applies to mobile phase preparations.");
        }

        var snapshotJson = JsonSerializer.Serialize(SolutionMasterResponse.From(solution), JsonOptions);
        var startedAt = _clock.UtcNow.UtcDateTime;

        var prep = new SolutionPreparation
        {
            SectionId = solution.SectionId,
            SolutionMasterId = solution.Id,
            Type = solution.Type,
            RecipeSnapshotJson = snapshotJson,
            HplcMethodId = method?.Id,
            Status = SolutionPreparationStatus.InProgress,
            StartedByUserId = userId,
            StartedAt = startedAt,
        };

        var order = 1;
        foreach (var c in solution.Components.OrderBy(c => c.Order))
        {
            prep.Components.Add(new SolutionPreparationComponent
            {
                Order = order++,
                MaterialMasterEntryId = c.MaterialMasterEntryId,
                EntryCode = c.MaterialMasterEntry?.Code ?? string.Empty,
                EntryName = c.MaterialMasterEntry?.Name ?? string.Empty,
                RecipeQuantity = c.Quantity,
                RecipeUnit = c.Unit,
            });
        }

        prep.StatusHistory.Add(new SolutionPreparationStatusHistory
        {
            FromStatus = null,
            ToStatus = SolutionPreparationStatus.InProgress,
            ChangedByUserId = userId,
            ChangedAt = startedAt,
        });

        _db.CurrentUserId = userId;
        _db.SolutionPreparations.Add(prep);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(prep.Id, userId, ct);
    }

    public async Task<SolutionPreparationResponse> SaveAsync(int id, SavePreparationRequest r, int userId, CancellationToken ct = default)
    {
        var prep = await LoadPrepAsync(id, ct);
        await EnsureAccessAsync(prep, userId, ct);

        if (prep.Status != SolutionPreparationStatus.InProgress)
            throw new InvalidOperationException("This preparation is closed.");

        var today = _clock.LabToday;

        foreach (var input in r.Components)
        {
            var component = prep.Components.FirstOrDefault(c => c.Id == input.ComponentId)
                ?? throw new InvalidOperationException($"Component {input.ComponentId} does not belong to this preparation.");

            if (!input.MaterialId.HasValue)
            {
                component.MaterialId = null;
                component.QuantityUsed = null;
                continue;
            }

            if (input.QuantityUsed.HasValue && input.QuantityUsed.Value <= 0)
                throw new InvalidOperationException($"Quantity used for \"{component.EntryName}\" must be greater than zero.");

            var lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == input.MaterialId.Value, ct)
                ?? throw new NotFoundException($"Material {input.MaterialId} not found.");

            if (lot.SectionId != prep.SectionId)
                throw new InvalidOperationException($"Lot \"{lot.BatchNumber}\" belongs to another laboratory.");

            var check = LotUsability.Check(lot, component.MaterialMasterEntryId, input.QuantityUsed, today);
            if (!check.Usable)
                throw new InvalidOperationException($"{component.EntryName} (batch {lot.BatchNumber}): {check.Reason}");

            component.MaterialId = lot.Id;
            component.QuantityUsed = input.QuantityUsed;
        }

        if (r.FinalVolumeMl.HasValue && r.FinalVolumeMl.Value <= 0)
            throw new InvalidOperationException("Final volume must be greater than zero.");

        if (r.MeasuredPh.HasValue && (r.MeasuredPh.Value < 0 || r.MeasuredPh.Value > 14))
            throw new InvalidOperationException("Measured pH must be between 0 and 14.");

        prep.FinalVolumeMl = r.FinalVolumeMl;
        prep.MeasuredPh = r.MeasuredPh;

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, userId, ct);
    }

    public async Task<List<LotOption>> GetLotOptionsAsync(int id, int componentId, int userId, CancellationToken ct = default)
    {
        var prep = await LoadPrepAsync(id, ct);
        await EnsureAccessAsync(prep, userId, ct);

        var component = prep.Components.FirstOrDefault(c => c.Id == componentId)
            ?? throw new NotFoundException($"Component {componentId} not found on this preparation.");

        var today = _clock.LabToday;
        var lots = await _db.Materials.AsNoTracking()
            .Where(m => m.SectionId == prep.SectionId && m.MaterialMasterEntryId == component.MaterialMasterEntryId)
            .OrderBy(m => m.ExpiryDate)
            .ToListAsync(ct);

        return lots.Select(lot =>
        {
            var check = LotUsability.Check(lot, component.MaterialMasterEntryId, component.QuantityUsed, today);
            return new LotOption(lot.Id, lot.BatchNumber, lot.ExpiryDate, lot.QuantityRemaining, lot.Unit, check.Usable, check.Reason);
        }).ToList();
    }

    // One SaveChangesAsync: signs first (a wrong password must leave
    // nothing else persisted - ElectronicSignatureService.SignAsync's
    // failure path saves its own audit row immediately, so nothing may be
    // mutated before it succeeds), then consumes stock and allocates the
    // code. A code clash is reported, not retried (Review Focus: "one
    // succeeds, the other gets the clash message and can sign again").
    public async Task<SolutionPreparationResponse> CompleteAsync(int id, CompletePreparationRequest r, int userId, string? ip, CancellationToken ct = default)
    {
        var prep = await LoadPrepAsync(id, ct);
        await EnsureAccessAsync(prep, userId, ct);

        if (prep.Status != SolutionPreparationStatus.InProgress)
            throw new InvalidOperationException("This preparation is closed.");

        if (prep.Components.Any(c => !c.MaterialId.HasValue || !c.QuantityUsed.HasValue))
            throw new InvalidOperationException("Every component needs a lot and a quantity before completion.");

        if (!prep.FinalVolumeMl.HasValue)
            throw new InvalidOperationException("Final volume is required before completion.");

        var snapshot = JsonSerializer.Deserialize<SolutionMasterResponse>(prep.RecipeSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("The preparation's recipe snapshot could not be read.");

        if (snapshot.PhTarget.HasValue)
        {
            if (!prep.MeasuredPh.HasValue)
                throw new InvalidOperationException("Measured pH is required before completion.");

            if (snapshot.PhTolerance.HasValue)
            {
                var diff = Math.Abs(prep.MeasuredPh.Value - snapshot.PhTarget.Value);
                if (diff > snapshot.PhTolerance.Value)
                    throw new InvalidOperationException(
                        $"Measured pH {prep.MeasuredPh.Value} is outside {snapshot.PhTarget.Value} ± {snapshot.PhTolerance.Value}.");
            }
        }

        var today = _clock.LabToday;
        var picks = new List<(SolutionPreparationComponent Component, Material Lot)>();
        foreach (var component in prep.Components)
        {
            var lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == component.MaterialId!.Value, ct)
                ?? throw new InvalidOperationException($"{component.EntryName}: the selected lot no longer exists.");

            var check = LotUsability.Check(lot, component.MaterialMasterEntryId, component.QuantityUsed, today);
            if (!check.Usable)
                throw new InvalidOperationException($"{component.EntryName} (batch {lot.BatchNumber}): {check.Reason}");

            picks.Add((component, lot));
        }

        // Signs first - see the method comment. Only after this succeeds
        // does anything below mutate tracked state.
        var signature = await _signatures.SignAsync(
            userId, r.Password, SignatureMeaning.PreparationConfirmed, "SolutionPreparation", prep.Id, r.Comment, ip);

        foreach (var (component, lot) in picks)
        {
            await _materials.ConsumeAsync(lot.Id, lot.MaterialType, component.QuantityUsed!.Value, userId);
        }

        var nowUtc = _clock.UtcNow.UtcDateTime;
        var labLocal = _clock.ToLabLocal(nowUtc);
        var methodAbbreviation = prep.Type == SolutionType.MobilePhase ? prep.HplcMethod?.Abbreviation : null;
        var head = SolutionPreparationCode.Head(prep.Type, methodAbbreviation);
        prep.Code = await SolutionPreparationCode.NextAsync(
            _db.SolutionPreparations.Where(p => p.Code != null).Select(p => p.Code!), head, labLocal, ct);

        prep.PreparedByUserId = userId;
        prep.PreparedAt = nowUtc;
        prep.ExpiresAt = ShelfLifeExpiry(nowUtc, snapshot.ShelfLifeValue, snapshot.ShelfLifeUnit);
        prep.Status = SolutionPreparationStatus.Prepared;
        prep.Signature = signature;

        prep.StatusHistory.Add(new SolutionPreparationStatusHistory
        {
            FromStatus = SolutionPreparationStatus.InProgress,
            ToStatus = SolutionPreparationStatus.Prepared,
            ChangedByUserId = userId,
            ChangedAt = nowUtc,
            Reason = r.Comment,
        });

        _db.CurrentUserId = userId;
        if (!await _db.TrySaveChangesAsync(UniqueIndexNames.SolutionPreparationCode))
            throw new InvalidOperationException("Another preparation took that code at the same moment - sign again.");

        return await GetByIdAsync(id, userId, ct);
    }

    public async Task<SolutionPreparationResponse> CancelAsync(int id, string reason, int userId, CancellationToken ct = default)
    {
        var prep = await LoadPrepAsync(id, ct);
        await EnsureAccessAsync(prep, userId, ct);

        if (prep.Status != SolutionPreparationStatus.InProgress)
            throw new InvalidOperationException("This preparation is closed.");

        var trimmedReason = ValidateReason(reason);
        var now = _clock.UtcNow.UtcDateTime;

        prep.Status = SolutionPreparationStatus.Cancelled;
        prep.StatusHistory.Add(new SolutionPreparationStatusHistory
        {
            FromStatus = SolutionPreparationStatus.InProgress,
            ToStatus = SolutionPreparationStatus.Cancelled,
            ChangedByUserId = userId,
            ChangedAt = now,
            Reason = trimmedReason,
        });

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, userId, ct);
    }

    public async Task<SolutionPreparationResponse> DiscardAsync(int id, string reason, int userId, CancellationToken ct = default)
    {
        var prep = await LoadPrepAsync(id, ct);
        await EnsureAccessAsync(prep, userId, ct);

        var now = _clock.UtcNow.UtcDateTime;
        if (SolutionPreparationResponse.EffectiveStatusOf(prep, now) != SolutionPreparationStatus.Prepared)
            throw new InvalidOperationException("This preparation is closed.");

        var trimmedReason = ValidateReason(reason);

        prep.Status = SolutionPreparationStatus.Discarded;
        prep.StatusHistory.Add(new SolutionPreparationStatusHistory
        {
            FromStatus = SolutionPreparationStatus.Prepared,
            ToStatus = SolutionPreparationStatus.Discarded,
            ChangedByUserId = userId,
            ChangedAt = now,
            Reason = trimmedReason,
        });

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, userId, ct);
    }

    // Prepared + unexpired, for the given solution (and, for a mobile
    // phase, the method it was prepared for). S6 (HPLC workspace) uses
    // this to offer preparations for a run's mobile phase channels.
    public async Task<List<SolutionPreparationListItem>> GetAvailableAsync(int userId, int solutionMasterId, int? hplcMethodId, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        var now = _clock.UtcNow.UtcDateTime;

        var query = ListQuery().Where(p =>
            p.SolutionMasterId == solutionMasterId
            && p.Status == SolutionPreparationStatus.Prepared
            && (!p.ExpiresAt.HasValue || p.ExpiresAt.Value > now));

        if (scope != null) query = query.Where(p => scope.Contains(p.SectionId));
        if (hplcMethodId.HasValue) query = query.Where(p => p.HplcMethodId == hplcMethodId.Value);

        var rows = await query.OrderByDescending(p => p.PreparedAt).ToListAsync(ct);
        return await ToListItemsAsync(rows, ct);
    }

    // Traceability: which preparations consumed a given stock lot (Material
    // lot view "Consumed by preparations" list).
    public async Task<List<SolutionPreparationListItem>> GetByLotAsync(int materialId, int userId, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        var query = ListQuery().Where(p => p.Components.Any(c => c.MaterialId == materialId));

        if (scope != null) query = query.Where(p => scope.Contains(p.SectionId));

        var rows = await query.OrderByDescending(p => p.StartedAt).ToListAsync(ct);
        return await ToListItemsAsync(rows, ct);
    }

    // SolutionPreparationExpiryWorker calls this every 5 minutes. Every
    // read also treats Prepared && ExpiresAt <= now as Expired, so this
    // only matters for what a plain Status filter sees, not correctness.
    public async Task<int> ExpireDueAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow.UtcDateTime;
        var due = await _db.SolutionPreparations
            .Where(p => p.Status == SolutionPreparationStatus.Prepared && p.ExpiresAt != null && p.ExpiresAt <= now)
            .ToListAsync(ct);

        if (due.Count == 0) return 0;

        foreach (var p in due)
        {
            p.Status = SolutionPreparationStatus.Expired;
            _db.SolutionPreparationStatusHistories.Add(new SolutionPreparationStatusHistory
            {
                SolutionPreparationId = p.Id,
                FromStatus = SolutionPreparationStatus.Prepared,
                ToStatus = SolutionPreparationStatus.Expired,
                ChangedByUserId = null,
                ChangedAt = now,
                Reason = "Expired automatically",
            });
        }

        _db.CurrentUserId = null;
        await _db.SaveChangesAsync(ct);
        return due.Count;
    }

    private static DateTime ShelfLifeExpiry(DateTime preparedAtUtc, int shelfLifeValue, ShelfLifeUnit unit) =>
        unit == ShelfLifeUnit.Hours ? preparedAtUtc.AddHours(shelfLifeValue) : preparedAtUtc.AddDays(shelfLifeValue);

    private static string ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required.");
        var trimmed = reason.Trim();
        if (trimmed.Length > 500)
            throw new InvalidOperationException("Reason cannot exceed 500 characters.");
        return trimmed;
    }

    private async Task EnsureAccessAsync(SolutionPreparation prep, int userId, CancellationToken ct)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(prep.SectionId))
            throw new NotFoundException($"Solution preparation {prep.Id} not found.");
    }

    private async Task<SolutionPreparation> LoadPrepAsync(int id, CancellationToken ct) =>
        await _db.SolutionPreparations
            .Include(p => p.Section)
            .Include(p => p.SolutionMaster)
            .Include(p => p.HplcMethod)
            .Include(p => p.Components).ThenInclude(c => c.Material)
            .Include(p => p.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException($"Solution preparation {id} not found.");

    private async Task<SolutionMaster?> LoadSolutionForSnapshotAsync(int id, CancellationToken ct) =>
        await _db.SolutionMasters
            .Include(s => s.Section)
            .Include(s => s.PhAdjustingEntry)
            .Include(s => s.StandardEntry)
            .Include(s => s.ReferenceSolution)
            .Include(s => s.Components).ThenInclude(c => c.MaterialMasterEntry)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    private IQueryable<SolutionPreparation> ListQuery() =>
        _db.SolutionPreparations
            .Include(p => p.SolutionMaster)
            .Include(p => p.HplcMethod)
            .Include(p => p.Components)
            .AsNoTracking();

    private async Task<List<SolutionPreparationListItem>> ToListItemsAsync(List<SolutionPreparation> rows, CancellationToken ct)
    {
        var now = _clock.UtcNow.UtcDateTime;
        var userIds = rows.Where(p => p.PreparedByUserId.HasValue).Select(p => p.PreparedByUserId!.Value).Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return rows.Select(p => new SolutionPreparationListItem(
            p.Id, p.Code, p.Type, p.SolutionMaster?.Name ?? string.Empty, p.HplcMethod?.Abbreviation,
            SolutionPreparationResponse.EffectiveStatusOf(p, now), p.PreparedAt, p.ExpiresAt,
            p.PreparedByUserId.HasValue && names.TryGetValue(p.PreparedByUserId.Value, out var n) ? n : null))
            .ToList();
    }

    private async Task<SolutionPreparationResponse> BuildResponseAsync(SolutionPreparation prep, CancellationToken ct)
    {
        var userIds = new HashSet<int> { prep.StartedByUserId };
        if (prep.PreparedByUserId.HasValue) userIds.Add(prep.PreparedByUserId.Value);
        foreach (var h in prep.StatusHistory)
            if (h.ChangedByUserId.HasValue) userIds.Add(h.ChangedByUserId.Value);

        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return SolutionPreparationResponse.From(prep, names, _clock.UtcNow.UtcDateTime);
    }
}

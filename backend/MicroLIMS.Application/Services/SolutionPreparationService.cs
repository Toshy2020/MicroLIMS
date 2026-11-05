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
// Material lines send MaterialId + QuantityUsed; PreparedSolution lines send SourcePreparationId
// (QuantityUsed optional, never deducted); Text lines send nothing.
public record PreparationComponentInput(int ComponentId, int? MaterialId, decimal? QuantityUsed, int? SourcePreparationId = null);
// The autoclave fields are required at completion when the recipe needs sterilization, and
// must be left null otherwise.
public record SavePreparationRequest(
    List<PreparationComponentInput> Components, decimal? FinalVolumeMl, decimal? MeasuredPh,
    int? AutoclaveEquipmentId = null, string? AutoclaveProgram = null, string? LoadType = null,
    decimal? AutoclaveTemperature = null, int? AutoclaveCycleTime = null, int? AutoclaveCycleNumber = null);
public record CompletePreparationRequest(string Password, string? Comment);
public record LotOption(int MaterialId, string BatchNumber, DateTime? ExpiryDate, decimal QuantityRemaining, MaterialUnit Unit, bool Usable, string? Reason, decimal? Purity);
public record SourcePreparationOption(int PreparationId, string? Code, DateTime? PreparedAt, DateTime? ExpiresAt, ApprovalGateStatus? ApprovalStatus, bool Usable, string? Reason);

// Solution Preparation area (HPLC chain S4, spec 4): prepares Mobile
// Phases, Diluents, Reagents, Neutralizers and Titrants from the Solution
// master against real Material stock lots (and, for the general types,
// against prepared bottles of other solutions - recorded, never deducted). Start snapshots the master (D7); Save records the
// picked lots and quantities; Complete is one signed SaveChangesAsync that
// deducts stock (MaterialService.ConsumeAsync), allocates a code
// (SolutionPreparationCode), sets an expiry and, when the recipe needs a
// release, leaves the record PendingReview (SolutionPreparationReleaseService
// decides it). Cancel/Discard close the
// record without restoring stock. ExpireDueAsync is called by
// SolutionPreparationExpiryWorker (API layer) every 5 minutes.
public class SolutionPreparationService
{
    private static readonly JsonSerializerOptions JsonOptions = SnapshotJson.Options;

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
        var scope = await _scope.GetReadableSectionIdsAsync(userId, ct);
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
        await EnsureAccessAsync(prep, userId, ct, SectionAccess.Read);
        return await BuildResponseAsync(prep, ct);
    }

    public async Task<SolutionPreparationResponse> StartAsync(StartPreparationRequest r, int userId, CancellationToken ct = default)
    {
        var solution = await LoadSolutionForSnapshotAsync(r.SolutionMasterId, ct)
            ?? throw new NotFoundException($"Solution master {r.SolutionMasterId} not found.");

        var scope = await _scope.GetMemberSectionIdsAsync(userId, ct);
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
                Kind = c.Kind,
                MaterialMasterEntryId = c.MaterialMasterEntryId,
                SourceSolutionMasterId = c.SourceSolutionMasterId,
                Text = c.Text,
                EntryCode = c.MaterialMasterEntry?.Code ?? string.Empty,
                EntryName = c.Kind == SolutionComponentKind.PreparedSolution
                    ? c.SourceSolutionMaster?.Name ?? string.Empty
                    : c.MaterialMasterEntry?.Name ?? string.Empty,
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
        var now = _clock.UtcNow.UtcDateTime;
        var snapshot = ReadSnapshot(prep);

        foreach (var input in r.Components)
        {
            var component = prep.Components.FirstOrDefault(c => c.Id == input.ComponentId)
                ?? throw new InvalidOperationException($"Component {input.ComponentId} does not belong to this preparation.");

            if (component.Kind == SolutionComponentKind.Text)
            {
                if (input.MaterialId.HasValue || input.SourcePreparationId.HasValue || input.QuantityUsed.HasValue)
                    throw new InvalidOperationException("A text line has nothing to pick.");
                continue;
            }

            if (component.Kind == SolutionComponentKind.PreparedSolution)
            {
                if (input.MaterialId.HasValue)
                    throw new InvalidOperationException($"{component.EntryName} is a prepared solution: pick a prepared bottle, not a stock lot.");

                if (!input.SourcePreparationId.HasValue)
                {
                    component.SourcePreparationId = null;
                    component.QuantityUsed = null;
                    continue;
                }

                if (input.QuantityUsed.HasValue && input.QuantityUsed.Value <= 0)
                    throw new InvalidOperationException($"Quantity used for \"{component.EntryName}\" must be greater than zero.");

                var source = await _db.SolutionPreparations.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.SourcePreparationId.Value, ct)
                    ?? throw new NotFoundException($"Solution preparation {input.SourcePreparationId} not found.");

                var sourceCheck = CheckSource(source, component, prep.SectionId, now);
                if (!sourceCheck.Usable)
                    throw new InvalidOperationException($"{component.EntryName} (preparation {source.Code}): {sourceCheck.Reason}");

                component.SourcePreparationId = source.Id;
                component.QuantityUsed = input.QuantityUsed;
                continue;
            }

            if (input.SourcePreparationId.HasValue)
                throw new InvalidOperationException($"{component.EntryName} is a stock material: pick a lot, not a prepared bottle.");

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
                throw new InvalidOperationException($"Lot \"{lot.LotLabel}\" belongs to another laboratory.");

            var check = LotUsability.Check(lot, component.MaterialMasterEntryId!.Value, input.QuantityUsed, today);
            if (!check.Usable)
                throw new InvalidOperationException($"{component.EntryName} (lot {lot.LotLabel}): {check.Reason}");

            component.MaterialId = lot.Id;
            component.QuantityUsed = input.QuantityUsed;
        }

        if (r.FinalVolumeMl.HasValue && r.FinalVolumeMl.Value <= 0)
            throw new InvalidOperationException("Final volume must be greater than zero.");

        if (r.MeasuredPh.HasValue && (r.MeasuredPh.Value < 0 || r.MeasuredPh.Value > 14))
            throw new InvalidOperationException("Measured pH must be between 0 and 14.");

        prep.FinalVolumeMl = r.FinalVolumeMl;
        prep.MeasuredPh = r.MeasuredPh;

        await ApplySterilizationAsync(prep, snapshot, r, ct);

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, userId, ct);
    }

    public async Task<List<LotOption>> GetLotOptionsAsync(int id, int componentId, int userId, CancellationToken ct = default)
    {
        var prep = await LoadPrepAsync(id, ct);
        await EnsureAccessAsync(prep, userId, ct, SectionAccess.Read);

        var component = prep.Components.FirstOrDefault(c => c.Id == componentId)
            ?? throw new NotFoundException($"Component {componentId} not found on this preparation.");

        if (component.Kind != SolutionComponentKind.Material)
            throw new InvalidOperationException($"{component.EntryName} is not a stock material.");

        var today = _clock.LabToday;
        var lots = await _db.Materials.AsNoTracking()
            .Where(m => m.SectionId == prep.SectionId && m.MaterialMasterEntryId == component.MaterialMasterEntryId)
            .OrderBy(m => m.ExpiryDate)
            .ToListAsync(ct);

        return lots.Select(lot =>
        {
            var check = LotUsability.Check(lot, component.MaterialMasterEntryId!.Value, component.QuantityUsed, today);
            return new LotOption(lot.Id, lot.LotLabel, lot.ExpiryDate, lot.QuantityRemaining, lot.Unit, check.Usable, check.Reason, lot.Purity);
        }).ToList();
    }

    // Prepared bottles of the component's source recipe, usable or not (with the
    // reason), for the PreparedSolution picker. Valid = same laboratory, Prepared and
    // unexpired, and released when the source recipe needs a release.
    public async Task<List<SourcePreparationOption>> GetSourcePreparationsAsync(int id, int componentId, int userId, CancellationToken ct = default)
    {
        var prep = await LoadPrepAsync(id, ct);
        await EnsureAccessAsync(prep, userId, ct, SectionAccess.Read);

        var component = prep.Components.FirstOrDefault(c => c.Id == componentId)
            ?? throw new NotFoundException($"Component {componentId} not found on this preparation.");

        if (component.Kind != SolutionComponentKind.PreparedSolution)
            throw new InvalidOperationException($"{component.EntryName} is not a prepared solution.");

        var now = _clock.UtcNow.UtcDateTime;
        var candidates = await _db.SolutionPreparations.AsNoTracking()
            .Where(p => p.SectionId == prep.SectionId && p.SolutionMasterId == component.SourceSolutionMasterId
                && p.Code != null
                && (p.Status == SolutionPreparationStatus.Prepared || p.Status == SolutionPreparationStatus.Expired))
            .OrderByDescending(p => p.PreparedAt)
            .ToListAsync(ct);

        return candidates.Select(p =>
        {
            var check = CheckSource(p, component, prep.SectionId, now);
            return new SourcePreparationOption(p.Id, p.Code, p.PreparedAt, p.ExpiresAt, p.ApprovalStatus, check.Usable, check.Reason);
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

        if (prep.Components.Any(c => c.Kind == SolutionComponentKind.Material && (!c.MaterialId.HasValue || !c.QuantityUsed.HasValue)))
            throw new InvalidOperationException("Every component needs a lot and a quantity before completion.");

        if (prep.Components.Any(c => c.Kind == SolutionComponentKind.PreparedSolution && !c.SourcePreparationId.HasValue))
            throw new InvalidOperationException("Every prepared solution component needs a prepared bottle before completion.");

        if (!prep.FinalVolumeMl.HasValue)
            throw new InvalidOperationException("Final volume is required before completion.");

        var snapshot = ReadSnapshot(prep);

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

        await ValidateSterilizationAsync(prep, snapshot, ct);

        var today = _clock.LabToday;
        var picks = new List<(SolutionPreparationComponent Component, Material Lot)>();
        foreach (var component in prep.Components.Where(c => c.Kind == SolutionComponentKind.Material))
        {
            var lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == component.MaterialId!.Value, ct)
                ?? throw new InvalidOperationException($"{component.EntryName}: the selected lot no longer exists.");

            var check = LotUsability.Check(lot, component.MaterialMasterEntryId!.Value, component.QuantityUsed, today);
            if (!check.Usable)
                throw new InvalidOperationException($"{component.EntryName} (lot {lot.LotLabel}): {check.Reason}");

            picks.Add((component, lot));
        }

        // Prepared bottles are re-checked (they may have expired or been rejected since
        // they were picked) but never deducted.
        var nowCheck = _clock.UtcNow.UtcDateTime;
        foreach (var component in prep.Components.Where(c => c.Kind == SolutionComponentKind.PreparedSolution))
        {
            var source = await _db.SolutionPreparations.AsNoTracking().FirstOrDefaultAsync(p => p.Id == component.SourcePreparationId!.Value, ct)
                ?? throw new InvalidOperationException($"{component.EntryName}: the selected preparation no longer exists.");

            var check = CheckSource(source, component, prep.SectionId, nowCheck);
            if (!check.Usable)
                throw new InvalidOperationException($"{component.EntryName} (preparation {source.Code}): {check.Reason}");
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
        prep.ApprovalStatus = snapshot.ReleaseRequired ? ApprovalGateStatus.PendingReview : null;

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

    // Prepared + unexpired (and released, when the recipe needs a release - a
    // PendingReview or Rejected preparation is not offered), for the given solution (and, for a mobile
    // phase, the method it was prepared for). S6 (HPLC workspace) uses
    // this to offer preparations for a run's mobile phase channels.
    public async Task<List<SolutionPreparationListItem>> GetAvailableAsync(int userId, int solutionMasterId, int? hplcMethodId, CancellationToken ct = default)
    {
        var scope = await _scope.GetReadableSectionIdsAsync(userId, ct);
        var now = _clock.UtcNow.UtcDateTime;

        var query = ListQuery().Where(p =>
            p.SolutionMasterId == solutionMasterId
            && p.Status == SolutionPreparationStatus.Prepared
            && (!p.ExpiresAt.HasValue || p.ExpiresAt.Value > now)
            && (p.ApprovalStatus == null || p.ApprovalStatus == ApprovalGateStatus.Approved));

        if (scope != null) query = query.Where(p => scope.Contains(p.SectionId));
        if (hplcMethodId.HasValue) query = query.Where(p => p.HplcMethodId == hplcMethodId.Value);

        var rows = await query.OrderByDescending(p => p.PreparedAt).ToListAsync(ct);
        return await ToListItemsAsync(rows, ct);
    }

    // Traceability: which preparations consumed a given stock lot (Material
    // lot view "Consumed by preparations" list).
    public async Task<List<SolutionPreparationListItem>> GetByLotAsync(int materialId, int userId, CancellationToken ct = default)
    {
        var scope = await _scope.GetReadableSectionIdsAsync(userId, ct);
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

    private static SolutionMasterResponse ReadSnapshot(SolutionPreparation prep) =>
        JsonSerializer.Deserialize<SolutionMasterResponse>(prep.RecipeSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("The preparation's recipe snapshot could not be read.");

    // A prepared bottle may stand in for a PreparedSolution line when it is a Prepared,
    // unexpired preparation of the line's source recipe in the same laboratory, and is
    // released when its recipe needs a release (ApprovalStatus null = no release needed).
    private static LotCheck CheckSource(SolutionPreparation source, SolutionPreparationComponent component, int sectionId, DateTime nowUtc)
    {
        if (source.SectionId != sectionId)
            return new(false, "Preparation belongs to another laboratory.");
        if (source.SolutionMasterId != component.SourceSolutionMasterId)
            return new(false, $"Preparation is not a preparation of {component.EntryName}.");

        var status = SolutionPreparationResponse.EffectiveStatusOf(source, nowUtc);
        if (status == SolutionPreparationStatus.Expired)
            return new(false, $"Expired on {source.ExpiresAt:yyyy-MM-dd}.");
        if (status != SolutionPreparationStatus.Prepared)
            return new(false, $"Preparation is {status}.");

        if (source.ApprovalStatus == ApprovalGateStatus.PendingReview)
            return new(false, "Preparation is awaiting release.");
        if (source.ApprovalStatus == ApprovalGateStatus.Rejected)
            return new(false, "Preparation was rejected.");

        return new(true, null);
    }

    private static bool HasSterilizationFields(SavePreparationRequest r) =>
        r.AutoclaveEquipmentId.HasValue || !string.IsNullOrWhiteSpace(r.AutoclaveProgram) || !string.IsNullOrWhiteSpace(r.LoadType)
        || r.AutoclaveTemperature.HasValue || r.AutoclaveCycleTime.HasValue || r.AutoclaveCycleNumber.HasValue;

    // Stores the autoclave record from a Save. Values given are checked here too, so a
    // bad one is reported when entered; completeness is checked at completion.
    private async Task ApplySterilizationAsync(SolutionPreparation prep, SolutionMasterResponse snapshot, SavePreparationRequest r, CancellationToken ct)
    {
        if (!snapshot.SterilizationRequired)
        {
            if (HasSterilizationFields(r))
                throw new InvalidOperationException("This solution does not require sterilization; the autoclave record must be empty.");

            prep.AutoclaveEquipmentId = null;
            prep.AutoclaveProgram = null;
            prep.LoadType = null;
            prep.AutoclaveTemperature = null;
            prep.AutoclaveCycleTime = null;
            prep.AutoclaveCycleNumber = null;
            return;
        }

        var program = string.IsNullOrWhiteSpace(r.AutoclaveProgram) ? null : r.AutoclaveProgram.Trim();
        var loadType = string.IsNullOrWhiteSpace(r.LoadType) ? null : r.LoadType.Trim();
        if (program is { Length: > 100 }) throw new InvalidOperationException("Autoclave program cannot exceed 100 characters.");
        if (loadType is { Length: > 100 }) throw new InvalidOperationException("Load type cannot exceed 100 characters.");
        if (r.AutoclaveTemperature is <= 0) throw new InvalidOperationException("Autoclave temperature must be greater than zero.");
        if (r.AutoclaveCycleTime is <= 0) throw new InvalidOperationException("Autoclave cycle time must be greater than zero.");
        if (r.AutoclaveCycleNumber is <= 0) throw new InvalidOperationException("Autoclave cycle number must be greater than zero.");

        if (r.AutoclaveEquipmentId.HasValue
            && !await _db.Equipment.AnyAsync(e => e.Id == r.AutoclaveEquipmentId.Value && e.Type == EquipmentType.Autoclave, ct))
            throw new InvalidOperationException("Selected equipment is not a valid autoclave.");

        prep.AutoclaveEquipmentId = r.AutoclaveEquipmentId;
        prep.AutoclaveProgram = program;
        prep.LoadType = loadType;
        prep.AutoclaveTemperature = r.AutoclaveTemperature;
        prep.AutoclaveCycleTime = r.AutoclaveCycleTime;
        prep.AutoclaveCycleNumber = r.AutoclaveCycleNumber;
    }

    // Completion gate: a recipe that needs sterilization needs the whole autoclave record.
    private async Task ValidateSterilizationAsync(SolutionPreparation prep, SolutionMasterResponse snapshot, CancellationToken ct)
    {
        if (!snapshot.SterilizationRequired) return;

        if (!prep.AutoclaveEquipmentId.HasValue || string.IsNullOrWhiteSpace(prep.AutoclaveProgram) || string.IsNullOrWhiteSpace(prep.LoadType)
            || !prep.AutoclaveTemperature.HasValue || !prep.AutoclaveCycleTime.HasValue || !prep.AutoclaveCycleNumber.HasValue)
            throw new InvalidOperationException("The autoclave record (equipment, program, load type, temperature, cycle time and cycle number) is required before completion.");

        if (!await _db.Equipment.AnyAsync(e => e.Id == prep.AutoclaveEquipmentId.Value && e.Type == EquipmentType.Autoclave, ct))
            throw new InvalidOperationException("Selected equipment is not a valid autoclave.");

        if (prep.AutoclaveTemperature.Value <= 0) throw new InvalidOperationException("Autoclave temperature must be greater than zero.");
        if (prep.AutoclaveCycleTime.Value <= 0) throw new InvalidOperationException("Autoclave cycle time must be greater than zero.");
        if (prep.AutoclaveCycleNumber.Value <= 0) throw new InvalidOperationException("Autoclave cycle number must be greater than zero.");
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

    private async Task EnsureAccessAsync(SolutionPreparation prep, int userId, CancellationToken ct, SectionAccess access = SectionAccess.Write)
    {
        var scope = await _scope.GetSectionIdsAsync(userId, access, ct);
        if (scope != null && !scope.Contains(prep.SectionId))
            throw new NotFoundException($"Solution preparation {prep.Id} not found.");
    }

    private async Task<SolutionPreparation> LoadPrepAsync(int id, CancellationToken ct) =>
        await _db.SolutionPreparations
            .Include(p => p.Section)
            .Include(p => p.SolutionMaster)
            .Include(p => p.HplcMethod)
            .Include(p => p.AutoclaveEquipment)
            .Include(p => p.Components).ThenInclude(c => c.Material)
            .Include(p => p.Components).ThenInclude(c => c.SourcePreparation)
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
            .Include(s => s.Components).ThenInclude(c => c.SourceSolutionMaster)
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
            p.PreparedByUserId.HasValue && names.TryGetValue(p.PreparedByUserId.Value, out var n) ? n : null,
            p.ApprovalStatus))
            .ToList();
    }

    private async Task<SolutionPreparationResponse> BuildResponseAsync(SolutionPreparation prep, CancellationToken ct)
    {
        var userIds = new HashSet<int> { prep.StartedByUserId };
        if (prep.PreparedByUserId.HasValue) userIds.Add(prep.PreparedByUserId.Value);
        if (prep.ReleasedByUserId.HasValue) userIds.Add(prep.ReleasedByUserId.Value);
        foreach (var h in prep.StatusHistory)
            if (h.ChangedByUserId.HasValue) userIds.Add(h.ChangedByUserId.Value);

        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        CurrentFactorDto? currentFactor = null;
        if (prep.Type == SolutionType.Titrant)
        {
            var standardizations = await _db.TitrantStandardizations.AsNoTracking()
                .Where(x => x.SolutionPreparationId == prep.Id)
                .ToListAsync(ct);
            currentFactor = TitrantStandardizationService.ComputeCurrentFactor(standardizations, _clock.UtcNow.UtcDateTime);
        }

        return SolutionPreparationResponse.From(prep, names, _clock.UtcNow.UtcDateTime, currentFactor);
    }
}

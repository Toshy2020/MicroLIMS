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

public record StandardizationReplicateInput(int? StandardMaterialId, decimal? StandardWeightMg,
    int? ReferencePreparationId, decimal? ReferenceVolumeMl, decimal TitrantVolumeMl, decimal? BlankMl);

public record StandardizeRequest(List<StandardizationReplicateInput> Replicates, string Password, string? Comment);

// The titrant settings snapshotted onto each TitrantStandardization at the
// moment it was performed (the master may change afterwards - the record
// keeps what applied when it was signed, same reasoning as S4's
// RecipeSnapshotJson on SolutionPreparation).
public record TitrantSettingsSnapshot(
    decimal NominalStrength, TitrantStrengthUnit StrengthUnit, StandardizationMode Mode,
    int? StandardEntryId, decimal? EquivalenceMgPerMl, int? ReferenceSolutionId,
    bool BlankRequired, int ReplicateCount, decimal FactorMin, decimal FactorMax,
    decimal MaxRsdPercent, int ValidityDays);

// Titrant standardization (HPLC chain S5, spec 4): replicate titrations
// against a primary-standard lot or another standardized VS, evaluated by
// TitrationEngine. Settings come from the preparation's RecipeSnapshotJson
// (the S2 titrant settings as they were when the preparation was made), not
// from the live SolutionMaster. Sign-before-mutate, same ordering as
// SolutionPreparationService.CompleteAsync - a wrong password must leave
// nothing else persisted.
public class TitrantStandardizationService
{
    private static readonly JsonSerializerOptions JsonOptions = SnapshotJson.Options;

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IElectronicSignatureService _signatures;
    private readonly ILabClock _clock;

    public TitrantStandardizationService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        IElectronicSignatureService signatures,
        ILabClock? clock = null)
    {
        _db = db;
        _scope = scope;
        _signatures = signatures;
        _clock = clock ?? LabClock.Default;
    }

    public async Task<TitrantStandardizationResponse> StandardizeAsync(int preparationId, StandardizeRequest r, int userId, string? ip, CancellationToken ct = default)
    {
        var prep = await LoadPrepAsync(preparationId, ct);
        await EnsureAccessAsync(prep, userId, ct);

        if (prep.Type != SolutionType.Titrant)
            throw new InvalidOperationException("Only titrant preparations can be standardized.");

        var nowUtc = _clock.UtcNow.UtcDateTime;
        if (SolutionPreparationResponse.EffectiveStatusOf(prep, nowUtc) != SolutionPreparationStatus.Prepared)
            throw new InvalidOperationException("This preparation is expired or discarded and cannot be standardized.");

        var snapshot = JsonSerializer.Deserialize<SolutionMasterResponse>(prep.RecipeSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("The preparation's recipe snapshot could not be read.");

        if (!snapshot.StandardizationMode.HasValue || !snapshot.NominalStrength.HasValue || !snapshot.StrengthUnit.HasValue
            || !snapshot.ReplicateCount.HasValue || !snapshot.FactorMin.HasValue || !snapshot.FactorMax.HasValue || !snapshot.MaxRsdPercent.HasValue)
            throw new InvalidOperationException("This titrant is not configured for standardization.");

        var mode = snapshot.StandardizationMode.Value;

        if (r.Replicates == null || r.Replicates.Count != snapshot.ReplicateCount.Value)
            throw new InvalidOperationException($"Exactly {snapshot.ReplicateCount.Value} replicate(s) are required.");

        var today = _clock.LabToday;
        var replicateEntities = new List<TitrantStandardizationReplicate>();
        var factors = new List<decimal>();

        for (int i = 0; i < r.Replicates.Count; i++)
        {
            var input = r.Replicates[i];
            var replicateNo = i + 1;

            if (input.TitrantVolumeMl <= 0)
                throw new InvalidOperationException($"Replicate {replicateNo}: titrant volume must be greater than zero.");

            decimal blank;
            if (snapshot.BlankRequired)
            {
                if (!input.BlankMl.HasValue)
                    throw new InvalidOperationException($"Replicate {replicateNo}: a blank is required.");
                blank = input.BlankMl.Value;
            }
            else
            {
                if (input.BlankMl.HasValue)
                    throw new InvalidOperationException($"Replicate {replicateNo}: this titrant does not use a blank.");
                blank = 0m;
            }

            decimal factor;
            Material? lot = null;
            SolutionPreparation? refPrep = null;
            decimal? refFactorSnapshot = null;

            if (mode == StandardizationMode.PrimaryStandard)
            {
                if (input.ReferencePreparationId.HasValue || input.ReferenceVolumeMl.HasValue)
                    throw new InvalidOperationException($"Replicate {replicateNo}: reference fields do not apply to primary-standard standardization.");
                if (!input.StandardMaterialId.HasValue || !input.StandardWeightMg.HasValue)
                    throw new InvalidOperationException($"Replicate {replicateNo}: a standard lot and weight are required.");
                if (!snapshot.StandardEntryId.HasValue || !snapshot.EquivalenceMgPerMl.HasValue)
                    throw new InvalidOperationException("This titrant has no primary standard configured.");

                lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == input.StandardMaterialId.Value, ct)
                    ?? throw new InvalidOperationException($"Replicate {replicateNo}: standard material not found.");

                if (lot.SectionId != prep.SectionId)
                    throw new InvalidOperationException($"Replicate {replicateNo}: standard lot belongs to another laboratory.");

                var check = LotUsability.Check(lot, snapshot.StandardEntryId.Value, null, today);
                if (!check.Usable)
                    throw new InvalidOperationException($"Replicate {replicateNo}: standard lot (batch {lot.BatchNumber}): {check.Reason}");

                try
                {
                    factor = TitrationEngine.PrimaryStandardFactor(
                        input.StandardWeightMg.Value, lot.Purity, input.TitrantVolumeMl, blank, snapshot.EquivalenceMgPerMl.Value);
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException($"Replicate {replicateNo}: {ex.Message}");
                }
            }
            else
            {
                if (input.StandardMaterialId.HasValue || input.StandardWeightMg.HasValue)
                    throw new InvalidOperationException($"Replicate {replicateNo}: standard-lot fields do not apply to standardization against a volumetric solution.");
                if (!input.ReferencePreparationId.HasValue || !input.ReferenceVolumeMl.HasValue)
                    throw new InvalidOperationException($"Replicate {replicateNo}: a reference preparation and volume are required.");
                if (!snapshot.ReferenceSolutionId.HasValue)
                    throw new InvalidOperationException("This titrant has no reference solution configured.");

                refPrep = await _db.SolutionPreparations.FirstOrDefaultAsync(p => p.Id == input.ReferencePreparationId.Value, ct)
                    ?? throw new InvalidOperationException($"Replicate {replicateNo}: reference preparation not found.");

                if (refPrep.SectionId != prep.SectionId)
                    throw new InvalidOperationException($"Replicate {replicateNo}: reference preparation belongs to another laboratory.");

                if (refPrep.SolutionMasterId != snapshot.ReferenceSolutionId.Value)
                    throw new InvalidOperationException($"Replicate {replicateNo}: reference preparation is not of this titrant's configured reference solution.");

                if (SolutionPreparationResponse.EffectiveStatusOf(refPrep, nowUtc) != SolutionPreparationStatus.Prepared)
                    throw new InvalidOperationException($"Replicate {replicateNo}: reference preparation is expired or discarded.");

                var refSnapshot = JsonSerializer.Deserialize<SolutionMasterResponse>(refPrep.RecipeSnapshotJson, JsonOptions)
                    ?? throw new InvalidOperationException($"Replicate {replicateNo}: reference preparation's recipe snapshot could not be read.");

                if (!refSnapshot.StrengthUnit.HasValue || refSnapshot.StrengthUnit.Value != snapshot.StrengthUnit.Value)
                    throw new InvalidOperationException($"Replicate {replicateNo}: reference titrant's strength unit does not match.");
                if (!refSnapshot.NominalStrength.HasValue)
                    throw new InvalidOperationException($"Replicate {replicateNo}: reference titrant has no nominal strength configured.");

                var refRecords = await _db.TitrantStandardizations.AsNoTracking()
                    .Where(x => x.SolutionPreparationId == refPrep.Id)
                    .ToListAsync(ct);
                var refCurrent = ComputeCurrentFactor(refRecords, nowUtc);

                var refStandardizedSameDay = refCurrent.StandardizedAt.HasValue
                    && DateOnly.FromDateTime(_clock.ToLabLocal(refCurrent.StandardizedAt.Value)) == today;
                var refAcceptable = refCurrent.State == "Valid" || (refCurrent.State == "BeforeEachUse" && refStandardizedSameDay);

                if (!refAcceptable || !refCurrent.Factor.HasValue)
                    throw new InvalidOperationException($"Replicate {replicateNo}: reference preparation's standardization is expired, failed or missing.");

                refFactorSnapshot = refCurrent.Factor.Value;

                try
                {
                    factor = TitrationEngine.AgainstVolumetricSolutionFactor(
                        input.ReferenceVolumeMl.Value, refFactorSnapshot.Value, refSnapshot.NominalStrength.Value,
                        input.TitrantVolumeMl, blank, snapshot.NominalStrength.Value);
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException($"Replicate {replicateNo}: {ex.Message}");
                }
            }

            factors.Add(factor);
            replicateEntities.Add(new TitrantStandardizationReplicate
            {
                ReplicateNo = replicateNo,
                StandardMaterialId = lot?.Id,
                StandardWeightMg = input.StandardWeightMg,
                StandardPurityPercent = lot?.Purity,
                ReferencePreparationId = refPrep?.Id,
                ReferenceVolumeMl = input.ReferenceVolumeMl,
                ReferenceFactor = refFactorSnapshot,
                TitrantVolumeMl = input.TitrantVolumeMl,
                BlankMl = input.BlankMl,
                Factor = factor,
            });
        }

        // Each replicate weighed standard from its lot: deduct the per-lot sum
        // (mg -> lot unit, g / kg only). Validated here, before signing; the
        // decrement happens after SignAsync in the same SaveChanges.
        var deductions = new List<(Material Lot, decimal Quantity)>();
        foreach (var g in replicateEntities.Where(x => x.StandardMaterialId.HasValue && x.StandardWeightMg.HasValue)
                     .GroupBy(x => x.StandardMaterialId!.Value))
        {
            var lot = await _db.Materials.FirstAsync(m => m.Id == g.Key, ct);
            var mg = g.Sum(x => x.StandardWeightMg!.Value);
            var quantity = lot.Unit switch
            {
                MaterialUnit.Gram => mg / 1000m,
                MaterialUnit.Kilogram => mg / 1_000_000m,
                _ => throw new InvalidOperationException(
                    $"Lot {lot.LotLabel} is stocked in {lot.Unit}; a standard weight in mg cannot be deducted from it."),
            };
            var check = LotUsability.Check(lot, snapshot.StandardEntryId!.Value, quantity, today);
            if (!check.Usable)
                throw new InvalidOperationException($"Standard lot {lot.LotLabel}: {check.Reason}");
            deductions.Add((lot, quantity));
        }

        var evaluation = TitrationEngine.Evaluate(factors, snapshot.FactorMin.Value, snapshot.FactorMax.Value, snapshot.MaxRsdPercent.Value);

        // Signs first - see the class comment. Only after this succeeds does
        // anything below get added to the change tracker.
        var signature = await _signatures.SignAsync(
            userId, r.Password, SignatureMeaning.TitrantStandardized, "TitrantStandardization", prep.Id, r.Comment, ip);

        var validityDays = snapshot.ValidityDays ?? 0;
        var settingsSnapshot = new TitrantSettingsSnapshot(
            snapshot.NominalStrength.Value, snapshot.StrengthUnit.Value, mode,
            snapshot.StandardEntryId, snapshot.EquivalenceMgPerMl, snapshot.ReferenceSolutionId,
            snapshot.BlankRequired, snapshot.ReplicateCount.Value, snapshot.FactorMin.Value, snapshot.FactorMax.Value,
            snapshot.MaxRsdPercent.Value, validityDays);

        var record = new TitrantStandardization
        {
            SolutionPreparationId = prep.Id,
            Mode = mode,
            SettingsSnapshotJson = JsonSerializer.Serialize(settingsSnapshot, JsonOptions),
            MeanFactor = evaluation.MeanFactor,
            RsdPercent = evaluation.RsdPercent,
            Passed = evaluation.Passed,
            FailureReasons = evaluation.FailureReasons.Count > 0 ? string.Join(" ", evaluation.FailureReasons) : null,
            StandardizedByUserId = userId,
            StandardizedAt = nowUtc,
            ValidUntil = validityDays <= 0 ? null : nowUtc.AddDays(validityDays),
            Signature = signature,
            Replicates = replicateEntities,
        };

        foreach (var (lot, quantity) in deductions)
        {
            lot.QuantityRemaining -= quantity;
            lot.LastModifiedByUserId = userId;
            lot.LastModifiedAt = nowUtc;
        }

        _db.CurrentUserId = userId;
        _db.TitrantStandardizations.Add(record);
        await _db.SaveChangesAsync(ct);

        var name = await _db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return TitrantStandardizationResponse.From(record, name);
    }

    public async Task<List<TitrantStandardizationResponse>> GetForPreparationAsync(int preparationId, int userId, CancellationToken ct = default)
    {
        var prep = await LoadPrepAsync(preparationId, ct);
        await EnsureAccessAsync(prep, userId, ct);

        var records = await _db.TitrantStandardizations.AsNoTracking()
            .Include(x => x.Replicates).ThenInclude(x => x.StandardMaterial)
            .Include(x => x.Replicates).ThenInclude(x => x.ReferencePreparation)
            .Where(x => x.SolutionPreparationId == preparationId)
            .OrderByDescending(x => x.StandardizedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        var userIds = records.Select(x => x.StandardizedByUserId).Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return records
            .Select(x => TitrantStandardizationResponse.From(x, names.TryGetValue(x.StandardizedByUserId, out var n) ? n : null))
            .ToList();
    }

    // No access check by design - used both as a public read and internally
    // (by StandardizeAsync, when the standardization's own preparation has
    // already been access-checked) to evaluate a reference preparation's
    // status without a second round trip through EnsureAccessAsync.
    public async Task<CurrentFactorDto> GetCurrentFactorAsync(int preparationId, CancellationToken ct = default)
    {
        var records = await _db.TitrantStandardizations.AsNoTracking()
            .Where(x => x.SolutionPreparationId == preparationId)
            .ToListAsync(ct);

        return ComputeCurrentFactor(records, _clock.UtcNow.UtcDateTime);
    }

    // The current factor is the latest Passed record; ValidUntil null means
    // "restandardize before each use" (ValidityDays = 0), otherwise Valid or
    // Due depending on whether ValidUntil is still in the future. A failed
    // record never becomes current - the previous passed one (if any) does.
    public static CurrentFactorDto ComputeCurrentFactor(IReadOnlyList<TitrantStandardization> records, DateTime nowUtc)
    {
        var current = records.Where(r => r.Passed).OrderByDescending(r => r.StandardizedAt).ThenByDescending(r => r.Id).FirstOrDefault();
        if (current == null)
            return new CurrentFactorDto(null, null, null, "NotStandardized");

        if (current.ValidUntil == null)
            return new CurrentFactorDto(current.MeanFactor, current.StandardizedAt, null, "BeforeEachUse");

        var state = current.ValidUntil.Value > nowUtc ? "Valid" : "Due";
        return new CurrentFactorDto(current.MeanFactor, current.StandardizedAt, current.ValidUntil, state);
    }

    public async Task<List<LotOption>> GetStandardLotOptionsAsync(int preparationId, int userId, CancellationToken ct = default)
    {
        var prep = await LoadPrepAsync(preparationId, ct);
        await EnsureAccessAsync(prep, userId, ct);

        var snapshot = JsonSerializer.Deserialize<SolutionMasterResponse>(prep.RecipeSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("The preparation's recipe snapshot could not be read.");

        if (!snapshot.StandardEntryId.HasValue)
            return new List<LotOption>();

        var today = _clock.LabToday;
        var lots = await _db.Materials.AsNoTracking()
            .Where(m => m.SectionId == prep.SectionId && m.MaterialMasterEntryId == snapshot.StandardEntryId.Value)
            .OrderBy(m => m.ExpiryDate)
            .ToListAsync(ct);

        return lots.Select(lot =>
        {
            var check = LotUsability.Check(lot, snapshot.StandardEntryId.Value, null, today);
            return new LotOption(lot.Id, lot.BatchNumber, lot.ExpiryDate, lot.QuantityRemaining, lot.Unit, check.Usable, check.Reason);
        }).ToList();
    }

    public async Task<List<SolutionPreparationListItem>> GetReferenceOptionsAsync(int preparationId, int userId, CancellationToken ct = default)
    {
        var prep = await LoadPrepAsync(preparationId, ct);
        await EnsureAccessAsync(prep, userId, ct);

        var snapshot = JsonSerializer.Deserialize<SolutionMasterResponse>(prep.RecipeSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("The preparation's recipe snapshot could not be read.");

        if (!snapshot.ReferenceSolutionId.HasValue)
            return new List<SolutionPreparationListItem>();

        var nowUtc = _clock.UtcNow.UtcDateTime;
        var candidates = await _db.SolutionPreparations.AsNoTracking()
            .Include(p => p.SolutionMaster)
            .Include(p => p.HplcMethod)
            .Where(p => p.SolutionMasterId == snapshot.ReferenceSolutionId.Value
                && p.SectionId == prep.SectionId
                && p.Status == SolutionPreparationStatus.Prepared
                && (!p.ExpiresAt.HasValue || p.ExpiresAt.Value > nowUtc))
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return new List<SolutionPreparationListItem>();

        var candidateIds = candidates.Select(p => p.Id).ToList();
        var allRecords = await _db.TitrantStandardizations.AsNoTracking()
            .Where(x => candidateIds.Contains(x.SolutionPreparationId))
            .ToListAsync(ct);

        var valid = candidates
            .Where(p => ComputeCurrentFactor(allRecords.Where(x => x.SolutionPreparationId == p.Id).ToList(), nowUtc).State == "Valid")
            .ToList();

        var userIds = valid.Where(p => p.PreparedByUserId.HasValue).Select(p => p.PreparedByUserId!.Value).Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return valid.Select(p => new SolutionPreparationListItem(
            p.Id, p.Code, p.Type, p.SolutionMaster?.Name ?? string.Empty, p.HplcMethod?.Abbreviation,
            SolutionPreparationResponse.EffectiveStatusOf(p, nowUtc), p.PreparedAt, p.ExpiresAt,
            p.PreparedByUserId.HasValue && names.TryGetValue(p.PreparedByUserId.Value, out var n) ? n : null))
            .ToList();
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
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException($"Solution preparation {id} not found.");
}

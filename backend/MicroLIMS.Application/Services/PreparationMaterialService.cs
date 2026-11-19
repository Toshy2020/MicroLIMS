using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services;

public enum PreparationMaterialRole { Diluent, Neutralizer }

// What an Item's diluent or neutralizer is: a source and the one master of
// that source. Source null = legacy free text, not yet mapped.
public record PreparationMaterialChoice(
    PreparationMaterialSource? Source,
    int? MediaProductId = null,
    int? SolutionMasterId = null,
    int? MaterialMasterEntryId = null);

public record ResolvedPreparationMaterial(PreparationMaterialChoice Choice, string Name);

// A lot that passed every rule, ready to be copied onto the SamplePreparation.
public record PreparationLot(
    PreparationMaterialSource Source, int? MediaId, int? SolutionPreparationId, int? MaterialId, string Code);

// Diluent / neutralizer traceability for Test Preparation (plan 3.3-3.4):
// validates the master an Item points at, validates the lot an analyst picks
// at confirmation, lists the lots they may pick, and answers "where used".
// Nothing is deducted from a picked lot (D9).
public class PreparationMaterialService
{
    public const string ContainedInMediumName = "Contained in the medium";
    private const int MaxOptions = 200;

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;

    public PreparationMaterialService(IMicroLimsDbContext db, IUserSectionScopeService scope, TimeProvider? timeProvider = null)
    {
        _db = db;
        _scope = scope;
        _time = timeProvider ?? TimeProvider.System;
    }

    public static PreparationMaterialChoice ChoiceOf(ItemPreparationConfiguration c, PreparationMaterialRole role) =>
        role == PreparationMaterialRole.Diluent
            ? new(c.DiluentSource, c.DiluentMediaProductId, c.DiluentSolutionMasterId, c.DiluentMaterialMasterEntryId)
            : new(c.NeutralizerSource, c.NeutralizerMediaProductId, c.NeutralizerSolutionMasterId, c.NeutralizerMaterialMasterEntryId);

    // Copies a validated choice and its display name onto the configuration.
    public static void Apply(ItemPreparationConfiguration c, PreparationMaterialRole role, ResolvedPreparationMaterial r)
    {
        var ch = r.Choice;
        var name = r.Name.Length <= 200 ? r.Name : r.Name[..200];
        if (role == PreparationMaterialRole.Diluent)
        {
            c.Diluent = name;
            c.DiluentSource = ch.Source;
            c.DiluentMediaProductId = ch.MediaProductId;
            c.DiluentSolutionMasterId = ch.SolutionMasterId;
            c.DiluentMaterialMasterEntryId = ch.MaterialMasterEntryId;
        }
        else
        {
            c.Neutralizer = name;
            c.NeutralizerSource = ch.Source;
            c.NeutralizerMediaProductId = ch.MediaProductId;
            c.NeutralizerSolutionMasterId = ch.SolutionMasterId;
            c.NeutralizerMaterialMasterEntryId = ch.MaterialMasterEntryId;
        }
    }

    public static bool NeedsMapping(ItemPreparationConfiguration c) => c.DiluentSource is null || c.NeutralizerSource is null;

    // ---- Configuration: validate the master an Item points at ----------------

    public async Task<ResolvedPreparationMaterial> ResolveAsync(PreparationMaterialRole role, PreparationMaterialChoice? choice)
    {
        if (choice?.Source is not PreparationMaterialSource source)
        {
            if (role == PreparationMaterialRole.Neutralizer)
                throw new BusinessRuleException(
                    MessageCodes.SolutionNeutralizerSourceRequired,
                    "Choose where the neutralizer comes from: a media product, a solution recipe or a material entry, or contained in the medium.");
            throw new BusinessRuleException(
                MessageCodes.SolutionDiluentSourceRequired,
                "Choose where the diluent comes from: a media product, a solution recipe or a material entry.");
        }

        // Exactly the id matching the source may be set.
        void OnlyOne(bool media, bool solution, bool entry)
        {
            if ((!media && choice.MediaProductId.HasValue) || (!solution && choice.SolutionMasterId.HasValue) || (!entry && choice.MaterialMasterEntryId.HasValue))
            {
                if (role == PreparationMaterialRole.Diluent)
                {
                    switch (source)
                    {
                        case PreparationMaterialSource.MediaProduct:
                            throw new BusinessRuleException(
                                MessageCodes.SolutionDiluentMediaProductOnly,
                                "The diluent source is MediaProduct: set only the media product.");
                        case PreparationMaterialSource.SolutionRecipe:
                            throw new BusinessRuleException(
                                MessageCodes.SolutionDiluentSolutionRecipeOnly,
                                "The diluent source is SolutionRecipe: set only the solution recipe.");
                        case PreparationMaterialSource.MaterialEntry:
                            throw new BusinessRuleException(
                                MessageCodes.SolutionDiluentMaterialEntryOnly,
                                "The diluent source is MaterialEntry: set only the material entry.");
                    }
                }
                else
                {
                    switch (source)
                    {
                        case PreparationMaterialSource.MediaProduct:
                            throw new BusinessRuleException(
                                MessageCodes.SolutionNeutralizerMediaProductOnly,
                                "The neutralizer source is MediaProduct: set only the media product.");
                        case PreparationMaterialSource.SolutionRecipe:
                            throw new BusinessRuleException(
                                MessageCodes.SolutionNeutralizerSolutionRecipeOnly,
                                "The neutralizer source is SolutionRecipe: set only the solution recipe.");
                        case PreparationMaterialSource.MaterialEntry:
                            throw new BusinessRuleException(
                                MessageCodes.SolutionNeutralizerMaterialEntryOnly,
                                "The neutralizer source is MaterialEntry: set only the material entry.");
                        case PreparationMaterialSource.ContainedInMedium:
                            throw new BusinessRuleException(
                                MessageCodes.SolutionNeutralizerContainedInMediumSourceOnly,
                                "The neutralizer source is ContainedInMedium: set only the source.");
                    }
                }
            }
        }

        switch (source)
        {
            case PreparationMaterialSource.MediaProduct:
            {
                OnlyOne(true, false, false);
                if (choice.MediaProductId is not int id)
                {
                    if (role == PreparationMaterialRole.Diluent)
                        throw new BusinessRuleException(MessageCodes.SolutionDiluentMediaProductRequired, "Choose the media product for the diluent.");
                    throw new BusinessRuleException(MessageCodes.SolutionNeutralizerMediaProductRequired, "Choose the media product for the neutralizer.");
                }
                var product = await _db.MediaProducts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id)
                    ?? throw new BusinessRuleException(
                        MessageCodes.SolutionMediaProductNotFound,
                        $"Media product {id} not found.",
                        new Dictionary<string, string> { ["mediaProductId"] = id.ToString(CultureInfo.InvariantCulture) });
                if (!product.IsActive)
                {
                    if (role == PreparationMaterialRole.Diluent)
                        throw new BusinessRuleException(
                            MessageCodes.SolutionDiluentMediaProductInactive,
                            $"Media product '{product.Name}' is inactive and can't be chosen as the diluent.",
                            new Dictionary<string, string> { ["name"] = product.Name });
                    throw new BusinessRuleException(
                        MessageCodes.SolutionNeutralizerMediaProductInactive,
                        $"Media product '{product.Name}' is inactive and can't be chosen as the neutralizer.",
                        new Dictionary<string, string> { ["name"] = product.Name });
                }
                return new(new(source, MediaProductId: product.Id), product.Name);
            }
            case PreparationMaterialSource.SolutionRecipe:
            {
                OnlyOne(false, true, false);
                if (choice.SolutionMasterId is not int id)
                {
                    if (role == PreparationMaterialRole.Diluent)
                        throw new BusinessRuleException(MessageCodes.SolutionDiluentRecipeRequired, "Choose the solution recipe for the diluent.");
                    throw new BusinessRuleException(MessageCodes.SolutionNeutralizerRecipeRequired, "Choose the solution recipe for the neutralizer.");
                }
                var master = await _db.SolutionMasters.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id)
                    ?? throw new BusinessRuleException(
                        MessageCodes.SolutionRecipeNotFound,
                        $"Solution recipe {id} not found.",
                        new Dictionary<string, string> { ["solutionMasterId"] = id.ToString(CultureInfo.InvariantCulture) });
                if (!master.IsActive)
                {
                    if (role == PreparationMaterialRole.Diluent)
                        throw new BusinessRuleException(
                            MessageCodes.SolutionDiluentRecipeInactive,
                            $"Solution recipe '{master.Name}' is inactive and can't be chosen as the diluent.",
                            new Dictionary<string, string> { ["name"] = master.Name });
                    throw new BusinessRuleException(
                        MessageCodes.SolutionNeutralizerRecipeInactive,
                        $"Solution recipe '{master.Name}' is inactive and can't be chosen as the neutralizer.",
                        new Dictionary<string, string> { ["name"] = master.Name });
                }
                if (!master.Type.IsGeneral())
                    throw new BusinessRuleException(
                        MessageCodes.SolutionRecipeWrongType,
                        $"Solution recipe '{master.Name}' is a {master.Type} recipe; choose a Diluent, Reagent or Neutralizer recipe.",
                        new Dictionary<string, string> { ["name"] = master.Name, ["type"] = master.Type.ToString() });
                return new(new(source, SolutionMasterId: master.Id), master.Name);
            }
            case PreparationMaterialSource.MaterialEntry:
            {
                OnlyOne(false, false, true);
                if (choice.MaterialMasterEntryId is not int id)
                {
                    if (role == PreparationMaterialRole.Diluent)
                        throw new BusinessRuleException(MessageCodes.SolutionDiluentMaterialEntryRequired, "Choose the material entry for the diluent.");
                    throw new BusinessRuleException(MessageCodes.SolutionNeutralizerMaterialEntryRequired, "Choose the material entry for the neutralizer.");
                }
                var entry = await _db.MaterialMasterEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id)
                    ?? throw new BusinessRuleException(
                        MessageCodes.SolutionMaterialEntryNotFound,
                        $"Material entry {id} not found.",
                        new Dictionary<string, string> { ["entryId"] = id.ToString(CultureInfo.InvariantCulture) });
                if (!entry.IsActive)
                {
                    if (role == PreparationMaterialRole.Diluent)
                        throw new BusinessRuleException(
                            MessageCodes.SolutionDiluentMaterialEntryInactive,
                            $"Material entry '{entry.Name}' is inactive and can't be chosen as the diluent.",
                            new Dictionary<string, string> { ["name"] = entry.Name });
                    throw new BusinessRuleException(
                        MessageCodes.SolutionNeutralizerMaterialEntryInactive,
                        $"Material entry '{entry.Name}' is inactive and can't be chosen as the neutralizer.",
                        new Dictionary<string, string> { ["name"] = entry.Name });
                }
                return new(new(source, MaterialMasterEntryId: entry.Id), entry.Name);
            }
            case PreparationMaterialSource.ContainedInMedium:
                if (role != PreparationMaterialRole.Neutralizer)
                    throw new BusinessRuleException(
                        MessageCodes.SolutionOnlyNeutralizerContainedInMedium,
                        "Only the neutralizer can be contained in the medium; choose the diluent's source.");
                OnlyOne(false, false, false);
                return new(new(source), ContainedInMediumName);
            default:
                if (role == PreparationMaterialRole.Diluent)
                    throw new BusinessRuleException(MessageCodes.SolutionUnknownDiluentSource, "Unknown diluent source.");
                throw new BusinessRuleException(MessageCodes.SolutionUnknownNeutralizerSource, "Unknown neutralizer source.");
        }
    }

    // ---- Confirmation: validate the picked lot --------------------------------

    // Returns null when nothing is picked (legacy configuration or contained in
    // the medium). Otherwise the lot is required and must pass every rule.
    public async Task<PreparationLot?> RequireLotAsync(
        PreparationMaterialRole role, PreparationMaterialChoice configured, int? lotId, int userId, CancellationToken ct = default)
    {
        if (configured.Source is null)
        {
            if (lotId.HasValue)
            {
                if (role == PreparationMaterialRole.Diluent)
                    throw new BusinessRuleException(
                        MessageCodes.SolutionDiluentNotLinkedToMaster,
                        "The diluent of this item is not linked to a master record yet, so no diluent lot can be recorded.");
                throw new BusinessRuleException(
                    MessageCodes.SolutionNeutralizerNotLinkedToMaster,
                    "The neutralizer of this item is not linked to a master record yet, so no neutralizer lot can be recorded.");
            }
            return null;
        }

        if (configured.Source == PreparationMaterialSource.ContainedInMedium)
        {
            if (lotId.HasValue)
            {
                if (role == PreparationMaterialRole.Diluent)
                    throw new BusinessRuleException(
                        MessageCodes.SolutionDiluentContainedInMediumNoLot,
                        "The diluent is contained in the medium; no diluent lot is picked.");
                throw new BusinessRuleException(
                    MessageCodes.SolutionNeutralizerContainedInMediumNoLot,
                    "The neutralizer is contained in the medium; no neutralizer lot is picked.");
            }
            return null;
        }

        if (lotId is not int id)
        {
            if (role == PreparationMaterialRole.Diluent)
                throw new BusinessRuleException(MessageCodes.SolutionDiluentLotRequired, "Pick the diluent lot used.");
            throw new BusinessRuleException(MessageCodes.SolutionNeutralizerLotRequired, "Pick the neutralizer lot used.");
        }

        var readable = await _scope.GetReadableSectionIdsAsync(userId, ct);
        var now = _time.GetUtcNow().UtcDateTime;
        var source = configured.Source.Value;

        switch (source)
        {
            case PreparationMaterialSource.MediaProduct:
            {
                var media = await _db.Media.Include(m => m.Material).FirstOrDefaultAsync(m => m.Id == id, ct)
                    ?? throw new BusinessRuleException(
                        MessageCodes.MediaLotNotFound,
                        $"Media lot {id} not found.",
                        new Dictionary<string, string> { ["mediaId"] = id.ToString(CultureInfo.InvariantCulture) });
                if (readable is not null && !readable.Contains(media.Material!.SectionId))
                    throw new BusinessRuleException(
                        MessageCodes.SolutionMediaLotWrongSection,
                        $"Media lot {media.LotNumber} belongs to a laboratory section you can't access.",
                        new Dictionary<string, string> { ["lotNumber"] = media.LotNumber });
                var productName = await _db.MediaProducts.AsNoTracking()
                    .Where(p => p.Id == configured.MediaProductId).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? "the configured media product";
                var check = CheckMedia(media, configured.MediaProductId, productName, now);
                if (!check.Usable)
                {
                    var code = check.Code!;
                    throw new BusinessRuleException(code, check.Reason!, check.Params);
                }
                return new(source, media.Id, null, null, Truncate(media.LotNumber));
            }
            case PreparationMaterialSource.SolutionRecipe:
            {
                var prep = await _db.SolutionPreparations.Include(p => p.SolutionMaster).FirstOrDefaultAsync(p => p.Id == id, ct)
                    ?? throw new BusinessRuleException(
                        MessageCodes.SolutionPreparationNotFound,
                        $"Solution preparation {id} not found.",
                        new Dictionary<string, string> { ["preparationId"] = id.ToString(CultureInfo.InvariantCulture) });
                var prepLabel = LabelOf(prep);
                if (readable is not null && !readable.Contains(prep.SectionId))
                    throw new BusinessRuleException(
                        MessageCodes.SolutionPreparationWrongSection,
                        $"Preparation {prepLabel} belongs to a laboratory section you can't access.",
                        new Dictionary<string, string> { ["label"] = prepLabel });
                var check = CheckSolution(prep, configured.SolutionMasterId, now);
                if (!check.Usable)
                {
                    var code = check.Code!;
                    throw new BusinessRuleException(code, check.Reason!, check.Params);
                }
                return new(source, null, prep.Id, null, Truncate(prepLabel));
            }
            default:
            {
                var lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == id, ct)
                    ?? throw new BusinessRuleException(
                        MessageCodes.SolutionStockLotNotFound,
                        $"Stock lot {id} not found.",
                        new Dictionary<string, string> { ["materialId"] = id.ToString(CultureInfo.InvariantCulture) });
                var lotLabel = LabelOf(lot);
                if (readable is not null && !readable.Contains(lot.SectionId))
                    throw new BusinessRuleException(
                        MessageCodes.SolutionStockLotWrongSection,
                        $"Stock lot {lotLabel} belongs to a laboratory section you can't access.",
                        new Dictionary<string, string> { ["lotLabel"] = lotLabel });
                var check = CheckMaterial(lot, configured.MaterialMasterEntryId, now);
                if (!check.Usable)
                {
                    var code = check.Code!;
                    throw new BusinessRuleException(code, check.Reason!, check.Params);
                }
                return new(source, null, null, lot.Id, Truncate(lotLabel));
            }
        }
    }

    // Copies the choice (source snapshot) and the validated lot onto the record.
    public static void Snapshot(SamplePreparation prep, PreparationMaterialRole role, PreparationMaterialChoice configured, PreparationLot? lot)
    {
        if (role == PreparationMaterialRole.Diluent)
        {
            prep.DiluentSource = configured.Source;
            prep.DiluentMediaId = lot?.MediaId;
            prep.DiluentSolutionPreparationId = lot?.SolutionPreparationId;
            prep.DiluentMaterialId = lot?.MaterialId;
            prep.DiluentLotCode = lot?.Code;
        }
        else
        {
            prep.NeutralizerSource = configured.Source;
            prep.NeutralizerMediaId = lot?.MediaId;
            prep.NeutralizerSolutionPreparationId = lot?.SolutionPreparationId;
            prep.NeutralizerMaterialId = lot?.MaterialId;
            prep.NeutralizerLotCode = lot?.Code;
        }
    }

    // How a diluent / neutralizer reads on reports and the audit trail: "TSB — lot TSB/03/26".
    public static string WithLot(string name, string? lotCode) =>
        string.IsNullOrEmpty(lotCode) ? name : $"{name} — lot {lotCode}";

    // One line for the e-signature comment: "Diluent: TSB (MediaProduct), lot TSB/03/26".
    public static string Describe(PreparationMaterialRole role, string name, PreparationMaterialSource? source, string? lotCode) =>
        $"{(role == PreparationMaterialRole.Diluent ? "Diluent" : "Neutralizer")}: {name} ({(source?.ToString() ?? "unmapped")})"
        + (string.IsNullOrEmpty(lotCode) ? string.Empty : $", lot {lotCode}");

    // ---- Lot rules, shared by validation and the options list -------------------

    private sealed record CodedLotCheck(bool Usable, string? Reason, string? Code = null, Dictionary<string, string>? Params = null);

    private static CodedLotCheck CheckMedia(Media media, int? productId, string productName, DateTime now)
    {
        if (media.Material?.MediaProductId != productId)
            return new(false, $"Media lot {media.LotNumber} is not a lot of {productName}.",
                MessageCodes.SolutionMediaLotWrongProduct,
                new Dictionary<string, string> { ["lotNumber"] = media.LotNumber, ["productName"] = productName });
        if (!media.IsReleasedForUse)
            return new(false, $"Media lot {media.LotNumber} is not released for use.",
                MessageCodes.SolutionMediaLotNotReleased,
                new Dictionary<string, string> { ["lotNumber"] = media.LotNumber });
        if (media.Status != MediaStatus.Active)
            return new(false, $"Media lot {media.LotNumber} is {media.Status} and cannot be used.",
                MessageCodes.SolutionMediaLotWrongStatus,
                new Dictionary<string, string> { ["lotNumber"] = media.LotNumber, ["status"] = media.Status.ToString() });
        if (media.ExpiryDate <= now)
            return new(false, $"Media lot {media.LotNumber} expired on {media.ExpiryDate:yyyy-MM-dd}.",
                MessageCodes.SolutionMediaLotExpired,
                new Dictionary<string, string> { ["lotNumber"] = media.LotNumber, ["expiresAt"] = media.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        return new(true, null);
    }

    private static CodedLotCheck CheckSolution(SolutionPreparation prep, int? masterId, DateTime now)
    {
        var label = LabelOf(prep);
        if (prep.SolutionMasterId != masterId)
            return new(false, $"Preparation {label} is not a preparation of the configured solution recipe.",
                MessageCodes.SolutionPreparationWrongMaster,
                new Dictionary<string, string> { ["label"] = label });
        var status = SolutionPreparationResponse.EffectiveStatusOf(prep, now);
        if (status == SolutionPreparationStatus.Expired)
            return new(false, $"Preparation {label} expired on {prep.ExpiresAt:yyyy-MM-dd}.",
                MessageCodes.MediaAdditiveSolutionExpired,
                new Dictionary<string, string> { ["label"] = label, ["expiresAt"] = prep.ExpiresAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty });
        if (status != SolutionPreparationStatus.Prepared)
            return new(false, $"Preparation {label} is {status} and cannot be used.",
                MessageCodes.MediaAdditiveSolutionInvalidStatus,
                new Dictionary<string, string> { ["label"] = label, ["status"] = status.ToString() });
        if (prep.ApprovalStatus == ApprovalGateStatus.PendingReview)
            return new(false, $"Preparation {label} is awaiting release.",
                MessageCodes.MediaAdditiveSolutionAwaitingRelease,
                new Dictionary<string, string> { ["label"] = label });
        if (prep.ApprovalStatus == ApprovalGateStatus.Rejected)
            return new(false, $"Preparation {label} was rejected.",
                MessageCodes.MediaAdditiveSolutionRejected,
                new Dictionary<string, string> { ["label"] = label });
        return new(true, null);
    }

    private static CodedLotCheck CheckMaterial(Material lot, int? entryId, DateTime now)
    {
        var label = LabelOf(lot);
        if (entryId is not int id)
            return new(false, $"Stock lot {label} does not match the configured material entry.",
                MessageCodes.SolutionStockLotWrongEntry,
                new Dictionary<string, string> { ["lotLabel"] = label });
        var check = LotUsability.Check(lot, id, null, DateOnly.FromDateTime(now));
        return check.Usable
            ? new(true, null)
            : new(false, $"Stock lot {label} cannot be used: {check.Reason}",
                MessageCodes.SolutionStockLotUnusable,
                new Dictionary<string, string> { ["lotLabel"] = label, ["reason"] = check.Reason ?? string.Empty });
    }

    private static string LabelOf(SolutionPreparation p) => p.Code ?? $"#{p.Id}";

    private static string LabelOf(Material m) =>
        !string.IsNullOrWhiteSpace(m.StockId) ? m.StockId : !string.IsNullOrWhiteSpace(m.Code) ? m.Code : m.BatchNumber;

    private static string Truncate(string value) => value.Length <= 100 ? value : value[..100];

    // ---- Lot options for the analyst's picker ----------------------------------

    public async Task<PreparationLotOptionsDto> GetLotOptionsAsync(ItemPreparationConfiguration config, int userId, CancellationToken ct = default)
    {
        var readable = (await _scope.GetReadableSectionIdsAsync(userId, ct))?.ToList();
        return new PreparationLotOptionsDto(
            config.Id,
            NeedsMapping(config),
            await GroupAsync(config.Diluent, ChoiceOf(config, PreparationMaterialRole.Diluent), readable, ct),
            await GroupAsync(config.Neutralizer, ChoiceOf(config, PreparationMaterialRole.Neutralizer), readable, ct));
    }

    // The lots of one chosen master, for a manual first entry that has no configuration yet.
    public async Task<PreparationLotGroupDto> GetLotGroupForMasterAsync(
        PreparationMaterialSource source, int masterId, int userId, CancellationToken ct = default)
    {
        var choice = source switch
        {
            PreparationMaterialSource.MediaProduct => new PreparationMaterialChoice(source, MediaProductId: masterId),
            PreparationMaterialSource.SolutionRecipe => new PreparationMaterialChoice(source, SolutionMasterId: masterId),
            PreparationMaterialSource.MaterialEntry => new PreparationMaterialChoice(source, MaterialMasterEntryId: masterId),
            _ => new PreparationMaterialChoice(source)
        };
        var resolved = await ResolveAsync(PreparationMaterialRole.Neutralizer, choice);
        var readable = (await _scope.GetReadableSectionIdsAsync(userId, ct))?.ToList();
        return await GroupAsync(resolved.Name, resolved.Choice, readable, ct);
    }

    private async Task<PreparationLotGroupDto> GroupAsync(
        string name, PreparationMaterialChoice choice, List<int>? readable, CancellationToken ct)
    {
        var options = new List<PreparationLotOptionDto>();
        var now = _time.GetUtcNow().UtcDateTime;

        switch (choice.Source)
        {
            case PreparationMaterialSource.MediaProduct:
            {
                var query = _db.Media.AsNoTracking().Include(m => m.Material)
                    .Where(m => m.Material!.MediaProductId == choice.MediaProductId
                        && m.Status != MediaStatus.Destroyed && m.Status != MediaStatus.Expired && m.Status != MediaStatus.OutOfStock
                        && m.ExpiryDate > now);
                if (readable is not null) query = query.Where(m => readable.Contains(m.Material!.SectionId));
                foreach (var m in await query.OrderBy(m => m.ExpiryDate).Take(MaxOptions).ToListAsync(ct))
                {
                    var check = CheckMedia(m, choice.MediaProductId, name, now);
                    options.Add(new(m.Id, m.LotNumber, m.ExpiryDate, check.Usable, check.Reason));
                }
                break;
            }
            case PreparationMaterialSource.SolutionRecipe:
            {
                var query = _db.SolutionPreparations.AsNoTracking()
                    .Where(p => p.SolutionMasterId == choice.SolutionMasterId && p.Status == SolutionPreparationStatus.Prepared
                        && (p.ExpiresAt == null || p.ExpiresAt > now));
                if (readable is not null) query = query.Where(p => readable.Contains(p.SectionId));
                foreach (var p in await query.OrderBy(p => p.ExpiresAt == null).ThenBy(p => p.ExpiresAt).Take(MaxOptions).ToListAsync(ct))
                {
                    var check = CheckSolution(p, choice.SolutionMasterId, now);
                    options.Add(new(p.Id, LabelOf(p), p.ExpiresAt, check.Usable, check.Reason));
                }
                break;
            }
            case PreparationMaterialSource.MaterialEntry:
            {
                var today = now.Date;
                var query = _db.Materials.AsNoTracking()
                    .Where(m => m.MaterialMasterEntryId == choice.MaterialMasterEntryId && m.QuantityRemaining > 0
                        && (m.ExpiryDate == null || m.ExpiryDate >= today));
                if (readable is not null) query = query.Where(m => readable.Contains(m.SectionId));
                foreach (var m in await query.OrderBy(m => m.ExpiryDate == null).ThenBy(m => m.ExpiryDate).Take(MaxOptions).ToListAsync(ct))
                {
                    var check = CheckMaterial(m, choice.MaterialMasterEntryId, now);
                    options.Add(new(m.Id, LabelOf(m), m.ExpiryDate, check.Usable, check.Reason));
                }
                break;
            }
        }

        var lotRequired = choice.Source is not null && choice.Source != PreparationMaterialSource.ContainedInMedium;
        return new PreparationLotGroupDto(choice.Source, name, lotRequired, options);
    }

    // ---- Where used ---------------------------------------------------------------

    // Every sample whose diluent or neutralizer was this lot, limited to the samples
    // the user can read. Exactly one of the three ids.
    public async Task<List<PreparationWhereUsedDto>> GetWhereUsedAsync(
        int? mediaId, int? solutionPreparationId, int? materialId, int userId, CancellationToken ct = default)
    {
        var given = new[] { mediaId, solutionPreparationId, materialId }.Count(v => v.HasValue);
        if (given != 1)
            throw new BusinessRuleException(
                MessageCodes.SolutionWhereUsedRequiresSingleId,
                "Give exactly one of mediaId, solutionPreparationId or materialId.");

        var readable = (await _scope.GetReadableSectionIdsAsync(userId, ct))?.ToList();

        int lotSectionId;
        if (mediaId.HasValue)
            lotSectionId = (await _db.Media.AsNoTracking().Where(m => m.Id == mediaId).Select(m => (int?)m.Material!.SectionId).FirstOrDefaultAsync(ct))
                ?? throw new NotFoundException(
                    MessageCodes.MediaLotNotFound,
                    $"Media lot {mediaId} not found.",
                    new Dictionary<string, string> { ["mediaId"] = mediaId.Value.ToString(CultureInfo.InvariantCulture) });
        else if (solutionPreparationId.HasValue)
            lotSectionId = (await _db.SolutionPreparations.AsNoTracking().Where(p => p.Id == solutionPreparationId).Select(p => (int?)p.SectionId).FirstOrDefaultAsync(ct))
                ?? throw new NotFoundException(
                    MessageCodes.SolutionPreparationNotFound,
                    $"Solution preparation {solutionPreparationId} not found.",
                    new Dictionary<string, string> { ["preparationId"] = solutionPreparationId.Value.ToString(CultureInfo.InvariantCulture) });
        else
            lotSectionId = (await _db.Materials.AsNoTracking().Where(m => m.Id == materialId).Select(m => (int?)m.SectionId).FirstOrDefaultAsync(ct))
                ?? throw new NotFoundException(
                    MessageCodes.SolutionStockLotNotFound,
                    $"Stock lot {materialId} not found.",
                    new Dictionary<string, string> { ["materialId"] = materialId!.Value.ToString(CultureInfo.InvariantCulture) });

        if (readable is not null && !readable.Contains(lotSectionId))
            throw new UnauthorizedAccessException("This lot belongs to a laboratory section you are not assigned to.");

        var query = _db.SamplePreparations.AsNoTracking().AsQueryable();
        query = mediaId.HasValue ? query.Where(p => p.DiluentMediaId == mediaId || p.NeutralizerMediaId == mediaId)
            : solutionPreparationId.HasValue ? query.Where(p => p.DiluentSolutionPreparationId == solutionPreparationId || p.NeutralizerSolutionPreparationId == solutionPreparationId)
            : query.Where(p => p.DiluentMaterialId == materialId || p.NeutralizerMaterialId == materialId);
        if (readable is not null)
            query = query.Where(p => p.Sample!.TestOrders.Any(t => !t.IsSuperseded && readable.Contains(t.SectionId)));

        var rows = await query
            .OrderByDescending(p => p.PreparedAt)
            .Select(p => new
            {
                p.SampleId,
                p.Sample!.ReferenceNumber,
                ItemName = p.Sample.Item != null ? p.Sample.Item.Name : null,
                p.PreparedAt,
                p.PreparedByUserId,
                AsDiluent = mediaId.HasValue ? p.DiluentMediaId == mediaId
                    : solutionPreparationId.HasValue ? p.DiluentSolutionPreparationId == solutionPreparationId
                    : p.DiluentMaterialId == materialId,
                AsNeutralizer = mediaId.HasValue ? p.NeutralizerMediaId == mediaId
                    : solutionPreparationId.HasValue ? p.NeutralizerSolutionPreparationId == solutionPreparationId
                    : p.NeutralizerMaterialId == materialId
            })
            .ToListAsync(ct);

        var userIds = rows.Select(r => r.PreparedByUserId).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.Username, ct);

        var result = new List<PreparationWhereUsedDto>();
        foreach (var r in rows)
        {
            var by = names.TryGetValue(r.PreparedByUserId, out var n) ? n : null;
            if (r.AsDiluent)
                result.Add(new(r.SampleId, r.ReferenceNumber, r.ItemName ?? r.ReferenceNumber, "Diluent", r.PreparedAt, by));
            if (r.AsNeutralizer)
                result.Add(new(r.SampleId, r.ReferenceNumber, r.ItemName ?? r.ReferenceNumber, "Neutralizer", r.PreparedAt, by));
        }
        return result;
    }
}

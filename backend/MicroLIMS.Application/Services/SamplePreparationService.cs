using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Manual entry - used only when the Item has no preparation configuration
// yet. What the analyst enters here becomes the Item's standing config.
public record PrepareSampleRequest(
    int SampleId, decimal Amount, string Technique, decimal? FiltrationVolume, decimal? WashingVolume,
    string Diluent, string Neutralizer, int UserId, string Password);

// Confirm-only - the Item already has a configuration; the analyst signs
// off that those steps were the ones performed.
// ExpectedConfigurationId is set by the grouped path only: it pins the
// confirmation to the exact configuration row the analyst was shown, so a
// Section Head edit landing mid-batch skips the sample instead of signing
// it against steps nobody read.
public record ConfirmPreparationRequest(int SampleId, int UserId, string Password, int? ExpectedConfigurationId = null);

// Test Preparation - Product/RM/PM only, once per Sample. Must complete
// before any result can be entered for any of that sample's TestOrders.
public class SamplePreparationService
{
    private readonly MicroLimsDbContext _db;
    private readonly PreparationParameterValidator _validator;
    private readonly IElectronicSignatureService _signatures;

    public SamplePreparationService(
        MicroLimsDbContext db,
        PreparationParameterValidator validator,
        IElectronicSignatureService signatures)
    {
        _db = db;
        _validator = validator;
        _signatures = signatures;
    }

    // Manual fallback: writes the sample's preparation AND seeds the Item's
    // configuration (PendingReview) from the same values, in one transaction.
    public async Task<SamplePreparation> PrepareAsync(PrepareSampleRequest request, string? ipAddress = null)
    {
        var sample = await LoadPreparableSampleAsync(request.SampleId, request.UserId);

        if (sample.ItemId is null)
            throw new InvalidOperationException("This sample has no Item and cannot use the preparation configuration flow.");

        var parameters = new PreparationParameters(
            request.Amount, request.Technique, request.FiltrationVolume, request.WashingVolume,
            request.Diluent, request.Neutralizer);

        await _validator.ValidateAsync(parameters);

        // Seed the Item's standing configuration from this first manual entry.
        // Usable immediately by later samples; Section Head reviews after the
        // fact so this sample is never held up.
        // Deliberately NOT added to the change tracker here: a failed
        // signature calls SaveChanges to log the attempt, which would flush
        // anything already tracked. It reaches the context via
        // prep.SourceConfiguration below, which is only added after signing.
        var config = await _db.ItemPreparationConfigurations.FirstOrDefaultAsync(c => c.ItemId == sample.ItemId.Value);
        if (config is null)
        {
            config = new ItemPreparationConfiguration
            {
                ItemId = sample.ItemId.Value,
                Amount = request.Amount,
                Technique = request.Technique,
                FiltrationVolume = request.FiltrationVolume,
                WashingVolume = request.WashingVolume,
                Diluent = request.Diluent.Trim(),
                Neutralizer = request.Neutralizer.Trim(),
                ApprovalStatus = ApprovalGateStatus.PendingReview,
                CreatedByUserId = request.UserId
            };
        }

        var prep = new SamplePreparation
        {
            SampleId = request.SampleId,
            Amount = request.Amount,
            Technique = request.Technique,
            FiltrationVolume = request.FiltrationVolume,
            WashingVolume = request.WashingVolume,
            Diluent = request.Diluent.Trim(),
            Neutralizer = request.Neutralizer.Trim(),
            PreparedByUserId = request.UserId,
            SourceConfiguration = config,
            WasConfirmedFromConfig = false
        };

        return await CommitPreparationAsync(sample, prep, request.UserId, request.Password, ipAddress);
    }

    // Confirm-only: every value is copied from the Item's configuration, so
    // editing that config later cannot rewrite this sample's record.
    public async Task<SamplePreparation> ConfirmFromConfigurationAsync(ConfirmPreparationRequest request, string? ipAddress = null)
    {
        var sample = await LoadPreparableSampleAsync(request.SampleId, request.UserId);

        if (sample.ItemId is null)
            throw new InvalidOperationException("This sample has no Item and cannot use the preparation configuration flow.");

        var config = await _db.ItemPreparationConfigurations.FirstOrDefaultAsync(c => c.ItemId == sample.ItemId.Value)
            ?? throw new InvalidOperationException("This item has no preparation configuration to confirm.");

        if (request.ExpectedConfigurationId is int expectedConfigId && config.Id != expectedConfigId)
            throw new InvalidOperationException(
                "This item's preparation configuration changed after the group was loaded. "
                + "Reload the grouped action and review the steps before confirming.");

        // Re-validate at confirmation time in case the config was left in an
        // invalid state (e.g. blank Diluent/Neutralizer from data predating
        // validation).
        await _validator.ValidateAsync(new PreparationParameters(
            config.Amount, config.Technique, config.FiltrationVolume, config.WashingVolume,
            config.Diluent, config.Neutralizer));

        var prep = new SamplePreparation
        {
            SampleId = request.SampleId,
            Amount = config.Amount,
            Technique = config.Technique,
            FiltrationVolume = config.FiltrationVolume,
            WashingVolume = config.WashingVolume,
            Diluent = config.Diluent,
            Neutralizer = config.Neutralizer,
            PreparedByUserId = request.UserId,
            SourceConfigurationId = config.Id,
            WasConfirmedFromConfig = true
        };

        return await CommitPreparationAsync(sample, prep, request.UserId, request.Password, ipAddress);
    }

    private async Task<Sample> LoadPreparableSampleAsync(int sampleId, int userId)
    {
        var sample = await _db.Samples.FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (await _db.SamplePreparations.AnyAsync(p => p.SampleId == sampleId))
            throw new InvalidOperationException("This sample has already been prepared.");

        // Ownership Rule: If an analyst was already assigned by the Section Head,
        // only that assigned analyst may prepare the sample unless reassigned.
        var assignedAnalystId = await _db.TestOrders
            .Where(t => t.SampleId == sampleId && t.AssignedAnalystId != null && !t.IsSuperseded)
            .Select(t => t.AssignedAnalystId)
            .FirstOrDefaultAsync();

        if (assignedAnalystId != null && assignedAnalystId.Value != userId)
        {
            var assignedUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == assignedAnalystId.Value);
            var assignedName = assignedUser?.FullName ?? $"User #{assignedAnalystId}";
            throw new InvalidOperationException(
                $"This sample is assigned to {assignedName}. Only the assigned analyst may perform sample preparation, unless reassigned by an authorized Section Head.");
        }

        return sample;
    }

    // One transaction: the preparation snapshot, the signature that attests
    // to it, and the sample moving to Ready all land together or not at all.
    private async Task<SamplePreparation> CommitPreparationAsync(
        Sample sample, SamplePreparation prep, int userId, string password, string? ipAddress)
    {
        // Signs first - if password verification fails, nothing below is
        // written. Signed against the Sample rather than the SamplePreparation
        // because the latter has no Id until SaveChanges, and one
        // SaveChangesAsync is this codebase's atomicity boundary (same
        // ordering as SampleApprovalService.DecideAsync).
        await _signatures.SignAsync(
            userId, password, SignatureMeaning.PreparationConfirmed,
            ReviewEntityTypes.Sample, sample.Id, null, ipAddress);

        sample.PreparationStatus = SamplePreparationStatus.Ready;
        _db.SamplePreparations.Add(prep);

        await _db.SaveChangesAsync();

        // "Start Testing" - the person who completes preparation is
        // assigned as the analyst for every test on this sample that
        // hasn't started yet. Tests already past Waiting keep whoever's
        // already on them.
        var waitingOrders = await _db.TestOrders
            .Where(t => t.SampleId == sample.Id && t.CurrentStep == WorkflowStep.Waiting)
            .ToListAsync();
        foreach (var order in waitingOrders)
            order.AssignedAnalystId = userId;
        if (waitingOrders.Count > 0)
            await _db.SaveChangesAsync();

        return prep;
    }

    public async Task<bool> IsPreparedAsync(int sampleId) =>
        await _db.SamplePreparations.AnyAsync(p => p.SampleId == sampleId);

    // ---- Grouped Test Preparation -------------------------------------
    // Only the confirm-only path groups. Manual first entry cannot: its
    // values are per-item and become that item's standing configuration,
    // so one shared form across several items would write the wrong
    // protocol. EM/After Cleaning/Water cannot either - each of those
    // samples is defined by its own room/machine/sampling-point set.

    private static readonly SampleCategory[] GroupablePreparationCategories =
    {
        SampleCategory.FinishedProduct, SampleCategory.RawMaterial, SampleCategory.PackagingMaterial
    };

    public async Task<GroupedPreparationResponse> GetGroupedPreparationAsync(
        List<int> sampleIds, int userId, CancellationToken ct = default)
    {
        var ids = (sampleIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            return new GroupedPreparationResponse(new(), 0, new());

        var samples = await _db.Samples
            .Include(s => s.Item)
            .Where(s => ids.Contains(s.Id))
            .ToListAsync(ct);

        var preparedSampleIds = (await _db.SamplePreparations
            .Where(p => ids.Contains(p.SampleId))
            .Select(p => p.SampleId)
            .ToListAsync(ct)).ToHashSet();

        // One query for the whole selection - LoadPreparableSampleAsync does
        // this per sample, which is fine for a single confirmation but not
        // for a panel that reloads on every checkbox change.
        var assignments = await _db.TestOrders
            .Where(t => ids.Contains(t.SampleId) && t.AssignedAnalystId != null && !t.IsSuperseded)
            .Select(t => new { t.SampleId, AnalystId = t.AssignedAnalystId!.Value })
            .Distinct()
            .ToListAsync(ct);

        var assignedBySample = assignments
            .GroupBy(a => a.SampleId)
            .ToDictionary(g => g.Key, g => g.First().AnalystId);

        var assignedAnalystIds = assignedBySample.Values.Distinct().ToList();
        var analystNames = await _db.Users
            .Where(u => assignedAnalystIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.Username, ct);

        var itemIds = samples.Where(s => s.ItemId != null).Select(s => s.ItemId!.Value).Distinct().ToList();
        var configsByItem = await _db.ItemPreparationConfigurations
            .Where(c => itemIds.Contains(c.ItemId))
            .ToDictionaryAsync(c => c.ItemId, ct);

        var excluded = new List<ExcludedPreparationSampleDto>();
        var eligible = new List<(Sample Sample, ItemPreparationConfiguration Config, int? AnalystId, string? AnalystName)>();

        foreach (var sample in samples.OrderBy(s => s.Id))
        {
            var displayName = sample.Item?.Name ?? sample.ReferenceNumber;

            void Exclude(string reason) =>
                excluded.Add(new ExcludedPreparationSampleDto(sample.Id, sample.ReferenceNumber, displayName, reason));

            if (sample.PreparationStatus != SamplePreparationStatus.NeedsPreparation || preparedSampleIds.Contains(sample.Id))
            {
                Exclude("Already prepared.");
                continue;
            }

            if (!GroupablePreparationCategories.Contains(sample.Category))
            {
                Exclude($"{sample.Category} samples are prepared individually - their locations or sampling points are selected per sample.");
                continue;
            }

            if (sample.ItemId is null)
            {
                Exclude("This sample has no Item and cannot use the preparation configuration flow.");
                continue;
            }

            if (!configsByItem.TryGetValue(sample.ItemId.Value, out var config))
            {
                Exclude("This item has no preparation configuration yet - prepare one sample individually to record it first.");
                continue;
            }

            var hasAssignment = assignedBySample.TryGetValue(sample.Id, out var assignedId);
            var assignedName = hasAssignment && analystNames.TryGetValue(assignedId, out var an) ? an : null;

            if (hasAssignment && assignedId != userId)
            {
                Exclude($"Assigned to {assignedName ?? ("User #" + assignedId)} - only the assigned analyst may prepare this sample.");
                continue;
            }

            eligible.Add((sample, config, hasAssignment ? assignedId : null, assignedName));
        }

        var groups = eligible
            .GroupBy(e => e.Config.Id)
            .Select(g =>
            {
                var config = g.First().Config;
                var item = g.First().Sample.Item;
                return new GroupedPreparationDto(
                    GroupKey: $"PREPARE|{config.Id}",
                    ConfigurationId: config.Id,
                    ItemId: config.ItemId,
                    ItemName: item?.Name ?? ("Item #" + config.ItemId),
                    ApprovalStatus: config.ApprovalStatus.ToString(),
                    Amount: config.Amount,
                    Technique: config.Technique,
                    FiltrationVolume: config.FiltrationVolume,
                    WashingVolume: config.WashingVolume,
                    Diluent: config.Diluent,
                    Neutralizer: config.Neutralizer,
                    SampleCount: g.Count(),
                    Samples: g.Select(e => new GroupedPreparationSampleDto(
                        e.Sample.Id,
                        e.Sample.ReferenceNumber,
                        e.Sample.Item?.Name ?? e.Sample.ReferenceNumber,
                        e.Sample.BatchNumber,
                        e.Sample.Category.ToString(),
                        e.AnalystId,
                        e.AnalystName
                    )).ToList()
                );
            })
            .OrderByDescending(g => g.SampleCount)
            .ThenBy(g => g.ItemName)
            .ToList();

        return new GroupedPreparationResponse(groups, excluded.Count, excluded);
    }

    // One password entry, one ElectronicSignature per sample: each sample's
    // audit trail still points at its own signature, and each sample's
    // snapshot + status change + signature still commit inside the single
    // SaveChangesAsync that CommitPreparationAsync owns. A sample that fails
    // its own rule is skipped and reported rather than rolling back the ones
    // that succeeded - the same partial-success contract grouped incubation
    // setup already uses.
    public async Task<BatchConfirmPreparationResponse> ConfirmBatchFromConfigurationAsync(
        BatchConfirmPreparationRequest request, int userId, string? ipAddress = null, CancellationToken ct = default)
    {
        var ids = (request.SampleIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("No samples were selected for grouped preparation.");

        if (!await _db.ItemPreparationConfigurations.AnyAsync(c => c.Id == request.ConfigurationId, ct))
            throw new InvalidOperationException("The preparation configuration for this group no longer exists.");

        var references = await _db.Samples
            .Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.ReferenceNumber, ct);

        var succeeded = new List<BatchPreparationSuccessItem>();
        var skipped = new List<BatchPreparationSkippedItem>();
        var anySigned = false;

        foreach (var sampleId in ids)
        {
            var reference = references.TryGetValue(sampleId, out var r) ? r : ("Sample-" + sampleId);

            try
            {
                var prep = await ConfirmFromConfigurationAsync(
                    new ConfirmPreparationRequest(sampleId, userId, request.Password, request.ConfigurationId),
                    ipAddress);

                anySigned = true;
                succeeded.Add(new BatchPreparationSuccessItem(
                    sampleId, reference, prep.Id, "Preparation confirmed and signed."));
            }
            // A wrong password can only surface on the first sample that
            // reaches the signature - nothing is written yet, so fail the
            // whole run rather than leaving one failed-attempt audit row per
            // selected sample behind.
            catch (SignatureVerificationException) when (!anySigned)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                skipped.Add(new BatchPreparationSkippedItem(sampleId, reference, ex.Message));
            }
        }

        return new BatchConfirmPreparationResponse(
            ids.Count, succeeded.Count, skipped.Count, succeeded, skipped);
    }
}

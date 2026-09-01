using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Manual entry - used only when the Item has no preparation configuration
// yet. What the analyst enters here becomes the Item's standing config.
public record PrepareSampleRequest(
    int SampleId, decimal Amount, string Unit, string Technique, decimal? FiltrationVolume, decimal? WashingVolume,
    int DiluentTypeId, int? DiluentMediaId, int NeutralizerId, int UserId, string Password);

// Confirm-only - the Item already has a configuration; the analyst signs
// off that those steps were the ones performed.
public record ConfirmPreparationRequest(int SampleId, int UserId, string Password);

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
            request.Amount, request.Unit, request.Technique, request.FiltrationVolume, request.WashingVolume,
            request.DiluentTypeId, request.DiluentMediaId, request.NeutralizerId);

        var diluentType = await _validator.ValidateAsync(parameters);
        var resolvedDiluentMediaId = diluentType.RequiresBatchTracking ? request.DiluentMediaId : null;

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
                Unit = request.Unit,
                Technique = request.Technique,
                FiltrationVolume = request.FiltrationVolume,
                WashingVolume = request.WashingVolume,
                DiluentTypeId = request.DiluentTypeId,
                DiluentMediaId = resolvedDiluentMediaId,
                NeutralizerId = request.NeutralizerId,
                ApprovalStatus = ApprovalGateStatus.PendingReview,
                CreatedByUserId = request.UserId
            };
        }

        var prep = new SamplePreparation
        {
            SampleId = request.SampleId,
            Amount = request.Amount,
            Unit = request.Unit,
            Technique = request.Technique,
            FiltrationVolume = request.FiltrationVolume,
            WashingVolume = request.WashingVolume,
            DiluentTypeId = request.DiluentTypeId,
            DiluentMediaId = resolvedDiluentMediaId,
            NeutralizerId = request.NeutralizerId,
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

        // Re-validate at confirmation time: a media lot that was fine when the
        // config was written may have expired or been rejected since.
        var diluentType = await _validator.ValidateAsync(new PreparationParameters(
            config.Amount, config.Unit, config.Technique, config.FiltrationVolume, config.WashingVolume,
            config.DiluentTypeId, config.DiluentMediaId, config.NeutralizerId));

        var prep = new SamplePreparation
        {
            SampleId = request.SampleId,
            Amount = config.Amount,
            Unit = config.Unit,
            Technique = config.Technique,
            FiltrationVolume = config.FiltrationVolume,
            WashingVolume = config.WashingVolume,
            DiluentTypeId = config.DiluentTypeId,
            DiluentMediaId = diluentType.RequiresBatchTracking ? config.DiluentMediaId : null,
            NeutralizerId = config.NeutralizerId,
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
}

using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record IdentityConfirmationRow(int MediaId, int IncubatorEquipmentId, DateTime IncubationStart, DateTime IncubationEnd, string ObservationText);
public record PrepareCryovialsRequest(
    int MaterialId, int NumberOfVialsPrepared, DateTime ExpiryDate, string StorageCondition, string PhysicalCheckText,
    List<IdentityConfirmationRow> Panel, int DiscsUsed, int UserId);

// Cryovial batches are prepared directly from a LyophilizedMicroorganism
// Material row (Inventory Materials Stock) - there is no separate
// reference-strain receiving step. Approval is a hard gate: an
// unapproved (or rejected/destroyed) batch cannot be used in GPT (see
// GptWorkflowEngine.EnsureCryovialApprovedAsync).
public class CryovialService
{
    private readonly MicroLimsDbContext _db;
    private readonly MaterialService _materialService;
    private readonly SegregationOfDutiesGuard _segregationOfDuties;
    private readonly ReviewGateService _reviewGate;
    private readonly CryovialSummaryService _summary;
    private readonly RecordArchiveService _archive;

    public CryovialService(MicroLimsDbContext db, MaterialService materialService,
        SegregationOfDutiesGuard segregationOfDuties, ReviewGateService reviewGate,
        CryovialSummaryService summary, RecordArchiveService archive)
    {
        _db = db;
        _materialService = materialService;
        _segregationOfDuties = segregationOfDuties;
        _reviewGate = reviewGate;
        _summary = summary;
        _archive = archive;
    }

    public async Task<List<Cryovial>> GetAllAsync() =>
        await _db.Cryovials.Include(c => c.Material).Include(c => c.Organism).Include(c => c.IdentityConfirmations).OrderByDescending(c => c.Id).ToListAsync();

    public async Task<Cryovial> PrepareCryovialsAsync(PrepareCryovialsRequest request)
    {
        var material = await _db.Materials.Include(m => m.Organism).FirstOrDefaultAsync(m => m.Id == request.MaterialId)
            ?? throw new InvalidOperationException($"Material {request.MaterialId} not found.");

        if (material.MaterialType != MaterialType.LyophilizedMicroorganism)
            throw new InvalidOperationException($"Material {material.MaterialName} is not a Lyophilized Microorganism item.");

        if (material.OrganismId is null || material.Organism is null)
            throw new InvalidOperationException($"Set an Organism on material \"{material.MaterialName}\" (Inventory > Materials Stock) before preparing cryovials from it.");

        foreach (var row in request.Panel)
        {
            var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == row.MediaId)
                ?? throw new InvalidOperationException($"Media {row.MediaId} not found.");
            if (!media.IsReleasedForUse)
                throw new InvalidOperationException($"Media lot {media.LotNumber} is not GPT-released - cannot be used for identity confirmation.");
        }

        if (request.Panel.Count == 0)
            throw new InvalidOperationException("At least one identity confirmation row is required.");

        // Guards + decrements the Material row in memory - not saved
        // until the SaveChangesAsync below, so a failure here leaves
        // both the stock and the cryovial batch untouched.
        await _materialService.ConsumeAsync(request.MaterialId, MaterialType.LyophilizedMicroorganism, request.DiscsUsed, request.UserId);

        var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = yearStart.AddYears(1);
        var countThisYear = await _db.Cryovials.CountAsync(c =>
            c.MaterialId == material.Id && c.PreparedAt >= yearStart && c.PreparedAt < yearEnd);

        var sequence = (countThisYear + 1).ToString("D2");
        var codePrefix = !string.IsNullOrWhiteSpace(material.Code) ? material.Code : SanitizeForCodePrefix(material.MaterialName);
        var code = $"{codePrefix}/{sequence}/{DateTime.UtcNow:yy}";

        var cryovial = new Cryovial
        {
            Code = code,
            MaterialId = material.Id,
            OrganismId = material.OrganismId.Value,
            OrganismNameSnapshot = material.Organism.ScientificName,
            ManufacturerName = material.ManufacturerName,
            ExpiryDate = request.ExpiryDate,
            NumberOfVialsPrepared = request.NumberOfVialsPrepared,
            VialsRemaining = request.NumberOfVialsPrepared,
            StorageCondition = request.StorageCondition,
            PhysicalCheckText = request.PhysicalCheckText,
            ApprovalStatus = ApprovalGateStatus.PendingReview,
            PreparedByUserId = request.UserId
        };

        foreach (var row in request.Panel)
        {
            cryovial.IdentityConfirmations.Add(new IdentityConfirmationEntry
            {
                MediaId = row.MediaId, IncubatorEquipmentId = row.IncubatorEquipmentId,
                IncubationStart = row.IncubationStart, IncubationEnd = row.IncubationEnd, ObservationText = row.ObservationText
            });
        }

        _db.Cryovials.Add(cryovial);
        await _db.SaveChangesAsync();
        return cryovial;
    }

    // A rejected batch is also destroyed - a batch whose identity
    // confirmation failed must not linger usable.
    //
    // The decision is signed (password re-verified) and logged, and the
    // approver may not be the person who prepared the batch: this gate is
    // the only thing standing between an unverified batch and every media
    // evaluation that will later challenge organisms from it.
    public async Task<Cryovial> ApproveAsync(int cryovialId, bool approved, int userId, string password, string? comment, string? ipAddress)
    {
        var cryovial = await _db.Cryovials.FirstOrDefaultAsync(c => c.Id == cryovialId)
            ?? throw new InvalidOperationException($"Cryovial {cryovialId} not found.");

        if (cryovial.ApprovalStatus != ApprovalGateStatus.PendingReview)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} has already been decided ({cryovial.ApprovalStatus}).");

        if (await _segregationOfDuties.DidUserPrepareCryovialAsync(cryovialId, userId))
            throw new InvalidOperationException("You cannot approve a cryovial batch you prepared.");

        // Signs first - if password verification fails, nothing below is
        // written (the signature, the event, and the state change below
        // commit together in the single SaveChangesAsync at the end).
        await _reviewGate.SignAndLogAsync(
            ReviewEntityTypes.Cryovial, cryovialId, userId, password,
            approved ? SignatureMeaning.Approved : SignatureMeaning.Rejected,
            ReviewWorkflowEventType.ApprovalDecisionMade, comment, ipAddress,
            approved ? ApprovalDecision.Approve : ApprovalDecision.Reject);

        cryovial.ApprovalStatus = approved ? ApprovalGateStatus.Approved : ApprovalGateStatus.Rejected;
        cryovial.ApprovedByUserId = userId;
        cryovial.ApprovedAt = DateTime.UtcNow;
        if (!approved) cryovial.IsDestroyed = true;

        await _db.SaveChangesAsync();

        // Freeze an immutable PDF of the batch record as decided.
        var document = await _summary.BuildReportDocumentAsync(cryovialId);
        if (document is not null)
            await _archive.ArchiveAsync(ReviewEntityTypes.Cryovial, cryovialId, document,
                approved ? "Cryovial batch approved" : "Cryovial batch rejected", userId);

        return cryovial;
    }

    // Vial-level usage tracking only - deliberately NOT tied to GPT.
    // GptWorkflowEngine.EnsureCryovialApprovedAsync references an
    // approved batch and consumes nothing, because one thawed vial is
    // used across multiple media-type GPT runs.
    public async Task ThawVialAsync(int cryovialId, int userId, string? notes)
    {
        var cryovial = await _db.Cryovials.FirstOrDefaultAsync(c => c.Id == cryovialId)
            ?? throw new InvalidOperationException($"Cryovial {cryovialId} not found.");

        if (cryovial.ApprovalStatus != ApprovalGateStatus.Approved)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} is not approved - cannot thaw a vial.");
        if (cryovial.IsDestroyed)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} has been destroyed and cannot be used.");
        if (cryovial.ExpiryDate.Date < DateTime.UtcNow.Date)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} is expired and cannot be used.");
        if (cryovial.VialsRemaining <= 0)
            throw new InvalidOperationException($"No vials remaining in batch {cryovial.Code}.");

        cryovial.VialsRemaining--;
        _db.ThawEvents.Add(new ThawEvent { CryovialId = cryovialId, ThawedByUserId = userId, Notes = notes });
        await _db.SaveChangesAsync();
    }

    public async Task DestroyAsync(int cryovialId)
    {
        var cryovial = await _db.Cryovials.FirstOrDefaultAsync(c => c.Id == cryovialId)
            ?? throw new InvalidOperationException($"Cryovial {cryovialId} not found.");
        cryovial.IsDestroyed = true;
        await _db.SaveChangesAsync();
    }

    // Used by GPT: a Cryovial batch can only supply challenge organisms if approved.
    public async Task<bool> IsCryovialApprovedAsync(int cryovialId) =>
        await _db.Cryovials.AnyAsync(c => c.Id == cryovialId && c.ApprovalStatus == ApprovalGateStatus.Approved && !c.IsDestroyed);

    // Material.Code isn't guaranteed present (nullable) - falls back to
    // an alphanumeric, uppercased version of the material's name so the
    // code always has a usable prefix. Mirrors MediaPreparationService.
    private static string SanitizeForCodePrefix(string materialName) =>
        new string(materialName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}

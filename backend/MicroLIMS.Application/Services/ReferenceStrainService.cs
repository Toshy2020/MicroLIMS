using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record IdentityConfirmationRow(int MediaId, int IncubatorEquipmentId, DateTime IncubationStart, DateTime IncubationEnd, string ObservationText);
public record ReceiveStrainRequest(string OrganismName, string AtccNumber, int NumberOfDiscs, string ManufacturerName, DateTime ExpiryDate, string StorageCondition, string PhysicalCheckText, List<IdentityConfirmationRow> Panel, int UserId);
public record PrepareCryovialsRequest(int ReferenceStrainId, string ManufacturerName, DateTime ExpiryDate, int NumberOfVialsPrepared, string StorageCondition, string PhysicalCheckText, List<IdentityConfirmationRow> Panel, int DiscsUsed, int UserId);

public class ReferenceStrainService
{
    private const int MaxRsPassage = 2;
    private const int MaxCvPassage = 3;

    private readonly MicroLimsDbContext _db;

    public ReferenceStrainService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<ReferenceStrain>> GetAllAsync() =>
        await _db.ReferenceStrains.Include(s => s.Cryovials).Include(s => s.IdentityConfirmations).ToListAsync();

    // Multiple RS can be received in one session - caller loops this per row.
    public async Task<ReferenceStrain> ReceiveAsync(ReceiveStrainRequest request)
    {
        foreach (var row in request.Panel)
        {
            var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == row.MediaId)
                ?? throw new InvalidOperationException($"Media {row.MediaId} not found.");
            if (!media.IsReleasedForUse)
                throw new InvalidOperationException($"Media lot {media.LotNumber} is not GPT-released - cannot be used for identity confirmation.");
        }

        var strain = new ReferenceStrain
        {
            OrganismName = request.OrganismName,
            AtccNumber = request.AtccNumber,
            PassageNumber = 1,
            NumberOfDiscs = request.NumberOfDiscs,
            DiscsRemaining = request.NumberOfDiscs,
            ManufacturerName = request.ManufacturerName,
            ExpiryDate = request.ExpiryDate,
            StorageCondition = request.StorageCondition,
            PhysicalCheckText = request.PhysicalCheckText,
            ReceivedByUserId = request.UserId
        };

        if (strain.PassageNumber > MaxRsPassage)
            throw new InvalidOperationException($"Reference strain passage number cannot exceed {MaxRsPassage}.");

        foreach (var row in request.Panel)
        {
            strain.IdentityConfirmations.Add(new IdentityConfirmationEntry
            {
                MediaId = row.MediaId, IncubatorEquipmentId = row.IncubatorEquipmentId,
                IncubationStart = row.IncubationStart, IncubationEnd = row.IncubationEnd, ObservationText = row.ObservationText
            });
        }

        _db.ReferenceStrains.Add(strain);
        await _db.SaveChangesAsync();

        // Code: RS + receiving sequence + MM/YY (assigned once saved, per spec).
        strain.Code = $"RS {strain.Id:D2}/{DateTime.UtcNow:MM}/{DateTime.UtcNow:yy}";
        await _db.SaveChangesAsync();
        return strain;
    }

    public async Task<ReferenceStrain> ApproveAsync(int referenceStrainId, bool approved, int userId)
    {
        var strain = await _db.ReferenceStrains.FirstOrDefaultAsync(s => s.Id == referenceStrainId)
            ?? throw new InvalidOperationException($"Reference strain {referenceStrainId} not found.");
        strain.ApprovalStatus = approved ? ApprovalGateStatus.Approved : ApprovalGateStatus.Rejected;
        await _db.SaveChangesAsync();
        return strain;
    }

    // Multiple cryovial batches can be prepared in one session.
    public async Task<Cryovial> PrepareCryovialsAsync(PrepareCryovialsRequest request)
    {
        var strain = await _db.ReferenceStrains.FirstOrDefaultAsync(s => s.Id == request.ReferenceStrainId)
            ?? throw new InvalidOperationException($"Reference strain {request.ReferenceStrainId} not found.");

        if (strain.ApprovalStatus != ApprovalGateStatus.Approved)
            throw new InvalidOperationException("This reference strain must be approved before cryovials can be prepared from it.");

        if (strain.ExpiryDate.Date < DateTime.UtcNow.Date)
            throw new InvalidOperationException($"Reference strain {strain.Code} is expired and cannot be used to prepare cryovials.");

        if (request.DiscsUsed <= 0)
            throw new InvalidOperationException("Discs used must be greater than zero.");

        if (strain.DiscsRemaining < request.DiscsUsed)
            throw new InvalidOperationException(
                $"Insufficient discs: reference strain {strain.Code} has {strain.DiscsRemaining} disc(s) remaining, {request.DiscsUsed} requested.");

        var passageNumber = strain.PassageNumber + 1;
        if (passageNumber > MaxCvPassage)
            throw new InvalidOperationException($"Cryovial passage number cannot exceed {MaxCvPassage}.");

        foreach (var row in request.Panel)
        {
            var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == row.MediaId)
                ?? throw new InvalidOperationException($"Media {row.MediaId} not found.");
            if (!media.IsReleasedForUse)
                throw new InvalidOperationException($"Media lot {media.LotNumber} is not GPT-released - cannot be used for identity confirmation.");
        }

        // Per-session vial sequence resets each new preparation session
        // (not cumulative across sessions for the same RS).
        var vialSequence = 1;
        var cryovial = new Cryovial
        {
            ReferenceStrainId = strain.Id,
            PassageNumber = passageNumber,
            ManufacturerName = request.ManufacturerName,
            ExpiryDate = request.ExpiryDate,
            NumberOfVialsPrepared = request.NumberOfVialsPrepared,
            StorageCondition = request.StorageCondition,
            PhysicalCheckText = request.PhysicalCheckText
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
        strain.DiscsRemaining -= request.DiscsUsed;
        await _db.SaveChangesAsync();

        // Code: RS code + per-session vial sequence, e.g. CV01/RS0107026.
        cryovial.Code = $"CV{vialSequence:D2}/{strain.Code.Replace(" ", "")}";
        await _db.SaveChangesAsync();
        return cryovial;
    }

    public async Task<Cryovial> ApproveCryovialAsync(int cryovialId, bool approved, int userId)
    {
        var cryovial = await _db.Cryovials.FirstOrDefaultAsync(c => c.Id == cryovialId)
            ?? throw new InvalidOperationException($"Cryovial {cryovialId} not found.");
        cryovial.ApprovalStatus = approved ? ApprovalGateStatus.Approved : ApprovalGateStatus.Rejected;
        await _db.SaveChangesAsync();
        return cryovial;
    }

    public async Task<PassageEvent> RecordPassageAsync(int cryovialId, int userId, string? notes)
    {
        var cryovial = await _db.Cryovials.FirstOrDefaultAsync(c => c.Id == cryovialId)
            ?? throw new InvalidOperationException($"Cryovial {cryovialId} not found.");

        if (cryovial.IsDestroyed)
            throw new InvalidOperationException("Cannot record a passage for a destroyed cryovial.");

        cryovial.PassageNumber++;
        var passageEvent = new PassageEvent { CryovialId = cryovialId, PassageNumber = cryovial.PassageNumber, PerformedByUserId = userId, Notes = notes };
        _db.PassageEvents.Add(passageEvent);
        await _db.SaveChangesAsync();
        return passageEvent;
    }

    public async Task MarkThawedAsync(int cryovialId)
    {
        var cryovial = await _db.Cryovials.FirstOrDefaultAsync(c => c.Id == cryovialId)
            ?? throw new InvalidOperationException($"Cryovial {cryovialId} not found.");
        cryovial.ThawedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DestroyAsync(int cryovialId)
    {
        var cryovial = await _db.Cryovials.FirstOrDefaultAsync(c => c.Id == cryovialId)
            ?? throw new InvalidOperationException($"Cryovial {cryovialId} not found.");
        cryovial.IsDestroyed = true;
        await _db.SaveChangesAsync();
    }

    // Used by GPT: a Cryovial can only supply challenge organisms if approved.
    public async Task<bool> IsCryovialApprovedAsync(int cryovialId) =>
        await _db.Cryovials.AnyAsync(c => c.Id == cryovialId && c.ApprovalStatus == ApprovalGateStatus.Approved && !c.IsDestroyed);
}

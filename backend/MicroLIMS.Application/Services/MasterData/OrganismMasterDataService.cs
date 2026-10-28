using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: organism. Behind OrganismMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class OrganismMasterDataService
{
    private readonly IMicroLimsDbContext _db;

    public OrganismMasterDataService(IMicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<OrganismResponse>> GetOrganismsAsync() =>
        (await _db.Organisms.AsNoTracking().OrderBy(o => o.ScientificName).ToListAsync()).Select(OrganismResponse.From).ToList();

    public async Task<OrganismResponse> CreateOrganismAsync(CreateOrganismRequest request)
    {
        if (await _db.Organisms.AnyAsync(o => o.ScientificName.ToLower() == request.ScientificName.ToLower()))
            throw new InvalidOperationException($"Organism \"{request.ScientificName}\" already exists in the Organism list.");

        await EnsureAtccUniqueAsync(request.AtccNumber, excludeId: null);

        var entity = new Organism { ScientificName = request.ScientificName, AtccNumber = request.AtccNumber, CommonName = request.CommonName, Description = request.Description };
        _db.Organisms.Add(entity);
        await _db.SaveChangesAsync();
        return OrganismResponse.From(entity);
    }

    public async Task<OrganismResponse> UpdateOrganismAsync(int id, UpdateOrganismRequest request)
    {
        var entity = await _db.Organisms.FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException($"Organism {id} not found.");
        RecordVersion.EnsureCurrent(_db, entity);

        if (await _db.Organisms.AnyAsync(o => o.Id != id && o.ScientificName.ToLower() == request.ScientificName.ToLower()))
            throw new InvalidOperationException($"Organism \"{request.ScientificName}\" already exists in the Organism list.");

        await EnsureAtccUniqueAsync(request.AtccNumber, excludeId: id);

        entity.ScientificName = request.ScientificName;
        entity.AtccNumber = request.AtccNumber;
        entity.CommonName = request.CommonName;
        entity.Description = request.Description;
        await _db.SaveChangesAsync();
        return OrganismResponse.From(entity);
    }

    // The legacy ATCC number is unique (filtered unique index); report a clash
    // by name instead of as a raw database error.
    private async Task EnsureAtccUniqueAsync(string? atccNumber, int? excludeId)
    {
        var atcc = string.IsNullOrWhiteSpace(atccNumber) ? null : atccNumber.Trim().ToLower();
        if (atcc == null) return;
        var clash = await _db.Organisms.FirstOrDefaultAsync(o => o.Id != excludeId && o.AtccNumber != null && o.AtccNumber.Trim().ToLower() == atcc);
        if (clash != null)
            throw new InvalidOperationException($"ATCC number \"{atccNumber!.Trim()}\" is already used by organism \"{clash.ScientificName}\".");
    }

    // Blocked (not a raw FK error) if any MediaConfigurationChallenge,
    // MediaEvaluationChallenge, Cryovial, Material, test-step target
    // organism or reference strain still references this organism - all
    // are Restrict FKs (see OrganismConfiguration and friends), same
    // "guard with a clear message" pattern as ItemService.DeleteAsync.
    public async Task<object> DeleteOrganismAsync(int id)
    {
        var entity = await _db.Organisms.FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException($"Organism {id} not found.");

        var configChallengeCount = await _db.MediaConfigurationChallenges.CountAsync(c => c.OrganismId == id);
        var challengeCount = await _db.MediaEvaluationChallenges.CountAsync(c => c.OrganismId == id);
        var cryovialCount = await _db.Cryovials.CountAsync(c => c.OrganismId == id);
        var materialCount = await _db.Materials.CountAsync(m => m.OrganismId == id);
        var targetStepCount = await _db.TestWorkflowSteps.CountAsync(s => s.TargetOrganismId == id);
        var strainCount = await _db.ReferenceOrganisms.CountAsync(r => r.OrganismId == id);
        var totalUses = configChallengeCount + challengeCount + cryovialCount + materialCount + targetStepCount + strainCount;

        if (totalUses > 0)
            throw new InvalidOperationException(
                $"Cannot delete '{entity.ScientificName}' - it is referenced by {totalUses} record(s) " +
                $"(Media Configuration Challenges: {configChallengeCount}, Media Evaluation Challenges: {challengeCount}, Cryovials: {cryovialCount}, Materials: {materialCount}, Test Master target organism steps: {targetStepCount}, Reference organisms: {strainCount}).");

        _db.Organisms.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }
}

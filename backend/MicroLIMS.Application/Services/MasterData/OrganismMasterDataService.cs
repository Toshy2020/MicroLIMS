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

        var entity = new Organism { ScientificName = request.ScientificName, AtccNumber = request.AtccNumber, CommonName = request.CommonName, Description = request.Description };
        _db.Organisms.Add(entity);
        await _db.SaveChangesAsync();
        return OrganismResponse.From(entity);
    }

    public async Task<OrganismResponse> UpdateOrganismAsync(int id, UpdateOrganismRequest request)
    {
        var entity = await _db.Organisms.FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException($"Organism {id} not found.");

        if (await _db.Organisms.AnyAsync(o => o.Id != id && o.ScientificName.ToLower() == request.ScientificName.ToLower()))
            throw new InvalidOperationException($"Organism \"{request.ScientificName}\" already exists in the Organism list.");

        entity.ScientificName = request.ScientificName;
        entity.AtccNumber = request.AtccNumber;
        entity.CommonName = request.CommonName;
        entity.Description = request.Description;
        await _db.SaveChangesAsync();
        return OrganismResponse.From(entity);
    }

    // Blocked (not a raw FK error) if any MediaConfigurationChallenge,
    // MediaEvaluationChallenge, Cryovial, or Material still references
    // this organism - all four are Restrict FKs (see OrganismConfiguration
    // and friends), same "guard with a clear message" pattern as
    // ItemService.DeleteAsync.
    public async Task<object> DeleteOrganismAsync(int id)
    {
        var entity = await _db.Organisms.FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException($"Organism {id} not found.");

        var configChallengeCount = await _db.MediaConfigurationChallenges.CountAsync(c => c.OrganismId == id);
        var challengeCount = await _db.MediaEvaluationChallenges.CountAsync(c => c.OrganismId == id);
        var cryovialCount = await _db.Cryovials.CountAsync(c => c.OrganismId == id);
        var materialCount = await _db.Materials.CountAsync(m => m.OrganismId == id);
        var totalUses = configChallengeCount + challengeCount + cryovialCount + materialCount;

        if (totalUses > 0)
            throw new InvalidOperationException(
                $"Cannot delete '{entity.ScientificName}' - it is referenced by {totalUses} record(s) " +
                $"(Media Configuration Challenges: {configChallengeCount}, Media Evaluation Challenges: {challengeCount}, Cryovials: {cryovialCount}, Materials: {materialCount}).");

        _db.Organisms.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }
}

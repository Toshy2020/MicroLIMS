using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: media. Behind MediaMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class MediaMasterDataService
{
    private readonly IMicroLimsDbContext _db;

    public MediaMasterDataService(IMicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<object> GetMediaConfigurationsAsync()
    {
        var configs = await _db.MediaConfigurations
            .Include(m => m.IncubationCondition)
            .OrderBy(m => m.Name)
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.MediaProductId,
                MediaProductCode = m.MediaProduct != null ? m.MediaProduct.Code : null,
                m.EvaluationType,
                m.MediaIncubationConditionId,
                IncubationMinHours = m.IncubationCondition != null ? m.IncubationCondition.IncubationMinHours : 0,
                IncubationMaxHours = m.IncubationCondition != null ? m.IncubationCondition.IncubationMaxHours : 0,
                TemperatureMin = m.IncubationCondition != null ? m.IncubationCondition.TemperatureMin : 0m,
                TemperatureMax = m.IncubationCondition != null ? m.IncubationCondition.TemperatureMax : 0m,
                m.RecoveryPercentMin,
                m.RecoveryPercentMax,
                Challenges = m.Challenges.Select(c => new
                {
                    c.Id,
                    c.MediaConfigurationId,
                    c.OrganismId,
                    c.ChallengeRole,
                    c.ExpectedDescription,
                    c.InitialInoculum,
                    Organism = c.Organism == null ? null : new
                    {
                        c.Organism.Id,
                        c.Organism.ScientificName,
                        c.Organism.AtccNumber,
                        c.Organism.CommonName
                    }
                }).ToList()
            })
            .ToListAsync();

        return configs;
    }

    public async Task<object> CreateMediaConfigurationAsync(CreateMediaConfigurationRequest request)
    {
        var product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == request.MediaProductId)
            ?? throw new InvalidOperationException($"Media product with ID {request.MediaProductId} not found.");

        var condition = await _db.MediaIncubationConditions.FirstOrDefaultAsync(c => c.Id == request.MediaIncubationConditionId)
            ?? throw new InvalidOperationException($"Incubation condition {request.MediaIncubationConditionId} not found.");
        if (condition.MediaProductId != product.Id)
        {
            throw new InvalidOperationException("The chosen incubation condition belongs to a different media product.");
        }

        var exists = await _db.MediaConfigurations.AnyAsync(m => m.MediaProductId == request.MediaProductId);
        if (exists)
        {
            throw new InvalidOperationException($"'{product.Name}' already has an evaluation configuration. Edit it instead.");
        }

        if (!Enum.IsDefined(typeof(EvaluationType), request.EvaluationType))
            throw new InvalidOperationException("Valid evaluation type is required.");

        if (request.EvaluationType == EvaluationType.GrowthPromotion)
        {
            if (request.RecoveryPercentMin.HasValue && request.RecoveryPercentMax.HasValue &&
                request.RecoveryPercentMin > request.RecoveryPercentMax)
            {
                throw new InvalidOperationException("Recovery percent min cannot exceed recovery percent max.");
            }
        }

        var entity = new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = request.EvaluationType,
            MediaIncubationConditionId = condition.Id,
            RecoveryPercentMin = request.EvaluationType == EvaluationType.GrowthPromotion ? request.RecoveryPercentMin : null,
            RecoveryPercentMax = request.EvaluationType == EvaluationType.GrowthPromotion ? request.RecoveryPercentMax : null
        };

        if (request.Challenges != null && request.Challenges.Count > 0)
        {
            foreach (var ch in request.Challenges)
            {
                var organismExists = await _db.Organisms.AnyAsync(o => o.Id == ch.OrganismId);
                if (!organismExists)
                    throw new InvalidOperationException($"Organism with ID {ch.OrganismId} not found.");

                if (request.EvaluationType == EvaluationType.IndicationInhibition)
                {
                    if (!ch.ChallengeRole.HasValue || !Enum.IsDefined(typeof(ChallengeRole), ch.ChallengeRole.Value))
                        throw new InvalidOperationException("Challenge role is required for Indication/Inhibition evaluation.");
                }

                entity.Challenges.Add(new MediaConfigurationChallenge
                {
                    OrganismId = ch.OrganismId,
                    ChallengeRole = request.EvaluationType == EvaluationType.IndicationInhibition ? ch.ChallengeRole : null,
                    ExpectedDescription = (request.EvaluationType == EvaluationType.IndicationInhibition && ch.ChallengeRole == ChallengeRole.Indication)
                        ? ch.ExpectedDescription
                        : null,
                    InitialInoculum = string.IsNullOrWhiteSpace(ch.InitialInoculum) ? null : ch.InitialInoculum.Trim()
                });
            }
        }

        _db.MediaConfigurations.Add(entity);
        await _db.SaveChangesAsync();

        var created = new
        {
            entity.Id,
            entity.Name,
            entity.MediaProductId,
            MediaProductCode = product.Code,
            entity.EvaluationType,
            entity.MediaIncubationConditionId,
            condition.IncubationMinHours,
            condition.IncubationMaxHours,
            condition.TemperatureMin,
            condition.TemperatureMax,
            entity.RecoveryPercentMin,
            entity.RecoveryPercentMax,
            Challenges = entity.Challenges.Select(c => new
            {
                c.Id,
                c.MediaConfigurationId,
                c.OrganismId,
                c.ChallengeRole,
                c.ExpectedDescription,
                c.InitialInoculum
            }).ToList()
        };

        return created;
    }

    public async Task<object> UpdateMediaConfigurationAsync(int id, UpdateMediaConfigurationRequest request)
    {
        var entity = await _db.MediaConfigurations
            .Include(m => m.Challenges)
            .FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new NotFoundException($"Media configuration {id} not found.");

        if (request.MediaProductId != entity.MediaProductId)
        {
            throw new InvalidOperationException("A configuration can't be moved to another media product.");
        }

        var product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == entity.MediaProductId)
            ?? throw new InvalidOperationException($"Media product with ID {entity.MediaProductId} not found.");

        var condition = await _db.MediaIncubationConditions.FirstOrDefaultAsync(c => c.Id == request.MediaIncubationConditionId)
            ?? throw new InvalidOperationException($"Incubation condition {request.MediaIncubationConditionId} not found.");
        if (condition.MediaProductId != entity.MediaProductId)
        {
            throw new InvalidOperationException("The chosen incubation condition belongs to a different media product.");
        }

        if (!Enum.IsDefined(typeof(EvaluationType), request.EvaluationType))
            throw new InvalidOperationException("Valid evaluation type is required.");

        if (request.EvaluationType == EvaluationType.GrowthPromotion)
        {
            if (request.RecoveryPercentMin.HasValue && request.RecoveryPercentMax.HasValue &&
                request.RecoveryPercentMin > request.RecoveryPercentMax)
            {
                throw new InvalidOperationException("Recovery percent min cannot exceed recovery percent max.");
            }
        }

        entity.Name = product.Name;
        entity.EvaluationType = request.EvaluationType;
        entity.MediaIncubationConditionId = condition.Id;
        entity.RecoveryPercentMin = request.EvaluationType == EvaluationType.GrowthPromotion ? request.RecoveryPercentMin : null;
        entity.RecoveryPercentMax = request.EvaluationType == EvaluationType.GrowthPromotion ? request.RecoveryPercentMax : null;

        var incomingChallenges = request.Challenges ?? new List<CreateMediaConfigurationChallengeRequest>();

        foreach (var ch in incomingChallenges)
        {
            var organismExists = await _db.Organisms.AnyAsync(o => o.Id == ch.OrganismId);
            if (!organismExists)
                throw new InvalidOperationException($"Organism with ID {ch.OrganismId} not found.");

            if (request.EvaluationType == EvaluationType.IndicationInhibition)
            {
                if (!ch.ChallengeRole.HasValue || !Enum.IsDefined(typeof(ChallengeRole), ch.ChallengeRole.Value))
                    throw new InvalidOperationException("Challenge role is required for Indication/Inhibition evaluation.");
            }
        }

        var duplicateIncoming = incomingChallenges
            .GroupBy(c => new { c.OrganismId, Role = request.EvaluationType == EvaluationType.IndicationInhibition ? c.ChallengeRole : null })
            .Any(g => g.Count() > 1);
        if (duplicateIncoming)
            throw new InvalidOperationException("Duplicate challenge organism and role combination in request.");

        var toRemove = entity.Challenges.Where(existing =>
            !incomingChallenges.Any(inc =>
                inc.OrganismId == existing.OrganismId &&
                (request.EvaluationType != EvaluationType.IndicationInhibition || inc.ChallengeRole == existing.ChallengeRole)
            )).ToList();

        foreach (var rem in toRemove)
        {
            entity.Challenges.Remove(rem);
        }

        foreach (var inc in incomingChallenges)
        {
            var role = request.EvaluationType == EvaluationType.IndicationInhibition ? inc.ChallengeRole : null;
            var desc = (request.EvaluationType == EvaluationType.IndicationInhibition && inc.ChallengeRole == ChallengeRole.Indication)
                ? inc.ExpectedDescription
                : null;
            var inoculum = string.IsNullOrWhiteSpace(inc.InitialInoculum) ? null : inc.InitialInoculum.Trim();

            var existing = entity.Challenges.FirstOrDefault(e =>
                e.OrganismId == inc.OrganismId &&
                (request.EvaluationType != EvaluationType.IndicationInhibition || e.ChallengeRole == inc.ChallengeRole));

            if (existing != null)
            {
                existing.ChallengeRole = role;
                existing.ExpectedDescription = desc;
                existing.InitialInoculum = inoculum;
            }
            else
            {
                entity.Challenges.Add(new MediaConfigurationChallenge
                {
                    OrganismId = inc.OrganismId,
                    ChallengeRole = role,
                    ExpectedDescription = desc,
                    InitialInoculum = inoculum
                });
            }
        }

        await _db.SaveChangesAsync();

        var updated = new
        {
            entity.Id,
            entity.Name,
            entity.MediaProductId,
            MediaProductCode = product.Code,
            entity.EvaluationType,
            entity.MediaIncubationConditionId,
            condition.IncubationMinHours,
            condition.IncubationMaxHours,
            condition.TemperatureMin,
            condition.TemperatureMax,
            entity.RecoveryPercentMin,
            entity.RecoveryPercentMax,
            Challenges = entity.Challenges.Select(c => new
            {
                c.Id,
                c.MediaConfigurationId,
                c.OrganismId,
                c.ChallengeRole,
                c.ExpectedDescription,
                c.InitialInoculum
            }).ToList()
        };

        return updated;
    }

    public async Task<object> DeleteMediaConfigurationAsync(int id)
    {
        var entity = await _db.MediaConfigurations.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new NotFoundException($"Media configuration {id} not found.");

        _db.MediaConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }
}

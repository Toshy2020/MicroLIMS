using MicroLIMS.Application.Helpers;
using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

// Query surface + thin pass-through to IMediaEvaluationEngine for the
// mutating actions, mirroring the old GptService's shape.
public class MediaEvaluationService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IMediaEvaluationEngine _engine;
    private readonly TimeProvider _time;

    public MediaEvaluationService(IMicroLimsDbContext db, IMediaEvaluationEngine engine, TimeProvider? timeProvider = null)
    {
        _db = db;
        _engine = engine;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<List<MediaEvaluationResponse>> GetAllAsync(MediaEvaluationStatus? status = null, IReadOnlyCollection<int>? sectionIds = null)
    {
        var query = _db.MediaEvaluations.Include(e => e.Media!).ThenInclude(m => m.Material).AsQueryable();
        if (sectionIds != null) query = query.Where(e => sectionIds.Contains(e.Media!.Material!.SectionId));
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);
        return (await query.OrderByDescending(e => e.Id).ToListAsync()).Select(MediaEvaluationResponse.From).ToList();
    }

    public async Task<MediaEvaluationResponse> GetByIdAsync(int id) =>
        MediaEvaluationResponse.From(await _db.MediaEvaluations
            .Include(e => e.Media!).ThenInclude(m => m.Material)
            .Include(e => e.Challenges).ThenInclude(c => c.Cryovial)
            .Include(e => e.Challenges).ThenInclude(c => c.ThawEvent)
            .Include(e => e.Challenges).ThenInclude(c => c.Incubation)
            .Include(e => e.Challenges).ThenInclude(c => c.Organism)
            .Include(e => e.Challenges).ThenInclude(c => c.ReferenceMedia)
            .Include(e => e.Challenges).ThenInclude(c => c.LyophilizedDisk)
            .FirstOrDefaultAsync(e => e.Id == id)
        ?? throw new NotFoundException($"Media evaluation {id} not found."));

    // Every vial thawed in the last 24 hours (and not destroyed) out of an
    // approved, unexpired batch of the challenge's organism in the
    // evaluated lot's section, newest first,
    // with the media lots each one has already served. Empty means the
    // analyst has to thaw a vial (ThawAndSelectVialAsync) first.
    public async Task<List<ThawedVialOptionResponse>> GetThawedVialOptionsAsync(int challengeId)
    {
        var challenge = await _db.MediaEvaluationChallenges
            .Where(c => c.Id == challengeId)
            .Select(c => new { c.OrganismId, SectionId = c.MediaEvaluation!.Media!.Material!.SectionId })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Challenge {challengeId} not found.");

        var now = _time.GetUtcNow().UtcDateTime;
        var today = now.Date;
        var thawedSince = now - CryovialThaw.UsableFor;
        var thaws = await _db.ThawEvents
            .Include(t => t.Cryovial)
            .Include(t => t.UsedInChallenges).ThenInclude(c => c.MediaEvaluation!).ThenInclude(e => e.Media)
            .Where(t => t.Cryovial!.OrganismId == challenge.OrganismId
                && t.Cryovial.Material!.SectionId == challenge.SectionId
                && t.Cryovial.ApprovalStatus == ApprovalGateStatus.Approved
                && !t.Cryovial.IsDestroyed
                && t.Cryovial.ExpiryDate >= today
                && t.ThawedAt > thawedSince
                && t.DestructionItem == null)
            .OrderByDescending(t => t.ThawedAt)
            .ToListAsync();

        var userIds = thaws.Select(t => t.ThawedByUserId).Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

        return thaws.Select(t => new ThawedVialOptionResponse
        {
            ThawEventId = t.Id,
            CryovialId = t.CryovialId,
            CryovialCode = t.Cryovial!.Code,
            ThawedAt = t.ThawedAt,
            ThawedByUserId = t.ThawedByUserId,
            ThawedByName = names.TryGetValue(t.ThawedByUserId, out var n) ? n : "Unknown",
            Notes = t.Notes,
            UsedInMediaLots = t.UsedInChallenges
                .Select(c => c.MediaEvaluation?.Media?.LotNumber)
                .OfType<string>().Distinct().OrderBy(l => l).ToList(),
        }).ToList();
    }

    public Task SelectThawedVialAsync(int challengeId, int thawEventId, int userId) =>
        _engine.SelectThawedVialAsync(challengeId, thawEventId, userId);

    public async Task<ThawEventResponse> ThawAndSelectVialAsync(int challengeId, int cryovialId, int userId, string? notes) =>
        ThawEventResponse.From(await _engine.ThawAndSelectVialAsync(challengeId, cryovialId, userId, notes));

    public Task SelectLyophilizedDiskAsync(int challengeId, int materialId, int userId) =>
        _engine.SelectLyophilizedDiskAsync(challengeId, materialId, userId);

    public async Task<IncubationResponse> RecordIncubationAsync(int challengeId, int incubatorEquipmentId, int userId) =>
        IncubationResponse.From(await _engine.RecordIncubationAsync(challengeId, incubatorEquipmentId, userId));

    public async Task<MediaEvaluationChallengeResponse> RecordResultAsync(RecordResultRequest request) =>
        MediaEvaluationChallengeResponse.From(await _engine.RecordResultAsync(request));
}

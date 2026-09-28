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

    public MediaEvaluationService(IMicroLimsDbContext db, IMediaEvaluationEngine engine)
    {
        _db = db;
        _engine = engine;
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
            .Include(e => e.Challenges).ThenInclude(c => c.Incubation)
            .Include(e => e.Challenges).ThenInclude(c => c.Organism)
            .Include(e => e.Challenges).ThenInclude(c => c.ReferenceMedia)
            .Include(e => e.Challenges).ThenInclude(c => c.LyophilizedDisk)
            .FirstOrDefaultAsync(e => e.Id == id)
        ?? throw new NotFoundException($"Media evaluation {id} not found."));

    public Task SelectCryovialAsync(int challengeId, int cryovialId, int userId) =>
        _engine.SelectCryovialAsync(challengeId, cryovialId, userId);

    public Task SelectLyophilizedDiskAsync(int challengeId, int materialId, int userId) =>
        _engine.SelectLyophilizedDiskAsync(challengeId, materialId, userId);

    public async Task<IncubationResponse> RecordIncubationAsync(int challengeId, int incubatorEquipmentId, int userId) =>
        IncubationResponse.From(await _engine.RecordIncubationAsync(challengeId, incubatorEquipmentId, userId));

    public async Task<MediaEvaluationChallengeResponse> RecordResultAsync(RecordResultRequest request) =>
        MediaEvaluationChallengeResponse.From(await _engine.RecordResultAsync(request));
}

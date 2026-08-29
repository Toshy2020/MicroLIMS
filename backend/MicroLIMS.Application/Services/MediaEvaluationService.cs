using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Query surface + thin pass-through to IMediaEvaluationEngine for the
// mutating actions, mirroring the old GptService's shape.
public class MediaEvaluationService
{
    private readonly MicroLimsDbContext _db;
    private readonly IMediaEvaluationEngine _engine;

    public MediaEvaluationService(MicroLimsDbContext db, IMediaEvaluationEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    public async Task<List<MediaEvaluation>> GetAllAsync(MediaEvaluationStatus? status = null)
    {
        var query = _db.MediaEvaluations.Include(e => e.Media!).ThenInclude(m => m.Material).AsQueryable();
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);
        return await query.OrderByDescending(e => e.Id).ToListAsync();
    }

    public async Task<MediaEvaluation> GetByIdAsync(int id) =>
        await _db.MediaEvaluations
            .Include(e => e.Media!).ThenInclude(m => m.Material)
            .Include(e => e.Challenges).ThenInclude(c => c.Cryovial)
            .Include(e => e.Challenges).ThenInclude(c => c.Incubation)
            .Include(e => e.Challenges).ThenInclude(c => c.Organism)
            .Include(e => e.Challenges).ThenInclude(c => c.ReferenceMedia)
            .Include(e => e.Challenges).ThenInclude(c => c.LyophilizedDisk)
            .FirstOrDefaultAsync(e => e.Id == id)
        ?? throw new InvalidOperationException($"Media evaluation {id} not found.");

    public Task SelectCryovialAsync(int challengeId, int cryovialId, int userId) =>
        _engine.SelectCryovialAsync(challengeId, cryovialId, userId);

    public Task SelectLyophilizedDiskAsync(int challengeId, int materialId, int userId) =>
        _engine.SelectLyophilizedDiskAsync(challengeId, materialId, userId);

    public Task<Incubation> RecordIncubationAsync(int challengeId, int incubatorEquipmentId, int userId) =>
        _engine.RecordIncubationAsync(challengeId, incubatorEquipmentId, userId);

    public Task<MediaEvaluationChallenge> RecordResultAsync(RecordResultRequest request) =>
        _engine.RecordResultAsync(request);
}

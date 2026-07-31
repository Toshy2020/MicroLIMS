using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services;

public class GptService
{
    private readonly IGptWorkflowEngine _engine;
    private readonly MediaPreparationService _mediaPrep;

    public GptService(IGptWorkflowEngine engine, MediaPreparationService mediaPrep)
    {
        _engine = engine;
        _mediaPrep = mediaPrep;
    }

    public Task<List<Media>> GetAllMediaAsync() => _mediaPrep.GetAllAsync();

    public Task<Media> AdvanceStageAsync(int mediaId, int userId) => _engine.AdvanceStageAsync(mediaId, userId);

    public Task<GptChallengeResult> RecordGeneralAgarAsync(GeneralAgarChallengeRequest request) => _engine.RecordGeneralAgarChallengeAsync(request);
    public Task<GptChallengeResult> RecordGeneralBrothAsync(GeneralBrothChallengeRequest request) => _engine.RecordGeneralBrothChallengeAsync(request);
    public Task<GptChallengeResult> RecordSelectiveAsync(SelectiveChallengeRequest request) => _engine.RecordSelectiveChallengeAsync(request);

    public Task<Media> ReleaseAsync(int mediaId, int userId) => _engine.ReleaseAsync(mediaId, userId);
    public Task<bool> IsReleasedForUseAsync(int mediaId) => _engine.IsReleasedForUseAsync(mediaId);
}

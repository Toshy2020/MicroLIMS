using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.Competency;

public sealed record CompetencySubject(string TestCode, int SectionId, int? ItemId = null);

public sealed record CompetencyReason(string Code, string Text, IReadOnlyDictionary<string, string> Params);

public sealed record CompetencyDecision(
    bool Allowed,
    IReadOnlyList<CompetencyReason> Reasons,
    CompetencyReason? SopNotChecked,
    int? ViaOverrideId)
{
    public string? RecordingAuditReason => ViaOverrideId is int id
        ? $"Competency override #{id}" + (SopNotChecked is null ? string.Empty : $" {SopNotChecked.Text}")
        : SopNotChecked?.Text;

    public string[] ReasonTexts => Reasons.Select(reason => reason.Text).ToArray();

    public ActionCheck ToActionCheck() => Allowed
        ? ActionCheck.Ok
        : ActionCheck.No(
            Reasons[0].Code,
            string.Join(" ", ReasonTexts),
            Reasons[0].Params,
            Reasons.Select(r => new ApiError(r.Code, r.Params.Count > 0 ? r.Params : null, r.Text)).ToArray());
}

public interface ICompetencyGuard
{
    Task<CompetencyDecision> EvaluateAsync(int userId, int testOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, CompetencyDecision>> EvaluateManyAsync(
        int userId, IReadOnlyCollection<int> testOrderIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, CompetencyDecision>> EvaluateAssigneeAsync(
        int assigneeId, IReadOnlyCollection<int> testOrderIds, CancellationToken cancellationToken = default);
    // Same decision core, but evaluates orders even where the laboratory gate is off (impact preview only).
    Task<IReadOnlyDictionary<int, CompetencyDecision>> EvaluateIgnoringGateAsync(
        int userId, IReadOnlyCollection<int> testOrderIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<CompetencySubject, CompetencyDecision>> EvaluateSubjectsAsync(
        int userId, IReadOnlyCollection<CompetencySubject> subjects, bool assigneeWording,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<(int UserId, int TestOrderId), CompetencyDecision>> EvaluateCandidatesAsync(
        IReadOnlyCollection<int> userIds, IReadOnlyCollection<int> testOrderIds, CancellationToken cancellationToken = default);
    Task EnsureAllowedAsync(int userId, int testOrderId, CancellationToken cancellationToken = default);
}

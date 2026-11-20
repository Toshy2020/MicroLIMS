using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.Competency;

public sealed record CompetencyRefusalReasonDto(string Code, IReadOnlyDictionary<string, string> Params, string Reason);

public sealed record CompetencySkipDto(
    int TestOrderId, string TestCode, IReadOnlyList<CompetencyRefusalReasonDto> Reasons)
{
    public string Code => Reasons[0].Code;
    public string Reason => string.Join(" ", Reasons.Select(reason => reason.Reason));
}

internal sealed record CompetencyBatch(
    IReadOnlySet<int> Allowed,
    IReadOnlyList<CompetencySkipDto> Refused,
    string? SopNotChecked,
    CompetencyDecision? SingleRefusedDecision)
{
    internal void ThrowIfRefused()
    {
        if (SingleRefusedDecision is { Reasons.Count: 1 } single)
            single.ToActionCheck().ThrowIfRefused();
        if (Refused.Count != 0)
        {
            var reasons = string.Join(" ", Refused.Select(skip => skip.Reason));
            throw new BusinessRuleException(MessageCodes.CompetencyRecordingRefused, reasons)
            {
                ErrorDetails = Refused.SelectMany(skip => skip.Reasons)
                    .Select(reason => new ApiError(reason.Code, reason.Params, reason.Reason)).ToList(),
            };
        }
    }

    internal void ThrowIfAllRefused()
    {
        if (Allowed.Count == 0)
            ThrowIfRefused();
    }
}

internal static class CompetencyBatchExtensions
{
    internal static async Task<CompetencyBatch> FilterAsync(
        this ICompetencyGuard guard, int userId, IReadOnlyCollection<TestOrder> orders, CancellationToken ct)
    {
        // SOP-only refusals do not carry a test code in their message parameters.
        var testCodes = orders.DistinctBy(order => order.Id).ToDictionary(order => order.Id, order => order.TestCode);
        var decisions = await guard.EvaluateManyAsync(userId, testCodes.Keys, ct);
        var allowed = decisions.Where(pair => pair.Value.Allowed).Select(pair => pair.Key).ToHashSet();
        var refused = decisions.Where(pair => !pair.Value.Allowed)
            .Select(pair => new CompetencySkipDto(pair.Key, testCodes[pair.Key], pair.Value.Reasons
                .Select(reason => new CompetencyRefusalReasonDto(reason.Code, reason.Params, reason.Text)).ToList())).ToList();
        var sopNotes = decisions.Values.Where(decision => decision.Allowed)
            .Select(decision => decision.SopNotChecked?.Text).OfType<string>().Distinct().ToList();
        var singleRefusal = refused.Count == 1 ? decisions[refused[0].TestOrderId] : null;
        return new CompetencyBatch(allowed, refused, sopNotes.Count == 0 ? null : string.Join(" ", sopNotes), singleRefusal);
    }
}

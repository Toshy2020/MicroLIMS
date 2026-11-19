using System.Globalization;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
namespace MicroLIMS.Application.Helpers;

public static class HplcResponse
{
    private static decimal EffectiveCoded(
        decimal response,
        decimal? isResponse,
        bool hasInternalStandard,
        string code,
        string errorMessage,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (!hasInternalStandard)
            return response;
        if (isResponse is null or <= 0m)
            throw new BusinessRuleException(code, errorMessage, parameters);
        return response / isResponse.Value;
    }

    public static decimal Effective(decimal response, decimal? isResponse, bool hasInternalStandard, string? errorMessage = null) =>
        EffectiveCoded(
            response,
            isResponse,
            hasInternalStandard,
            MessageCodes.HplcInternalStandardResponseDefault,
            errorMessage ?? "The internal standard response is required.");

    public static decimal ForSst(string analyteName, decimal response, decimal? isResponse, bool hasInternalStandard) =>
        EffectiveCoded(
            response,
            isResponse,
            hasInternalStandard,
            MessageCodes.HplcInternalStandardResponseRequired,
            $"{analyteName}: enter an internal standard response (above zero) for every injection.",
            new Dictionary<string, string> { ["analyte"] = analyteName });

    public static decimal ForReplicate(int replicateNo, decimal response, decimal? isResponse, bool hasInternalStandard) =>
        EffectiveCoded(
            response,
            isResponse,
            hasInternalStandard,
            MessageCodes.HplcReplicateInternalStandardRequired,
            $"Replicate {replicateNo}: the internal standard response is required.",
            new Dictionary<string, string> { ["replicate"] = replicateNo.ToString(CultureInfo.InvariantCulture) });
}

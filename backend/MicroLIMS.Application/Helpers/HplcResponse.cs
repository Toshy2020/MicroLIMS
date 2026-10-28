namespace MicroLIMS.Application.Helpers;

public static class HplcResponse
{
    public static decimal Effective(decimal response, decimal? isResponse, bool hasInternalStandard, string? errorMessage = null)
    {
        if (!hasInternalStandard)
            return response;
        if (isResponse is null or <= 0m)
            throw new InvalidOperationException(errorMessage ?? "The internal standard response is required.");
        return response / isResponse.Value;
    }

    public static decimal ForSst(string analyteName, decimal response, decimal? isResponse, bool hasInternalStandard) =>
        Effective(response, isResponse, hasInternalStandard, $"{analyteName}: enter an internal standard response (above zero) for every injection.");

    public static decimal ForReplicate(int replicateNo, decimal response, decimal? isResponse, bool hasInternalStandard) =>
        Effective(response, isResponse, hasInternalStandard, $"Replicate {replicateNo}: the internal standard response is required.");
}

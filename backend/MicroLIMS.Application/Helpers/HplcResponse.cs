namespace MicroLIMS.Application.Helpers;

public static class HplcResponse
{
    public static decimal Effective(decimal response, decimal? isResponse) =>
        isResponse.HasValue ? response / isResponse.Value : response;
}

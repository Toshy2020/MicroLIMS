namespace MicroLIMS.Shared.Helpers;

public static class ResultHelper
{
    // TAMC/TYMC frozen rule: whole numbers only.
    public static int ToWholeNumber(decimal value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}

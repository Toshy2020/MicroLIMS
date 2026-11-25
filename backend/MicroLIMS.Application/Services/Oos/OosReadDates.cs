namespace MicroLIMS.Application.Services.Oos;

internal static class OosReadDates
{
    public static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime()
        : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

namespace MicroLIMS.Shared.Helpers;

public static class DateHelper
{
    public static string ToLabDate(this DateTime date) => date.ToString("dd-MMM-yyyy");
    public static string ToLabDateTime(this DateTime date) => date.ToString("dd-MMM-yyyy HH:mm");
}

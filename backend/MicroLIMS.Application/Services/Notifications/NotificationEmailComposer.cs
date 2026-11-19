using System.Text;
using Microsoft.Extensions.Configuration;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services.Notifications;

/// <summary>
/// Formats subject and plain-text body for single notification emails and daily digests.
/// Never includes analytical result values, specification limits, passwords, or tokens.
/// </summary>
public static class NotificationEmailComposer
{
    private const string DefaultAppBaseUrl = "http://localhost:5173";

    private const string PreferenceFooter =
        "To change how and when you receive these emails, update your notification preferences in MicroLIMS.";

    public static string ResolveAppBaseUrl(IConfiguration? config)
    {
        var raw = config?["Frontend:Origin"] ?? config?["Frontend__Origin"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DefaultAppBaseUrl;
        }

        var first = raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(first) ? DefaultAppBaseUrl : first.TrimEnd('/');
    }

    public static (string Subject, string Body) Compose(
        NotificationLog notification,
        string appBaseUrl,
        string timeZoneId,
        DateTime? dueAtUtc = null)
    {
        var baseUrl = string.IsNullOrWhiteSpace(appBaseUrl) ? DefaultAppBaseUrl : appBaseUrl.TrimEnd('/');
        var info = NotificationCatalog.TryGet(notification.Type);
        var subject = info?.TitleTemplate ?? (string.IsNullOrWhiteSpace(notification.Title) ? notification.Type : notification.Title);
        var tz = LabClock.ResolveTimeZone(timeZoneId);
        var resolvedTzId = string.IsNullOrWhiteSpace(timeZoneId) ? tz.Id : timeZoneId;

        var identifier = FormatIdentifier(notification);
        var link = BuildLink(baseUrl, notification);

        var sb = new StringBuilder();
        sb.AppendLine($"Type: {subject} ({notification.Type})");
        if (!string.IsNullOrWhiteSpace(identifier))
        {
            sb.AppendLine($"Identifier: {identifier}");
        }

        sb.AppendLine();
        sb.AppendLine(notification.Message);
        sb.AppendLine();

        if (dueAtUtc.HasValue)
        {
            var localDue = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dueAtUtc.Value, DateTimeKind.Utc), tz);
            sb.AppendLine($"Due time ({resolvedTzId}): {localDue:yyyy-MM-dd HH:mm}");
        }
        else
        {
            var localCreated = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(notification.CreatedAt, DateTimeKind.Utc), tz);
            sb.AppendLine($"Recorded ({resolvedTzId}): {localCreated:yyyy-MM-dd HH:mm}");
        }

        sb.AppendLine($"Open in MicroLIMS: {link}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine(PreferenceFooter);
        sb.AppendLine($"Notification preferences: {baseUrl}/notifications/preferences");

        return (subject, sb.ToString());
    }

    public static (string Subject, string Body) ComposeDigest(
        IReadOnlyList<NotificationLog> items,
        string appBaseUrl,
        string timeZoneId,
        DateOnly labDate)
    {
        var baseUrl = string.IsNullOrWhiteSpace(appBaseUrl) ? DefaultAppBaseUrl : appBaseUrl.TrimEnd('/');
        var tz = LabClock.ResolveTimeZone(timeZoneId);
        var resolvedTzId = string.IsNullOrWhiteSpace(timeZoneId) ? tz.Id : timeZoneId;

        var subject = $"MicroLIMS Daily Digest ({labDate:yyyy-MM-dd}) — {items.Count} unread notification(s)";
        var sb = new StringBuilder();
        sb.AppendLine($"Daily Notification Digest for {labDate:yyyy-MM-dd} ({resolvedTzId})");
        sb.AppendLine($"Unread digest items: {items.Count}");
        sb.AppendLine();

        foreach (var item in items)
        {
            var info = NotificationCatalog.TryGet(item.Type);
            var typeLabel = info?.TitleTemplate ?? item.Type;
            var identifier = FormatIdentifier(item);
            var localCreated = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc), tz);
            var link = BuildLink(baseUrl, item);

            sb.Append($"- [{typeLabel} ({item.Type})]");
            if (!string.IsNullOrWhiteSpace(identifier))
            {
                sb.Append($" [{identifier}]");
            }

            sb.AppendLine($" ({localCreated:yyyy-MM-dd HH:mm} {resolvedTzId})");
            sb.AppendLine($"  {item.Message}");
            sb.AppendLine($"  Link: {link}");
        }

        sb.AppendLine();
        sb.AppendLine($"Notification centre: {baseUrl}/notifications");
        sb.AppendLine("---");
        sb.AppendLine(PreferenceFooter);
        sb.AppendLine($"Notification preferences: {baseUrl}/notifications/preferences");

        return (subject, sb.ToString());
    }

    private static string FormatIdentifier(NotificationLog n)
    {
        if (!string.IsNullOrWhiteSpace(n.TargetType) && n.TargetId.HasValue)
        {
            return $"{n.TargetType} #{n.TargetId.Value}";
        }

        if (n.SampleId.HasValue && n.TestOrderId.HasValue)
        {
            return $"Sample #{n.SampleId.Value} / TestOrder #{n.TestOrderId.Value}";
        }

        if (n.SampleId.HasValue)
        {
            return $"Sample #{n.SampleId.Value}";
        }

        if (n.TestOrderId.HasValue)
        {
            return $"TestOrder #{n.TestOrderId.Value}";
        }

        return string.Empty;
    }

    private static string BuildLink(string baseUrl, NotificationLog n)
    {
        var path = (n.TargetType, n.TargetId) switch
        {
            ("Sample", { } id) => $"/samples/{id}",
            ("TestOrder", _) when n.SampleId.HasValue => $"/samples/{n.SampleId.Value}",
            ("TestOrder", { } id) => $"/samples/{id}",
            ("Batch", { } id) => $"/batches/{id}",
            ("Media", _) => "/inventory/media",
            ("SolutionPreparation", _) => "/inventory/solutions",
            ("Cryovial", _) => "/inventory/strains",
            ("EquipmentInventory", _) => "/equipment",
            ("PreparationConfig", _) => "/configuration/preparations",
            ("MonitoringPlan", _) => "/monitoring",
            _ when n.SampleId.HasValue => $"/samples/{n.SampleId.Value}",
            _ => "/notifications"
        };

        return $"{baseUrl}{path}";
    }
}

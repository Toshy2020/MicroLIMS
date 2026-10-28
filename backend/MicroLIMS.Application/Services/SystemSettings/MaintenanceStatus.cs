using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.Application.Services.SystemSettings;

// What the public status endpoint, the write-blocking middleware and the
// background-job gate need to know about maintenance mode and the announcement.
// Deliberately nothing else: this is served to anonymous callers.
public sealed record MaintenanceStatus(bool Maintenance, string Message, string Banner, string Severity);

public static class MaintenanceStatusReader
{
    public static async Task<bool> IsMaintenanceOnAsync(this ISystemSettingsReader reader, CancellationToken cancellationToken = default) =>
        await reader.GetBoolAsync(SystemSettingKeys.MaintenanceMode, cancellationToken);

    public static async Task<MaintenanceStatus> GetMaintenanceStatusAsync(this ISystemSettingsReader reader, CancellationToken cancellationToken = default)
    {
        var on = await reader.GetBoolAsync(SystemSettingKeys.MaintenanceMode, cancellationToken);
        var message = await reader.GetStringAsync(SystemSettingKeys.MaintenanceMessage, cancellationToken);
        var banner = await reader.GetStringAsync(SystemSettingKeys.AnnouncementBanner, cancellationToken);
        var severity = await reader.GetStringAsync(SystemSettingKeys.AnnouncementSeverity, cancellationToken);
        return new MaintenanceStatus(on, message, banner, severity);
    }
}

using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;

namespace MicroLIMS.API.BackgroundServices;

// Reads a registry setting for a background worker so a change made in the
// admin Settings tab applies on the worker's next cycle without a restart.
// Without a registry in the scope (unit tests) the legacy configuration key
// and then the code default apply, exactly as before the registry existed.
internal static class WorkerSettings
{
    public static async Task<int> GetIntAsync(IServiceProvider scopeServices, IConfiguration configuration, string key, CancellationToken cancellationToken)
    {
        var reader = scopeServices.GetService<ISystemSettingsReader>();
        if (reader is not null)
            return await reader.GetIntAsync(key, cancellationToken);

        var definition = SystemSettingDefinitions.Get(key);
        var configured = definition.LegacyConfigKey is null ? null : configuration.GetValue<int?>(definition.LegacyConfigKey);
        return configured is > 0 ? configured.Value : SystemSettingDefinitions.DefaultInt(key);
    }
}

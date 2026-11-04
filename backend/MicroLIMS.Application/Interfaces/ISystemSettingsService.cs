using MicroLIMS.Application.DTOs;

namespace MicroLIMS.Application.Interfaces;

// Read side of the settings registry, used by every consumer (authentication,
// signatures, background workers). Values come from the database with the
// legacy configuration key and then the code default as fallback, cached with
// invalidation when a setting changes. It is separate from the management
// service because ElectronicSignatureService reads settings while the
// management service depends on ElectronicSignatureService to sign changes.
public interface ISystemSettingsReader
{
    Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default);
    Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default);
}

// The typed, validated registry: definitions live in code, values in the
// database. Every change needs a reason; GxP-relevant keys also need the
// administrator's electronic signature. History is append-only.
public interface ISystemSettingsService
{
    Task<SystemSettingsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<SystemSettingDto> UpdateAsync(string key, string? value, string? reason, int actingUserId, string? password, string? ipAddress);

    Task<IReadOnlyList<SystemSettingVersionDto>> GetHistoryAsync(string key, CancellationToken cancellationToken = default);
}

public static class SystemSettingsReaderExtensions
{
    // Consumers constructed without a registry (unit tests, tools) behave as
    // before the registry existed: the code default applies.
    public static Task<int> GetIntOrDefaultAsync(this ISystemSettingsReader? reader, string key, CancellationToken cancellationToken = default) =>
        reader is null
            ? Task.FromResult(Services.SystemSettings.SystemSettingDefinitions.DefaultInt(key))
            : reader.GetIntAsync(key, cancellationToken);
}

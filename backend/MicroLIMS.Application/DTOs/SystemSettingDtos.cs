namespace MicroLIMS.Application.DTOs;

public record SystemSettingsOverviewDto(
    IReadOnlyList<SystemSettingDto> Settings,
    IReadOnlyList<SystemSettingFixedItemDto> FixedItems);

// One registry setting with its effective value. Source says where the
// effective value comes from: "Database" (changed here), "Configuration"
// (legacy appsettings/environment key, until changed here) or "Default".
public record SystemSettingDto(
    string Key,
    string Category,
    string Description,
    string ValueType,
    string Value,
    string DefaultValue,
    string Source,
    string? Unit,
    long? Min,
    long? Max,
    IReadOnlyList<string>? AllowedValues,
    bool IsGxpRelevant,
    bool RequiresSignature,
    int Version,
    DateTime? LastChangedAt,
    int? LastChangedByUserId,
    string? LastChangedByName);

// A value shown for information only, because it is not stored in the registry
// (a secret, a startup-only setting, or a table edited elsewhere).
public record SystemSettingFixedItemDto(
    string Key,
    string Label,
    string Value,
    string Source,
    string Note,
    string? EditLocation);

public record SystemSettingVersionDto(
    int Version,
    string Value,
    string PreviousValue,
    int ChangedByUserId,
    string? ChangedByName,
    DateTime ChangedAt,
    string Reason,
    int? ElectronicSignatureId);

// Password is required only for GxP-relevant keys (the electronic signature).
public record UpdateSystemSettingRequest(string? Value, string? Reason, string? Password);

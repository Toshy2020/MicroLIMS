using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.SystemSettings;

// Management side of the settings registry. The panel controls the system,
// never GxP data: these are operating parameters. A change needs a reason and,
// for GxP-relevant keys, the administrator's electronic signature (signed
// before anything changes). Each change appends an immutable
// SystemSettingVersion and one Configuration-category audit row, in one
// transaction, then invalidates the cache so consumers see it without a restart.
public class SystemSettingsService : ISystemSettingsService
{
    internal const string EntityName = "SystemSetting";
    internal const string ChangedActionCode = "SystemSettingChanged";

    private readonly IMicroLimsDbContext _db;
    private readonly SystemSettingsReader _reader;
    private readonly SystemSettingsCache _cache;
    private readonly IElectronicSignatureService _signatureService;
    private readonly IConfiguration? _configuration;
    private readonly TimeProvider _time;
    private readonly TimeSpan? _accessTokenLifetime;

    public SystemSettingsService(
        IMicroLimsDbContext db,
        SystemSettingsCache cache,
        IElectronicSignatureService signatureService,
        IConfiguration? configuration = null,
        TimeProvider? timeProvider = null,
        TimeSpan? accessTokenLifetime = null)
    {
        _db = db;
        _cache = cache;
        _signatureService = signatureService;
        _configuration = configuration;
        _time = timeProvider ?? TimeProvider.System;
        _accessTokenLifetime = accessTokenLifetime;
        _reader = new SystemSettingsReader(db, cache, configuration, _time);
    }

    public async Task<SystemSettingsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _reader.LoadStoredAsync(cancellationToken);
        var rows = await _db.SystemSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, StringComparer.Ordinal, cancellationToken);

        // Last change per setting: version number, time and who.
        var latest = await _db.SystemSettingVersions.AsNoTracking()
            .Select(v => new { v.SettingId, v.Version, v.ChangedAt, v.ChangedByUserId })
            .ToListAsync(cancellationToken);
        var latestBySetting = latest.GroupBy(v => v.SettingId).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Version).First());
        var userIds = latestBySetting.Values.Select(v => v.ChangedByUserId).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var settings = SystemSettingDefinitions.All.Select(d =>
        {
            var (value, source) = SystemSettingsReader.Resolve(d, stored, _configuration);
            rows.TryGetValue(d.Key, out var row);
            var last = row is not null && latestBySetting.TryGetValue(row.Id, out var l) ? l : null;
            return new SystemSettingDto(
                d.Key, d.Category, d.Description, d.Type.ToString(), value, d.Default, source,
                d.Unit, d.Min, d.Max, d.AllowedValues, d.IsGxpRelevant, d.IsGxpRelevant,
                row?.CurrentVersion ?? 0, last?.ChangedAt, last?.ChangedByUserId,
                last is not null && names.TryGetValue(last.ChangedByUserId, out var n) ? n : null);
        }).ToList();

        return new SystemSettingsOverviewDto(settings, await BuildFixedItemsAsync(cancellationToken));
    }

    public async Task<SystemSettingDto> UpdateAsync(string key, string? value, string? reason, int actingUserId, string? password, string? ipAddress)
    {
        try
        {
            await UnitOfWork.RunAsync(_db, () => UpdateCoreAsync(key, value, reason, actingUserId, password, ipAddress));
        }
        finally
        {
            // After commit (or rollback): drop the snapshot so no consumer holds a stale value.
            _cache.Invalidate();
        }
        var overview = await GetOverviewAsync();
        return overview.Settings.Single(s => s.Key == key);
    }

    private async Task<bool> UpdateCoreAsync(string key, string? rawValue, string? rawReason, int actingUserId, string? password, string? ipAddress)
    {
        var definition = SystemSettingDefinitions.Find(key)
            ?? throw new NotFoundException($"System setting '{key}' does not exist.");
        var reason = AdminActionReason.Require(rawReason, "change a system setting");
        var newValue = definition.Normalize(rawValue);

        var stored = await _db.SystemSettings.ToListAsync();
        var row = stored.FirstOrDefault(s => s.Key == key);
        var (previousValue, _) = SystemSettingsReader.Resolve(
            definition,
            stored.ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal),
            _configuration);

        if (string.Equals(previousValue, newValue, StringComparison.Ordinal))
            throw new InvalidOperationException($"'{key}' already has the value {newValue}. Nothing was changed.");

        _db.CurrentUserId = actingUserId;
        _db.CurrentReason = reason;
        var now = _time.GetUtcNow().UtcDateTime;

        if (row is null)
        {
            row = new SystemSetting
            {
                Key = key,
                Value = previousValue,
                ValueType = definition.Type,
                Category = definition.Category,
                IsGxpRelevant = definition.IsGxpRelevant,
                Description = definition.Description,
                CurrentVersion = 0
            };
            _db.SystemSettings.Add(row);
            // The signature is bound to the row, so it needs its key first.
            await _db.SaveChangesAsync();
        }

        // Signs before anything changes: a wrong password throws and the whole
        // command rolls back; the refused attempt is recorded by the signature service.
        ElectronicSignature? signature = null;
        if (definition.IsGxpRelevant)
        {
            signature = await _signatureService.SignAsync(actingUserId, password ?? string.Empty, SignatureMeaning.SystemSettingChanged,
                EntityName, row.Id, $"{key}: {previousValue} -> {newValue}: {reason}", ipAddress);
        }

        row.Value = newValue;
        row.ValueType = definition.Type;
        row.Category = definition.Category;
        row.IsGxpRelevant = definition.IsGxpRelevant;
        row.Description = definition.Description;
        row.CurrentVersion += 1;

        _db.SystemSettingVersions.Add(new SystemSettingVersion
        {
            SettingId = row.Id,
            Version = row.CurrentVersion,
            Value = newValue,
            PreviousValue = previousValue,
            ChangedByUserId = actingUserId,
            ChangedAt = now,
            Reason = reason,
            ElectronicSignature = signature
        });

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = EntityName,
            EntityId = row.Id.ToString(),
            Action = "SYSTEM_SETTING_CHANGED",
            ActionCode = ChangedActionCode,
            ActionCategory = AuditActionCategory.Configuration,
            PreviousValue = JsonSerializer.Serialize(new { Key = key, Value = previousValue }),
            NewValue = JsonSerializer.Serialize(new { Key = key, Value = newValue, Version = row.CurrentVersion, Signed = signature is not null, Reason = reason }),
            Reason = reason,
            UserId = actingUserId,
            Timestamp = now
        });

        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<IReadOnlyList<SystemSettingVersionDto>> GetHistoryAsync(string key, CancellationToken cancellationToken = default)
    {
        if (SystemSettingDefinitions.Find(key) is null)
            throw new NotFoundException($"System setting '{key}' does not exist.");

        var versions = await _db.SystemSettingVersions.AsNoTracking()
            .Where(v => v.Setting!.Key == key)
            .OrderByDescending(v => v.Version)
            .ToListAsync(cancellationToken);
        var userIds = versions.Select(v => v.ChangedByUserId).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return versions.Select(v => new SystemSettingVersionDto(
            v.Version, v.Value, v.PreviousValue, v.ChangedByUserId,
            names.GetValueOrDefault(v.ChangedByUserId), v.ChangedAt, v.Reason, v.ElectronicSignatureId)).ToList();
    }

    // Values the registry deliberately does not hold, shown so an auditor sees
    // the whole picture. Nothing here is a secret.
    private async Task<IReadOnlyList<SystemSettingFixedItemDto>> BuildFixedItemsAsync(CancellationToken cancellationToken)
    {
        var items = new List<SystemSettingFixedItemDto>
        {
            new("tokens.accessLifetime", "Access token lifetime",
                _accessTokenLifetime is { } span ? $"{(int)span.TotalMinutes} minutes" : "Not available",
                "Configuration: Jwt:AccessTokenMinutes",
                "Fixed at startup: the token validator and issuer are built once, so changing it needs a configuration change and a restart.",
                null),
            new("password.complexity", "Required character classes", "Uppercase, lowercase, digit and special character",
                "Code: PasswordPolicy", "Not configurable.", null),
            new("password.mustChange", "Forced password change",
                "Every request except change-password, own identity and sign-out is refused until the password is changed",
                "Code: MustChangePasswordFilter", "Not configurable.", null),
            new("signature.locksAccount", "Failed signature locks the login account", "No",
                "Code: ElectronicSignatureService", "Not configurable.", null),
            new("lab.timeZone", "Laboratory time zone", _configuration?["Lab:TimeZoneId"] ?? "Africa/Cairo",
                "Configuration: Lab:TimeZoneId",
                "Timestamps are stored in UTC; this single zone drives display and day boundaries. Changing it needs a configuration change and a restart.",
                null),
            new("secrets", "Secrets (JWT signing key, SMTP password, connection strings, storage keys)", "********",
                "Environment / configuration",
                "Never stored in the database and never shown. Rotate them in the deployment environment.", null)
        };

        var documentControl = await _db.ConfigurationSettings.AsNoTracking()
            .OrderBy(s => s.SettingGroup).ThenBy(s => s.SettingKey)
            .Select(s => new { s.SettingKey, s.SettingValue })
            .ToListAsync(cancellationToken);
        items.AddRange(documentControl.Select(s => new SystemSettingFixedItemDto(
            s.SettingKey, s.SettingKey, s.SettingValue, "Document Control configuration table",
            "Edited with its own reason and audit trail in Document Control; shown here read-only.",
            "/document-control/admin")));

        return items;
    }
}

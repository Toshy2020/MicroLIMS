using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Authentication;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// WP3.1: the typed, validated settings registry. Definitions in code, values in
// the database with configuration then code default as fallback; a reason on
// every change and an e-signature on GxP-relevant keys; append-only history.
public class SystemSettingsRegistryTests
{
    private const string AdminPassword = "AdminP@ssw0rd!";
    private const string Reason = "Align with the 2026 security policy";

    private sealed class Rig
    {
        public required MicroLimsDbContext Db { get; init; }
        public required SystemSettingsCache Cache { get; init; }
        public required SystemSettingsService Service { get; init; }
        public required SystemSettingsReader Reader { get; init; }
    }

    private static Rig CreateRig(IConfiguration? configuration = null)
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new MicroLimsDbContext(options);
        db.Roles.Add(new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator", IsSystemRole = true, IsActive = true });
        db.Users.Add(new User { Id = 1, FullName = "Acting Admin", Username = "actor", RoleId = 1, IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword, 4) });
        db.SaveChanges();

        var cache = new SystemSettingsCache();
        var reader = new SystemSettingsReader(db, cache, configuration);
        var signatures = new ElectronicSignatureService(db, TimeProvider.System, reader);
        return new Rig { Db = db, Cache = cache, Reader = reader, Service = new SystemSettingsService(db, cache, signatures, configuration) };
    }

    private static IConfiguration Config(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder().AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => (string?)p.Value)).Build();

    // ---- definitions --------------------------------------------------------

    [Fact]
    public void EveryDefinition_HasAValidDefaultInsideItsOwnBounds()
    {
        foreach (var d in SystemSettingDefinitions.All)
            Assert.Equal(d.Default, d.Normalize(d.Default));
        Assert.Equal(SystemSettingDefinitions.All.Count, SystemSettingDefinitions.All.Select(d => d.Key).Distinct().Count());
    }

    [Fact]
    public void Defaults_MatchTheValuesThatWereHardCodedBefore()
    {
        Assert.Equal(AuthenticationService.MaxFailedAttempts, SystemSettingDefinitions.DefaultInt(SystemSettingKeys.LockoutMaxFailedAttempts));
        Assert.Equal(AuthenticationService.LockDuration.TotalMinutes, SystemSettingDefinitions.DefaultInt(SystemSettingKeys.LockoutDurationMinutes));
        Assert.Equal(AuthenticationService.RefreshTokenLifetime.TotalDays, SystemSettingDefinitions.DefaultInt(SystemSettingKeys.RefreshTokenLifetimeDays));
        Assert.Equal(AuthenticationService.PasswordResetTokenLifetime.TotalMinutes, SystemSettingDefinitions.DefaultInt(SystemSettingKeys.PasswordResetLifetimeMinutes));
        Assert.Equal(AuthenticationService.PasswordHistoryLimit, SystemSettingDefinitions.DefaultInt(SystemSettingKeys.PasswordHistoryLimit));
        Assert.Equal(MicroLIMS.Shared.Validation.PasswordPolicy.MinimumLength, SystemSettingDefinitions.DefaultInt(SystemSettingKeys.PasswordMinimumLength));
        Assert.Equal(ElectronicSignatureService.MaxFailedAttempts, SystemSettingDefinitions.DefaultInt(SystemSettingKeys.SignatureMaxFailedAttempts));
        Assert.Equal(ElectronicSignatureService.FailedAttemptWindow.TotalMinutes, SystemSettingDefinitions.DefaultInt(SystemSettingKeys.SignatureWindowMinutes));
    }

    [Fact]
    public void Normalize_ChecksTypeRangeAndAllowedValues()
    {
        var number = SystemSettingDefinitions.Get(SystemSettingKeys.LockoutMaxFailedAttempts);
        Assert.Equal("7", number.Normalize(" 7 "));
        Assert.Throws<InvalidOperationException>(() => number.Normalize("seven"));
        Assert.Throws<InvalidOperationException>(() => number.Normalize("2.5"));
        Assert.Throws<InvalidOperationException>(() => number.Normalize("2"));   // below min 3
        Assert.Throws<InvalidOperationException>(() => number.Normalize("21"));  // above max 20
        Assert.Throws<InvalidOperationException>(() => number.Normalize(" "));

        var flag = new SystemSettingDefinition("x.flag", "Test", SystemSettingValueType.Boolean, "false", "d");
        Assert.Equal("true", flag.Normalize("TRUE"));
        Assert.Throws<InvalidOperationException>(() => flag.Normalize("yes"));

        var choice = new SystemSettingDefinition("x.choice", "Test", SystemSettingValueType.String, "a", "d", AllowedValues: new[] { "a", "b" });
        Assert.Equal("b", choice.Normalize("b"));
        Assert.Throws<InvalidOperationException>(() => choice.Normalize("c"));
    }

    // ---- value precedence ---------------------------------------------------

    [Fact]
    public async Task Reader_FallsBackToLegacyConfigurationThenCodeDefault_ThenDatabase()
    {
        var plain = CreateRig();
        Assert.Equal(30, await plain.Reader.GetIntAsync(SystemSettingKeys.SessionTokenRetentionDays));

        var configured = CreateRig(Config(("SessionTokenCleanup:RetentionDays", "10")));
        Assert.Equal(10, await configured.Reader.GetIntAsync(SystemSettingKeys.SessionTokenRetentionDays));

        await configured.Service.UpdateAsync(SystemSettingKeys.SessionTokenRetentionDays, "45", Reason, 1, null, null);
        Assert.Equal(45, await configured.Reader.GetIntAsync(SystemSettingKeys.SessionTokenRetentionDays));
    }

    [Fact]
    public async Task Reader_IgnoresAStoredValueThatNoLongerValidates()
    {
        var rig = CreateRig();
        rig.Db.SystemSettings.Add(new SystemSetting { Key = SystemSettingKeys.SessionTokenRetentionDays, Value = "garbage", Category = "Retention", Description = "d", CurrentVersion = 1 });
        rig.Db.SaveChanges();
        Assert.Equal(30, await rig.Reader.GetIntAsync(SystemSettingKeys.SessionTokenRetentionDays));
    }

    // ---- changing a setting -------------------------------------------------

    [Fact]
    public async Task Update_WithoutAReason_IsRefused_AndNothingIsStored()
    {
        var rig = CreateRig();
        foreach (var reason in new string?[] { null, "", "   " })
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                rig.Service.UpdateAsync(SystemSettingKeys.SessionTokenRetentionDays, "45", reason, 1, null, null));
            Assert.Contains("reason", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Empty(rig.Db.SystemSettings);
        Assert.Empty(rig.Db.SystemSettingVersions);
    }

    [Fact]
    public async Task Update_RefusesUnknownKeys_InvalidValues_AndNoOpChanges()
    {
        var rig = CreateRig();
        await Assert.ThrowsAsync<NotFoundException>(() => rig.Service.UpdateAsync("no.such.key", "1", Reason, 1, null, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.UpdateAsync(SystemSettingKeys.SessionTokenRetentionDays, "0", Reason, 1, null, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.UpdateAsync(SystemSettingKeys.SessionTokenRetentionDays, "abc", Reason, 1, null, null));
        var same = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.UpdateAsync(SystemSettingKeys.SessionTokenRetentionDays, "30", Reason, 1, null, null));
        Assert.Contains("Nothing was changed", same.Message);
        Assert.Empty(rig.Db.SystemSettingVersions);
    }

    [Fact]
    public async Task Update_NonGxpKey_NeedsNoSignature_AppendsVersions_AndAuditsAsConfiguration()
    {
        var rig = CreateRig();

        var first = await rig.Service.UpdateAsync(SystemSettingKeys.SessionTokenRetentionDays, "45", Reason, 1, null, "10.0.0.1");
        Assert.Equal("45", first.Value);
        Assert.Equal("Database", first.Source);
        Assert.Equal(1, first.Version);
        Assert.Empty(rig.Db.ElectronicSignatures);

        await rig.Service.UpdateAsync(SystemSettingKeys.SessionTokenRetentionDays, "60", "Second change", 1, null, null);

        var versions = await rig.Service.GetHistoryAsync(SystemSettingKeys.SessionTokenRetentionDays);
        Assert.Equal(new[] { 2, 1 }, versions.Select(v => v.Version));
        Assert.Equal(("60", "45", "Second change"), (versions[0].Value, versions[0].PreviousValue, versions[0].Reason));
        Assert.Equal(("45", "30", Reason), (versions[1].Value, versions[1].PreviousValue, versions[1].Reason));
        Assert.All(versions, v => { Assert.Equal(1, v.ChangedByUserId); Assert.Null(v.ElectronicSignatureId); });

        var audit = await rig.Db.AuditLogs.Where(a => a.ActionCode == "SystemSettingChanged").ToListAsync();
        Assert.Equal(2, audit.Count);
        Assert.All(audit, a => { Assert.Equal(AuditActionCategory.Configuration, a.ActionCategory); Assert.Equal(1, a.UserId); });
        Assert.Contains(audit, a => a.Reason == Reason && a.NewValue!.Contains("\"Value\":\"45\"") && a.PreviousValue!.Contains("\"Value\":\"30\""));
    }

    [Fact]
    public async Task Update_GxpKey_RequiresTheSigningPassword()
    {
        var rig = CreateRig();
        var key = SystemSettingKeys.LockoutMaxFailedAttempts;

        // No password.
        await Assert.ThrowsAsync<SignatureVerificationException>(() => rig.Service.UpdateAsync(key, "7", Reason, 1, null, null));
        // Wrong password.
        await Assert.ThrowsAsync<SignatureVerificationException>(() => rig.Service.UpdateAsync(key, "7", Reason, 1, "not-the-password", null));

        Assert.Equal(AuthenticationService.MaxFailedAttempts, await rig.Reader.GetIntAsync(key));
        Assert.Empty(rig.Db.SystemSettingVersions);
        Assert.Empty(rig.Db.ElectronicSignatures);
        Assert.Equal(2, rig.Db.AuditLogs.Count(a => a.Action == "SignatureFailed"));

        // Correct password.
        var changed = await rig.Service.UpdateAsync(key, "7", Reason, 1, AdminPassword, "10.0.0.1");
        Assert.Equal("7", changed.Value);
        Assert.True(changed.RequiresSignature);

        var signature = Assert.Single(rig.Db.ElectronicSignatures);
        Assert.Equal(SignatureMeaning.SystemSettingChanged, signature.MeaningOfSignature);
        Assert.Equal(1, signature.UserId);

        var history = await rig.Service.GetHistoryAsync(key);
        Assert.Equal(signature.Id, Assert.Single(history).ElectronicSignatureId);
        Assert.Equal(AuditActionCategory.Configuration, rig.Db.AuditLogs.Single(a => a.ActionCode == "SystemSettingChanged").ActionCategory);
    }

    [Fact]
    public async Task Overview_ListsEverySetting_AndTheFixedItemsWithSecretsMasked()
    {
        var rig = CreateRig(Config(("Lab:TimeZoneId", "Africa/Cairo")));
        rig.Db.ConfigurationSettings.Add(new ConfigurationSetting { SettingKey = "DocumentControl.Watermark.OnDownload", SettingValue = "true", DataType = "boolean", SettingGroup = "DocumentControl" });
        rig.Db.SaveChanges();

        var overview = await rig.Service.GetOverviewAsync();

        Assert.Equal(SystemSettingDefinitions.All.Count, overview.Settings.Count);
        Assert.All(overview.Settings, s => Assert.Equal("Default", s.Source));
        Assert.Contains(overview.FixedItems, f => f.Key == "lab.timeZone" && f.Value == "Africa/Cairo");
        Assert.Contains(overview.FixedItems, f => f.Key == "secrets" && f.Value == "********");
        var dc = Assert.Single(overview.FixedItems, f => f.Key == "DocumentControl.Watermark.OnDownload");
        Assert.NotNull(dc.EditLocation);
        // No secret-looking value is ever exposed.
        Assert.DoesNotContain(overview.FixedItems, f => f.Value.Contains("Jwt", StringComparison.OrdinalIgnoreCase));
    }

    // ---- consumers pick up new values without a restart ----------------------

    [Fact]
    public async Task SignatureThrottle_UsesTheRegistryValue()
    {
        var rig = CreateRig();
        await rig.Service.UpdateAsync(SystemSettingKeys.SignatureMaxFailedAttempts, "3", Reason, 1, AdminPassword, null);
        var signatures = new ElectronicSignatureService(rig.Db, TimeProvider.System, rig.Reader);

        for (var i = 0; i < 3; i++)
            await Assert.ThrowsAsync<SignatureVerificationException>(() =>
                signatures.SignAsync(1, "wrong", SignatureMeaning.Reviewed, "Sample", 1, null, null));

        // The 4th attempt is refused before the password is looked at, with the default 5 it would not be.
        var ex = await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            signatures.SignAsync(1, AdminPassword, SignatureMeaning.Reviewed, "Sample", 1, null, null));
        Assert.Contains("Too many failed signature attempts", ex.Message);
    }

    [Fact]
    public async Task Lockout_UsesTheRegistryValue_AndPasswordMinimumLengthToo()
    {
        var rig = CreateRig();
        await rig.Service.UpdateAsync(SystemSettingKeys.LockoutMaxFailedAttempts, "3", Reason, 1, AdminPassword, null);
        await rig.Service.UpdateAsync(SystemSettingKeys.PasswordMinimumLength, "12", Reason, 1, AdminPassword, null);

        var emailSender = new EmailSender("", 587, "", "", "no-reply@microlims.local", false);
        var audit = new SecurityAuditService(rig.Db, new SystemSecurityRequestContext());
        AccessTokenIssuer issuer = _ => "jwt";
        var auth = new AuthenticationService(rig.Db, issuer, new PermissionService(rig.Db), emailSender,
            NullLogger<AuthenticationService>.Instance, audit, null, rig.Reader);

        for (var i = 0; i < 3; i++)
            await auth.LoginAsync("actor", "wrong");
        Assert.NotNull((await rig.Db.Users.AsNoTracking().SingleAsync(u => u.Id == 1)).LockedUntil);

        // 11 characters satisfy the default policy (8) but not the configured 12.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            auth.ChangePasswordAsync(1, AdminPassword, "Abcdefg1!xy"));
        Assert.Contains("at least 12 characters", ex.Message);
    }

    [Fact]
    public async Task ACachedValue_IsReplacedImmediatelyAfterAChange()
    {
        var rig = CreateRig();
        Assert.Equal(30, await rig.Reader.GetIntAsync(SystemSettingKeys.SessionTokenRetentionDays)); // warms the cache
        await rig.Service.UpdateAsync(SystemSettingKeys.SessionTokenRetentionDays, "90", Reason, 1, null, null);
        Assert.Equal(90, await rig.Reader.GetIntAsync(SystemSettingKeys.SessionTokenRetentionDays));
    }
}

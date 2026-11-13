using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Mfa;
using MicroLIMS.Application.Services.SystemSettings;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Mfa;

public class MfaPolicyServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private sealed class StubSystemSettingsReader : ISystemSettingsReader
    {
        private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal)
        {
            [SystemSettingKeys.MfaPolicy] = "RequiredForRoles",
            [SystemSettingKeys.MfaRequiredRoles] = "SystemAdministrator,SectionHead,QcManager",
        };

        private readonly Dictionary<string, int> _ints = new(StringComparer.Ordinal)
        {
            [SystemSettingKeys.MfaTrustedDeviceHours] = 12,
            [SystemSettingKeys.MfaEnrolmentGraceDays] = 7,
        };

        public CancellationToken? LastCancellationToken { get; private set; }

        public StubSystemSettingsReader WithPolicy(string policy)
        {
            _strings[SystemSettingKeys.MfaPolicy] = policy;
            return this;
        }

        public StubSystemSettingsReader WithRequiredRoles(string roles)
        {
            _strings[SystemSettingKeys.MfaRequiredRoles] = roles;
            return this;
        }

        public StubSystemSettingsReader WithGraceDays(int days)
        {
            _ints[SystemSettingKeys.MfaEnrolmentGraceDays] = days;
            return this;
        }

        public StubSystemSettingsReader WithTrustedDeviceHours(int hours)
        {
            _ints[SystemSettingKeys.MfaTrustedDeviceHours] = hours;
            return this;
        }

        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(_ints[key]);
        }

        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;
            throw new NotSupportedException($"Unexpected bool setting read: {key}");
        }

        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(_strings[key]);
        }
    }

    public static TheoryData<string, string, int, string, bool, int?, MfaLoginRequirement, int?> EvaluateCases() => new()
    {
        // Off: never asks for MFA, even with an active credential
        { "Off", "SystemAdministrator,SectionHead,QcManager", 7, "SystemAdministrator", true, null, MfaLoginRequirement.None, null },
        { "Off", "SystemAdministrator,SectionHead,QcManager", 7, "Analyst", false, null, MfaLoginRequirement.None, null },

        // Optional: enrolled users verify; non-enrolled users sign in without MFA
        { "Optional", "SystemAdministrator,SectionHead,QcManager", 7, "Analyst", true, null, MfaLoginRequirement.Verify, null },
        { "Optional", "SystemAdministrator,SectionHead,QcManager", 7, "SystemAdministrator", false, null, MfaLoginRequirement.None, null },

        // RequiredForRoles + listed role + no credential: grace period rules
        { "RequiredForRoles", "SystemAdministrator,SectionHead,QcManager", 7, "SectionHead", false, null, MfaLoginRequirement.EnrolWithinGrace, 7 },
        { "RequiredForRoles", "SystemAdministrator,SectionHead,QcManager", 7, "SectionHead", false, -3, MfaLoginRequirement.EnrolWithinGrace, 4 },
        { "RequiredForRoles", "SystemAdministrator,SectionHead,QcManager", 7, "SectionHead", false, -7, MfaLoginRequirement.EnrolNow, null },
        { "RequiredForRoles", "SystemAdministrator,SectionHead,QcManager", 7, "SectionHead", false, -8, MfaLoginRequirement.EnrolNow, null },
        { "RequiredForRoles", "SystemAdministrator,SectionHead,QcManager", 0, "SectionHead", false, null, MfaLoginRequirement.EnrolNow, null },
        { "RequiredForRoles", "SystemAdministrator,SectionHead,QcManager", 0, "SectionHead", false, -1, MfaLoginRequirement.EnrolNow, null },

        // RequiredForRoles + role list with spaces and mixed case
        { "RequiredForRoles", " sectionhead , QCMANAGER ", 7, "SectionHead", false, null, MfaLoginRequirement.EnrolWithinGrace, 7 },
        { "RequiredForRoles", " sectionhead , QCMANAGER ", 7, "qcmanager", false, null, MfaLoginRequirement.EnrolWithinGrace, 7 },

        // RequiredForRoles + unlisted role: none if unenrolled, verify if enrolled
        { "RequiredForRoles", "SystemAdministrator,SectionHead,QcManager", 7, "Analyst", false, null, MfaLoginRequirement.None, null },
        { "RequiredForRoles", "SystemAdministrator,SectionHead,QcManager", 7, "Analyst", true, null, MfaLoginRequirement.Verify, null },

        // RequiredForRoles / RequiredForAll + active credential -> Verify
        { "RequiredForRoles", "SystemAdministrator,SectionHead,QcManager", 7, "SystemAdministrator", true, null, MfaLoginRequirement.Verify, null },
        { "RequiredForAll", "SystemAdministrator,SectionHead,QcManager", 7, "Analyst", true, null, MfaLoginRequirement.Verify, null },

        // RequiredForAll + any role + no credential -> enrolment required (same grace rules)
        { "RequiredForAll", "SystemAdministrator,SectionHead,QcManager", 7, "Analyst", false, null, MfaLoginRequirement.EnrolWithinGrace, 7 },
        { "RequiredForAll", "SystemAdministrator,SectionHead,QcManager", 7, "Analyst", false, -3, MfaLoginRequirement.EnrolWithinGrace, 4 },
        { "RequiredForAll", "SystemAdministrator,SectionHead,QcManager", 7, "Analyst", false, -8, MfaLoginRequirement.EnrolNow, null },
        { "RequiredForAll", "SystemAdministrator,SectionHead,QcManager", 0, "Analyst", false, null, MfaLoginRequirement.EnrolNow, null },

        // Unknown policy string -> fail closed as RequiredForAll
        { "SomethingUnknown", "SystemAdministrator", 7, "Analyst", false, null, MfaLoginRequirement.EnrolWithinGrace, 7 },
        { "0", "SystemAdministrator", 7, "Analyst", false, -8, MfaLoginRequirement.EnrolNow, null },
        { "", "SystemAdministrator", 7, "Analyst", true, null, MfaLoginRequirement.Verify, null },
    };

    [Theory]
    [MemberData(nameof(EvaluateCases))]
    public async Task EvaluateAsync_AppliesPolicyRolesAndGraceRules(
        string policy,
        string requiredRoles,
        int graceDays,
        string roleType,
        bool hasActiveCredential,
        int? requiredSinceOffsetDays,
        MfaLoginRequirement expectedKind,
        int? expectedDueOffsetDays)
    {
        var reader = new StubSystemSettingsReader()
            .WithPolicy(policy)
            .WithRequiredRoles(requiredRoles)
            .WithGraceDays(graceDays);
        var service = new MfaPolicyService(reader);
        DateTime? requiredSinceUtc = requiredSinceOffsetDays is { } offset
            ? NowUtc.AddDays(offset)
            : null;
        DateTime? expectedDueAtUtc = expectedDueOffsetDays is { } dueOffset
            ? NowUtc.AddDays(dueOffset)
            : null;

        var result = await service.EvaluateAsync(roleType, hasActiveCredential, requiredSinceUtc, NowUtc);

        Assert.Equal(expectedKind, result.Kind);
        Assert.Equal(expectedDueAtUtc, result.EnrolmentDueAtUtc);
    }

    [Theory]
    [InlineData("Off", MfaPolicy.Off)]
    [InlineData("Optional", MfaPolicy.Optional)]
    [InlineData("RequiredForRoles", MfaPolicy.RequiredForRoles)]
    [InlineData("RequiredForAll", MfaPolicy.RequiredForAll)]
    [InlineData("unknown", MfaPolicy.RequiredForAll)]
    [InlineData("0", MfaPolicy.RequiredForAll)]
    [InlineData("", MfaPolicy.RequiredForAll)]
    public async Task GetPolicyAsync_ParsesKnownPoliciesAndFailsClosedOnUnknown(string rawPolicy, MfaPolicy expected)
    {
        var reader = new StubSystemSettingsReader().WithPolicy(rawPolicy);
        var service = new MfaPolicyService(reader);

        var policy = await service.GetPolicyAsync();

        Assert.Equal(expected, policy);
    }

    [Theory]
    [InlineData("Off", "SystemAdministrator", "SystemAdministrator", false)]
    [InlineData("Optional", "SystemAdministrator", "SystemAdministrator", false)]
    [InlineData("RequiredForRoles", " sectionhead , QCMANAGER ", "SectionHead", true)]
    [InlineData("RequiredForRoles", " sectionhead , QCMANAGER ", "qcmanager", true)]
    [InlineData("RequiredForRoles", " sectionhead , QCMANAGER ", "Analyst", false)]
    [InlineData("RequiredForAll", "SystemAdministrator", "Analyst", true)]
    [InlineData("UnknownPolicy", "SystemAdministrator", "Analyst", true)]
    public async Task IsRequiredForRoleAsync_MatchesPolicyAndNormalizedRoleList(
        string policy,
        string requiredRoles,
        string roleType,
        bool expected)
    {
        var reader = new StubSystemSettingsReader()
            .WithPolicy(policy)
            .WithRequiredRoles(requiredRoles);
        var service = new MfaPolicyService(reader);

        var required = await service.IsRequiredForRoleAsync(roleType);

        Assert.Equal(expected, required);
    }

    [Theory]
    [InlineData("SystemAdministrator")]
    [InlineData("SectionHead")]
    [InlineData("QcManager")]
    [InlineData("Qa")]
    public async Task DefaultRequiredRoles_RequireMfa_ForPrivilegedRolesIncludingQa(string roleType)
    {
        var defaults = SystemSettingDefinitions.Get(SystemSettingKeys.MfaRequiredRoles).Default;
        Assert.Contains(nameof(MicroLIMS.Domain.Enums.RoleType.Qa), defaults.Split(','));

        var reader = new StubSystemSettingsReader()
            .WithPolicy("RequiredForRoles")
            .WithRequiredRoles(defaults);
        var service = new MfaPolicyService(reader);

        Assert.True(await service.IsRequiredForRoleAsync(roleType));
        Assert.False(await service.IsRequiredForRoleAsync("Analyst"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(24)]
    public async Task GetTrustedDeviceHoursAsync_ReturnsConfiguredHours(int hours)
    {
        var reader = new StubSystemSettingsReader().WithTrustedDeviceHours(hours);
        var service = new MfaPolicyService(reader);

        var result = await service.GetTrustedDeviceHoursAsync();

        Assert.Equal(hours, result);
    }

    [Fact]
    public async Task Methods_ForwardCancellationTokenToSettingsReader()
    {
        using var cts = new CancellationTokenSource();
        var reader = new StubSystemSettingsReader();
        var service = new MfaPolicyService(reader);

        await service.GetPolicyAsync(cts.Token);
        Assert.Equal(cts.Token, reader.LastCancellationToken);

        await service.IsRequiredForRoleAsync("SectionHead", cts.Token);
        Assert.Equal(cts.Token, reader.LastCancellationToken);

        await service.EvaluateAsync("SectionHead", false, null, NowUtc, cts.Token);
        Assert.Equal(cts.Token, reader.LastCancellationToken);

        await service.GetTrustedDeviceHoursAsync(cts.Token);
        Assert.Equal(cts.Token, reader.LastCancellationToken);
    }

    [Theory]
    [InlineData("Off", "SystemAdministrator", false, false, 7, -10, MfaUserState.NotRequired)]
    [InlineData("Off", "SystemAdministrator", true, false, 7, -10, MfaUserState.Enrolled)]
    [InlineData("Off", "Analyst", false, true, 7, null, MfaUserState.TokenPendingConfirmation)]
    [InlineData("Optional", "SystemAdministrator", false, false, 7, -10, MfaUserState.NotRequired)]
    [InlineData("Optional", "Analyst", true, false, 7, null, MfaUserState.Enrolled)]
    [InlineData("Optional", "Analyst", false, true, 7, null, MfaUserState.TokenPendingConfirmation)]
    [InlineData("RequiredForRoles", "Analyst", false, false, 7, null, MfaUserState.NotRequired)]
    [InlineData("RequiredForRoles", "SectionHead", true, false, 7, -20, MfaUserState.Enrolled)]
    [InlineData("RequiredForRoles", "SectionHead", false, true, 7, -20, MfaUserState.TokenPendingConfirmation)]
    [InlineData("RequiredForRoles", "SectionHead", false, false, 7, null, MfaUserState.DueByDate)]
    [InlineData("RequiredForRoles", "SectionHead", false, false, 7, -6, MfaUserState.DueByDate)]
    [InlineData("RequiredForRoles", "SectionHead", false, false, 7, -7, MfaUserState.Overdue)]
    [InlineData("RequiredForRoles", "SectionHead", false, false, 0, null, MfaUserState.Overdue)]
    [InlineData("RequiredForAll", "Analyst", false, false, 7, -8, MfaUserState.Overdue)]
    public async Task GetUserStateAsync_ComputesAllFiveStatesAndGraceBoundaries(
        string policy,
        string roleType,
        bool hasActiveCredential,
        bool hasPendingHardwareToken,
        int graceDays,
        int? requiredSinceDaysOffset,
        MfaUserState expected)
    {
        var reader = new StubSystemSettingsReader()
            .WithPolicy(policy)
            .WithGraceDays(graceDays);
        var service = new MfaPolicyService(reader);
        DateTime? requiredSince = requiredSinceDaysOffset.HasValue
            ? NowUtc.AddDays(requiredSinceDaysOffset.Value)
            : null;

        var state = await service.GetUserStateAsync(
            roleType,
            hasActiveCredential,
            hasPendingHardwareToken,
            requiredSince,
            NowUtc);

        Assert.Equal(expected, state);
    }

    [Fact]
    public async Task GetUserStateAsync_BoundaryBeforeAndAtExactGraceEnd_MatchesEvaluateAsync()
    {
        var reader = new StubSystemSettingsReader()
            .WithPolicy("RequiredForRoles")
            .WithGraceDays(7);
        var service = new MfaPolicyService(reader);
        var requiredSince = NowUtc.AddDays(-7);

        var justBeforeEnd = NowUtc.AddTicks(-1);
        Assert.Equal(MfaLoginRequirement.EnrolWithinGrace, (await service.EvaluateAsync("SectionHead", false, requiredSince, justBeforeEnd)).Kind);
        Assert.Equal(MfaUserState.DueByDate, await service.GetUserStateAsync("SectionHead", false, false, requiredSince, justBeforeEnd));

        Assert.Equal(MfaLoginRequirement.EnrolNow, (await service.EvaluateAsync("SectionHead", false, requiredSince, NowUtc)).Kind);
        Assert.Equal(MfaUserState.Overdue, await service.GetUserStateAsync("SectionHead", false, false, requiredSince, NowUtc));
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Filters;
using MicroLIMS.API.Json;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Security;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Security;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MfaEndpointsPostgresTests
{
    private const string Password = "MfaEndpointsPass1!";
    private const string GenericMfaFailure = "The code is not correct.";
    private const string JwtSigningKey =
        "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUYwMTIzNDU2Nzg5QUJDREVGMDEyMzQ1Njc4OUFCQ0RFRg==";

    private static readonly string FixedKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private readonly PostgresTestFixture _fixture;

    public MfaEndpointsPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private async Task<(WebApplication App, HttpClient Client)> StartHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = JwtSigningKey,
            ["Jwt:Issuer"] = "MicroLIMS",
            ["Jwt:Audience"] = "MicroLIMS.Client",
            ["Mfa:ActiveKeyId"] = "test-k1",
            ["Mfa:EncryptionKeys:test-k1"] = FixedKeyBase64,
            ["ConnectionStrings:DefaultConnection"] = _fixture.TestConnectionString
        });

        var jwtSettings = JwtConfiguration.Resolve(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(jwtSettings);
        builder.Services.AddMicroLimsDbContext(opt => opt.UseNpgsql(_fixture.TestConnectionString));
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddMicroLimsJwtAuthentication(jwtSettings);
        builder.Services.AddMicroLimsAuthorization();
        builder.Services.AddMicroLimsRateLimiting(builder.Configuration);
        builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
                options.Filters.Add<MustChangePasswordFilter>();
            })
            .AddApplicationPart(typeof(AuthenticationController).Assembly)
            .AddJsonOptions(o => ApiJsonOptions.Configure(o.JsonSerializerOptions));

        var app = builder.Build();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private async Task<(int UserId, string Username, byte[] Secret)> SeedUserWithCredentialCoreAsync(RoleType roleType = RoleType.Analyst)
    {
        var protector = new AesGcmSecretProtector(new MfaOptions
        {
            ActiveKeyId = "test-k1",
            EncryptionKeys = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["test-k1"] = FixedKeyBase64
            }
        });

        var secret = Totp.NewSecret();
        var tag = Guid.NewGuid().ToString("N");
        var username = $"mfa_http_{tag}";

        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .Where(r => r.Type == roleType)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();

        var user = new User
        {
            FullName = $"MFA HTTP {tag[..8]}",
            Username = username,
            Email = $"{username}@example.test",
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        arrangeDb.Users.Add(user);
        await arrangeDb.SaveChangesAsync();

        arrangeDb.UserMfaCredentials.Add(new UserMfaCredential
        {
            UserId = user.Id,
            Type = MfaCredentialType.AuthenticatorApp,
            SecretProtected = protector.Protect(secret),
            StepSeconds = 30,
            Status = MfaCredentialStatus.Active,
            CreatedAt = DateTime.UtcNow,
            ConfirmedAt = DateTime.UtcNow
        });
        await arrangeDb.SaveChangesAsync();

        return (user.Id, username, secret);
    }

    private async Task<(string Username, byte[] Secret)> SeedUserWithCredentialAsync()
    {
        var (_, username, secret) = await SeedUserWithCredentialCoreAsync();
        return (username, secret);
    }

    private async Task<(int UserId, string Username)> SeedUserWithoutCredentialAsync(
        RoleType roleType = RoleType.Analyst,
        DateTime? mfaRequiredSince = null)
    {
        var tag = Guid.NewGuid().ToString("N");
        var username = $"mfa_http_nocred_{tag}";

        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .Where(r => r.Type == roleType)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();

        var user = new User
        {
            FullName = $"MFA HTTP NoCred {tag[..8]}",
            Username = username,
            Email = $"{username}@example.test",
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            MfaRequiredSince = mfaRequiredSince
        };
        arrangeDb.Users.Add(user);
        await arrangeDb.SaveChangesAsync();

        return (user.Id, username);
    }

    private static async Task<string> LoginBearerAsync(HttpClient client, string username)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var json = await ReadJsonAsync(resp);
        var token = json.RootElement.GetProperty("data").GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private static string CurrentCode(byte[] secret) =>
        Totp.Generate(secret, Totp.StepAt(DateTime.UtcNow, 30));

    private static string DifferentCode(byte[] secret)
    {
        var step = Totp.StepAt(DateTime.UtcNow, 30);
        var current = Totp.Generate(secret, step);
        return current == "000000" ? "111111" : "000000";
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    [PostgresFact]
    public async Task MfaToken_AsBearerOrRefreshToken_IsRejected()
    {
        var (username, _) = await SeedUserWithCredentialAsync();
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        using var loginJson = await ReadJsonAsync(loginResp);
        var data = loginJson.RootElement.GetProperty("data");
        var mfaToken = data.GetProperty("mfaToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(mfaToken));
        Assert.NotEqual(3, mfaToken!.Split('.').Length);

        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mfaToken);
        var meResp = await client.SendAsync(meReq);
        Assert.Equal(HttpStatusCode.Unauthorized, meResp.StatusCode);

        var refreshResp = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = mfaToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResp.StatusCode);
        using var refreshJson = await ReadJsonAsync(refreshResp);
        Assert.False(refreshJson.RootElement.GetProperty("success").GetBoolean());
    }

    [PostgresFact]
    public async Task Login_UserWithActiveCredential_Returns200_WithMfaRequired_AndNoTokens()
    {
        var (username, _) = await SeedUserWithCredentialAsync();
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        using var loginJson = await ReadJsonAsync(loginResp);
        Assert.True(loginJson.RootElement.GetProperty("success").GetBoolean());
        var data = loginJson.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("mfaRequired").GetBoolean());
        Assert.False(data.GetProperty("mfaEnrolmentRequired").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("mfaToken").GetString()));
        Assert.Equal(JsonValueKind.Null, data.GetProperty("token").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("refreshToken").ValueKind);
    }

    [PostgresFact]
    public async Task Verify_Endpoint_CorrectCode_ReturnsTokens()
    {
        var (username, secret) = await SeedUserWithCredentialAsync();
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        using var loginJson = await ReadJsonAsync(loginResp);
        var mfaToken = loginJson.RootElement.GetProperty("data").GetProperty("mfaToken").GetString()!;

        var verifyResp = await client.PostAsJsonAsync("/api/auth/mfa/verify", new
        {
            mfaToken,
            code = CurrentCode(secret),
            trustDevice = true
        });
        Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);

        using var verifyJson = await ReadJsonAsync(verifyResp);
        Assert.True(verifyJson.RootElement.GetProperty("success").GetBoolean());
        var data = verifyJson.RootElement.GetProperty("data");
        var accessToken = data.GetProperty("token").GetString();
        var refreshToken = data.GetProperty("refreshToken").GetString();
        var deviceToken = data.GetProperty("deviceToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));
        Assert.False(string.IsNullOrWhiteSpace(deviceToken));
        Assert.False(data.GetProperty("mfaRequired").GetBoolean());
        Assert.False(data.GetProperty("mfaEnrolmentRequired").GetBoolean());

        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var meResp = await client.SendAsync(meReq);
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);
    }

    [PostgresFact]
    public async Task Verify_Endpoint_WrongCode_Returns401_GenericMessage()
    {
        var (username, secret) = await SeedUserWithCredentialAsync();
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        using var loginJson = await ReadJsonAsync(loginResp);
        var mfaToken = loginJson.RootElement.GetProperty("data").GetProperty("mfaToken").GetString()!;

        var verifyResp = await client.PostAsJsonAsync("/api/auth/mfa/verify", new
        {
            mfaToken,
            code = DifferentCode(secret),
            trustDevice = false
        });
        Assert.Equal(HttpStatusCode.Unauthorized, verifyResp.StatusCode);

        using var verifyJson = await ReadJsonAsync(verifyResp);
        Assert.False(verifyJson.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(GenericMfaFailure, verifyJson.RootElement.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, verifyJson.RootElement.GetProperty("data").ValueKind);
    }

    [PostgresFact]
    public async Task Status_WithoutToken_Returns401()
    {
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var resp = await client.GetAsync("/api/auth/mfa/status");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [PostgresFact]
    public async Task EnrolStart_Authenticated_WrongPassword_IsRefused_AndCreatesNoCredential()
    {
        // Also proves a grace-period access token can access authenticated enrolment endpoints.
        var (userId, username) = await SeedUserWithoutCredentialAsync(
            RoleType.SectionHead,
            mfaRequiredSince: DateTime.UtcNow.AddDays(-1));
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var accessToken = await LoginBearerAsync(client, username);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/enrol/start")
        {
            Content = JsonContent.Create(new { password = "WrongPassword9!" })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        using var json = await ReadJsonAsync(resp);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.UserMfaCredentials.AsNoTracking().AnyAsync(c => c.UserId == userId));
    }

    [PostgresFact]
    public async Task EnrolStartAndConfirm_Authenticated_ActivatesCredential_ReturnsTenRecoveryCodes()
    {
        var (userId, username) = await SeedUserWithoutCredentialAsync(
            RoleType.SectionHead,
            mfaRequiredSince: DateTime.UtcNow.AddDays(-1));
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var accessToken = await LoginBearerAsync(client, username);

        using var startReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/enrol/start")
        {
            Content = JsonContent.Create(new { password = Password })
        };
        startReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var startResp = await client.SendAsync(startReq);
        Assert.Equal(HttpStatusCode.OK, startResp.StatusCode);
        Assert.Equal("no-store", startResp.Headers.CacheControl?.ToString());

        using var startJson = await ReadJsonAsync(startResp);
        var startData = startJson.RootElement.GetProperty("data");
        var manualKey = startData.GetProperty("manualKey").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(manualKey));
        Assert.StartsWith("otpauth://totp/MicroLIMS:", startData.GetProperty("otpAuthUri").GetString());
        Assert.Contains("<svg", startData.GetProperty("qrSvg").GetString());

        var code = CurrentCode(Totp.FromBase32(manualKey));
        using var confirmReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/enrol/confirm")
        {
            Content = JsonContent.Create(new { code })
        };
        confirmReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var confirmResp = await client.SendAsync(confirmReq);
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);
        Assert.Equal("no-store", confirmResp.Headers.CacheControl?.ToString());

        using var confirmJson = await ReadJsonAsync(confirmResp);
        var recoveryCodes = confirmJson.RootElement.GetProperty("data").GetProperty("recoveryCodes");
        Assert.Equal(10, recoveryCodes.GetArrayLength());

        using var statusReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/mfa/status");
        statusReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var statusResp = await client.SendAsync(statusReq);
        Assert.Equal(HttpStatusCode.OK, statusResp.StatusCode);
        using var statusJson = await ReadJsonAsync(statusResp);
        var statusData = statusJson.RootElement.GetProperty("data");
        Assert.True(statusData.GetProperty("enrolled").GetBoolean());
        Assert.True(statusData.GetProperty("required").GetBoolean());
        Assert.Equal(10, statusData.GetProperty("recoveryCodesRemaining").GetInt32());

        await using var assertDb = _fixture.CreateDbContext();
        var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaCredentialStatus.Active, cred.Status);
        Assert.Equal(10, await assertDb.MfaRecoveryCodes.AsNoTracking().CountAsync(r => r.UserId == userId));
    }

    [PostgresFact]
    public async Task EnrolTokenStartAndConfirm_LoginTime_ReturnsRecoveryCodesAndTokens()
    {
        var (userId, username) = await SeedUserWithoutCredentialAsync(
            RoleType.SectionHead,
            mfaRequiredSince: DateTime.UtcNow.AddDays(-10));
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        using var loginJson = await ReadJsonAsync(loginResp);
        var loginData = loginJson.RootElement.GetProperty("data");
        Assert.True(loginData.GetProperty("mfaEnrolmentRequired").GetBoolean());
        var mfaToken = loginData.GetProperty("mfaToken").GetString()!;

        var startResp = await client.PostAsJsonAsync("/api/auth/mfa/enrol/token/start", new { mfaToken });
        Assert.Equal(HttpStatusCode.OK, startResp.StatusCode);
        Assert.Equal("no-store", startResp.Headers.CacheControl?.ToString());

        using var startJson = await ReadJsonAsync(startResp);
        var manualKey = startJson.RootElement.GetProperty("data").GetProperty("manualKey").GetString()!;
        var code = CurrentCode(Totp.FromBase32(manualKey));

        var confirmResp = await client.PostAsJsonAsync("/api/auth/mfa/enrol/token/confirm", new { mfaToken, code });
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);
        Assert.Equal("no-store", confirmResp.Headers.CacheControl?.ToString());

        using var confirmJson = await ReadJsonAsync(confirmResp);
        var data = confirmJson.RootElement.GetProperty("data");
        Assert.Equal(10, data.GetProperty("recoveryCodes").GetArrayLength());
        var issuedLogin = data.GetProperty("login");
        var accessToken = issuedLogin.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(issuedLogin.GetProperty("refreshToken").GetString()));

        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var meResp = await client.SendAsync(meReq);
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);

        await using var assertDb = _fixture.CreateDbContext();
        var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaCredentialStatus.Active, cred.Status);
        var refresh = await assertDb.RefreshTokens.AsNoTracking().SingleAsync(r => r.UserId == userId);
        Assert.True(refresh.MfaVerified);
    }

    [PostgresFact]
    public async Task EnrolTokenStart_WithVerifyPurposeToken_IsRefused()
    {
        var (userId, username, _) = await SeedUserWithCredentialCoreAsync();
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        using var loginJson = await ReadJsonAsync(loginResp);
        var verifyToken = loginJson.RootElement.GetProperty("data").GetProperty("mfaToken").GetString()!;

        var resp = await client.PostAsJsonAsync("/api/auth/mfa/enrol/token/start", new { mfaToken = verifyToken });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("enrolment session", body, StringComparison.OrdinalIgnoreCase);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.UserMfaCredentials.AsNoTracking()
            .AnyAsync(c => c.UserId == userId && c.Status == MfaCredentialStatus.Pending));
    }

    [PostgresFact]
    public async Task Verify_WithEnrolPurposeToken_IsRefused()
    {
        var (userId, username) = await SeedUserWithoutCredentialAsync(
            RoleType.SectionHead,
            mfaRequiredSince: DateTime.UtcNow.AddDays(-10));
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        using var loginJson = await ReadJsonAsync(loginResp);
        var enrolToken = loginJson.RootElement.GetProperty("data").GetProperty("mfaToken").GetString()!;

        var resp = await client.PostAsJsonAsync("/api/auth/mfa/verify", new
        {
            mfaToken = enrolToken,
            code = "123456",
            trustDevice = false
        });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.RefreshTokens.AsNoTracking().AnyAsync(r => r.UserId == userId));
    }

    [PostgresFact]
    public async Task DeleteTrustedDevice_OfAnotherUser_Returns404_AndLeavesItLive()
    {
        var (userAId, _, _) = await SeedUserWithCredentialCoreAsync();
        var (_, usernameB) = await SeedUserWithoutCredentialAsync();

        int deviceId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var device = new TrustedDevice
            {
                UserId = userAId,
                TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(12)
            };
            arrangeDb.TrustedDevices.Add(device);
            await arrangeDb.SaveChangesAsync();
            deviceId = device.Id;
        }

        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var tokenB = await LoginBearerAsync(client, usernameB);
        using var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/auth/mfa/trusted-devices/{deviceId}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var delResp = await client.SendAsync(delReq);

        Assert.Equal(HttpStatusCode.NotFound, delResp.StatusCode);

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.TrustedDevices.AsNoTracking().SingleAsync(d => d.Id == deviceId);
        Assert.Null(persisted.RevokedAt);
    }

    [PostgresFact]
    public async Task RemoveFactor_WhenRoleRequired_IsRefused()
    {
        var (userId, username, secret) = await SeedUserWithCredentialCoreAsync(RoleType.SectionHead);
        const string recoveryCode = "23456-789AB";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.MfaRecoveryCodes.Add(new MfaRecoveryCode
            {
                UserId = userId,
                CodeHash = MicroLIMS.Application.Services.AdminPasswordRecoveryService.HashRecoveryCode(recoveryCode),
                CreatedAt = DateTime.UtcNow
            });
            await arrangeDb.SaveChangesAsync();
        }

        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        using var loginJson = await ReadJsonAsync(loginResp);
        var mfaToken = loginJson.RootElement.GetProperty("data").GetProperty("mfaToken").GetString()!;

        var verifyResp = await client.PostAsJsonAsync("/api/auth/mfa/verify", new
        {
            mfaToken,
            recoveryCode,
            trustDevice = false
        });
        Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);
        using var verifyJson = await ReadJsonAsync(verifyResp);
        var accessToken = verifyJson.RootElement.GetProperty("data").GetProperty("token").GetString()!;

        using var deleteReq = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/mfa")
        {
            Content = JsonContent.Create(new { password = Password, code = CurrentCode(secret) })
        };
        deleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var deleteResp = await client.SendAsync(deleteReq);

        Assert.Equal(HttpStatusCode.BadRequest, deleteResp.StatusCode);

        await using var assertDb = _fixture.CreateDbContext();
        var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaCredentialStatus.Active, cred.Status);
    }

    [PostgresFact]
    public async Task UserIdInBody_IsIgnored()
    {
        var (userAId, _) = await SeedUserWithoutCredentialAsync();
        var (userBId, usernameB) = await SeedUserWithoutCredentialAsync();
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var tokenB = await LoginBearerAsync(client, usernameB);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/enrol/start")
        {
            Content = JsonContent.Create(new { userId = userAId, password = Password })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.UserMfaCredentials.AsNoTracking().AnyAsync(c => c.UserId == userAId));
        Assert.True(await assertDb.UserMfaCredentials.AsNoTracking()
            .AnyAsync(c => c.UserId == userBId && c.Status == MfaCredentialStatus.Pending));
    }
}

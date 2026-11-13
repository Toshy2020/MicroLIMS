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
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private async Task<(string Username, byte[] Secret)> SeedUserWithCredentialAsync()
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
            .Where(r => r.Type == RoleType.Analyst)
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

        return (username, secret);
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
}

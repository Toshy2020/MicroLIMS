using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.Extensions;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

/// <summary>
/// Verifies the SMTP configuration resolution, EmailSender behavior,
/// password reset email dispatch, and secret redaction.
/// </summary>
public class SmtpConfigurationTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    // =========================================================================
    // 1. Missing SMTP configuration behavior
    // =========================================================================

    [Fact]
    public void Resolve_WhenSmtpHostMissing_ReturnsUnconfiguredOptions()
    {
        var config = Config();
        var options = SmtpConfiguration.Resolve(config);

        Assert.False(options.IsConfigured);
        Assert.Equal(string.Empty, options.Host);
        Assert.Equal(SmtpConfiguration.DefaultPort, options.Port);
        Assert.Equal(SmtpConfiguration.DefaultFromAddress, options.FromAddress);
        Assert.True(options.EnableSsl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \n")]
    public void Resolve_WhenSmtpHostEmptyOrWhitespace_ReturnsUnconfiguredOptions(string hostValue)
    {
        var config = Config(("Smtp:Host", hostValue));
        var options = SmtpConfiguration.Resolve(config);

        Assert.False(options.IsConfigured);
        Assert.Equal(string.Empty, options.Host);
    }

    [Fact]
    public async Task EmailSender_WhenHostUnconfigured_IsConfiguredIsFalse_AndSendAsyncIsSafeNoOp()
    {
        var unconfiguredSender = new EmailSender(string.Empty, 587, "", "", "no-reply@microlims.local", true);

        Assert.False(unconfiguredSender.IsConfigured);

        // Calling SendAsync on unconfigured sender must complete safely without throwing
        var exception = await Record.ExceptionAsync(() =>
            unconfiguredSender.SendAsync("analyst@example.com", "Test Subject", "Test Body"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task EmailSender_WhenRecipientEmptyOrWhitespace_SendAsyncIsSafeNoOp()
    {
        var sender = new EmailSender("smtp.example.com", 587, "", "", "no-reply@microlims.local", true);

        // Even with a host, missing recipient must be a safe no-op rather than throwing
        var exception1 = await Record.ExceptionAsync(() => sender.SendAsync("", "Subject", "Body"));
        var exception2 = await Record.ExceptionAsync(() => sender.SendAsync("   ", "Subject", "Body"));

        Assert.Null(exception1);
        Assert.Null(exception2);
    }

    // =========================================================================
    // 2. Valid SMTP configuration loading
    // =========================================================================

    [Fact]
    public void Resolve_WithStandardAppsettingsSection_BindsAllProperties()
    {
        var config = Config(
            ("Smtp:Host", "smtp.sendgrid.net"),
            ("Smtp:Port", "465"),
            ("Smtp:Username", "apikey"),
            ("Smtp:Password", "SG.test-secret-key-12345"),
            ("Smtp:FromAddress", "lab-notifications@microlims.local"),
            ("Smtp:EnableSsl", "true")
        );

        var options = SmtpConfiguration.Resolve(config);

        Assert.True(options.IsConfigured);
        Assert.Equal("smtp.sendgrid.net", options.Host);
        Assert.Equal(465, options.Port);
        Assert.Equal("apikey", options.Username);
        Assert.Equal("SG.test-secret-key-12345", options.Password);
        Assert.Equal("lab-notifications@microlims.local", options.FromAddress);
        Assert.True(options.EnableSsl);
    }

    [Fact]
    public void Resolve_WithEnvironmentVariableDoubleUnderscore_BindsAllProperties()
    {
        var config = Config(
            ("Smtp__Host", "smtp.office365.com"),
            ("Smtp__Port", "587"),
            ("Smtp__Username", "lims@domain.com"),
            ("Smtp__Password", "MySuperPassword!"),
            ("Smtp__FromAddress", "noreply@domain.com"),
            ("Smtp__EnableSsl", "true")
        );

        var options = SmtpConfiguration.Resolve(config);

        Assert.True(options.IsConfigured);
        Assert.Equal("smtp.office365.com", options.Host);
        Assert.Equal(587, options.Port);
        Assert.Equal("lims@domain.com", options.Username);
        Assert.Equal("MySuperPassword!", options.Password);
        Assert.Equal("noreply@domain.com", options.FromAddress);
        Assert.True(options.EnableSsl);
    }

    [Fact]
    public void Resolve_WithPartialConfiguration_AppliesDefaultPortFromAddressAndSsl()
    {
        var config = Config(
            ("Smtp:Host", "mail.internal.network")
        );

        var options = SmtpConfiguration.Resolve(config);

        Assert.True(options.IsConfigured);
        Assert.Equal("mail.internal.network", options.Host);
        Assert.Equal(SmtpConfiguration.DefaultPort, options.Port);
        Assert.Equal(SmtpConfiguration.DefaultFromAddress, options.FromAddress);
        Assert.True(options.EnableSsl);
        Assert.Equal(string.Empty, options.Username);
        Assert.Equal(string.Empty, options.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithEmptyOrWhitespaceFromAddress_FallsBackToDefaultFromAddress(string emptyFrom)
    {
        var config = Config(
            ("Smtp:Host", "smtp.example.com"),
            ("Smtp:FromAddress", emptyFrom)
        );

        var options = SmtpConfiguration.Resolve(config);

        Assert.Equal(SmtpConfiguration.DefaultFromAddress, options.FromAddress);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void Resolve_WithInvalidPort_FallsBackToDefaultPort(string invalidPort)
    {
        var config = Config(
            ("Smtp:Host", "smtp.example.com"),
            ("Smtp:Port", invalidPort)
        );

        var options = SmtpConfiguration.Resolve(config);

        Assert.Equal(SmtpConfiguration.DefaultPort, options.Port);
    }

    [Fact]
    public void Resolve_WithExplicitSslFalse_BindsFalseCorrectly()
    {
        var config = Config(
            ("Smtp:Host", "localhost"),
            ("Smtp:Port", "1025"),
            ("Smtp:EnableSsl", "false")
        );

        var options = SmtpConfiguration.Resolve(config);

        Assert.False(options.EnableSsl);
    }

    [Fact]
    public void ServiceCollection_RegistersSmtpOptionsAndEmailSender_CorrectlyLoaded()
    {
        var config = Config(
            ("Smtp:Host", "smtp.labcorp.local"),
            ("Smtp:Port", "587"),
            ("Smtp:Username", "service-account"),
            ("Smtp:Password", "pwd-123"),
            ("Smtp:FromAddress", "lims@labcorp.local"),
            ("Smtp:EnableSsl", "true")
        );

        var services = new ServiceCollection();
        var smtpOptions = SmtpConfiguration.Resolve(config);
        services.AddSingleton(smtpOptions);
        services.AddScoped<IEmailSender>(sp => new EmailSender(sp.GetRequiredService<SmtpOptions>()));

        using var provider = services.BuildServiceProvider();
        var resolvedOptions = provider.GetRequiredService<SmtpOptions>();
        var resolvedSender = provider.GetRequiredService<IEmailSender>() as EmailSender;

        Assert.NotNull(resolvedSender);
        Assert.True(resolvedSender.IsConfigured);
        Assert.Equal("smtp.labcorp.local", resolvedSender.Host);
        Assert.Equal(587, resolvedSender.Port);
        Assert.Equal("service-account", resolvedSender.Username);
        Assert.Equal("lims@labcorp.local", resolvedSender.FromAddress);
        Assert.True(resolvedSender.EnableSsl);
    }

    // =========================================================================
    // 3. Password-reset email generation/dispatch uses the configured SMTP service
    // =========================================================================

    [Fact]
    public async Task RequestPasswordResetAsync_WhenUserHasEmail_DispatchesResetEmailWithToken()
    {
        var db = CreateDbContext();
        var spySender = new SpyEmailSender();
        var authService = CreateAuthService(db, spySender);

        var user = new User
        {
            Id = 10,
            Username = "scientist1",
            FullName = "Lead Scientist",
            Email = "scientist@laboratory.org",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldValidPass1!"),
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act: Request password reset
        await authService.RequestPasswordResetAsync("scientist1");

        // Assert: Reset token was persisted in DB
        var tokenRecord = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
        Assert.NotNull(tokenRecord);
        Assert.True(tokenRecord.IsValid);

        // Assert: Email was dispatched via the IEmailSender
        Assert.Single(spySender.Sent);
        var dispatched = spySender.Sent[0];

        Assert.Equal("scientist@laboratory.org", dispatched.To);
        Assert.Equal("MicroLIMS Password Reset", dispatched.Subject);
        Assert.Contains("A password reset was requested for your MicroLIMS account.", dispatched.Body);
        Assert.Contains("Reset token:", dispatched.Body);
        Assert.Contains("This token expires at", dispatched.Body);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_WhenUserHasNoEmail_DoesNotDispatchEmail()
    {
        var db = CreateDbContext();
        var spySender = new SpyEmailSender();
        var authService = CreateAuthService(db, spySender);

        var user = new User
        {
            Id = 11,
            Username = "user-no-email",
            FullName = "No Email User",
            Email = null,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldValidPass1!"),
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act: Request password reset
        await authService.RequestPasswordResetAsync("user-no-email");

        // Assert: Reset token was created (prevents account enumeration)
        var tokenRecord = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
        Assert.NotNull(tokenRecord);

        // Assert: Email was NOT dispatched because there is no email on file
        Assert.Empty(spySender.Sent);
    }

    // =========================================================================
    // 4. No secrets are exposed in logs
    // =========================================================================

    [Fact]
    public void SmtpOptions_ToString_RedactsPassword()
    {
        var options = new SmtpOptions
        {
            Host = "smtp.secure-server.com",
            Port = 587,
            Username = "lims-admin",
            Password = "SuperSensitivePassword!123",
            FromAddress = "no-reply@microlims.local",
            EnableSsl = true
        };

        var str = options.ToString();

        Assert.DoesNotContain("SuperSensitivePassword!123", str);
        Assert.Contains("[REDACTED]", str);
        Assert.Contains("smtp.secure-server.com", str);
        Assert.Contains("587", str);
        Assert.Contains("lims-admin", str);
    }

    [Fact]
    public void EmailSender_ToString_RedactsPassword()
    {
        var sender = new EmailSender("smtp.test.local", 587, "user", "secret-password-xyz", "from@test.local", true);

        var str = sender.ToString();

        Assert.DoesNotContain("secret-password-xyz", str);
        Assert.Contains("[REDACTED]", str);
        Assert.Contains("smtp.test.local", str);
    }

    [Fact]
    public void EmailSender_DoesNotExposePasswordPropertyPublicly()
    {
        var passwordProp = typeof(EmailSender).GetProperty("Password", BindingFlags.Public | BindingFlags.Instance);
        Assert.Null(passwordProp);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static MicroLimsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static AuthenticationService CreateAuthService(MicroLimsDbContext db, IEmailSender emailSender)
    {
        Func<string, string, IEnumerable<string>, string> tokenIssuer = (id, role, permissionCodes) => "fake-jwt";
        var securityAudit = new SecurityAuditService(db, new SystemSecurityRequestContext());
        return new AuthenticationService(
            db,
            tokenIssuer,
            new PermissionService(db),
            emailSender,
            NullLogger<AuthenticationService>.Instance,
            securityAudit
        );
    }
}

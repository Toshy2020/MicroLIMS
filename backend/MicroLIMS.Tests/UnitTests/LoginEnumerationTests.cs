using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Anonymous endpoints must not reveal whether a username exists or what
// state its account is in - neither through the message nor through how
// long the answer takes.
//
// Not run alongside other tests: the timing comparison below measures CPU
// work, and a busy CI runner made one side ten times slower than the other.
[CollectionDefinition(nameof(LoginEnumerationTests), DisableParallelization = true)]
public class LoginEnumerationTestsCollection { }

[Collection(nameof(LoginEnumerationTests))]
public class LoginEnumerationTests
{
    private const string Password = "Real-Password-1!";
    private const string LoginFailed = "Invalid username or password.";

    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.Add(new Role { Id = 4, Type = RoleType.Analyst, Name = "Analyst" });
        db.SaveChanges();
        return db;
    }

    private static AuthenticationService Auth(MicroLimsDbContext db) => new(
        db, (id, role, codes) => $"jwt-for-{id}", new PermissionService(db),
        new EmailSender("", 587, "", "", "no-reply@microlims.local", false),
        NullLogger<AuthenticationService>.Instance,
        new SecurityAuditService(db, new SystemSecurityRequestContext()));

    // Production-cost hash (library default) unless a test needs otherwise,
    // so timing comparisons reflect a real deployment.
    private static User AddUser(MicroLimsDbContext db, string username, string? passwordHash = null,
        bool isActive = true, DateTime? lockedUntil = null)
    {
        var user = new User
        {
            FullName = username,
            Username = username,
            RoleId = 4,
            PasswordHash = passwordHash ?? BCrypt.Net.BCrypt.HashPassword(Password),
            IsActive = isActive,
            LockedUntil = lockedUntil
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task EveryKindOfFailedLogin_GetsTheSameMessage()
    {
        await using var db = NewDb();
        var testHash = TestPasswords.Hash(Password);
        AddUser(db, "active", testHash);
        AddUser(db, "locked", testHash, lockedUntil: DateTime.UtcNow.AddMinutes(15));
        AddUser(db, "disabled", testHash, isActive: false);
        var auth = Auth(db);

        var outcomes = new[]
        {
            await auth.LoginAsync("no-such-user", Password),
            await auth.LoginAsync("active", "wrong-password"),
            await auth.LoginAsync("locked", Password),       // right password, locked account
            await auth.LoginAsync("disabled", Password)      // right password, disabled account
        };

        Assert.All(outcomes, o =>
        {
            Assert.False(o.Success);
            Assert.Null(o.Token);
            Assert.Equal(LoginFailed, o.FailureReason);
        });
    }

    // Before: an unknown, locked or disabled account was refused without any
    // BCrypt work, so it answered in ~0 ms against ~100+ ms for a wrong
    // password - a reliable way to tell them apart.
    [Fact]
    public async Task UnknownLockedAndDisabledAccounts_TakeAsLongToRefuseAsAWrongPassword()
    {
        await using var db = NewDb();
        AddUser(db, "active");
        AddUser(db, "locked", lockedUntil: DateTime.UtcNow.AddMinutes(15));
        AddUser(db, "disabled", isActive: false);
        var auth = Auth(db);

        await auth.LoginAsync("warm-up", "x"); // JIT, EF model, the equaliser hash itself

        // Fastest of five: load from other work only ever adds time, so the
        // minimum is the closest measure of the refusal's own cost. (CI once
        // saw a 2051 ms median wrong password against 183 ms for 'disabled'.)
        async Task<double> FastestMs(string username)
        {
            var samples = new List<double>();
            for (var i = 0; i < 5; i++)
            {
                var sw = Stopwatch.StartNew();
                await auth.LoginAsync(username, "wrong-password");
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }
            return samples.Min();
        }

        var wrongPassword = await FastestMs("active");
        foreach (var username in new[] { "no-such-user", "locked", "disabled" })
        {
            var elapsed = await FastestMs(username);
            Assert.True(elapsed >= wrongPassword * 0.4,
                $"'{username}' was refused in {elapsed:F0} ms against {wrongPassword:F0} ms for a wrong password - " +
                "fast enough to tell the two apart.");
        }
    }

    [Fact]
    public async Task LockedAccountWithAnUnreadableHash_IsRefusedNormally_NotAServerError()
    {
        await using var db = NewDb();
        AddUser(db, "legacy", passwordHash: "not-a-bcrypt-hash", lockedUntil: DateTime.UtcNow.AddMinutes(15));

        var outcome = await Auth(db).LoginAsync("legacy", Password);

        Assert.False(outcome.Success);
        Assert.Equal(LoginFailed, outcome.FailureReason);
    }

    // The anonymous admin-recovery confirm endpoint: an unknown username, a
    // disabled account and a wrong code look identical to the caller.
    [Fact]
    public async Task RecoveryConfirm_UnknownUserDisabledUserAndWrongCode_AreIndistinguishable()
    {
        await using var db = NewDb();
        var testHash = TestPasswords.Hash(Password);
        AddUser(db, "active", testHash);
        AddUser(db, "disabled", testHash, isActive: false);
        var recovery = new AdminPasswordRecoveryService(db);

        async Task<string> Message(string username) =>
            (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                recovery.ConfirmRecoveryAsync(username, "WRONG-CODE-123", "New-Password-9!"))).Message;

        var messages = new[] { await Message("no-such-user"), await Message("disabled"), await Message("active") };

        Assert.All(messages, m => Assert.Equal("Invalid or expired recovery code.", m));
    }
}

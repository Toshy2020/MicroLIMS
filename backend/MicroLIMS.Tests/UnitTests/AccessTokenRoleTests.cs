using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The role claim in an access token comes only from the user's own Role row.
// An account whose role cannot be loaded is refused a token; it used to be
// issued an "Analyst" role claim by default.
//
// In-memory on purpose: the case under test is a user row whose role row is
// missing, which PostgreSQL's foreign key would refuse to store. What is
// proved is the service's branch on a null Role, not a relational rule.
public class AccessTokenRoleTests
{
    private const string Password = "Real-Password-1!";

    private static DbContextOptions<MicroLimsDbContext> NewOptions() =>
        new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static AuthenticationService Auth(MicroLimsDbContext db, List<AccessTokenSubject> issued) => new(
        db, subject => { issued.Add(subject); return $"jwt-for-{subject.UserId}"; }, new PermissionService(db),
        new EmailSender("", 587, "", "", "no-reply@microlims.local", false),
        NullLogger<AuthenticationService>.Instance,
        new SecurityAuditService(db, new SystemSecurityRequestContext()));

    private static async Task SeedAsync(DbContextOptions<MicroLimsDbContext> options, int roleId, bool addRole)
    {
        await using var arrangeDb = new MicroLimsDbContext(options);
        if (addRole)
            arrangeDb.Roles.Add(new Role { Id = roleId, Type = RoleType.SectionHead, Name = "Section Head", PermissionVersion = 3 });
        arrangeDb.Users.Add(new User
        {
            FullName = "Sam Head",
            Username = "sam",
            RoleId = roleId,
            PasswordHash = TestPasswords.Hash(Password),
            IsActive = true
        });
        await arrangeDb.SaveChangesAsync();
    }

    // Today the login query's Include of the required Role drops a role-less
    // user entirely, so this is refused as an unknown account before the
    // token issuer is reached; the issuer's own fail-closed throw guards any
    // future query that loads a user without its role. Either way the
    // contract is the same: no role, no token.
    [Fact]
    public async Task Login_UserWhoseRoleIsMissing_IsRefusedAToken()
    {
        var options = NewOptions();
        await SeedAsync(options, roleId: 999, addRole: false);
        var issued = new List<AccessTokenSubject>();

        await using (var actDb = new MicroLimsDbContext(options))
        {
            var outcome = await Auth(actDb, issued).LoginAsync("sam", Password);
            Assert.False(outcome.Success);
            Assert.Null(outcome.Token);
        }

        Assert.Empty(issued);
        await using var assertDb = new MicroLimsDbContext(options);
        Assert.False(await assertDb.RefreshTokens.AsNoTracking().AnyAsync());
    }

    [Fact]
    public async Task Login_UserWithRole_GetsThatRoleNotTheAnalystDefault()
    {
        var options = NewOptions();
        await SeedAsync(options, roleId: 2, addRole: true);
        var issued = new List<AccessTokenSubject>();

        await using (var actDb = new MicroLimsDbContext(options))
        {
            var outcome = await Auth(actDb, issued).LoginAsync("sam", Password);
            Assert.True(outcome.Success);
        }

        var subject = Assert.Single(issued);
        Assert.Equal(nameof(RoleType.SectionHead), subject.Role);
        Assert.Equal(2, subject.RoleId);
        Assert.Equal(3, subject.PermissionVersion);
    }
}

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

// The header shows the role's own name, not the base type the token carries,
// so /api/auth/me and the login response have to supply it.
public class CurrentUserRoleNameTests
{
    private const string Password = "Real-Password-1!";

    private static (MicroLimsDbContext Db, AuthenticationService Auth, List<AccessTokenSubject> Issued) Create()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.Add(new Role { Id = 9, Type = RoleType.Analyst, Name = "Senior Microbiologist", PermissionVersion = 4 });
        db.Users.Add(new User
        {
            Id = 5, FullName = "Sam Lab", Username = "sam", RoleId = 9, IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
        });
        db.SaveChanges();

        var issued = new List<AccessTokenSubject>();
        var auth = new AuthenticationService(
            db, subject => { issued.Add(subject); return "jwt"; }, new PermissionService(db),
            new EmailSender("", 587, "", "", "no-reply@microlims.local", false),
            NullLogger<AuthenticationService>.Instance,
            new SecurityAuditService(db, new SystemSecurityRequestContext()));
        return (db, auth, issued);
    }

    [Fact]
    public async Task Me_ReturnsTheRoleNameAndTheBaseType()
    {
        var (_, auth, _) = Create();

        var info = await auth.GetCurrentUserAsync(5);

        Assert.Equal("Senior Microbiologist", info!.RoleName);
        Assert.Equal("Analyst", info.Role);
    }

    [Fact]
    public async Task Me_FollowsARenameOfTheRole()
    {
        var (db, auth, _) = Create();
        db.Roles.Single().Name = "Lead Microbiologist";
        await db.SaveChangesAsync();

        Assert.Equal("Lead Microbiologist", (await auth.GetCurrentUserAsync(5))!.RoleName);
    }

    [Fact]
    public async Task Login_ReturnsTheRoleName_AndIssuesTheTokenForTheRoleAndItsPermissionVersion()
    {
        var (_, auth, issued) = Create();

        var outcome = await auth.LoginAsync("sam", Password);

        Assert.True(outcome.Success);
        Assert.Equal("Senior Microbiologist", outcome.RoleName);
        var subject = Assert.Single(issued);
        Assert.Equal("5", subject.UserId);
        Assert.Equal("Analyst", subject.Role);
        Assert.Equal(9, subject.RoleId);
        Assert.Equal(4, subject.PermissionVersion);
    }

    [Fact]
    public async Task Refresh_ReturnsTheCurrentRoleName_AndTheCurrentPermissionVersion()
    {
        var (db, auth, issued) = Create();
        var login = await auth.LoginAsync("sam", Password);
        var role = db.Roles.Single();
        role.Name = "Lead Microbiologist";
        role.PermissionVersion = 5;
        await db.SaveChangesAsync();

        var refreshed = await auth.RefreshAsync(login.RefreshToken!);

        Assert.True(refreshed.Success);
        Assert.Equal("Lead Microbiologist", refreshed.RoleName);
        Assert.Equal(new[] { 4, 5 }, issued.Select(s => s.PermissionVersion));
    }
}

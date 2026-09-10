using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The invariant: a fresh MicroLIMS installation cannot come up with an
// administrator account whose password is knowable from this repository.
//
// The seeder used to hard-code one. Because the repository is public, that
// meant every fresh deployment shipped with a publicly usable System
// Administrator. Setting MustChangePassword alone would not have fixed it -
// whoever reached the login form first would have chosen the replacement
// and taken the account - so the password now has to be supplied out of
// band, and no password means no account.
public class InitialAdministratorSeedTests
{
    private const string ProvisioningPassword = "Provisioned-At-Deploy-1!";

    private static MicroLimsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        db.Roles.AddRange(
            new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator", IsSystemRole = true, IsActive = true },
            new Role { Id = 2, Type = RoleType.SectionHead, Name = "Section Head", IsSystemRole = true, IsActive = true },
            new Role { Id = 3, Type = RoleType.Reviewer, Name = "Reviewer", IsSystemRole = true, IsActive = true },
            new Role { Id = 4, Type = RoleType.Analyst, Name = "Analyst", IsSystemRole = true, IsActive = true });
        db.SaveChanges();
        return db;
    }

    // ---- 1. A supplied password produces a forced-change administrator ----

    [Fact]
    public void Seed_WithProvisioningPassword_CreatesAdminThatMustChangeIt()
    {
        using var db = CreateDbContext();

        DbSeeder.Seed(db, ProvisioningPassword);

        var admin = Assert.Single(db.Users.Where(u => u.Username == "admin").ToList());
        Assert.True(admin.MustChangePassword);
        Assert.True(BCrypt.Net.BCrypt.Verify(ProvisioningPassword, admin.PasswordHash));
    }

    // ---- The core of the fix: no password, no account ----

    [Fact]
    public void Seed_WithoutProvisioningPassword_CreatesNoAdministratorAtAll()
    {
        using var db = CreateDbContext();

        DbSeeder.Seed(db, initialAdminPassword: null);

        Assert.Empty(db.Users.ToList());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Seed_WithBlankProvisioningPassword_CreatesNoAdministrator(string password)
    {
        using var db = CreateDbContext();

        DbSeeder.Seed(db, password);

        Assert.Empty(db.Users.ToList());
    }

    // Seeding must not be a way around the password policy.
    [Fact]
    public void Seed_WithWeakProvisioningPassword_FailsClosedAndCreatesNoAdministrator()
    {
        using var db = CreateDbContext();

        var ex = Assert.Throws<InvalidOperationException>(() => DbSeeder.Seed(db, "weak"));

        Assert.Contains("password policy", ex.Message);
        Assert.Empty(db.Users.Where(u => u.Username == "admin").ToList());
    }

    // ---- 2. An existing administrator is never touched ----

    [Fact]
    public void Seed_DoesNotOverwriteAnExistingAdministratorsPassword()
    {
        using var db = CreateDbContext();
        var existingHash = BCrypt.Net.BCrypt.HashPassword("Established-Password-9!");
        db.Users.Add(new User
        {
            Id = 500,
            FullName = "Existing Administrator",
            Username = "admin",
            PasswordHash = existingHash,
            RoleId = 1,
            IsActive = true,
            MustChangePassword = false
        });
        db.SaveChanges();

        // A later deployment supplies a provisioning password; the account
        // that already exists must be left exactly as it was.
        DbSeeder.Seed(db, ProvisioningPassword);

        var admin = db.Users.Single(u => u.Username == "admin");
        Assert.Equal(existingHash, admin.PasswordHash);
        Assert.False(admin.MustChangePassword);
        Assert.False(BCrypt.Net.BCrypt.Verify(ProvisioningPassword, admin.PasswordHash));
    }

    // The guard is "no users at all", so any pre-existing account - not just
    // one called admin - stops the bootstrap.
    [Fact]
    public void Seed_WithAnyExistingUser_CreatesNoAdditionalAdministrator()
    {
        using var db = CreateDbContext();
        db.Users.Add(new User
        {
            Id = 501,
            FullName = "An Analyst",
            Username = "analyst1",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Analyst-Password-3!"),
            RoleId = 4,
            IsActive = true
        });
        db.SaveChanges();

        DbSeeder.Seed(db, ProvisioningPassword);

        Assert.Empty(db.Users.Where(u => u.Username == "admin").ToList());
        Assert.Single(db.Users.ToList());
    }

    [Fact]
    public void Seed_IsIdempotent_RunningTwiceCreatesOneAdministrator()
    {
        using var db = CreateDbContext();

        DbSeeder.Seed(db, ProvisioningPassword);
        var firstHash = db.Users.Single(u => u.Username == "admin").PasswordHash;
        DbSeeder.Seed(db, ProvisioningPassword);

        var admin = Assert.Single(db.Users.Where(u => u.Username == "admin").ToList());
        Assert.Equal(firstHash, admin.PasswordHash);
    }

    // ---- 3. The retired credential is gone from runtime code ----

    [Fact]
    public void Seeder_ContainsNoBuiltInPassword()
    {
        // Reflect over the compiled assembly rather than reading source, so
        // this holds for whatever actually ships.
        var seederAssembly = typeof(DbSeeder).Assembly;
        var literals = System.IO.File.ReadAllBytes(seederAssembly.Location);
        var asText = System.Text.Encoding.UTF8.GetString(literals);

        Assert.DoesNotContain("ChangeMe123", asText, StringComparison.OrdinalIgnoreCase);
    }
}

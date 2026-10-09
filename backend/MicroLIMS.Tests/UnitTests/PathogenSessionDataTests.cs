using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class PathogenSessionDataTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task UserName_ResolvesReferencedUsers_AndFallsBackToUnknown()
    {
        using var db = NewDb();
        var role = new Role { Id = 1, Name = "Analyst", Type = RoleType.Analyst };
        db.Roles.Add(role);
        db.Users.AddRange(
            new User { Id = 5, FullName = "Mazen Asharaf", Username = "mazen", PasswordHash = "h", RoleId = 1, Role = role },
            new User { Id = 6, FullName = "Not Referenced", Username = "nr", PasswordHash = "h", RoleId = 1, Role = role },
            new User { Id = 7, FullName = "Assigned Analyst", Username = "aa", PasswordHash = "h", RoleId = 1, Role = role });
        var sample = new Sample
        {
            Id = 1,
            ReferenceNumber = "S-1",
            TestOrders = { new TestOrder { Id = 11, TestCode = "TAMC", AssignedAnalystId = 7 } }
        };
        db.Samples.Add(sample);
        db.Incubations.Add(new Incubation { TestOrderId = 11, StepName = "TSB", StartedByUserId = 5 });
        await db.SaveChangesAsync();

        var data = await PathogenSessionData.LoadAsync(db, sample);

        Assert.Equal("Mazen Asharaf", data.UserName(5));          // incubation starter
        Assert.Equal("Assigned Analyst", data.UserName(7));       // test-order analyst
        Assert.Equal("Unknown", data.UserName(999));              // no such user
        Assert.Equal("Unknown", data.UserName(null));
        Assert.Equal("Unknown", data.UserName(6));                // exists, but nothing references it, so it is not loaded
    }

    [Fact]
    public async Task LoadAsync_ReturnsOnlyRowsOfThisSamplesTestOrders()
    {
        using var db = NewDb();
        var sample = new Sample { Id = 1, ReferenceNumber = "S-1", TestOrders = { new TestOrder { Id = 11, TestCode = "TAMC" } } };
        db.Samples.Add(sample);
        db.Incubations.AddRange(
            new Incubation { TestOrderId = 11, StepName = "TSB" },
            new Incubation { TestOrderId = 99, StepName = "TSB" });
        await db.SaveChangesAsync();

        var data = await PathogenSessionData.LoadAsync(db, sample);

        Assert.Equal(11, Assert.Single(data.Incubations).TestOrderId);
    }
}

using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class ElectronicSignatureTests
{
    private const string CorrectPassword = "Correct-Horse-1!";
    private const string WrongPassword = "Wrong-Horse-1!";

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<User> SeedUser(MicroLimsDbContext db, RoleType roleType = RoleType.Reviewer)
    {
        var role = new Role { Type = roleType, Name = roleType.ToString() };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { FullName = "Jane Reviewer", Username = "jane", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<TestOrder> SeedResultEnteredOrder(MicroLimsDbContext db, int analystId)
    {
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = $"CTRL-{Guid.NewGuid():N}", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        db.Results.Add(new Result { TestOrderId = order.Id, RawValue = "10", Type = ResultType.Numeric, EnteredByUserId = analystId });
        await db.SaveChangesAsync();

        return order;
    }

    [Fact]
    public async Task SignAsync_WrongPassword_ThrowsAndWritesNoSignature()
    {
        await using var db = NewDb();
        var user = await SeedUser(db);
        var service = new ElectronicSignatureService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SignAsync(user.Id, WrongPassword, SignatureMeaning.Reviewed, "TestOrder", 1, "comment", "127.0.0.1"));

        Assert.Equal("Password verification failed. The signature was not applied.", ex.Message);
        Assert.Empty(db.ElectronicSignatures);
    }

    [Fact]
    public async Task SignAsync_CorrectPassword_QueuesSignatureWithSnapshot()
    {
        await using var db = NewDb();
        var user = await SeedUser(db);
        var service = new ElectronicSignatureService(db);

        var signature = await service.SignAsync(user.Id, CorrectPassword, SignatureMeaning.Approved, "TestOrder", 1, "looks good", "127.0.0.1");
        await db.SaveChangesAsync();

        var stored = await db.ElectronicSignatures.SingleAsync();
        Assert.Equal(user.FullName, stored.UserFullNameSnapshot);
        Assert.Equal(user.Username, stored.UsernameSnapshot);
        Assert.Equal("Reviewer", stored.RoleSnapshot);
        Assert.Equal(SignatureMeaning.Approved, stored.MeaningOfSignature);
        Assert.Equal("looks good", stored.Comment);
        Assert.Equal(signature.Id, stored.Id);
    }

    [Fact]
    public async Task SignAsync_WrongPassword_DoesNotIncrementFailedLoginAttemptsOrLockAccount()
    {
        await using var db = NewDb();
        var user = await SeedUser(db);
        var service = new ElectronicSignatureService(db);

        for (var i = 0; i < 10; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SignAsync(user.Id, WrongPassword, SignatureMeaning.Reviewed, "TestOrder", 1, null, null));
        }

        var reloaded = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Equal(0, reloaded.FailedLoginAttempts);
        Assert.False(reloaded.IsLocked);
        Assert.Null(reloaded.LockedUntil);
    }

    [Fact]
    public async Task MarkReviewedAsync_WrongPassword_WritesNoSignatureAndNoStatusChange()
    {
        await using var db = NewDb();
        var user = await SeedUser(db);
        var order = await SeedResultEnteredOrder(db, analystId: 999);
        var review = new ReviewService(db, new SegregationOfDutiesGuard(db), new ElectronicSignatureService(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() => review.MarkReviewedAsync(order.Id, user.Id, "comment", WrongPassword, null));

        Assert.Empty(db.ElectronicSignatures);
        var reloaded = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(ApprovalStatus.ResultEntered, reloaded.Status);
        Assert.Empty(db.WorkflowHistories);
    }

    [Fact]
    public async Task MarkReviewedAsync_CorrectPassword_WritesBothSignatureAndStatusChange()
    {
        await using var db = NewDb();
        var user = await SeedUser(db);
        var order = await SeedResultEnteredOrder(db, analystId: 999);
        var review = new ReviewService(db, new SegregationOfDutiesGuard(db), new ElectronicSignatureService(db));

        await review.MarkReviewedAsync(order.Id, user.Id, "comment", CorrectPassword, "10.0.0.1");

        var signature = await db.ElectronicSignatures.SingleAsync();
        Assert.Equal("TestOrder", signature.EntityType);
        Assert.Equal(order.Id, signature.EntityId);
        Assert.Equal(SignatureMeaning.Reviewed, signature.MeaningOfSignature);
        Assert.Equal("10.0.0.1", signature.IpAddress);

        var reloaded = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(ApprovalStatus.Reviewed, reloaded.Status);
    }
}

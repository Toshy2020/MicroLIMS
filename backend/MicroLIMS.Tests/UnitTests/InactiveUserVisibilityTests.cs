using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Infrastructure.Authentication;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Owner decision Q14: no hard delete; inactive users are kept (GxP attribution)
// but are never offered, listed, assigned or notified. Historical records keep
// showing who did what.
public class InactiveUserVisibilityTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.Roles.Add(new Role { Id = 1, Name = "Analyst", Type = RoleType.Analyst, IsActive = true });
        db.Users.AddRange(
            new User { Id = 1, Username = "alice", FullName = "Alice Active", RoleId = 1, IsActive = true },
            new User { Id = 2, Username = "bob", FullName = "Bob Active", RoleId = 1, IsActive = true },
            new User { Id = 3, Username = "ivan", FullName = "Ivan Inactive", RoleId = 1, IsActive = false });
        db.SaveChanges();
        return db;
    }

    private static UserService Users(MicroLimsDbContext db)
    {
        var emailSender = new EmailSender("", 587, "", "", "no-reply@microlims.local", false);
        var securityAudit = new SecurityAuditService(db, new SystemSecurityRequestContext());
        AccessTokenIssuer tokenIssuer = _ => "t";
        var auth = new AuthenticationService(db, tokenIssuer, new PermissionService(db), emailSender,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthenticationService>.Instance, securityAudit);
        return new UserService(db, auth, securityAudit);
    }

    [Fact]
    public async Task UserList_DefaultsToActive_AndSplitsInactive()
    {
        using var db = NewDb();
        var users = Users(db);

        Assert.DoesNotContain(await users.GetAllAsync(), u => u.Username == "ivan");
        Assert.DoesNotContain(await users.GetAllAsync("active"), u => u.Username == "ivan");
        var inactive = await users.GetAllAsync("inactive");
        Assert.Equal("ivan", Assert.Single(inactive).Username);
        Assert.Equal(3, (await users.GetAllAsync("all")).Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => users.GetAllAsync("bogus"));
    }

    [Fact]
    public async Task Directory_And_EligibleAnalysts_ExcludeInactive()
    {
        using var db = NewDb();
        var users = Users(db);

        Assert.DoesNotContain(await users.GetDirectoryAsync(), u => u.FullName == "Ivan Inactive");
        Assert.DoesNotContain(await users.GetEligibleAnalystsAsync(1), u => u.Username == "ivan");
    }

    [Fact]
    public async Task DirectMessage_NotificationsSkipInactive_AndConversationRefusesThem()
    {
        using var db = NewDb();
        var spy = new SpyNotificationService();
        var service = TestServiceFactory.Message(db, spy);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateConversationAsync(new CreateConversationRequest(null, true, new List<int> { 2, 3 }, "Hi"), 1));

        // A conversation that predates the deactivation: the inactive participant is not notified.
        var conv = await service.CreateConversationAsync(new CreateConversationRequest("Team", true, new List<int> { 2 }, "Hello"), 1);
        db.ConversationParticipants.Add(new ConversationParticipant { ConversationId = conv.Id, UserId = 3 });
        await db.SaveChangesAsync();
        spy.Sent.Clear();
        await service.SendMessageAsync(conv.Id, new SendMessageRequest("Later"), 1);

        Assert.Contains(spy.Sent, s => s.UserId == 2);
        Assert.DoesNotContain(spy.Sent, s => s.UserId == 3);
    }

    [Fact]
    public async Task DirectMessageHistory_StillNamesAnInactiveSender()
    {
        using var db = NewDb();
        db.Users.First(u => u.Id == 3).IsActive = true;
        await db.SaveChangesAsync();
        var service = TestServiceFactory.Message(db);
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, false, new List<int> { 3 }, "Old note"), 1);
        await service.SendMessageAsync(conv.Id, new SendMessageRequest("From Ivan"), 3);
        db.Users.First(u => u.Id == 3).IsActive = false;
        await db.SaveChangesAsync();

        var messages = await service.GetMessagesAsync(conv.Id, 1);
        Assert.Contains(messages, m => m.SenderUserId == 3 && m.SenderName == "Ivan Inactive");
    }

    [Fact]
    public async Task DiscussionNotifications_SkipInactiveAuthorAndCommenters()
    {
        using var db = NewDb();
        var spy = new SpyNotificationService();
        var service = TestServiceFactory.Discussion(db, notifications: spy);

        var post = await service.CreatePostAsync(new CreateDiscussionPostRequest("T", "C", DiscussionCategory.Equipment), null, authorUserId: 3);
        await service.AddCommentAsync(post.Id, new CreateDiscussionCommentRequest("by bob"), authorUserId: 2);
        spy.Sent.Clear();

        await service.AddCommentAsync(post.Id, new CreateDiscussionCommentRequest("by alice"), authorUserId: 1);

        Assert.Contains(spy.Sent, s => s.UserId == 2);
        Assert.DoesNotContain(spy.Sent, s => s.UserId == 3);

        spy.Sent.Clear();
        await service.UpdatePostAsync(post.Id, new UpdateDiscussionPostRequest("T2", "C2", DiscussionCategory.Equipment), 1, canEditAny: true);
        Assert.DoesNotContain(spy.Sent, s => s.UserId == 3);
    }

    [Fact]
    public async Task SectionMembership_CannotBeAssignedToInactiveUser()
    {
        using var db = NewDb();
        var org = new LaboratoryOrganizationService(db, new UserSectionScopeService(db));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            org.ReplaceMembershipsAsync(3, new List<UserOrgMembershipDto>(), 1));
        Assert.Contains("Inactive", ex.Message);
    }

    [Fact]
    public async Task AnalystAvailability_CannotBeRecordedForInactiveAnalyst()
    {
        using var db = NewDb();
        var service = TestServiceFactory.AnalystAvailability(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new MicroLIMS.Application.Services.Kpi.SaveAvailabilityRequest(3, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), 0.5m, "Leave"), 1));
    }

    [Fact]
    public async Task AuditTrail_StillResolvesAnInactiveUsersName()
    {
        using var db = NewDb();
        db.AuditLogs.Add(new AuditLog { EntityName = "Sample", EntityId = "10", Action = "Create", UserId = 3, Timestamp = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var results = await new AuditSearchService(db).SearchAsync(new AuditSearchRequest(
            null, null, null, null, null, null, null, null, null, null, null, null, null));

        var entry = Assert.Single(results, r => r.EntityName == "Sample");
        Assert.Equal("Ivan Inactive", entry.UserName);
        Assert.Equal("ivan", entry.UserUsername);
    }
}

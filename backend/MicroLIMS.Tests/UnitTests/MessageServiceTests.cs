using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class MessageServiceTests
{
    private static MicroLimsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        var analystRole = new Role { Id = 1, Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(analystRole);

        db.Users.AddRange(
            new User { Id = 1, Username = "alice", FullName = "Alice Analyst", RoleId = 1, IsActive = true },
            new User { Id = 2, Username = "bob", FullName = "Bob Analyst", RoleId = 1, IsActive = true },
            new User { Id = 3, Username = "charlie", FullName = "Charlie Analyst", RoleId = 1, IsActive = true }
        );
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task CreateConversation_OneOnOne_ReusesExistingIfAlreadyPresent()
    {
        using var db = CreateDbContext();
        var spyNotif = new SpyNotificationService();
        var service = TestServiceFactory.Message(db, spyNotif);

        var req1 = new CreateConversationRequest(null, IsGroup: false, new List<int> { 1, 2 }, "Hello Bob");
        var conv1 = await service.CreateConversationAsync(req1, creatorUserId: 1);

        Assert.NotNull(conv1);
        Assert.False(conv1.IsGroup);
        Assert.Equal(2, conv1.Participants.Count);
        Assert.Equal("Bob Analyst", conv1.Title); // 1-on-1 default title for Alice
        Assert.Equal("Hello Bob", conv1.LastMessage?.Content);

        // Bob was notified of message
        Assert.Contains(spyNotif.Sent, s => s.UserId == 2 && s.Message.Contains("Hello Bob"));

        // When Alice tries to start a 1-on-1 conversation with Bob again, existing conversation is reused
        var req2 = new CreateConversationRequest(null, IsGroup: false, new List<int> { 2 }, "Second message to Bob");
        var conv2 = await service.CreateConversationAsync(req2, creatorUserId: 1);

        Assert.Equal(conv1.Id, conv2.Id);
        Assert.Equal("Second message to Bob", conv2.LastMessage?.Content);

        // Verify total messages in this conversation is 2
        var messages = await service.GetMessagesAsync(conv1.Id, userId: 1);
        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public async Task CreateGroupConversation_SupportsMultipleParticipants()
    {
        using var db = CreateDbContext();
        var service = TestServiceFactory.Message(db);

        var req = new CreateConversationRequest("Lab Team Alpha", IsGroup: true, new List<int> { 1, 2, 3 }, "Team kickoff");
        var groupConv = await service.CreateConversationAsync(req, creatorUserId: 1);

        Assert.True(groupConv.IsGroup);
        Assert.Equal("Lab Team Alpha", groupConv.Title);
        Assert.Equal(3, groupConv.Participants.Count);
        Assert.Equal("Team kickoff", groupConv.LastMessage?.Content);
    }

    [Fact]
    public async Task UnreadCount_AndMarkAsRead_TracksPerParticipant()
    {
        using var db = CreateDbContext();
        var service = TestServiceFactory.Message(db);

        var req = new CreateConversationRequest(null, IsGroup: false, new List<int> { 1, 2 }, "Message 1 from Alice");
        var conv = await service.CreateConversationAsync(req, creatorUserId: 1);

        // Alice (creator) has 0 unread
        var aliceUnread = await service.GetTotalUnreadCountAsync(userId: 1);
        Assert.Equal(0, aliceUnread);

        // Bob has 1 unread message
        var bobUnread = await service.GetTotalUnreadCountAsync(userId: 2);
        Assert.Equal(1, bobUnread);

        // Alice sends another message
        await service.SendMessageAsync(conv.Id, new SendMessageRequest("Message 2 from Alice"), senderUserId: 1);
        bobUnread = await service.GetTotalUnreadCountAsync(userId: 2);
        Assert.Equal(2, bobUnread);

        // Bob marks conversation as read
        await service.MarkAsReadAsync(conv.Id, userId: 2);
        bobUnread = await service.GetTotalUnreadCountAsync(userId: 2);
        Assert.Equal(0, bobUnread);
    }

    [Fact]
    public async Task NonParticipant_CannotAccessMessagesOrSend()
    {
        using var db = CreateDbContext();
        var service = TestServiceFactory.Message(db);

        var req = new CreateConversationRequest(null, IsGroup: false, new List<int> { 1, 2 }, "Private chat");
        var conv = await service.CreateConversationAsync(req, creatorUserId: 1);

        // User 3 is not a participant
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetMessagesAsync(conv.Id, userId: 3));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SendMessageAsync(conv.Id, new SendMessageRequest("Intruder"), senderUserId: 3));
    }
}

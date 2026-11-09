using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Caller-supplied window sizes (months, take) are clamped inside the services,
// so an out-of-range query-string value can neither throw nor request an
// unbounded result. The clamp is plain LINQ/date arithmetic, so the in-memory
// provider is sufficient; arrange, act and assert use separate contexts.
public class QueryParameterClampTests
{
    private static MicroLimsDbContext NewContext(string dbName) =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(dbName).Options);

    [Theory]
    [InlineData(100_000, 60)]
    [InlineData(61, 60)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(12, 12)]
    public async Task DashboardMonthlyTrend_ClampsMonthsToOneToSixty(int requested, int expectedPoints)
    {
        var dbName = Guid.NewGuid().ToString();
        await using var actDb = NewContext(dbName);

        var trend = await TestServiceFactory.Dashboard(actDb).GetMonthlyTrendAsync(requested);

        Assert.Equal(expectedPoints, trend.Count);
    }

    [Theory]
    [InlineData(100_000, 60)]
    [InlineData(61, 60)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(12, 12)]
    public async Task ReportingCompletedByMonth_ClampsMonthsToOneToSixty(int requested, int expectedPoints)
    {
        var dbName = Guid.NewGuid().ToString();
        await using var actDb = NewContext(dbName);

        var points = await new ReportingQueryService(actDb).GetCompletedByMonthAsync(requested);

        Assert.Equal(expectedPoints, points.Count);
    }

    [Theory]
    [InlineData(1_000_000, 100)]
    [InlineData(101, 100)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(25, 25)]
    public async Task GetMessages_ClampsTakeToOneToHundred(int requestedTake, int expectedMessages)
    {
        var dbName = Guid.NewGuid().ToString();
        int conversationId;
        int senderId;
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var arrangeDb = NewContext(dbName))
        {
            // The sender must exist: the service's Include(SenderUser) is an inner join.
            var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
            arrangeDb.Roles.Add(role);
            await arrangeDb.SaveChangesAsync();
            var sender = new User { Username = "alice", FullName = "Alice Analyst", RoleId = role.Id, IsActive = true };
            arrangeDb.Users.Add(sender);
            await arrangeDb.SaveChangesAsync();
            senderId = sender.Id;

            var conversation = new Conversation { IsGroup = false, CreatedByUserId = senderId, CreatedAt = start, LastMessageAt = start };
            arrangeDb.Conversations.Add(conversation);
            await arrangeDb.SaveChangesAsync();
            conversationId = conversation.Id;

            arrangeDb.ConversationParticipants.Add(new ConversationParticipant { ConversationId = conversationId, UserId = senderId, JoinedAt = start });
            for (var i = 0; i < 120; i++)
                arrangeDb.DirectMessages.Add(new DirectMessage { ConversationId = conversationId, SenderUserId = senderId, Content = $"m{i}", CreatedAt = start.AddMinutes(i) });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = NewContext(dbName);
        var messages = await TestServiceFactory.Message(actDb).GetMessagesAsync(conversationId, userId: senderId, take: requestedTake);

        Assert.Equal(expectedMessages, messages.Count);
    }
}

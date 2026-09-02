using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DiscussionServiceTests
{
    private static MicroLimsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        var analystRole = new Role { Id = 1, Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        var headRole = new Role { Id = 2, Name = "Section Head", Type = RoleType.SectionHead, IsActive = true };
        db.Roles.AddRange(analystRole, headRole);

        db.Users.AddRange(
            new User { Id = 1, Username = "analyst1", FullName = "Alice Analyst", RoleId = 1, IsActive = true },
            new User { Id = 2, Username = "analyst2", FullName = "Bob Analyst", RoleId = 1, IsActive = true },
            new User { Id = 3, Username = "head1", FullName = "Charlie Head", RoleId = 2, IsActive = true }
        );
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task CreatePost_WithAttachments_SavesPostAndFilesCorrectly()
    {
        using var db = CreateDbContext();
        var storage = new InMemoryFileStorageService();
        var service = TestServiceFactory.Discussion(db, storage);

        var fileBytes = System.Text.Encoding.UTF8.GetBytes("sample attachment data");
        var attachments = new List<(string FileName, string ContentType, byte[] Data)>
        {
            ("test_doc.pdf", "application/pdf", fileBytes)
        };

        var request = new CreateDiscussionPostRequest(
            "Water Alert Issue",
            "Observed high counts in Room B water point.",
            DiscussionCategory.Water,
            IsImportant: true
        );

        var post = await service.CreatePostAsync(request, attachments, authorUserId: 1);

        Assert.NotNull(post);
        Assert.Equal("Water Alert Issue", post.Title);
        Assert.Equal(DiscussionCategory.Water, post.Category);
        Assert.True(post.IsImportant);
        Assert.Equal(1, post.CurrentVersion);
        Assert.Single(post.Attachments);
        Assert.Equal("test_doc.pdf", post.Attachments[0].FileName);
        Assert.Equal("application/pdf", post.Attachments[0].ContentType);

        // Verify file stored in storage
        var (downloadData, contentType, fileName) = await service.GetAttachmentContentAsync(post.Id, post.Attachments[0].Id);
        Assert.Equal(fileBytes, downloadData);
        Assert.Equal("test_doc.pdf", fileName);
    }

    [Fact]
    public async Task UpdatePost_PreservesPreviousVersionInHistory_AndNotifiesParticipants()
    {
        using var db = CreateDbContext();
        var spyNotif = new SpyNotificationService();
        var service = TestServiceFactory.Discussion(db, notifications: spyNotif);

        // User 1 creates post
        var createReq = new CreateDiscussionPostRequest("Initial Title", "Initial Content", DiscussionCategory.Equipment);
        var post = await service.CreatePostAsync(createReq, null, authorUserId: 1);

        // User 2 comments on post (participating in discussion)
        await service.AddCommentAsync(post.Id, new CreateDiscussionCommentRequest("Interesting observation."), authorUserId: 2);

        // Clear spy before post update
        spyNotif.Sent.Clear();

        // User 1 updates post
        var updateReq = new UpdateDiscussionPostRequest("Updated Title", "Updated Content with corrections", DiscussionCategory.Equipment, IsImportant: false);
        var updated = await service.UpdatePostAsync(post.Id, updateReq, actingUserId: 1, canEditAny: false);

        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal(2, updated.CurrentVersion);
        Assert.True(updated.IsEdited);
        Assert.NotNull(updated.LastEditedAt);

        // Verify version history
        var history = await service.GetPostHistoryAsync(post.Id);
        Assert.Single(history);
        Assert.Equal(1, history[0].VersionNumber);
        Assert.Equal("Initial Title", history[0].Title);
        Assert.Equal("Initial Content", history[0].Content);
        Assert.Equal(1, history[0].ChangedByUserId);

        // Verify participant notification: User 2 (commenter) was notified of discussion update
        Assert.Contains(spyNotif.Sent, s => s.UserId == 2 && s.Message.Contains("was updated by Alice Analyst"));
        // Editor (User 1) was not notified
        Assert.DoesNotContain(spyNotif.Sent, s => s.UserId == 1);
    }

    [Fact]
    public async Task AddComment_NotifiesPostAuthor_AndPreviousCommenters()
    {
        using var db = CreateDbContext();
        var spyNotif = new SpyNotificationService();
        var service = TestServiceFactory.Discussion(db, notifications: spyNotif);

        var post = await service.CreatePostAsync(new CreateDiscussionPostRequest("EM Discussion", "Particle counts", DiscussionCategory.EnvironmentalMonitoring), null, authorUserId: 1);

        spyNotif.Sent.Clear();

        // User 2 comments -> User 1 (author) notified
        await service.AddCommentAsync(post.Id, new CreateDiscussionCommentRequest("First comment from User 2"), authorUserId: 2);
        Assert.Single(spyNotif.Sent);
        Assert.Equal(1, spyNotif.Sent[0].UserId);
        Assert.Contains("Bob Analyst commented on your discussion", spyNotif.Sent[0].Message);

        spyNotif.Sent.Clear();

        // User 3 comments -> User 1 (author) AND User 2 (previous commenter) notified
        await service.AddCommentAsync(post.Id, new CreateDiscussionCommentRequest("Second comment from User 3"), authorUserId: 3);
        Assert.Equal(2, spyNotif.Sent.Count);
        Assert.Contains(spyNotif.Sent, s => s.UserId == 1);
        Assert.Contains(spyNotif.Sent, s => s.UserId == 2);
    }

    [Fact]
    public async Task UpdateAndCommentPermissions_GuardNonAuthorsWithoutModerationPrivilege()
    {
        using var db = CreateDbContext();
        var service = TestServiceFactory.Discussion(db);

        var post = await service.CreatePostAsync(new CreateDiscussionPostRequest("Author Post", "Content", DiscussionCategory.Products), null, authorUserId: 1);

        // User 2 tries to edit User 1's post without canEditAny -> UnauthorizedAccessException
        var updateReq = new UpdateDiscussionPostRequest("Hacked Title", "Hacked Content", DiscussionCategory.Products);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdatePostAsync(post.Id, updateReq, actingUserId: 2, canEditAny: false));

        // User 3 (Section Head / Admin) edits with canEditAny=true -> succeeds
        var headUpdated = await service.UpdatePostAsync(post.Id, new UpdateDiscussionPostRequest("Head Updated Title", "Content", DiscussionCategory.Products), actingUserId: 3, canEditAny: true);
        Assert.Equal("Head Updated Title", headUpdated.Title);
    }

    [Fact]
    public async Task GetFeed_CategoryAndSearchFilters_WorkAccurately()
    {
        using var db = CreateDbContext();
        var service = TestServiceFactory.Discussion(db);

        await service.CreatePostAsync(new CreateDiscussionPostRequest("Water Testing Standard", "ISO procedure for water", DiscussionCategory.Water), null, 1);
        await service.CreatePostAsync(new CreateDiscussionPostRequest("Autoclave Calibration", "Hirayama unit check", DiscussionCategory.Equipment), null, 1);
        await service.CreatePostAsync(new CreateDiscussionPostRequest("Media Growth Promotion", "TSB batch evaluation", DiscussionCategory.MediaMaterials), null, 1);

        // Filter by category
        var waterFeed = await service.GetFeedAsync(category: DiscussionCategory.Water);
        Assert.Single(waterFeed.Items);
        Assert.Equal("Water Testing Standard", waterFeed.Items[0].Title);

        // Search by keyword
        var searchFeed = await service.GetFeedAsync(search: "Hirayama");
        Assert.Single(searchFeed.Items);
        Assert.Equal("Autoclave Calibration", searchFeed.Items[0].Title);

        // All items
        var allFeed = await service.GetFeedAsync();
        Assert.Equal(3, allFeed.TotalCount);
    }
}

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

    private static readonly byte[] Pdf = System.Text.Encoding.UTF8.GetBytes("%PDF-1.7 minimal");
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };

    private static CreateDiscussionPostRequest AnyPost() =>
        new("Attachment policy", "Checking attachment limits", DiscussionCategory.Water);

    private static async Task<string> RejectionAsync(MicroLimsDbContext db, InMemoryFileStorageService storage,
        List<(string FileName, string ContentType, byte[] Data)> attachments)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.Discussion(db, storage).CreatePostAsync(AnyPost(), attachments, authorUserId: 1));

        // Refused before anything was written: no post, no stored file.
        Assert.Equal(0, await db.DiscussionPosts.CountAsync());
        Assert.Empty(storage.Files);
        return ex.Message;
    }

    [Fact]
    public async Task CreatePost_MoreThanFiveAttachments_IsRefusedAndNothingIsSaved()
    {
        using var db = CreateDbContext();
        var files = Enumerable.Range(1, DiscussionAttachmentPolicy.MaxFiles + 1)
            .Select(i => ($"page{i}.pdf", "application/pdf", Pdf)).ToList();

        Assert.Contains("at most 5 attachments", await RejectionAsync(db, new InMemoryFileStorageService(), files));
    }

    [Fact]
    public async Task CreatePost_AttachmentOverTenMegabytes_IsRefused()
    {
        using var db = CreateDbContext();
        var big = new byte[DiscussionAttachmentPolicy.MaxFileBytes + 1];
        Pdf.CopyTo(big, 0);

        Assert.Contains("10 MB", await RejectionAsync(db, new InMemoryFileStorageService(),
            new() { ("big.pdf", "application/pdf", big) }));
    }

    [Theory]
    [InlineData("payload.exe")]
    [InlineData("page.html")]
    [InlineData("script.js")]
    [InlineData("noextension")]
    public async Task CreatePost_DisallowedFileType_IsRefused(string fileName)
    {
        using var db = CreateDbContext();

        Assert.Contains("not an allowed attachment type", await RejectionAsync(db, new InMemoryFileStorageService(),
            new() { (fileName, "application/octet-stream", Pdf) }));
    }

    // An executable renamed to .pdf: the extension is allowed, the content is not.
    [Fact]
    public async Task CreatePost_FileWhoseContentDoesNotMatchItsExtension_IsRefused()
    {
        using var db = CreateDbContext();
        var windowsExecutable = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 }; // "MZ"

        Assert.Contains("does not look like a valid PDF", await RejectionAsync(db, new InMemoryFileStorageService(),
            new() { ("invoice.pdf", "application/pdf", windowsExecutable) }));
    }

    // The type served on download comes from the extension, not from what
    // the client declared.
    [Fact]
    public async Task CreatePost_StoresTheContentTypeForTheExtension_NotTheClientsDeclaration()
    {
        using var db = CreateDbContext();
        var storage = new InMemoryFileStorageService();

        await TestServiceFactory.Discussion(db, storage).CreatePostAsync(AnyPost(), new()
        {
            ("chart.png", "text/html", Png),
            ("notes.txt", "application/x-msdownload", System.Text.Encoding.UTF8.GetBytes("plain notes"))
        }, authorUserId: 1);

        var types = await db.DiscussionAttachments.OrderBy(a => a.OriginalFileName)
            .Select(a => new { a.OriginalFileName, a.ContentType }).ToListAsync();
        Assert.Equal("image/png", types.Single(t => t.OriginalFileName == "chart.png").ContentType);
        Assert.Equal("text/plain", types.Single(t => t.OriginalFileName == "notes.txt").ContentType);
    }

    [Fact]
    public async Task CreatePost_FiveValidAttachmentsOfAllowedTypes_AreAccepted()
    {
        using var db = CreateDbContext();
        var storage = new InMemoryFileStorageService();
        var ole = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00 };
        var zip = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 };

        var post = await TestServiceFactory.Discussion(db, storage).CreatePostAsync(AnyPost(), new()
        {
            ("report.pdf", "application/pdf", Pdf),
            ("plate.png", "image/png", Png),
            ("legacy.xls", "application/vnd.ms-excel", ole),
            ("counts.xlsx", "application/octet-stream", zip),
            ("readings.csv", "text/csv", System.Text.Encoding.UTF8.GetBytes("a,b\n1,2"))
        }, authorUserId: 1);

        Assert.Equal(5, post.Attachments.Count);
        Assert.Equal(5, storage.Files.Count);
    }

    [Fact]
    public async Task CreatePost_WithAttachments_SavesPostAndFilesCorrectly()
    {
        using var db = CreateDbContext();
        var storage = new InMemoryFileStorageService();
        var service = TestServiceFactory.Discussion(db, storage);

        // A real PDF starts with "%PDF-"; attachments are checked against it.
        var fileBytes = System.Text.Encoding.UTF8.GetBytes("%PDF-1.7 sample attachment data");
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

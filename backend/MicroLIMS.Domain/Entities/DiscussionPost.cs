using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class DiscussionPost
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DiscussionCategory Category { get; set; } = DiscussionCategory.Other;
    public int AuthorUserId { get; set; }
    public User? AuthorUser { get; set; }
    public bool IsImportant { get; set; }
    public int CurrentVersion { get; set; } = 1;
    public bool IsEdited { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public int? LastEditedByUserId { get; set; }
    public User? LastEditedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public List<DiscussionAttachment> Attachments { get; set; } = new();
    public List<DiscussionComment> Comments { get; set; } = new();
    public List<DiscussionPostVersion> Versions { get; set; } = new();
}

namespace MicroLIMS.Domain.Entities;

public class DiscussionComment
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public DiscussionPost? Post { get; set; }
    public int AuthorUserId { get; set; }
    public User? AuthorUser { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsEdited { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
}

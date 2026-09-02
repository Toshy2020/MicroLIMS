using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class DiscussionPostVersion
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public DiscussionPost? Post { get; set; }
    public int VersionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DiscussionCategory Category { get; set; }
    public int ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

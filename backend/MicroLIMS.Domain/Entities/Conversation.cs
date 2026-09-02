namespace MicroLIMS.Domain.Entities;

public class Conversation
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public bool IsGroup { get; set; }
    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public List<ConversationParticipant> Participants { get; set; } = new();
    public List<DirectMessage> Messages { get; set; } = new();
}

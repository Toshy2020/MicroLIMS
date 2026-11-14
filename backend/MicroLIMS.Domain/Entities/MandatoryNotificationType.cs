namespace MicroLIMS.Domain.Entities;

// Marks a notification catalogue type as mandatory for a laboratory section:
// members receive immediate email (when allowed) regardless of user preference.
public class MandatoryNotificationType
{
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Type { get; set; } = string.Empty;
    public int SetByUserId { get; set; }
    public User? SetByUser { get; set; }
    public DateTime SetAt { get; set; }
}
